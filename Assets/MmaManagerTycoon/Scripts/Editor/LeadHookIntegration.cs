using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine.SceneManagement;

namespace MmaManagerTycoon.Editor
{
 [InitializeOnLoad]
 public static class LeadHookIntegration
 {
  const string Root="Assets/MmaManagerTycoon";
  const string Art=Root+"/Art/Characters/AmateurFighter/fighter_lead_hook_sheet.png";
  const string Generated=Root+"/TrainingPreview";
  const string Interior=Generated+"/GymInteriorPreview.unity";
  const string Output="/Users/souhilbenammour/Documents/Codex/2026-10-14/cre/outputs/LeadHook";
  const string Backup="/Users/souhilbenammour/Documents/Codex/2026-10-14/cre/work/lead_hook_integration_backup";
  const string Pending="MMA.LeadHook.Validation";
  // SHA-256 of the approved render in outputs/LeadHook; guards against
  // integrating anything other than the reviewed artwork.
  const string ApprovedSheet="fcbd303c940c867b96da6df9cee76abe9633f0adfb166c6ba79a9e956ff4cda2";
  const int Frames=20,Columns=6,Rows=4;
  const float Fps=20f;
  const float ContactPhase=9f/20f;
  // The authored exit time every transition out of a strike uses.
  const float ExitTime=.99f;
  static int stage,lastHookImpacts;
  static double started;
  static float pauseAt,pausePhase,strikeEnd,stableAt,observedImpactPhase;
  static double strikeRequestAt;
  static float strikeRequestNt;
  static string recoveryExit="";
  static string observedImpactSprite="";
  static bool entered,screenshot,impactChecked;
  static float recordedClipLength;
  static Quaternion pauseBag;
  static int pauseImpacts;
  static readonly HashSet<string> frames=new HashSet<string>();
  static readonly List<string> frameLog=new List<string>();
  static readonly List<string> errors=new List<string>();
  static LeadHookIntegration(){EditorApplication.update+=Run;EditorApplication.playModeStateChanged+=PlayChanged;}
  static void Run()
  {
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
   EditorApplication.update-=Run;
   if(File.Exists(Output+"/unity-integration.json"))return;
   try{Build();}catch(Exception e){File.WriteAllText(Output+"/integration-error.txt",e.ToString());Debug.LogException(e);}
  }
  static string Sha(string path)
  {
   using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
  }
  static void Build()
  {
   if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new Exception("An open scene has unsaved edits; integration deferred.");
   if(!File.Exists(Art))throw new Exception("Lead hook sheet missing.");
   if(Sha(Art)!=ApprovedSheet)throw new Exception("The lead hook sheet on disk is not the approved render; integration refused.");
   // Everything here is already approved and must come out byte for byte identical.
   // The lead hook is additive: no existing clip, sheet or mask may move.
   var guarded=new[]{Generated+"/Idle.anim",Generated+"/Jab.anim",Generated+"/LowKick.anim",Generated+"/HighKick.anim",
    Root+"/Art/Characters/AmateurFighter/fighter_guard.png",
    Root+"/Art/Characters/AmateurFighter/fighter_idle_sheet.png",
    Root+"/Art/Characters/AmateurFighter/fighter_jab_sheet.png",
    Root+"/Art/Characters/AmateurFighter/fighter_low_kick_sheet.png",
    Root+"/Art/Characters/AmateurFighter/fighter_high_kick_sheet.png",
    Art,
    Root+"/Art/Characters/AmateurFighter/SkinMasks/idle_skin_mask.png",
    Root+"/Art/Characters/AmateurFighter/SkinMasks/jab_skin_mask.png",
    Root+"/Art/Characters/AmateurFighter/SkinMasks/low_kick_skin_mask.png",
    Root+"/Art/Characters/AmateurFighter/SkinMasks/high_kick_skin_mask.png",
    Root+"/Art/Characters/AmateurFighter/SkinMasks/lead_hook_skin_mask.png"};
   foreach(var path in guarded)if(!File.Exists(path))throw new Exception("An approved asset is missing: "+path);
   var guardedBefore=guarded.ToDictionary(path=>path,path=>Sha(path));
   AssetDatabase.ImportAsset(Art,ImportAssetOptions.ForceUpdate);
   // Same pixel-art contract as the approved sheets; a mismatch here is what
   // makes the hook swim or blur next to the idle.
   var importer=AssetImporter.GetAtPath(Art) as TextureImporter;
   if(!importer)throw new Exception("Lead hook sheet has no texture importer.");
   importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
   importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
   importer.spritePixelsPerUnit=64;importer.npotScale=TextureImporterNPOTScale.None;importer.alphaIsTransparency=true;importer.maxTextureSize=2048;
   var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteGenerateFallbackPhysicsShape=false;importer.SetTextureSettings(settings);
   var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
   // 6 columns x 4 rows, read left to right then top to bottom exactly like the
   // approved sheet; Unity's rect origin is bottom-left so the row index flips.
   var rects=Enumerable.Range(0,Frames).Select(i=>new SpriteRect{name="lead_hook_"+i.ToString("D2"),rect=new Rect(i%Columns*160,(Rows-1-i/Columns)*128,160,128),alignment=SpriteAlignment.Custom,pivot=new Vector2(.4f,.0859375f),spriteID=GUID.Generate()}).ToArray();
   provider.SetSpriteRects(rects);provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
   provider.Apply();importer.SaveAndReimport();
   var sprites=AssetDatabase.LoadAllAssetsAtPath(Art).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
   if(sprites.Length!=Frames)throw new Exception("Expected 20 lead hook frames but sliced "+sprites.Length+".");
   for(int i=0;i<Frames;i++)if(sprites[i].name!="lead_hook_"+i.ToString("D2"))throw new Exception("Lead hook frame "+i+" is named "+sprites[i].name+".");
   foreach(var s in sprites)
   {
    if(s.rect.width!=160f||s.rect.height!=128f)throw new Exception("Lead hook cell size is wrong on "+s.name+".");
    // Unity reports the pivot in pixels of the cell; accept the normalised
    // form too so the check cannot be defeated by a units change.
    bool normalised=Mathf.Abs(s.pivot.x-.4f)<1e-4f&&Mathf.Abs(s.pivot.y-.0859375f)<1e-4f;
    bool pixels=Mathf.Abs(s.pivot.x-64f)<1e-3f&&Mathf.Abs(s.pivot.y-11f)<1e-3f;
    if(!normalised&&!pixels)throw new Exception("Lead hook pivot is "+s.pivot+" on "+s.name+" instead of 0.4 / 0.0859375.");
   }
   var clip=new AnimationClip{name="LeadHook",frameRate=Fps};
   // One trailing key repeats frame 0 so a looping playthrough is exactly
   // 20 frames at 20 fps and the guard pose closes the cycle.
   var keys=Enumerable.Range(0,Frames+1).Select(i=>new ObjectReferenceKeyframe{time=i/Fps,value=sprites[i%Frames]}).ToArray();
   AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{path="",type=typeof(SpriteRenderer),propertyName="m_Sprite"},keys);
   var clipSettings=AnimationUtility.GetAnimationClipSettings(clip);clipSettings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,clipSettings);
   var existingClip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Generated+"/LeadHook.anim");
   if(existingClip){EditorUtility.CopySerialized(clip,existingClip);UnityEngine.Object.DestroyImmediate(clip);clip=existingClip;EditorUtility.SetDirty(clip);}
   else AssetDatabase.CreateAsset(clip,Generated+"/LeadHook.anim");
   var binding=new EditorCurveBinding{path="",type=typeof(SpriteRenderer),propertyName="m_Sprite"};
   if(Mathf.Abs(clip.frameRate-Fps)>1e-3f)throw new Exception("Lead hook clip frame rate is "+clip.frameRate+".");
   var written=AnimationUtility.GetObjectReferenceCurve(clip,binding);
   if(written==null||written.Length!=Frames+1)throw new Exception("Lead hook clip holds "+(written==null?0:written.Length)+" keys instead of 21.");
   for(int i=0;i<=Frames;i++)if(Mathf.Abs(written[i].time-i/Fps)>1e-4f||written[i].value as Sprite!=sprites[i%Frames])throw new Exception("Lead hook key "+i+" does not hold the approved frame at "+i+" / 20 s.");
   recordedClipLength=clip.length;
   // A looping clip reports one frame more than the cycle it plays: the
   // approved 28 frame high kick reports 1.45 s for a 1.40 s loop.
   float expectedLength=(Frames+1)/Fps;
   if(Mathf.Abs(recordedClipLength-expectedLength)>1e-3f)throw new Exception("Lead hook clip is "+recordedClipLength.ToString("F4")+" s instead of the "+expectedLength.ToString("F4")+" s a looping "+(Frames/Fps).ToString("F2")+" s clip reports.");
   var sampleScene=EditorSceneManager.NewPreviewScene();
   try
   {
    var obj=new GameObject("Lead hook frame sampler");SceneManager.MoveGameObjectToScene(obj,sampleScene);var sr=obj.AddComponent<SpriteRenderer>();obj.AddComponent<Animator>();
    for(int i=0;i<Frames;i++){clip.SampleAnimation(obj,(i+.1f)/Fps);if(sr.sprite!=sprites[i])throw new Exception("Lead hook frame sample mismatch at frame "+i+".");}
   }
   finally{EditorSceneManager.ClosePreviewScene(sampleScene);}
   var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Generated+"/Fighter.controller");
   if(!controller)throw new Exception("Fighter controller missing.");
   if(!Directory.Exists(Backup))Directory.CreateDirectory(Backup);
   if(!File.Exists(Backup+"/Fighter.controller"))File.Copy(Generated+"/Fighter.controller",Backup+"/Fighter.controller",false);
   if(!File.Exists(Backup+"/GymInteriorPreview.unity"))File.Copy(Interior,Backup+"/GymInteriorPreview.unity",false);
   var sm=controller.layers[0].stateMachine;
   if(sm.defaultState==null||sm.defaultState.name!="Idle")throw new Exception("The controller no longer rests in Idle.");
   var idle=sm.states.Single(s=>s.state.name=="Idle").state;
   var jab=sm.states.Single(s=>s.state.name=="Jab").state;
   var low=sm.states.Single(s=>s.state.name=="LowKick").state;
   var high=sm.states.Single(s=>s.state.name=="HighKick").state;
   var motionStates=new[]{idle,jab,low,high};
   var motionPaths=motionStates.Select(s=>AssetDatabase.GetAssetPath(s.motion)).ToArray();
   foreach(var name in new[]{"Training","LowKick","HighKick"})if(!controller.parameters.Any(p=>p.name==name))throw new Exception("Existing "+name+" parameter was lost.");
   if(!controller.parameters.Any(p=>p.name=="LeadHook"))controller.AddParameter("LeadHook",AnimatorControllerParameterType.Bool);
   var hook=sm.states.FirstOrDefault(s=>s.state.name=="LeadHook").state;
   if(!hook)hook=sm.AddState("LeadHook");
   hook.motion=clip;
   var strikes=new[]{jab,low,high,hook};
   foreach(var state in new[]{idle,jab,low,high,hook})foreach(var transition in state.transitions.ToArray())
    if(transition.destinationState==idle||strikes.Contains(transition.destinationState))state.RemoveTransition(transition);
   // Idle picks a strike as soon as training is on; the three strike flags act
   // as one mutually exclusive drill id, so every transition out of Idle names
   // the whole id and the lead hook cannot be shadowed by the jab.
   Link(idle,jab,true,false,false,false,false);
   Link(idle,low,true,true,false,false,false);
   Link(idle,high,true,false,true,false,false);
   Link(idle,hook,true,false,false,true,false);
   // Switches wait for the end of the cycle so the active strike always finishes.
   Link(jab,low,true,true,false,false,true);Link(jab,high,true,false,true,false,true);Link(jab,hook,true,false,false,true,true);
   Link(low,jab,true,false,false,false,true);Link(low,high,true,false,true,false,true);Link(low,hook,true,false,false,true,true);
   Link(high,jab,true,false,false,false,true);Link(high,low,true,true,false,false,true);Link(high,hook,true,false,false,true,true);
   Link(hook,jab,true,false,false,false,true);Link(hook,low,true,true,false,false,true);Link(hook,high,true,false,true,false,true);
   // Stopping training returns to Idle at the end of the cycle, never mid-strike.
   // These check Training only: the drill id is still set while the strike plays
   // out, so requiring a cleared flag here would deadlock the state.
   Link(jab,idle,false,null,null,null,true);Link(low,idle,false,null,null,null,true);
   Link(high,idle,false,null,null,null,true);Link(hook,idle,false,null,null,null,true);
   EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
   for(int i=0;i<motionStates.Length;i++)if(AssetDatabase.GetAssetPath(motionStates[i].motion)!=motionPaths[i])throw new Exception("State "+motionStates[i].name+" lost its approved clip.");
   var outgoing=new Dictionary<string,int>{{"Idle",4},{"Jab",4},{"LowKick",4},{"HighKick",4},{"LeadHook",4}};
   foreach(var state in new[]{idle,jab,low,high,hook})
   {
    if(state.transitions.Length!=outgoing[state.name])throw new Exception(state.name+" holds "+state.transitions.Length+" transitions instead of "+outgoing[state.name]+".");
    if(state.transitions.Any(t=>t.destinationState==null))throw new Exception(state.name+" has a transition with no destination.");
   }
   foreach(var pair in guardedBefore)if(Sha(pair.Key)!=pair.Value)throw new Exception("An approved asset changed during integration: "+pair.Key);
   var scene=SceneManager.GetSceneByPath(Interior);
   if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene(Interior);
   var preview=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TrainingPreview>()).Single();
   if(preview.fighter.runtimeAnimatorController!=controller||!preview.bag||!preview.fighterSprite)throw new Exception("Interior training references are invalid.");
   if(File.Exists(Output+"/integration-error.txt"))File.Delete(Output+"/integration-error.txt");
   File.WriteAllText(Output+"/unity-integration.json",Report(null,null));
   SessionState.SetString("MMA.LeadHook.PreviousPlayScene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
   EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Interior);SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
  }
  static void Link(AnimatorState from,AnimatorState to,bool training,bool? lowKick,bool? highKick,bool? leadHook,bool finish)
  {
   var t=from.AddTransition(to);t.hasExitTime=finish;t.exitTime=ExitTime;t.hasFixedDuration=true;t.duration=0;t.canTransitionToSelf=false;
   t.AddCondition(training?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,"Training");
   if(lowKick.HasValue)t.AddCondition(lowKick.Value?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,"LowKick");
   if(highKick.HasValue)t.AddCondition(highKick.Value?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,"HighKick");
   if(leadHook.HasValue)t.AddCondition(leadHook.Value?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,"LeadHook");
  }
  static void PlayChanged(PlayModeStateChange change)
  {
   if(!SessionState.GetBool(Pending,false))return;
   if(change==PlayModeStateChange.EnteredPlayMode)
   {
    stage=0;started=EditorApplication.timeSinceStartup;strikeEnd=0;screenshot=false;entered=false;impactChecked=false;lastHookImpacts=0;observedImpactPhase=0;observedImpactSprite="";recoveryExit="";strikeRequestNt=0;strikeRequestAt=0;frames.Clear();frameLog.Clear();errors.Clear();
    Application.logMessageReceived+=Log;EditorApplication.update+=Check;
   }
   if(change==PlayModeStateChange.EnteredEditMode)
   {
    EditorApplication.update-=Check;Application.logMessageReceived-=Log;
    string path=SessionState.GetString("MMA.LeadHook.PreviousPlayScene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(path)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(path);SessionState.SetBool(Pending,false);
   }
  }
  static void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
  static void Check()
  {
   if(EditorApplication.timeSinceStartup-started>180){Finish(null,"Timed out during lead hook validation");return;}
   var p=UnityEngine.Object.FindAnyObjectByType<TrainingPreview>();if(!p||p.FramesRun<10)return;
   var state=p.fighter.GetCurrentAnimatorStateInfo(0);bool hook=state.IsName("LeadHook");
   if(hook&&p.fighterSprite.sprite)
   {
    frames.Add(p.fighterSprite.sprite.name);
    // Ordered log of the frames the hook actually displayed, with the clip
    // position each one appeared at. This is what makes a contact-frame
    // regression obvious.
    string shown=p.fighterSprite.sprite.name;
    if(frameLog.Count==0||frameLog[frameLog.Count-1]!=shown)frameLog.Add(shown+" at "+state.normalizedTime.ToString("F4"));
    if(!entered)
    {
     entered=true;
     if(state.normalizedTime>.3f)errors.Add("Lead hook was entered mid clip at normalized time "+state.normalizedTime.ToString("F3")+".");
    }
    if(p.LeadHookImpacts!=lastHookImpacts)
    {
     lastHookImpacts=p.LeadHookImpacts;
     // Capture on the hit itself. The capture costs a frame or two, so it has
     // to happen after the landing frame has already been read.
     if(!screenshot){ScreenCapture.CaptureScreenshot(Output+"/unity_lead_hook.png");screenshot=true;}
     if(!impactChecked)
     {
      impactChecked=true;observedImpactPhase=p.LastImpactPhase;observedImpactSprite=p.LastImpactSprite;
      if(observedImpactSprite!="lead_hook_09")errors.Add("Lead hook impact fired while showing "+observedImpactSprite+" instead of the contact frame lead_hook_09.");
      if(observedImpactPhase<.38f||observedImpactPhase>.52f)errors.Add("Lead hook impact fired at phase "+observedImpactPhase.ToString("F4")+" which is outside the expected band around "+ContactPhase.ToString("F4")+".");
     }
    }
   }
   switch(stage)
   {
    case 0:p.StartDrill(TrainingDrill.LeadHook);stage=1;break;
    case 1:
     if(hook&&state.normalizedTime>.15f){p.SetPaused(true);pauseAt=p.PreviewElapsed;pausePhase=state.normalizedTime;pauseBag=p.bag.localRotation;pauseImpacts=p.Impacts;stage=2;}break;
    case 2:
     if(p.PreviewElapsed>pauseAt+.25f)
     {
      if(Mathf.Abs(state.normalizedTime-pausePhase)>.001f||Quaternion.Angle(p.bag.localRotation,pauseBag)>.001f||p.Impacts!=pauseImpacts){Finish(p,"Pause changed animation or bag motion");return;}
      p.SetPaused(false);p.StartDrill(TrainingDrill.HighKick);RequestExit(state);stage=3;
     }break;
    case 3:
     if(hook)strikeEnd=Mathf.Max(strikeEnd,state.normalizedTime);
     if(state.IsName("HighKick"))
     {
      if(!RecoveryReachedExit()||p.LeadHookImpacts!=1){Finish(p,"Lead hook switch interrupted recovery or duplicated contact");return;}stage=4;
     }break;
    case 4:
     if(p.HighKickImpacts==1){p.StartDrill(TrainingDrill.LeadHook);stage=5;strikeEnd=0;}break;
    case 5:
     if(hook&&state.normalizedTime>.50f){p.SetTraining(false);RequestExit(state);stage=6;}break;
    case 6:
     if(hook)strikeEnd=Mathf.Max(strikeEnd,state.normalizedTime);
     if(state.IsName("Idle"))
     {
      if(!RecoveryReachedExit()||p.LeadHookImpacts!=2||p.HighKickImpacts!=1){Finish(p,"Stop interrupted recovery or contact count was wrong");return;}
      stableAt=p.PreviewElapsed;stage=7;
     }break;
    case 7:
     if(p.PreviewElapsed>stableAt+.2f)
     {
      if(!state.IsName("Idle")||p.Impacts!=3||frames.Count<16){Finish(p,"Idle stability or lead hook frame advance failed");return;}
      Finish(p,null);
     }break;
   }
  }
  // The strike was told to leave; remember when and from where, so a hook that
  // looks cut short can be told apart from a tick the editor missed.
  static void RequestExit(AnimatorStateInfo state)
  {
   strikeRequestNt=state.normalizedTime;strikeRequestAt=Time.realtimeSinceStartup;recoveryExit="";
  }
  // Every transition out of a strike is an exit time transition, so the hook
  // cannot leave its state before ExitTime. A sampled value below that is either
  // a real cut or a tick EditorApplication.update missed (a screenshot or a GC
  // stalls the editor but never the animator, which keeps to the wall clock).
  // The time between the request and the exit settles it.
  static bool RecoveryReachedExit()
  {
   if(strikeEnd>=.9f){recoveryExit="sampled";return true;}
   float required=(ExitTime-strikeRequestNt)*recordedClipLength;
   float waited=(float)(Time.realtimeSinceStartup-strikeRequestAt);
   recoveryExit="timed";
   return waited>=required-.05f;
  }
  static string Report(TrainingPreview p,string problem)
  {
   if(problem!=null)errors.Add(problem);
   bool pass=errors.Count==0;
   string result="{\n"+
    "  \"frames\":20,\n"+
    "  \"fps\":20,\n"+
    "  \"duration\":1.0,\n"+
    "  \"cell\":[160,128],\n"+
    "  \"pivot\":[0.4,0.0859375],\n"+
    "  \"contactFrameZeroBased\":9,\n"+
    "  \"contactTime\":0.45,\n"+
    "  \"impactImpulse\":22,\n"+
    "  \"approvedSheetSha256\":\""+ApprovedSheet+"\",\n"+
    "  \"playMode\":\""+(p==null?"pending":pass?"PASS":"FAIL")+"\",\n"+
    "  \"gameFrames\":"+(p?p.FramesRun:0)+",\n"+
    "  \"uniqueLeadHookFramesObserved\":"+frames.Count+",\n"+
    "  \"leadHookImpacts\":"+(p?p.LeadHookImpacts:0)+",\n"+
    "  \"highKickImpacts\":"+(p?p.HighKickImpacts:0)+",\n"+
    "  \"impactPhaseObserved\":"+observedImpactPhase.ToString("F4")+",\n"+
    "  \"impactPhaseAuthored\":"+ContactPhase.ToString("F4")+",\n"+
    "  \"impactSpriteObserved\":\""+observedImpactSprite+"\",\n"+
    "  \"clipLengthSeconds\":"+recordedClipLength.ToString("F4")+",\n"+
    "  \"exitTimeAuthored\":"+ExitTime.ToString("F2")+",\n"+
    "  \"recoveryExitConfirmedBy\":\""+recoveryExit+"\",\n"+
    "  \"recoveryExitSampledNt\":"+strikeEnd.ToString("F4")+",\n"+
    "  \"completedStage\":"+stage+",\n"+
    "  \"errors\":["+string.Join(",",errors.Select(e=>"\""+e.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\n","\\n")+"\""))+"]\n"+
    "}";
   return result;
  }
  static void Finish(TrainingPreview p,string problem)
  {
   EditorApplication.update-=Check;Application.logMessageReceived-=Log;
   string json=Report(p,problem);
   File.WriteAllText(Output+"/unity-integration.json",json);
   if(p!=null)File.WriteAllText(Output+"/playmode-frames.txt",frameLog.Count==0?"(no lead hook frames observed)":string.Join("\n",frameLog));
   string result="Lead hook Play Mode validation: "+(errors.Count==0?"PASS":"FAIL")+"\nGame frames: "+(p?p.FramesRun:0)+"\nUnique lead hook frames observed: "+frames.Count+"\nLead hook impacts: "+(p?p.LeadHookImpacts:0)+"\nHigh kick impacts: "+(p?p.HighKickImpacts:0)+"\nImpact phase observed: "+observedImpactPhase.ToString("F4")+" (authored "+ContactPhase.ToString("F4")+")\nImpact sprite observed: "+observedImpactSprite+"\nRecovery exit confirmed by: "+recoveryExit+" (observed nt "+strikeEnd.ToString("F4")+" of "+ExitTime.ToString("F2")+")\nCompleted test stage: "+stage+" / 7\n"+string.Join("\n",errors);
   File.WriteAllText(Output+"/playmode-validation.txt",result);Debug.Log("[MMA Lead Hook] "+result);EditorApplication.ExitPlaymode();
  }
 }
}
