using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MmaManagerTycoon.Editor
{
 [InitializeOnLoad]
 public static class GymWallReview
 {
  const string Output="/Users/souhilbenammour/Documents/Codex/2026-10-14/cre/outputs/GymWall";
  const string Interior="Assets/MmaManagerTycoon/TrainingPreview/GymInteriorPreview.unity";
  const string Pending="MMA.InteriorCheck.Pending";
  static double started;
  static bool trained,paused,resumed;
  static float pauseTime;
  static string firstSprite;
  static bool advanced;
  static GymWallReview(){EditorApplication.update+=Run;EditorApplication.playModeStateChanged+=PlayChanged;}
  static void Run()
  {
   if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;
   EditorApplication.update-=Run;
   if(File.Exists(Output+"/unity-wall-review.json"))return;
   var source=SceneManager.GetSceneByPath(Interior);
   if(!source.IsValid() || !source.isLoaded){File.WriteAllText(Output+"/review-error.txt","Interior scene not loaded");return;}
   if(source.isDirty){File.WriteAllText(Output+"/review-error.txt","Interior scene has unsaved edits; deferred.");return;}
   // Reconnect copied controls to the objects in their own saved scene.
   var roots=source.GetRootGameObjects();
   var controls=roots.SelectMany(r=>r.GetComponentsInChildren<TrainingPreview>()).Single();
   controls.fighter=roots.SelectMany(r=>r.GetComponentsInChildren<Animator>()).Single();
   controls.fighterSprite=controls.fighter.GetComponent<SpriteRenderer>();
   controls.bag=roots.Single(r=>r.name.StartsWith("Approved heavy bag")).transform;
   EditorUtility.SetDirty(controls);EditorSceneManager.MarkSceneDirty(source);EditorSceneManager.SaveScene(source);
   var scene=EditorSceneManager.NewPreviewScene();Texture2D texture=null,image=null;Sprite sprite=null;RenderTexture rt=null;var oldRT=RenderTexture.active;
   try
   {
    foreach(var root in roots){var clone=UnityEngine.Object.Instantiate(root);SceneManager.MoveGameObjectToScene(clone,scene);}
    var renderers=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SpriteRenderer>()).ToArray();
    if(renderers.Length!=27 || renderers.Any(r=>!r.sprite))throw new Exception("Expected all 27 saved interior sprites, including floor and contact shadows.");
    texture=new Texture2D(2,2,TextureFormat.RGBA32,false);texture.LoadImage(File.ReadAllBytes(Output+"/gym_wall.png"));texture.filterMode=FilterMode.Point;
    sprite=Sprite.Create(texture,new Rect(0,0,704,240),new Vector2(.5f,11f/240f),64,0,SpriteMeshType.FullRect);
    var wall=new GameObject("Proposed gym wall");SceneManager.MoveGameObjectToScene(wall,scene);wall.transform.position=new Vector3(.4f,.32f,0);
    var sr=wall.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingOrder=-50;sr.sharedMaterial=renderers[0].sharedMaterial;
    var camera=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>()).Single();camera.scene=scene;
    camera.transform.position=new Vector3(.4f,1.35f,-10);camera.orthographicSize=2.25f;
    rt=new RenderTexture(1920,864,24);rt.antiAliasing=1;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
    image=new Texture2D(1920,864,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,864),0,0);image.Apply();File.WriteAllBytes(Output+"/gym_wall_review.png",image.EncodeToPNG());
    File.WriteAllText(Output+"/unity-wall-review.json","{\"render\":\"PASS\",\"savedInteriorSprites\":27,\"floorAndShadowsReloaded\":true,\"trainingReferences\":\"Same-scene references verified and saved\",\"wall\":\"One new Blender wall asset; temporary review scene\"}");
    Debug.Log("[MMA Wall] Wall review rendered; saved floor and shadows verified.");
   }
   catch(Exception e){File.WriteAllText(Output+"/review-error.txt",e.ToString());Debug.LogException(e);}
   finally{RenderTexture.active=oldRT;EditorSceneManager.ClosePreviewScene(scene);foreach(var o in new UnityEngine.Object[]{sprite,texture,image,rt})if(o)UnityEngine.Object.DestroyImmediate(o);}
   if(File.Exists(Output+"/unity-wall-review.json") && !Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
   {
    SessionState.SetString("MMA.InteriorCheck.PreviousPlayScene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
    SessionState.SetBool(Pending,true);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Interior);EditorApplication.EnterPlaymode();
   }
  }
  static void PlayChanged(PlayModeStateChange state)
  {
   if(!SessionState.GetBool(Pending,false))return;
   if(state==PlayModeStateChange.EnteredPlayMode){started=EditorApplication.timeSinceStartup;trained=paused=resumed=advanced=false;firstSprite=null;EditorApplication.update+=Check;}
   if(state==PlayModeStateChange.EnteredEditMode)
   {
    EditorApplication.update-=Check;var path=SessionState.GetString("MMA.InteriorCheck.PreviousPlayScene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(path)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(path);SessionState.SetBool(Pending,false);
   }
  }
  static void Check()
  {
   var p=UnityEngine.Object.FindAnyObjectByType<TrainingPreview>();
   if(EditorApplication.timeSinceStartup-started>120){Finish("FAIL: timed out");return;}
   if(!p || p.FramesRun<2)return;
   if(!p.fighter || !p.fighterSprite || !p.bag){Finish("FAIL: missing training references");return;}
   if(firstSprite==null)firstSprite=p.fighterSprite.sprite.name;
   if(p.fighterSprite.sprite.name!=firstSprite)advanced=true;
   var t=p.PreviewElapsed;
   if(t>.5f&&!trained){p.SetTraining(true);trained=true;}
   if(t>1.8f&&!paused){p.SetPaused(true);pauseTime=p.fighter.GetCurrentAnimatorStateInfo(0).normalizedTime;paused=true;}
   if(t>2.1f&&!resumed)
   {
    if(Mathf.Abs(pauseTime-p.fighter.GetCurrentAnimatorStateInfo(0).normalizedTime)>.001f){Finish("FAIL: pause drift");return;}
    p.SetPaused(false);resumed=true;p.SetTraining(false);
   }
   if(t>2.5f)Finish(advanced && p.Impacts>0 && p.fighter.GetCurrentAnimatorStateInfo(0).IsName("Idle")?"PASS: saved interior controls, animation advance, bag impact, pause, resume, and return to idle. Frames: "+p.FramesRun:"FAIL: animation or impact check");
  }
  static void Finish(string result){File.WriteAllText(Output+"/interior-playmode-validation.txt",result+"\n");Debug.Log("[MMA Interior] "+result);EditorApplication.update-=Check;EditorApplication.ExitPlaymode();}
 }
}
