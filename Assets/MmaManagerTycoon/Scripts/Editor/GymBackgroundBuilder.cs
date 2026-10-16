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
    public static class GymBackgroundBuilder
    {
        const string Root="Assets/MmaManagerTycoon";
        const string Art=Root+"/Art/Background/";
        const string ScenePath=Root+"/TrainingPreview/GymBackgroundPreview.unity";
        const string Output="/Users/souhilbenammour/Documents/Codex/2026-10-14/cre/outputs/GymBackground";
        static GymBackgroundBuilder() { EditorApplication.update+=Run; }
        static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update-=Run;
            if(File.Exists(Output+"/integration.json"))return;
            try { Build(); } catch(Exception e) { File.WriteAllText(Output+"/error.txt",e.ToString());Debug.LogException(e); }
        }
        static void Build()
        {
            // Never replace the original training scene or overwrite a hand-edited preview.
            if(File.Exists(ScenePath))throw new Exception("Background preview already exists; refusing to overwrite.");
            if(!AssetDatabase.CopyAsset(Root+"/TrainingPreview/TrainingPreview.unity",ScenePath))throw new Exception("Could not copy training preview.");
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var holder=new GameObject("Approved background props").transform;
                var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/TrainingPreview/PixelArt.mat");
                Action<string,string,Vector2,int> put=(name,path,pos,order)=>
                {
                    var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+path);
                    if(!sprite)throw new Exception("Missing sprite: "+path);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(Art+path);
                    if(importer.filterMode!=FilterMode.Point || importer.spritePixelsPerUnit!=64 || importer.mipmapEnabled)
                        throw new Exception("Unexpected pixel-art import settings: "+path);
                    var obj=new GameObject(name);obj.transform.SetParent(holder);obj.transform.position=pos;
                    var sr=obj.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingOrder=order;sr.sharedMaterial=material;
                };
                Vector2 shelf=new Vector2(-3.15f,.12f);
                put("Steel and timber mitt shelf","StorageShelf/storage_shelf.png",shelf,-20);
                int[,] mitts={{34,45},{55,45},{76,45},{104,74},{126,74},{44,103},{66,103}};
                for(int i=0;i<mitts.GetLength(0);i++)
                    put("Boxing focus mitt "+(i+1),"BoxingMitts/boxing_mitt_"+(i%2==0?"front":"back")+".png",
                        shelf+new Vector2((mitts[i,0]-80)/64f,(116-mitts[i,1])/64f),-19);
                Vector2 cabinet=new Vector2(2.1f,.12f);
                put("Glove cabinet body","GlassCabinet/cabinet_body.png",cabinet,-20);
                foreach(int row in new[]{48,76,104})foreach(int col in new[]{37,76})
                    put("Display glove "+col+"-"+row,"BoxingGlove/boxing_glove.png",cabinet+new Vector2((col-56)/64f,(116-row)/64f),-19);
                put("Cabinet glass doors","GlassCabinet/cabinet_glass.png",cabinet,-18);
                put("Open empty carton A","CardboardBox/cardboard_box.png",new Vector2(3.35f,.12f),-17);
                put("Open empty carton B","CardboardBox/cardboard_box.png",new Vector2(4.35f,.12f),-17);
                var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).Single();
                camera.transform.position=new Vector3(.4f,1.5f,-10);camera.orthographicSize=2.7f;
                camera.backgroundColor=new Color(.09f,.105f,.12f);
                EditorSceneManager.SaveScene(scene);
                // Actual Unity render, at integer pixel scale (192 pixels high in world space).
                // A tighter scene camera gives 3x source-pixel output for a clean layout review.
                float oldSize=camera.orthographicSize;var oldPos=camera.transform.position;
                camera.orthographicSize=2f;camera.transform.position=new Vector3(.4f,1.65f,-10);
                var rt=new RenderTexture(1920,768,24);rt.antiAliasing=1;
                var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
                Texture2D image=null;
                try
                {
                    camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                    image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                    image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
                    File.WriteAllBytes(Output+"/unity_background_preview.png",image.EncodeToPNG());
                }
                finally
                {
                    camera.targetTexture=oldTarget;RenderTexture.active=oldActive;
                    UnityEngine.Object.DestroyImmediate(rt);if(image)UnityEngine.Object.DestroyImmediate(image);
                    camera.orthographicSize=oldSize;camera.transform.position=oldPos;
                }
                if(holder.childCount!=18)throw new Exception("Expected 18 background sprite instances.");
                File.WriteAllText(Output+"/integration.json","{\"scene\":\""+ScenePath+"\",\"backgroundSpriteInstances\":18,\"mitts\":7,\"gloves\":6,\"emptyCartons\":2,\"glassSorting\":\"body < gloves < glass\",\"spriteImportChecks\":\"PASS\",\"unityCameraRender\":\"PASS\"}");
                Debug.Log("[MMA Background] Imported and rendered 18 approved background sprites successfully.");
            }
            finally {EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);}
            if(!Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
