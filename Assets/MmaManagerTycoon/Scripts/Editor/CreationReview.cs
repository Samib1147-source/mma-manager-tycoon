using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace MmaManagerTycoon.Editor
{
    [InitializeOnLoad]
    public static class CreationReview
    {
        const string Output = "/Users/souhilbenammour/Documents/Codex/2026-10-14/cre/outputs/CareerRedesign";
        const string Scene = "Assets/MmaManagerTycoon/CareerUI/CareerStart.unity";
        const string Pending = "MMA.CreationReview";
        static readonly List<string> checks = new List<string>(), errors = new List<string>(), warnings = new List<string>();
        static int stage, nextFrame;
        static double started;
        static RenderTexture target;
        static PanelSettings panel;
        static string firstFrame, profileBefore;
        static CreationReview() { EditorApplication.update += Install; EditorApplication.playModeStateChanged += Changed; }
        static void Install()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlaying && File.Exists(Output+"/stop-preview.request") && !SessionState.GetBool(Pending,false) && !SessionState.GetBool("MMA.HighKick.Validation",false))
            { File.Delete(Output+"/stop-preview.request"); EditorApplication.ExitPlaymode(); return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= Install;
            if (File.Exists(Output+"/validation.txt") && !File.Exists(Output+"/revalidate.request")) return;
            if (File.Exists(Output+"/revalidate.request")) File.Delete(Output+"/revalidate.request");
            SessionState.SetString(Pending+".previous",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode();
        }
        static void Changed(PlayModeStateChange change)
        {
            if(!SessionState.GetBool(Pending,false))return;
            if(change==PlayModeStateChange.EnteredPlayMode)
            { stage=0;nextFrame=20;started=EditorApplication.timeSinceStartup;checks.Clear();errors.Clear();warnings.Clear();Application.logMessageReceived+=Log;EditorApplication.update+=Check; }
            if(change==PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update-=Check;Application.logMessageReceived-=Log;Clean();
                string previous=SessionState.GetString(Pending+".previous","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);SessionState.SetBool(Pending,false);
            }
        }
        static void Log(string message,string stack,LogType type)
        { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message); if(type==LogType.Warning)warnings.Add(message); }
        static void Assert(bool value,string message) { if(!value)throw new Exception(message);checks.Add(message); }
        static void Press(Button button)
        { if(button==null||!button.enabledInHierarchy)throw new Exception("Missing or disabled button");button.Focus();using(var e=NavigationSubmitEvent.GetPooled()){e.target=button;button.SendEvent(e);} }
        static void SetTarget(CareerStartUI ui,int width,int height)
        { Clean();panel=ui.GetComponent<UIDocument>().panelSettings;target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);target.Create();panel.targetTexture=target; }
        static void Clean() { if(panel)panel.targetTexture=null;if(target){target.Release();UnityEngine.Object.DestroyImmediate(target);}target=null;panel=null; }
        static void Capture(string name)
        {
            var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            try {image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());}
            finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);}
        }
        static void Dump(VisualElement root,string file)
        {
            var text=new StringBuilder();
            void Walk(VisualElement e,int depth){text.AppendLine(new string(' ',depth*2)+e.GetType().Name+" #"+e.name+" ."+string.Join(" .",e.GetClasses())+" "+e.worldBound);foreach(var child in e.Children())Walk(child,depth+1);}
            Walk(root,0);File.WriteAllText(Output+"/"+file+"-hierarchy.txt",text.ToString());
        }
        static void Layout(CareerStartUI ui)
        {
            var r=ui.Root;var bounds=r.Q("screen").worldBound;
            foreach(string name in new[]{"fighter-name","nickname","nationality","skin-tones","save-fighter","begin-life"})
            {
                var b=r.Q(name).worldBound;
                Assert(b.width>0&&b.height>0&&b.xMin>=bounds.xMin&&b.xMax<=bounds.xMax+1&&b.yMin>=bounds.yMin&&b.yMax<=bounds.yMax+1,name+" fits within the "+target.width+"×"+target.height+" screen");
            }
            var items=new[]{r.Q<TextField>("fighter-name"),r.Q<TextField>("nickname")};
            Assert(items[0].worldBound.yMax<items[1].worldBound.yMin,"Name and nickname do not overlap");
            Assert(r.Q("nationality").worldBound.yMax<r.Q("skin-tones").worldBound.yMin,"Nationality and appearance do not overlap");
            Assert(r.Q("skin-tones").worldBound.yMax<r.Q("save-fighter").worldBound.yMin,"Appearance and actions do not overlap");
            Assert(r.Q<Label>(className:"cr-title").resolvedStyle.unityFontDefinition.font.name.Contains("Barlow"),"Display typography uses Barlow Condensed");
            Assert(r.Q<Label>(className:"cr-description").resolvedStyle.unityFontDefinition.font.name.Contains("Inter"),"Body typography uses Inter");
        }
        static void Check()
        {
            if(EditorApplication.timeSinceStartup-started>180){Finish("Timed out");return;}
            var ui=UnityEngine.Object.FindAnyObjectByType<CareerStartUI>();if(!ui||!ui.Ready||ui.FramesRun<nextFrame)return;
            try
            {
                var r=ui.Root;
                switch(stage)
                {
                    case 0:
                        ui.validationSavePath=Output+"/test-profile.json";if(File.Exists(ui.validationSavePath))File.Delete(ui.validationSavePath);ui.ReloadProfile();SetTarget(ui,1920,1080);
                        firstFrame=ui.appearance.GetComponent<SpriteRenderer>().sprite.name;nextFrame=ui.FramesRun+30;stage++;break;
                    case 1:
                        Layout(ui);Capture("creation-empty-1080p");Dump(r,"creation");
                        Press(r.Q<Button>("save-fighter"));Assert(!File.Exists(ui.validationSavePath)&&r.Q<Label>("status").ClassListContains("error"),"Empty name is blocked with an inline error");
                        Assert(r.Q("fighter-name").ClassListContains("cr-invalid"),"Invalid name receives a field-level highlight");
                        r.Q<TextField>("fighter-name").value="  Alex Morgan  ";r.Q<TextField>("nickname").value="The Outlaw";Press(r.Q<Button>("save-fighter"));
                        Assert(!File.Exists(ui.validationSavePath)&&r.Q("nationality").ClassListContains("cr-invalid"),"Nationality remains required");
                        Press(r.Q<Button>("nationality"));r.Q<TextField>("country-search").value="zzzzno-country";
                        Assert(r.Query<Button>(className:"country-row").ToList().Count==0&&r.Q<Label>("country-count").text.Contains("No matches"),"Country search has a readable empty state");
                        r.Q<TextField>("country-search").value="France";
                        Assert(r.Query<Button>(className:"country-row").ToList().Count==1,"Country search filters by name");
                        Press(r.Q<Button>("country-FR"));Assert(ui.Draft.nationalityCode=="FR"&&r.Q("country-overlay").ClassListContains("hidden"),"Country selection updates identity and closes modal");
                        for(int i=0;i<FighterAppearance.ToneCount;i++){Press(r.Q<Button>("tone-"+i));Assert(ui.Draft.skinTone==i&&ui.appearance.SkinTone==i,"Skin tone "+i+" updates the live fighter");}
                        Press(r.Q<Button>("tone-2"));Press(r.Q<Button>("save-fighter"));
                        Assert(CareerProfiles.Load(ui.validationSavePath).fighterName=="Alex Morgan"&&ui.Saved,"Profile saves and cleans whitespace using the existing format");
                        ui.ReloadProfile();Assert(ui.Draft.nickname=="The Outlaw"&&ui.Draft.skinTone==2&&ui.Draft.nationalityCode=="FR","Reload restores nickname, nationality and appearance");
                        nextFrame=ui.FramesRun+25;stage++;break;
                    case 2:
                        Layout(ui);Capture("fighter-creation-1080p");
                        Assert(ui.appearance.GetComponent<Animator>().runtimeAnimatorController!=null,"Existing animation controller remains assigned");
                        Assert(ui.previewCamera.cullingMask==(1<<31)&&ui.previewCamera.backgroundColor.a==0,"Fighter preview uses isolated transparent camera composition");
                        SetTarget(ui,3840,2160);nextFrame=ui.FramesRun+20;stage++;break;
                    case 3:
                        Layout(ui);Capture("fighter-creation-4k");
                        Press(r.Q<Button>("nationality"));r.Q<TextField>("country-search").value="United";nextFrame=ui.FramesRun+20;stage++;break;
                    case 4:
                        Assert(r.Q("country-dialog").worldBound.yMax<=r.Q("screen").worldBound.yMax,"Nationality dialog fits the screen");
                        Assert(!r.Q(className:"form-panel").enabledSelf,"Nationality modal prevents background interaction");
                        Assert(r.Query<Button>(className:"country-row").ToList().Count>=3,"Search offers matching countries");
                        Capture("nationality-4k");Dump(r.Q("country-overlay"),"nationality");
                        r.Q<TextField>("country-search").value="FR";Assert(r.Q<Button>("country-FR")!=null,"Country search accepts ISO codes");
                        Press(r.Q<Button>("close-country"));Assert(r.Q(className:"form-panel").enabledSelf&&r.focusController.focusedElement==r.Q<Button>("nationality"),"Closing picker restores background and keyboard focus");
                        r.Q<TextField>("fighter-name").value=new string('W',32);r.Q<TextField>("nickname").value=new string('W',24);SetTarget(ui,1920,1080);nextFrame=ui.FramesRun+15;stage++;break;
                    case 5:
                        Layout(ui);Capture("long-name-1080p");
                        Assert(r.Q<Label>("preview-name").resolvedStyle.fontSize<=38,"Long names use a smaller display size");
                        ui.ReloadProfile();
                        string good=ui.validationSavePath,blocker=Output+"/write-blocker";File.WriteAllText(blocker,"test");ui.validationSavePath=blocker+"/save.json";
                        Assert(!ui.PersistDraft()&&r.Q<Label>("status").ClassListContains("error"),"Failed writes retain the draft and show an error");ui.validationSavePath=good;File.Delete(blocker);ui.ReloadProfile();
                        // Use the original model to seed a saved story; only identity editing is under review.
                        ui.Draft.life=new LifeProgress();FighterLife.Choose(ui.Draft.life,0);Assert(ui.PersistDraft(),"Existing life progress can still be saved");profileBefore=JsonUtility.ToJson(ui.Draft.life);
                        r.Q<TextField>("nickname").value="The Outlaw";Press(r.Q<Button>("save-fighter"));ui.ReloadProfile();
                        Assert(JsonUtility.ToJson(ui.Draft.life)==profileBefore&&FighterLife.Evaluate(ui.Draft.life).Money==80,"Editing identity preserves saved choices, pending receipt and money");
                        Press(r.Q<Button>("begin-life"));nextFrame=ui.FramesRun+35;stage++;break;
                    case 6:
                        Assert(ui.LifeUI.Visible&&r.Q("life-receipt")!=null,"Animated continue opens the existing pending receipt");
                        Assert(r.Q<Label>(className:"life-title").resolvedStyle.unityFontDefinition.font.name.Contains("Pixelify"),"Origins typography remains unchanged for the approval stage");
                        Assert(r.Q("life-receipt").Query<Label>().ToList().Any(l=>l.text.Contains("+2  Fight IQ")),"Existing confirmed outcome still shows exact stat changes");
                        Press(r.Q<Button>("life-profile"));nextFrame=ui.FramesRun+30;stage++;break;
                    case 7:
                        Assert(!ui.LifeUI.Visible&&r.Q("screen").resolvedStyle.opacity>.99f,"Returning from Origins restores the creation screen after its transition");
                        Assert(JsonUtility.ToJson(ui.Draft.life)==profileBefore,"Navigation preserves life progress");
                        // Reset only the isolated review save; never touch the player's default profile.
                        ui.Draft.life=null;ui.PersistDraft();nextFrame=ui.FramesRun+10;stage++;break;
                    case 8:
                        Assert(errors.Count==0,"No Unity Console errors during creation verification");Finish(null);break;
                }
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void Finish(string error)
        {
            EditorApplication.update-=Check;Application.logMessageReceived-=Log;if(error!=null)errors.Add(error);Clean();
            File.WriteAllText(Output+"/validation.txt",(errors.Count==0?"PASS":"FAIL")+" / "+checks.Count+" checks\n"+string.Join("\n",checks.Select(c=>"PASS: "+c))+"\n"+string.Join("\n",errors));
            File.WriteAllText(Output+"/console-warnings.txt",string.Join("\n",warnings.Distinct()));
            Debug.Log("[MMA Creation Redesign] "+(errors.Count==0?"PASS":"FAIL")+" / "+checks.Count+" checks / "+string.Join("; ",errors));EditorApplication.ExitPlaymode();
        }
    }
}
