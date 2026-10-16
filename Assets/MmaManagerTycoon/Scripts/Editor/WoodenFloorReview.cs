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
 public static class WoodenFloorReview
 {
  const string Output="/Users/souhilbenammour/Documents/Codex/2026-10-14/cre/outputs/WoodenFloor";
  static WoodenFloorReview(){EditorApplication.update+=Run;}
  static void Run()
  {
   if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;
   EditorApplication.update-=Run;
   if(File.Exists(Output+"/unity-floor-review.json"))return;
   var source=SceneManager.GetSceneByPath("Assets/MmaManagerTycoon/TrainingPreview/GymBackgroundPreview.unity");
   if(!source.IsValid() || !source.isLoaded){File.WriteAllText(Output+"/review-error.txt","Open GymBackgroundPreview to render the floor review.");return;}
   var scene=EditorSceneManager.NewPreviewScene();
   Texture2D floorTexture=null,shadowTexture=null,image=null;
   Sprite floorSprite=null,shadowSprite=null;
   RenderTexture rt=null;
   var oldRT=RenderTexture.active;
   try
   {
    foreach(var root in source.GetRootGameObjects())
    {
     var clone=UnityEngine.Object.Instantiate(root);SceneManager.MoveGameObjectToScene(clone,scene);
    }
    var renderers=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SpriteRenderer>()).ToArray();
    // Preserve baked form and highlights, applying a restrained warm room tint.
    foreach(var sr in renderers)sr.color=new Color(1f,.93f,.78f,1);
    floorTexture=new Texture2D(2,2,TextureFormat.RGBA32,false);floorTexture.LoadImage(File.ReadAllBytes(Output+"/wooden_floor.png"));floorTexture.filterMode=FilterMode.Point;
    floorSprite=Sprite.Create(floorTexture,new Rect(0,0,704,80),new Vector2(.5f,1),64,0,SpriteMeshType.FullRect);
    var floor=new GameObject("Proposed wooden floor");SceneManager.MoveGameObjectToScene(floor,scene);
    floor.transform.position=new Vector3(.4f,.34f+9f/64f,0);var floorRenderer=floor.AddComponent<SpriteRenderer>();floorRenderer.sprite=floorSprite;floorRenderer.sharedMaterial=renderers[0].sharedMaterial;floorRenderer.sortingOrder=-40;
    // Soft contact shadows anchor the props to the new floor without altering art.
    shadowTexture=new Texture2D(64,16,TextureFormat.RGBA32,false);shadowTexture.filterMode=FilterMode.Point;
    for(int y=0;y<16;y++)for(int x=0;x<64;x++)
    {
     float dx=(x-31.5f)/32,dy=(y-7.5f)/8;
     float alpha=Mathf.Pow(Mathf.Clamp01(1-dx*dx-dy*dy),2)*.35f;
     shadowTexture.SetPixel(x,y,new Color(.08f,.05f,.025f,Mathf.Round(alpha*16)/16));
    }
    shadowTexture.Apply();shadowSprite=Sprite.Create(shadowTexture,new Rect(0,0,64,16),new Vector2(.5f,.5f),64);
    foreach(var placement in new[]{new Vector3(-3.15f,.1f,2.2f),new Vector3(2.1f,.1f,1.35f),new Vector3(3.35f,.1f,.9f),new Vector3(4.35f,.1f,.9f),new Vector3(0,-.02f,3.8f)})
    {
     var go=new GameObject("Floor contact shadow");SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=new Vector3(placement.x,placement.y,0);go.transform.localScale=new Vector3(placement.z,.65f,1);
     var sr=go.AddComponent<SpriteRenderer>();sr.sprite=shadowSprite;sr.sharedMaterial=renderers[0].sharedMaterial;sr.sortingOrder=-30;
    }
    var camera=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>()).Single();
    camera.scene=scene;camera.transform.position=new Vector3(.4f,1.35f,-10);camera.orthographicSize=2.25f;camera.backgroundColor=new Color(.12f,.105f,.08f);
    rt=new RenderTexture(1920,864,24);rt.antiAliasing=1;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
    image=new Texture2D(1920,864,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,864),0,0);image.Apply();File.WriteAllBytes(Output+"/wooden_floor_gym_review.png",image.EncodeToPNG());
    File.WriteAllText(Output+"/unity-floor-review.json","{\"render\":\"PASS\",\"existingSpriteInstances\":"+renderers.Length+",\"floor\":\"One Blender-rendered floor sprite\",\"lighting\":\"Baked warm yellow floor lights, warm prop tint, contact shadows\",\"status\":\"Temporary Unity review scene; pending visual approval\"}");
    Debug.Log("[MMA Floor] Wooden floor and warm lighting review rendered successfully.");
   }
   catch(Exception e){File.WriteAllText(Output+"/review-error.txt",e.ToString());Debug.LogException(e);}
   finally
   {
    RenderTexture.active=oldRT;EditorSceneManager.ClosePreviewScene(scene);
    foreach(var o in new UnityEngine.Object[]{floorSprite,shadowSprite,floorTexture,shadowTexture,image,rt})if(o)UnityEngine.Object.DestroyImmediate(o);
   }
  }
 }
}
