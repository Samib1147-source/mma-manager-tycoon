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
    public static class TrainingPreviewBuilder
    {
        const string Root = "Assets/MmaManagerTycoon";
        const string Art = Root + "/Art/Characters/AmateurFighter";
        const string Generated = Root + "/TrainingPreview";
        const string ScenePath = Generated + "/TrainingPreview.unity";
        const string Output = "/Users/souhilbenammour/Documents/Codex/2026-10-14/cre/outputs/TrainingPreview";
        const string Pending = "MMA.PreviewValidation.Pending";
        static double started;
        static bool trainingStarted, pauseStarted, resumed, idleReturned, screenshot;
        static float pausedTime;
        static readonly HashSet<string> idleSprites = new HashSet<string>();
        static readonly HashSet<string> jabSprites = new HashSet<string>();
        static readonly List<string> failures = new List<string>();
        static bool pausedVerified;

        static TrainingPreviewBuilder()
        {
            EditorApplication.update += BuildOnce;
            EditorApplication.playModeStateChanged += PlayChanged;
        }
        static void BuildOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= BuildOnce;
            if (File.Exists(Generated + "/build-complete.json"))
            {
                if(File.Exists(Output+"/revalidate.request")) { File.Delete(Output+"/revalidate.request");BeginValidation(); }
                return;
            }
            try { Build(); }
            catch(Exception ex) { Directory.CreateDirectory(Output); File.WriteAllText(Output+"/build-error.txt",ex.ToString()); Debug.LogException(ex); }
        }
        static Sprite[] Slice(string clip)
        {
            string path = Art + "/fighter_" + clip + "_sheet.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new Exception("Missing sheet: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 64;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var rects = new List<SpriteRect>();
            for(int i=0;i<16;i++) rects.Add(new SpriteRect { name=clip+"_"+i.ToString("D2"), rect=new Rect(i%4*96,(3-i/4)*128,96,128), alignment=SpriteAlignment.Custom, pivot=new Vector2(.5f,.0859375f), spriteID=GUID.Generate() });
            provider.SetSpriteRects(rects.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
            provider.Apply(); importer.SaveAndReimport();
            var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
            if(sprites.Length!=16) throw new Exception("Expected 16 frames in "+clip);
            return sprites;
        }
        static AnimationClip Clip(string name,Sprite[] sprites,int fps)
        {
            var clip=new AnimationClip { name=name, frameRate=fps };
            var binding=new EditorCurveBinding { path="", type=typeof(SpriteRenderer), propertyName="m_Sprite" };
            var keys=new ObjectReferenceKeyframe[17];
            for(int i=0;i<16;i++) keys[i]=new ObjectReferenceKeyframe {time=i/(float)fps,value=sprites[i]};
            keys[16]=new ObjectReferenceKeyframe {time=16f/fps,value=sprites[0]};
            AnimationUtility.SetObjectReferenceCurve(clip,binding,keys);
            var settings=AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
            AssetDatabase.CreateAsset(clip,Generated+"/"+name+".anim");
            return clip;
        }
        static SpriteRenderer SpriteObject(string name,Sprite sprite,Vector3 position,int order,Material material)
        {
            var go=new GameObject(name);go.transform.position=position;
            var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingOrder=order;sr.sharedMaterial=material;
            return sr;
        }
        static void Build()
        {
            Directory.CreateDirectory(Output); Directory.CreateDirectory(Generated);AssetDatabase.Refresh();
            var idle=Slice("idle"); var jab=Slice("jab");
            var idleClip=Clip("Idle",idle,8);var jabClip=Clip("Jab",jab,16);
            var controller=AnimatorController.CreateAnimatorControllerAtPath(Generated+"/Fighter.controller");
            controller.AddParameter("Training",AnimatorControllerParameterType.Bool);
            var sm=controller.layers[0].stateMachine;
            var a=sm.AddState("Idle");a.motion=idleClip;var b=sm.AddState("Jab");b.motion=jabClip;sm.defaultState=a;
            var ab=a.AddTransition(b);ab.hasExitTime=false;ab.duration=0;ab.AddCondition(AnimatorConditionMode.If,0,"Training");
            var ba=b.AddTransition(a);ba.hasExitTime=false;ba.duration=0;ba.AddCondition(AnimatorConditionMode.IfNot,0,"Training");
            var shader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if(!shader) throw new Exception("2D unlit shader not found");
            var material=new Material(shader);AssetDatabase.CreateAsset(material,Generated+"/PixelArt.mat");
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=2.25f;
                camera.transform.position=new Vector3(0,1.5f,-10);camera.backgroundColor=new Color(.063f,.082f,.11f);camera.clearFlags=CameraClearFlags.SolidColor;
                camera.allowHDR=false;camera.allowMSAA=false;
                camera.gameObject.AddComponent<AudioListener>();
                var mat=SpriteObject("Approved training mat",AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/Equipment/TrainingMat/training_mat.png"),Vector3.zero,0,material);
                var bag=SpriteObject("Approved heavy bag",AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/Equipment/HeavyBag/heavy_bag.png"),new Vector3(.13f,3.05f,0),1,material);
                var fighter=SpriteObject("Approved amateur fighter",idle[0],new Vector3(-.75f,.171875f,0),2,material);
                var animator=fighter.gameObject.AddComponent<Animator>();animator.runtimeAnimatorController=controller;
                var preview=new GameObject("Training controls").AddComponent<TrainingPreview>();preview.fighter=animator;preview.bag=bag.transform;preview.fighterSprite=fighter;
                EditorSceneManager.SaveScene(scene,ScenePath);
                // Check every frame binding and ordering using Unity's own clip sampler.
                for(int i=0;i<16;i++)
                {
                    idleClip.SampleAnimation(fighter.gameObject,(i+.1f)/8);if(fighter.sprite!=idle[i]) throw new Exception("Idle sample mismatch at "+i);
                    jabClip.SampleAnimation(fighter.gameObject,(i+.1f)/16);if(fighter.sprite!=jab[i]) throw new Exception("Jab sample mismatch at "+i);
                }
            }
            finally { EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous); }
            AssetDatabase.SaveAssets();
            File.WriteAllText(Generated+"/build-complete.json","{\"slicedFrames\":32,\"sampledFrames\":32,\"scene\":\""+ScenePath+"\"}");
            File.WriteAllText(Output+"/integration.json","{\"slicedFrames\":32,\"sampledFrames\":32,\"scene\":\""+ScenePath+"\",\"playMode\":\"pending\"}");
            BeginValidation();
        }
        static void BeginValidation()
        {
            bool dirty=Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty);
            if(dirty) {File.WriteAllText(Output+"/playmode-deferred.txt","Play Mode check deferred because an existing scene has unsaved changes.");return;}
            SessionState.SetString("MMA.PreviousPlayScene",EditorSceneManager.playModeStartScene ? AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) : "");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
        }
        static void PlayChanged(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Pending,false)) return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                trainingStarted=pauseStarted=resumed=idleReturned=screenshot=pausedVerified=false;
                idleSprites.Clear();jabSprites.Clear();failures.Clear();
                started=EditorApplication.timeSinceStartup;EditorApplication.update+=ValidatePlay;
                Application.logMessageReceived+=CaptureError;
            }
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update-=ValidatePlay;Application.logMessageReceived-=CaptureError;
                var path=SessionState.GetString("MMA.PreviousPlayScene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(path)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                SessionState.SetBool(Pending,false);
                bool dirty=Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty);
                if(!dirty) EditorSceneManager.OpenScene(ScenePath);
            }
        }
        static void CaptureError(string message,string stack,LogType type)
        { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) failures.Add(message); }
        static void ValidatePlay()
        {
            double wall=EditorApplication.timeSinceStartup-started;
            var p=UnityEngine.Object.FindAnyObjectByType<TrainingPreview>();
            if(!p) {if(wall>60) Finish(null,"Preview component missing");return;}
            if(wall>120) {Finish(p,"Timed out waiting for game frames");return;}
            if(p.FramesRun<2) return;
            double t=p.PreviewElapsed;
            var state=p.fighter.GetCurrentAnimatorStateInfo(0);
            if(p.fighterSprite.sprite)
            { if(state.IsName("Idle")) idleSprites.Add(p.fighterSprite.sprite.name);if(state.IsName("Jab")) jabSprites.Add(p.fighterSprite.sprite.name); }
            if(t>.8 && !trainingStarted) {p.SetTraining(true);trainingStarted=true;}
            if(t>1.3 && !screenshot) {ScreenCapture.CaptureScreenshot(Output+"/unity_training_preview.png");screenshot=true;}
            if(t>2.1 && !pauseStarted) {p.SetPaused(true);pausedTime=p.fighter.GetCurrentAnimatorStateInfo(0).normalizedTime;pauseStarted=true;}
            if(t>2.45 && pauseStarted && !resumed)
            {
                pausedVerified=Mathf.Abs(p.fighter.GetCurrentAnimatorStateInfo(0).normalizedTime-pausedTime)<.001f;
                p.SetPaused(false);resumed=true;
            }
            if(t>2.9 && !idleReturned) {p.SetTraining(false);idleReturned=true;}
            if(t>3.4) Finish(p,null);
        }
        static void Finish(TrainingPreview p,string problem)
        {
            EditorApplication.update-=ValidatePlay;Application.logMessageReceived-=CaptureError;
            if(problem!=null) failures.Add(problem);
            if(idleSprites.Count<2) failures.Add("Idle did not advance");
            if(jabSprites.Count<3) failures.Add("Jab did not advance");
            if(!pausedVerified) failures.Add("Pause did not hold animation time");
            if(p && !p.fighter.GetCurrentAnimatorStateInfo(0).IsName("Idle")) failures.Add("Did not return to idle");
            if(p && p.Impacts<1) failures.Add("No bag impacts");
            string report="Play Mode validation: "+(failures.Count==0?"PASS":"FAIL")+"\nGame frames executed: "+(p?p.FramesRun:0)+"\nIdle frames observed: "+idleSprites.Count+"\nJab frames observed: "+jabSprites.Count+"\nPause held animation time: "+pausedVerified+"\nBag impacts: "+(p?p.Impacts:0)+"\n"+string.Join("\n",failures);
            File.WriteAllText(Output+"/playmode-validation.txt",report);Debug.Log("[MMA Preview] "+report);EditorApplication.ExitPlaymode();
        }
    }
}
