using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine.SceneManagement;

namespace MmaManagerTycoon.Editor
{
 [InitializeOnLoad]
 public static class LowKickIntegration
 {
  const string Root="Assets/MmaManagerTycoon";
  const string Art=Root+"/Art/Characters/AmateurFighter/fighter_low_kick_sheet.png";
  const string Generated=Root+"/TrainingPreview";
  const string Interior=Generated+"/GymInteriorPreview.unity";
  const string Output="/Users/souhilbenammour/Documents/Codex/2026-10-14/cre/outputs/LowKick";
  const string Pending="MMA.LowKick.Validation";
  static int stage;
  static double started;
  static float pauseAt,pausePhase,kickEnd,stableAt;
  static Quaternion pauseBag;
  static int pauseImpacts;
  static bool screenshot;
  static readonly HashSet<string> frames=new HashSet<string>();
  static readonly List<string> errors=new List<string>();
  static LowKickIntegration(){EditorApplication.update+=Run;EditorApplication.playModeStateChanged+=PlayChanged;}
  static void Run()
  {
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
   EditorApplication.update-=Run;
   if(File.Exists(Output+"/unity-integration.json"))return;
   try{Build();}catch(Exception e){File.WriteAllText(Output+"/integration-error.txt",e.ToString());Debug.LogException(e);}
  }
  static void Build()
  {
   if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new Exception("An open scene has unsaved edits; integration deferred.");
   var importer=AssetImporter.GetAtPath(Art) as TextureImporter;
   if(!importer)throw new Exception("Kick sheet missing.");
   importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
   importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
   importer.spritePixelsPerUnit=64;importer.npotScale=TextureImporterNPOTScale.None;importer.alphaIsTransparency=true;importer.maxTextureSize=2048;
   var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteGenerateFallbackPhysicsShape=false;importer.SetTextureSettings(settings);
   var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
   var rects=Enumerable.Range(0,24).Select(i=>new SpriteRect{name="low_kick_"+i.ToString("D2"),rect=new Rect(i%6*160,(3-i/6)*128,160,128),alignment=SpriteAlignment.Custom,pivot=new Vector2(.4f,.0859375f),spriteID=GUID.Generate()}).ToArray();
   provider.SetSpriteRects(rects);provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
   provider.Apply();importer.SaveAndReimport();
   var sprites=AssetDatabase.LoadAllAssetsAtPath(Art).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
   if(sprites.Length!=24)throw new Exception("Expected 24 kick frames.");
   var clip=new AnimationClip{name="LowKick",frameRate=20};
   var keys=Enumerable.Range(0,25).Select(i=>new ObjectReferenceKeyframe{time=i/20f,value=sprites[i%24]}).ToArray();
   AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{path="",type=typeof(SpriteRenderer),propertyName="m_Sprite"},keys);
   var clipSettings=AnimationUtility.GetAnimationClipSettings(clip);clipSettings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,clipSettings);
   var existingClip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Generated+"/LowKick.anim");
   if(existingClip){EditorUtility.CopySerialized(clip,existingClip);UnityEngine.Object.DestroyImmediate(clip);clip=existingClip;EditorUtility.SetDirty(clip);}
   else AssetDatabase.CreateAsset(clip,Generated+"/LowKick.anim");
   var sampleScene=EditorSceneManager.NewPreviewScene();
   try
   {
    var obj=new GameObject("Kick frame sampler");SceneManager.MoveGameObjectToScene(obj,sampleScene);var sr=obj.AddComponent<SpriteRenderer>();obj.AddComponent<Animator>();
    for(int i=0;i<24;i++){clip.SampleAnimation(obj,(i+.1f)/20);if(sr.sprite!=sprites[i])throw new Exception("Kick frame sample mismatch "+i);}
   }
   finally{EditorSceneManager.ClosePreviewScene(sampleScene);}
   var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Generated+"/Fighter.controller");
   if(!controller)throw new Exception("Fighter controller missing.");
   if(!File.Exists("/Users/souhilbenammour/Documents/Codex/2026-10-14/cre/work/low_kick_integration_backup/Fighter.controller"))File.Copy(Generated+"/Fighter.controller","/Users/souhilbenammour/Documents/Codex/2026-10-14/cre/work/low_kick_integration_backup/Fighter.controller",false);
   if(!controller.parameters.Any(p=>p.name=="LowKick"))controller.AddParameter("LowKick",AnimatorControllerParameterType.Bool);
   var sm=controller.layers[0].stateMachine;var idle=sm.states.Single(s=>s.state.name=="Idle").state;var jab=sm.states.Single(s=>s.state.name=="Jab").state;
   var kick=sm.states.FirstOrDefault(s=>s.state.name=="LowKick").state;if(!kick)kick=sm.AddState("LowKick");kick.motion=clip;
   foreach(var state in new[]{idle,jab,kick})foreach(var transition in state.transitions.ToArray())
    if(transition.destinationState==idle||transition.destinationState==jab||transition.destinationState==kick)state.RemoveTransition(transition);
   Link(idle,jab,false,true,false);Link(idle,kick,false,true,true);
   Link(jab,kick,true,true,true);Link(kick,jab,true,true,false);
   Link(jab,idle,true,false,null);Link(kick,idle,true,false,null);
   EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
   var scene=SceneManager.GetSceneByPath(Interior);
   if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene(Interior);
   var preview=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TrainingPreview>()).Single();
   if(preview.fighter.runtimeAnimatorController!=controller||!preview.bag||!preview.fighterSprite)throw new Exception("Interior training references are invalid.");
   if(File.Exists(Output+"/integration-error.txt"))File.Delete(Output+"/integration-error.txt");
   File.WriteAllText(Output+"/unity-integration.json","{\"framesSliced\":24,\"framesSampled\":24,\"fps\":20,\"duration\":1.2,\"contactTime\":0.45,\"pivot\":[0.4,0.0859375],\"finishStrikeBeforeSwitch\":true,\"playMode\":\"pending\"}");
   SessionState.SetString("MMA.LowKick.PreviousPlayScene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
   EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Interior);SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
  }
  static void Link(AnimatorState from,AnimatorState to,bool finish,bool training,bool? lowKick)
  {
   var t=from.AddTransition(to);t.hasExitTime=finish;t.exitTime=.99f;t.hasFixedDuration=true;t.duration=0;t.canTransitionToSelf=false;
   t.AddCondition(training?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,"Training");
   if(lowKick.HasValue)t.AddCondition(lowKick.Value?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,"LowKick");
  }
  static void PlayChanged(PlayModeStateChange change)
  {
   if(!SessionState.GetBool(Pending,false))return;
   if(change==PlayModeStateChange.EnteredPlayMode)
   {
    stage=0;started=EditorApplication.timeSinceStartup;kickEnd=0;screenshot=false;frames.Clear();errors.Clear();
    Application.logMessageReceived+=Log;EditorApplication.update+=Check;
   }
   if(change==PlayModeStateChange.EnteredEditMode)
   {
    EditorApplication.update-=Check;Application.logMessageReceived-=Log;
    string path=SessionState.GetString("MMA.LowKick.PreviousPlayScene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(path)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(path);SessionState.SetBool(Pending,false);
   }
  }
  static void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
  static void Check()
  {
   if(EditorApplication.timeSinceStartup-started>120){Finish(null,"Timed out during kick validation");return;}
   var p=UnityEngine.Object.FindAnyObjectByType<TrainingPreview>();if(!p||p.FramesRun<10)return;
   var state=p.fighter.GetCurrentAnimatorStateInfo(0);bool kick=state.IsName("LowKick");
   if(kick&&p.fighterSprite.sprite)frames.Add(p.fighterSprite.sprite.name);
   if(kick&&state.normalizedTime>=.40f&&!screenshot){ScreenCapture.CaptureScreenshot(Output+"/unity_low_kick.png");screenshot=true;}
   switch(stage)
   {
    case 0:p.StartDrill(TrainingDrill.LowKick);stage=1;break;
    case 1:
     if(kick&&state.normalizedTime>.15f){p.SetPaused(true);pauseAt=p.PreviewElapsed;pausePhase=state.normalizedTime;pauseBag=p.bag.localRotation;pauseImpacts=p.Impacts;stage=2;}break;
    case 2:
     if(p.PreviewElapsed>pauseAt+.25f)
     {
      if(Mathf.Abs(state.normalizedTime-pausePhase)>.001f||Quaternion.Angle(p.bag.localRotation,pauseBag)>.001f||p.Impacts!=pauseImpacts){Finish(p,"Pause changed animation or bag motion");return;}
      p.SetPaused(false);p.StartDrill(TrainingDrill.Jab);stage=3;
     }break;
    case 3:
     if(kick)kickEnd=Mathf.Max(kickEnd,state.normalizedTime);
     if(state.IsName("Jab"))
     {
      if(kickEnd<.9f||p.KickImpacts!=1){Finish(p,"Kick switch interrupted recovery or duplicated contact");return;}stage=4;
     }break;
    case 4:
     if(p.JabImpacts==1){p.StartDrill(TrainingDrill.LowKick);stage=5;kickEnd=0;}break;
    case 5:
     if(kick&&state.normalizedTime>.43f){p.SetTraining(false);stage=6;}break;
    case 6:
     if(kick)kickEnd=Mathf.Max(kickEnd,state.normalizedTime);
     if(state.IsName("Idle"))
     {
      if(kickEnd<.9f||p.KickImpacts!=2||p.JabImpacts!=1){Finish(p,"Stop interrupted recovery or contact count was wrong");return;}
      stableAt=p.PreviewElapsed;stage=7;
     }break;
    case 7:
     if(p.PreviewElapsed>stableAt+.2f)
     {
      if(!state.IsName("Idle")||p.Impacts!=3||frames.Count<12){Finish(p,"Idle stability or kick frame advance failed");return;}
      Finish(p,null);
     }break;
   }
  }
  static void Finish(TrainingPreview p,string problem)
  {
   EditorApplication.update-=Check;Application.logMessageReceived-=Log;if(problem!=null)errors.Add(problem);
   string result="Low kick Play Mode validation: "+(errors.Count==0?"PASS":"FAIL")+"\nGame frames: "+(p?p.FramesRun:0)+"\nUnique kick frames observed: "+frames.Count+"\nKick impacts: "+(p?p.KickImpacts:0)+"\nJab impacts: "+(p?p.JabImpacts:0)+"\nCompleted test stage: "+stage+" / 7\n"+string.Join("\n",errors);
   File.WriteAllText(Output+"/playmode-validation.txt",result);Debug.Log("[MMA Low Kick] "+result);EditorApplication.ExitPlaymode();
  }
 }
}
