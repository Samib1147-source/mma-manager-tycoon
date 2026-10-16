using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MmaManagerTycoon.Editor
{
 [InitializeOnLoad]
 public static class SkinOptionsIntegration
 {
  const string Root="Assets/MmaManagerTycoon";
  const string Interior=Root+"/TrainingPreview/GymInteriorPreview.unity";
  const string Output="/Users/souhilbenammour/Documents/Codex/2026-10-14/cre/outputs/SkinOptions";
  const string Pending="MMA.Skin.Validation";
  static readonly string[] SkinHex={"3b2523","59372d","784938","965e43","b67c56","cf986b","e3b48a","f0cfaa"};
  static int stage,tone,nextFrame,changes;
  static double started;
  static readonly HashSet<string> observed=new HashSet<string>();
  static readonly List<string> errors=new List<string>();
  static SkinOptionsIntegration(){EditorApplication.update+=Run;EditorApplication.playModeStateChanged+=PlayChanged;}
  static void Run()
  {
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
   EditorApplication.update-=Run;if(File.Exists(Output+"/integration.json"))return;
   try{Build();}catch(Exception e){File.WriteAllText(Output+"/error.txt",e.ToString());Debug.LogException(e);}
  }
  static Sprite Frame(string sheet,string name)=>AssetDatabase.LoadAllAssetsAtPath(Root+"/Art/Characters/AmateurFighter/"+sheet).OfType<Sprite>().Single(s=>s.name==name);
  static void Build()
  {
   var shader=Shader.Find("MMA/Fighter Skin Palette");if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Skin shader did not compile.");
   var original=AssetDatabase.LoadAssetAtPath<Material>(Root+"/TrainingPreview/PixelArt.mat");
   string materialPath=Root+"/TrainingPreview/FighterSkinPalette.mat";
   var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
   if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
   var sprites=new[]{Frame("fighter_idle_sheet.png","idle_00"),Frame("fighter_jab_sheet.png","jab_05"),Frame("fighter_low_kick_sheet.png","low_kick_09"),Frame("fighter_high_kick_sheet.png","high_kick_13"),Frame("fighter_lead_hook_sheet.png","lead_hook_09")};
   var masks=new[]{"idle","jab","low_kick","high_kick","lead_hook"}.Select(name=>
   {
    string path=Root+"/Art/Characters/AmateurFighter/SkinMasks/"+name+"_skin_mask.png";
    var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(!importer)throw new Exception("Missing mask "+name);
    importer.textureType=TextureImporterType.Default;importer.sRGBTexture=false;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.isReadable=true;importer.SaveAndReimport();
    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
   }).ToArray();
   int checks=ValidatePalette(sprites,masks,original,material);
   var scene=SceneManager.GetSceneByPath(Interior);
   if(!scene.IsValid()||!scene.isLoaded||scene.isDirty)throw new Exception("Open saved GymInteriorPreview before installing appearance controls.");
   string backup="/Users/souhilbenammour/Documents/Codex/2026-10-14/cre/work/skin_options_backup/GymInteriorPreview.unity";
   if(!File.Exists(backup))File.Copy(Interior,backup);
   var control=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TrainingPreview>()).Single();
   control.fighterSprite.sharedMaterial=material;
   var appearance=control.fighterSprite.GetComponent<FighterAppearance>();if(!appearance)appearance=control.fighterSprite.gameObject.AddComponent<FighterAppearance>();
   appearance.ConfigureMasks(sprites.Select(s=>s.texture).ToArray(),masks);appearance.SetSkinTone(2);EditorUtility.SetDirty(appearance);EditorUtility.SetDirty(control.fighterSprite);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
   RenderComparison(scene,sprites[0]);
   File.WriteAllText(Output+"/integration.json","{\"skinTones\":5,\"shaderRenderChecks\":"+checks+",\"clothingAndAlphaPreserved\":true,\"originalMediumMatches\":true,\"scene\":\""+Interior+"\",\"playMode\":\"pending\"}");
   if(File.Exists(Output+"/error.txt"))File.Delete(Output+"/error.txt");
   SessionState.SetString("MMA.Skin.PreviousPlayScene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
   EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Interior);SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
  }
  static Color32[] Capture(Camera cam,int width,int height,string path=null)
  {
   var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);rt.antiAliasing=1;
   var old=RenderTexture.active;var target=cam.targetTexture;Texture2D image=null;
   try
   {
    cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;image=new Texture2D(width,height,TextureFormat.RGBA32,false);
    image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();if(path!=null)File.WriteAllBytes(path,image.EncodeToPNG());return image.GetPixels32();
   }
   finally{cam.targetTexture=target;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(rt);if(image)UnityEngine.Object.DestroyImmediate(image);}
  }
  static bool Near(Color32 a,Color32 b,int tolerance)=>Math.Abs(a.r-b.r)<=tolerance&&Math.Abs(a.g-b.g)<=tolerance&&Math.Abs(a.b-b.b)<=tolerance;
  static bool IsSkin(Color32 c)
  {
   foreach(string hex in SkinHex){ColorUtility.TryParseHtmlString("#"+hex,out var match);if(Near(c,(Color32)match,2))return true;}return false;
  }
  static int ValidatePalette(Sprite[] sprites,Texture2D[] masks,Material original,Material palette)
  {
   var scene=EditorSceneManager.NewPreviewScene();int checks=0;
   try
   {
    var go=new GameObject("Palette pixel check");SceneManager.MoveGameObjectToScene(go,scene);var sr=go.AddComponent<SpriteRenderer>();
    var cameraObject=new GameObject("Palette check camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);var cam=cameraObject.AddComponent<Camera>();cam.scene=scene;cam.orthographic=true;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.clear;cam.allowHDR=false;cam.allowMSAA=false;
    FighterAppearance look=null;
    for(int frameIndex=0;frameIndex<sprites.Length;frameIndex++)
    {
     var sprite=sprites[frameIndex];var maskPixels=masks[frameIndex].GetPixels32();
     sr.sprite=sprite;sr.sharedMaterial=original;sr.SetPropertyBlock(null);
     cam.orthographicSize=sprite.rect.height/128f;cam.transform.position=new Vector3(sr.bounds.center.x,sr.bounds.center.y,-10);
     int width=(int)sprite.rect.width,height=(int)sprite.rect.height;
     var baseline=Capture(cam,width,height);
     sr.sharedMaterial=palette;if(!look)look=go.AddComponent<FighterAppearance>();look.ConfigureMasks(sprites.Select(s=>s.texture).ToArray(),masks);
     double previous=double.MaxValue;
     for(int t=0;t<5;t++)
     {
      look.SetSkinTone(t);var pixels=Capture(cam,width,height);int changed=0,skinPixels=0;double brightness=0;
      for(int i=0;i<pixels.Length;i++)
      {
       if(pixels[i].a!=baseline[i].a)throw new Exception("Palette changed transparency.");
       if(baseline[i].a==0)continue;
       bool skin=maskPixels[((int)sprite.rect.y+i/width)*masks[frameIndex].width+(int)sprite.rect.x+i%width].r>127;
       if(skin){skinPixels++;brightness+=pixels[i].r+pixels[i].g+pixels[i].b;if(!Near(pixels[i],baseline[i],2))changed++;}
       else if(!Near(pixels[i],baseline[i],1))throw new Exception("Palette changed a non-skin pixel in "+sprite.name);
       if(t==2&&!Near(pixels[i],baseline[i],1))throw new Exception("Original medium tone changed.");
      }
      if(skinPixels<100||t!=2&&changed<100)throw new Exception("Not enough skin pixels recolored in "+sprite.name+" tone "+t+" ("+changed+").");
      if(brightness>=previous)throw new Exception("Skin tones are not distinct from light to deep.");previous=brightness;checks++;
     }
    }
   }
   finally{EditorSceneManager.ClosePreviewScene(scene);}
   return checks;
  }
  static void RenderComparison(Scene source,Sprite guard)
  {
   var scene=EditorSceneManager.NewPreviewScene();
   try
   {
    var roots=source.GetRootGameObjects();
    foreach(var sr in roots.SelectMany(r=>r.GetComponentsInChildren<SpriteRenderer>()).Where(r=>r.name=="Approved MMA gym wall"||r.name=="Approved wooden floor"))
    {var copy=UnityEngine.Object.Instantiate(sr.gameObject);SceneManager.MoveGameObjectToScene(copy,scene);}
    var fighter=roots.SelectMany(r=>r.GetComponentsInChildren<TrainingPreview>()).Single().fighterSprite;
    var created=new List<FighterAppearance>();
    for(int i=0;i<5;i++)
    {
     var copy=UnityEngine.Object.Instantiate(fighter.gameObject);SceneManager.MoveGameObjectToScene(copy,scene);
     var animator=copy.GetComponent<Animator>();if(animator)UnityEngine.Object.DestroyImmediate(animator);
     var sr=copy.GetComponent<SpriteRenderer>();sr.sprite=guard;copy.transform.position=new Vector3(-3.6f+i*1.8f,.12f,0);
     var look=copy.GetComponent<FighterAppearance>();look.SetSkinTone(i);created.Add(look);
    }
    for(int i=0;i<5;i++)if(created[i].SkinTone!=i)throw new Exception("Skin choice leaked to another fighter.");
    var originalCamera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>()).Single();var obj=UnityEngine.Object.Instantiate(originalCamera.gameObject);SceneManager.MoveGameObjectToScene(obj,scene);
    var cam=obj.GetComponent<Camera>();cam.scene=scene;cam.transform.position=new Vector3(0,1.05f,-10);cam.orthographicSize=1.6875f;Capture(cam,1600,540,Output+"/skin_tones_in_gym.png");
   }
   finally{EditorSceneManager.ClosePreviewScene(scene);}
  }
  static void PlayChanged(PlayModeStateChange change)
  {
   if(!SessionState.GetBool(Pending,false))return;
   if(change==PlayModeStateChange.EnteredPlayMode){stage=0;tone=0;changes=0;nextFrame=10;started=EditorApplication.timeSinceStartup;observed.Clear();errors.Clear();Application.logMessageReceived+=Log;EditorApplication.update+=Check;}
   if(change==PlayModeStateChange.EnteredEditMode)
   {
    EditorApplication.update-=Check;Application.logMessageReceived-=Log;var path=SessionState.GetString("MMA.Skin.PreviousPlayScene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(path)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(path);SessionState.SetBool(Pending,false);
   }
  }
  static void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
  static void Check()
  {
   if(EditorApplication.timeSinceStartup-started>120){Finish("Timed out");return;}
   var p=UnityEngine.Object.FindAnyObjectByType<TrainingPreview>();if(!p||p.FramesRun<nextFrame)return;
   var appearance=p.fighterSprite.GetComponent<FighterAppearance>();if(!appearance){Finish("Missing appearance component");return;}
   string wanted=stage==0?"Idle":stage==1?"Jab":stage==2?"LowKick":stage==3?"HighKick":"LeadHook";var state=p.fighter.GetCurrentAnimatorStateInfo(0);if(!state.IsName(wanted))return;
   observed.Add(p.fighterSprite.sprite.name);
   if(tone<5)
   {
    int beforeHash=state.fullPathHash;float beforeTime=state.normalizedTime;appearance.SetSkinTone(tone);
    var after=p.fighter.GetCurrentAnimatorStateInfo(0);
    if(appearance.SkinTone!=tone||after.fullPathHash!=beforeHash||Mathf.Abs(after.normalizedTime-beforeTime)>.0001f){Finish("Skin change reset animation or selection");return;}
    tone++;changes++;nextFrame=p.FramesRun+10;return;
   }
   if(observed.Count<3){Finish("Animation did not advance during skin selection");return;}
   if(stage==4){ScreenCapture.CaptureScreenshot(Output+"/unity_skin_selector.png");Finish(null);return;}
   stage++;tone=0;observed.Clear();p.StartDrill(stage==1?TrainingDrill.Jab:stage==2?TrainingDrill.LowKick:stage==3?TrainingDrill.HighKick:TrainingDrill.LeadHook);nextFrame=p.FramesRun+2;
  }
  static void Finish(string problem)
  {
   EditorApplication.update-=Check;Application.logMessageReceived-=Log;if(problem!=null)errors.Add(problem);
   string report="Skin options Play Mode validation: "+(errors.Count==0?"PASS":"FAIL")+"\nSelections checked: "+changes+" / 25\nIdle, jab, low-kick, high-kick and lead-hook states checked: "+(stage+1)+" / 5\n"+string.Join("\n",errors);
   File.WriteAllText(Output+"/playmode-validation.txt",report);Debug.Log("[MMA Skin Options] "+report);EditorApplication.ExitPlaymode();
  }
 }
}
