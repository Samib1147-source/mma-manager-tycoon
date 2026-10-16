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
 public static class GymWallInstaller
 {
  const string Output="/Users/souhilbenammour/Documents/Codex/2026-10-14/cre/outputs/GymWall";
  const string ScenePath="Assets/MmaManagerTycoon/TrainingPreview/GymInteriorPreview.unity";
  const string Art="Assets/MmaManagerTycoon/Art/Background/GymWall/gym_wall.png";
  static GymWallInstaller(){EditorApplication.update+=Run;}
  static void Run()
  {
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
   EditorApplication.update-=Run;
   if(File.Exists(Output+"/wall-installation.json"))return;
   try{Install();}catch(Exception e){File.WriteAllText(Output+"/installation-error.txt",e.ToString());Debug.LogException(e);}
  }
  static void Install()
  {
   var scene=SceneManager.GetSceneByPath(ScenePath);
   if(!scene.IsValid()||!scene.isLoaded||scene.isDirty)throw new Exception("Interior scene must be open without unsaved edits.");
   var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art);
   var importer=AssetImporter.GetAtPath(Art) as TextureImporter;
   if(!sprite||!importer||importer.filterMode!=FilterMode.Point||importer.spritePixelsPerUnit!=64||importer.mipmapEnabled)throw new Exception("Wall sprite import check failed.");
   if(scene.GetRootGameObjects().Any(o=>o.name=="Approved MMA gym wall"))throw new Exception("Wall already exists; refusing duplicate placement.");
   File.Copy(ScenePath,"/Users/souhilbenammour/Documents/Codex/2026-10-14/cre/work/GymInteriorPreview_before_wall.unity",false);
   var reference=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SpriteRenderer>()).First();
   var go=new GameObject("Approved MMA gym wall");SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=new Vector3(.4f,.32f,0);
   var wall=go.AddComponent<SpriteRenderer>();wall.sprite=sprite;wall.sortingOrder=-50;wall.sharedMaterial=reference.sharedMaterial;
   // Keep the full approved layout visible in the standard 16:9 Game view.
   var camera=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>()).Single();camera.orthographicSize=2.8125f;
   EditorSceneManager.MarkSceneDirty(scene);
   if(!EditorSceneManager.SaveScene(scene))throw new Exception("Could not save completed interior.");
   scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
   var roots=scene.GetRootGameObjects();var all=roots.SelectMany(r=>r.GetComponentsInChildren<SpriteRenderer>()).ToArray();
   if(all.Length!=28||all.Any(r=>!r.sprite))throw new Exception("Saved scene should reload 28 valid sprites.");
   var p=roots.SelectMany(r=>r.GetComponentsInChildren<TrainingPreview>()).Single();
   if(!p.fighter||!p.bag||!p.fighterSprite||p.fighter.gameObject.scene!=scene||p.bag.gameObject.scene!=scene)throw new Exception("Training reference check failed after scene reload.");
   var savedWall=roots.Single(o=>o.name=="Approved MMA gym wall").GetComponent<SpriteRenderer>();
   if(savedWall.sprite!=sprite||savedWall.sortingOrder!=-50)throw new Exception("Wall did not persist correctly.");
   // Render a temporary camera clone, leaving saved camera framing intact.
   var savedCamera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>()).Single();
   var cameraObject=UnityEngine.Object.Instantiate(savedCamera.gameObject);var capture=cameraObject.GetComponent<Camera>();capture.orthographicSize=2.25f;
   var rt=new RenderTexture(1920,864,24);Texture2D image=null;var old=RenderTexture.active;
   try{capture.targetTexture=rt;capture.Render();RenderTexture.active=rt;image=new Texture2D(1920,864,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,864),0,0);image.Apply();File.WriteAllBytes(Output+"/unity_completed_room.png",image.EncodeToPNG());}
   finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(rt);if(image)UnityEngine.Object.DestroyImmediate(image);}
   EditorSceneManager.SaveScene(scene);
   File.WriteAllText(Output+"/wall-installation.json","{\"scene\":\""+ScenePath+"\",\"wallImported\":true,\"savedAndReloadedSprites\":28,\"wallSortingOrder\":-50,\"trainingReferences\":\"PASS\",\"unityRender\":\"PASS\",\"standardGameView\":\"Full layout framed at 16:9\"}");
   Debug.Log("[MMA Wall Install] PASS: approved wall saved, 28 sprites reloaded, training references intact.");
  }
 }
}
