using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MmaManagerTycoon.Editor
{
    [InitializeOnLoad]
    public static class PixelUIIntegration
    {
        const string Root = "Assets/MmaManagerTycoon";
        const string ScenePath = Root + "/CareerUI/CareerStart.unity";
        const string Training = Root + "/TrainingPreview/GymInteriorPreview.unity";
        const string Output = "/Users/souhilbenammour/Documents/Codex/2026-10-14/cre/outputs/PixelUI";
        const string Pending = "MMA.PixelUI.Validation";
        static readonly List<string> results = new List<string>();
        static readonly List<string> errors = new List<string>();
        static int stage, nextFrame;
        static double started, switched;
        static RenderTexture layoutTexture;
        static PanelSettings testedPanel;
        static PixelUIIntegration() { EditorApplication.update += Install; EditorApplication.playModeStateChanged += PlayChanged; }
        static void Install()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            // A one-shot request stops an idle preview so editor validation can run.
            if (EditorApplication.isPlaying && File.Exists(Output + "/stop-preview.request") && !SessionState.GetBool(Pending,false) && !SessionState.GetBool("MMA.HighKick.Validation",false))
            { File.Delete(Output + "/stop-preview.request"); EditorApplication.ExitPlaymode(); return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= Install;
            bool retry = File.Exists(Output + "/revalidate.request");
            if (File.Exists(Output + "/integration.json") && !retry) return;
            try
            {
                if (retry) File.Delete(Output + "/revalidate.request");
                if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) throw new Exception("Save open scenes before installing life-story UI.");
                var oldTraining = File.ReadAllBytes(Training);
                var scene = EditorSceneManager.OpenScene(ScenePath);
                var ui = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CareerStartUI>()).Single();
                ui.lifeStyles = AssetDatabase.LoadAssetAtPath<StyleSheet>(Root + "/CareerUI/LifeStory.uss");
                if (!ui.lifeStyles) throw new Exception("Life story stylesheet is missing.");
                EditorUtility.SetDirty(ui); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                string panelPath = Root + "/CareerUI/Resources/LifeTrainingPanel.asset";
                if (!AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath))
                {
                    var panel = UnityEngine.Object.Instantiate(ui.GetComponent<UIDocument>().panelSettings);
                    panel.name = "Life training status panel"; AssetDatabase.CreateAsset(panel, panelPath);
                }
                if (!EditorBuildSettings.scenes.Any(s => s.path == Training)) EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(Training, true) }).ToArray();
                AssetDatabase.SaveAssets();
                if (!oldTraining.SequenceEqual(File.ReadAllBytes(Training))) throw new Exception("Training scene must stay unchanged.");
                ValidateModel();
                File.WriteAllText(Output + "/integration.json", "{\"lifeChapters\":17,\"startingMoney\":100,\"mainStyleAge\":14,\"styles\":4,\"trainingPreserved\":true,\"playMode\":\"pending\"}");
                SessionState.SetString(Pending + ".previous", EditorSceneManager.playModeStartScene ? AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) : "");
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
                SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { File.WriteAllText(Output + "/error.txt", e.ToString()); Debug.LogException(e); }
        }
        static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        static void Pass(bool value, string message) { Assert(value, message); results.Add(message); }
        static void ValidateModel()
        {
            var messages = new List<string>();
            Assert(FighterLife.Chapters.Length == 17, "Expected one chapter for each age 0–16.");
            var start = FighterLife.Evaluate(null); Assert(start.Money == 100 && start.Age == 0, "Start with $100 at birth.");
            for (int age = 0; age <= 16; age++)
            {
                var chapter = FighterLife.Chapters[age];
                Assert(chapter.Choices.Any(c => c.Cash >= 0), "A free path is required at age " + age);
                Assert(chapter.Choices.All(c => c.Changes.Length > 0), "Every decision must affect stats.");
                Assert(chapter.Choices.All(c => (c.Style != null) == (age == 14)), "Style selection must happen only at age 14.");
            }
            messages.Add("17 chapters; every choice affects stats; every year has an affordable path.");
            var balances = new HashSet<int> { 100 }; int reachable = 0;
            foreach (var chapter in FighterLife.Chapters)
            {
                balances = new HashSet<int>(balances.SelectMany(m => chapter.Choices.Where(c => m + c.Cash >= 0).Select(c => m + c.Cash)));
                Assert(balances.Count > 0 && balances.Min() >= 0, "Money must never become negative."); reachable += balances.Count;
            }
            messages.Add("All " + reachable + " reachable age/balance combinations retain a valid no-debt route.");
            for (int style = 0; style < 4; style++)
            {
                var progress = new LifeProgress(); int expectedMoney = 100; int[] expectedStats = Enumerable.Repeat(10,17).ToArray();
                for (int age = 0; age <= 16; age++)
                {
                    int pick = age == 14 ? style : Array.FindIndex(FighterLife.Chapters[age].Choices, c => c.Cash >= 0);
                    var choice = FighterLife.Chapters[age].Choices[pick];
                    expectedMoney += choice.Cash;
                    foreach (var change in choice.Changes) expectedStats[(int)change.Stat] = Math.Max(0, Math.Min(100, expectedStats[(int)change.Stat] + change.Amount));
                    FighterLife.Choose(progress, pick);
                    Assert(!FighterLife.CanChoose(progress, pick), "Receipt must block a second grant.");
                    progress.pendingReceipt = false;
                }
                var state = FighterLife.Evaluate(progress);
                Assert(state.Complete && state.Age == 16 && state.Money == expectedMoney && state.Stats.SequenceEqual(expectedStats), "Final values must equal all chosen effects.");
                Assert(state.Style == FighterLife.Chapters[14].Choices[style].Style, "Style must persist.");
                Assert(!FighterLife.CanChoose(progress,0), "No extra decisions after age 16.");
            }
            messages.Add("All four main styles reach age 16 with exactly the expected stats and cash; duplicate choices blocked.");
            var low = new LifeState(); low.Stats[(int)FighterStat.Cardio] = 0;
            var capped = FighterLife.Preview(low,new LifeChoice("cap test","",0,new[]{new StatChange(FighterStat.Cardio,-2),new StatChange(FighterStat.Boxing,200)}));
            Assert(capped.Stats[(int)FighterStat.Cardio] == 0 && capped.Stats[(int)FighterStat.Boxing] == 100,"Ratings stay between 0 and 100.");
            var original = new CareerProfile { fighterName="Test Fighter", nationalityCode="FR" };
            string path = Output + "/model-profile.json"; CareerProfiles.Save(original,path);
            File.WriteAllText(path, "{\"version\":1,\"fighterName\":\"Legacy Fighter\",\"nickname\":\"\",\"nationalityCode\":\"FR\",\"skinTone\":2}");
            var legacy = FighterLife.Evaluate(CareerProfiles.Load(path).life);
            Assert(legacy.Money == 100 && legacy.Decisions == 0, "Existing identity-only profiles start at age 0 with $100.");
            original.life = new LifeProgress(); FighterLife.Choose(original.life,0); CareerProfiles.Save(original,path);
            var reloaded = CareerProfiles.Load(path); var once = FighterLife.Evaluate(reloaded.life); var twice = FighterLife.Evaluate(reloaded.life);
            Assert(once.Money==80 && twice.Money==80 && once.Stats.SequenceEqual(twice.Stats) && reloaded.life.pendingReceipt,"Reload must not grant effects again.");
            original.life.choices.Add(999); Assert(CareerProfiles.Validate(original)!=null,"Corrupt life choices must be rejected.");
            File.Delete(path); messages.Add("Old saves migrate safely; confirmed choice and receipt survive reload; corrupt choices rejected; ratings clamped.");
            File.WriteAllText(Output+"/model-validation.txt","Life model: PASS\n"+string.Join("\n",messages));
        }
        static void PlayChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending,false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            { stage=90; nextFrame=12; started=EditorApplication.timeSinceStartup; results.Clear(); errors.Clear(); Application.logMessageReceived+=Log; EditorApplication.update+=Check; }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update-=Check; Application.logMessageReceived-=Log; CleanCapture();
                string previous=SessionState.GetString(Pending+".previous",""); EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
                SessionState.SetBool(Pending,false);
            }
        }
        static void Log(string message,string stack,LogType type) { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) errors.Add(message); }
        static void Press(Button button)
        { Assert(button != null,"Missing UI button."); button.Focus(); using(var e=NavigationSubmitEvent.GetPooled()){ e.target=button; button.SendEvent(e); } }
        static void Check()
        {
            if(EditorApplication.timeSinceStartup-started>180){Finish("Timed out");return;}
            try
            {
                if(stage==11)
                {
                    var session=UnityEngine.Object.FindAnyObjectByType<CareerTrainingSession>();
                    var training=UnityEngine.Object.FindAnyObjectByType<TrainingPreview>();
                    if(!session||!training||training.FramesRun<15)return;
                    Pass(session.State.Complete&&session.Profile.life.amateurStarted&&session.State.Style=="Brazilian Jiu-Jitsu", "Amateur career opens the preserved training scene with life stats and style");
                    Pass(training.fighterSprite.GetComponent<FighterAppearance>().SkinTone==4,"Chosen skin tone carries into training");
                    Pass(CareerProfiles.Load(Output+"/ui-profile.json").life.amateurStarted,"Amateur start saved before scene transition");
                    ScreenCapture.CaptureScreenshot(Output+"/amateur_training.png"); stage=12; switched=EditorApplication.timeSinceStartup; return;
                }
                if(stage==12)
                {
                    if(EditorApplication.timeSinceStartup-switched>2)
                    {
                        var session=UnityEngine.Object.FindAnyObjectByType<CareerTrainingSession>();
                        Press(session.GetComponent<UIDocument>().rootVisualElement.Q<Button>("career-return-profile")); stage=13;
                    }
                    return;
                }
                if(stage==13)
                {
                    var returned=UnityEngine.Object.FindAnyObjectByType<CareerStartUI>(); if(!returned||!returned.Ready||returned.FramesRun<12)return;
                    Pass(returned.Draft.life!=null && returned.Draft.life.amateurStarted && FighterLife.Evaluate(returned.Draft.life).Money==40,"Return to fighter profile preserves completed career and funds");
                    Press(returned.Root.Q<Button>("begin-life"));
                    Pass(returned.Root.Q("life-summary")!=null,"Amateur profile can be reopened after training");
                    Finish(null); return;
                }
                var ui=UnityEngine.Object.FindAnyObjectByType<CareerStartUI>(); if(!ui||!ui.Ready||ui.FramesRun<nextFrame)return;
                var root=ui.Root;
                switch(stage)
                {
                    case 90:
                        ui.validationSavePath=Output+"/ui-profile.json"; if(File.Exists(ui.validationSavePath))File.Delete(ui.validationSavePath); ui.ReloadProfile();
                        root.Q<TextField>("fighter-name").value="Alex Morgan"; root.Q<TextField>("nickname").value="The Outlaw";
                        Press(root.Q<Button>("nationality")); root.Q<TextField>("country-search").value="France"; Press(root.Q<Button>("country-FR")); Press(root.Q<Button>("tone-4"));
                        nextFrame=ui.FramesRun+8; stage=91; break;
                    case 91:
                        Pass(root.Q<Label>(className:"form-title").resolvedStyle.unityFontDefinition.font != null && root.Q<Label>(className:"form-title").resolvedStyle.unityFontDefinition.font.name.Contains("Pixelify"), "Creator uses the imported pixel font");
                        var nicknameLabel=root.Q<TextField>("nickname").worldBound;
                        Pass(root.Q(className:"field-label-row").worldBound.yMax <= nicknameLabel.yMin + 1, "Nickname label does not overlap its input");
                        Pass(root.Q(className:"step-number").worldBound.height>=24, "Creation step badge remains fully visible");
                        ScreenCapture.CaptureScreenshot(Output+"/fighter_creation.png");
                        nextFrame=ui.FramesRun+5; stage=0; break;
                    case 0:
                        Press(root.Q<Button>("begin-life"));
                        Pass(ui.LifeUI.Visible&&ui.Draft.life.choices.Count==0&&root.Q<Label>("life-money").text=="$100","Begin Life Story starts at age 0 with $100");
                        AssertHiddenOutcomes(root);
                        Press(root.Q<Button>("life-choice-0"));
                        AssertHiddenOutcomes(root);
                        Pass(root.Q<Label>("life-money-preview").text.Contains("$80")&&ui.Draft.life.choices.Count==0,"Selecting previews exact cost without applying effects");
                        nextFrame=ui.FramesRun+5; stage++; break;
                    case 1:
                        Pass(root.Q<Label>(className:"life-title").resolvedStyle.unityFontDefinition.font != null && root.Q<Label>(className:"life-title").resolvedStyle.unityFontDefinition.font.name.Contains("Pixelify"), "Life story uses the imported pixel font");
                        Pass(true, "Stat outcomes hidden on cards and sidebar before and after selection; costs stay visible");
                        AssertCardsFit(root);
                        ScreenCapture.CaptureScreenshot(Output+"/life_age_0.png"); nextFrame=ui.FramesRun+5; stage++; break;
                    case 2:
                        string failedPath = Output + "/decision-write-blocker"; File.WriteAllText(failedPath,"test");
                        string normalPath = ui.validationSavePath; ui.validationSavePath=failedPath+"/save.json";
                        Press(root.Q<Button>("life-confirm"));
                        Pass(ui.Draft.life.choices.Count==0 && FighterLife.Evaluate(ui.Draft.life).Money==100 && !ui.Draft.life.pendingReceipt, "Failed decision save rolls back all stat and money changes");
                        ui.validationSavePath=normalPath; File.Delete(failedPath);
                        Press(root.Q<Button>("life-confirm"));
                        Pass(ui.Draft.life.pendingReceipt&&FighterLife.Evaluate(ui.Draft.life).Money==80&&root.Q("life-receipt")!=null,"Confirm applies cost once and shows before/after receipt");
                        Pass(root.Q("life-receipt").Query<Label>().ToList().Any(l=>l.text.Contains("+2  Fight IQ") && l.text.Contains("10 → 12")), "Confirmed decision reveals exact stat gain and before/after values");
                        ui.ReloadProfile(); Press(root.Q<Button>("begin-life"));
                        Pass(ui.Draft.life.pendingReceipt&&FighterLife.Evaluate(ui.Draft.life).Money==80,"Quit/resume restores result without duplicate rewards");
                        nextFrame=ui.FramesRun+5; stage++; break;
                    case 3:
                        ScreenCapture.CaptureScreenshot(Output+"/life_decision_result.png"); nextFrame=ui.FramesRun+5; stage++; break;
                    case 4:
                        if(ui.Draft.life.pendingReceipt)Press(root.Q<Button>("life-continue"));
                        int age=FighterLife.Evaluate(ui.Draft.life).Age;
                        if(age==14)
                        {
                            Pass(root.Query<Button>(className:"life-card").ToList().Count==4,"Age 14 presents Boxing, Muay Thai, Wrestling and Brazilian Jiu-Jitsu");
                            Press(root.Q<Button>("life-choice-3")); AssertHiddenOutcomes(root); nextFrame=ui.FramesRun+5; stage++; break;
                        }
                        AssertHiddenOutcomes(root);
                        int pick=FighterLife.CanChoose(ui.Draft.life,1)?1:Array.FindIndex(FighterLife.Chapters[age].Choices,c=>c.Cash>=0);
                        Press(root.Q<Button>("life-choice-"+pick)); Press(root.Q<Button>("life-confirm"));
                        Pass(ui.Draft.life.choices.Count==age+1&&CareerProfiles.Load(ui.validationSavePath).life.pendingReceipt,"Age "+age+" decision and receipt autosave");
                        nextFrame=ui.FramesRun+2; break;
                    case 5:
                        AssertCardsFit(root);
                        ScreenCapture.CaptureScreenshot(Output+"/life_age_14.png"); nextFrame=ui.FramesRun+5; stage++; break;
                    case 6:
                        Press(root.Q<Button>("life-confirm")); Press(root.Q<Button>("life-continue"));
                        Pass(FighterLife.Evaluate(ui.Draft.life).Style=="Brazilian Jiu-Jitsu","Main style persists into age 15");
                        Press(root.Q<Button>("life-choice-1")); Press(root.Q<Button>("life-confirm")); Press(root.Q<Button>("life-continue"));
                        Press(root.Q<Button>("life-choice-0"));
                        Pass(!root.Q<Button>("life-confirm").enabledSelf&&ui.Draft.life.choices.Count==16,"Unaffordable age-16 choice cannot be confirmed");
                        Press(root.Q<Button>("life-choice-2")); Press(root.Q<Button>("life-confirm")); Press(root.Q<Button>("life-continue"));
                        Pass(root.Q("life-summary")!=null&&FighterLife.Evaluate(ui.Draft.life).Money==40,"Age 16 summary preserves exact final balance of $40");
                        nextFrame=ui.FramesRun+5; stage++; break;
                    case 7:
                        ScreenCapture.CaptureScreenshot(Output+"/amateur_profile.png");
                        Press(root.Q<Button>("life-history"));
                        Pass(root.Query<VisualElement>(className:"life-history-item").ToList().Count==17,"Decision history includes all 17 years and their effects");
                        Press(root.Q<Button>("life-close-history"));
                        nextFrame=ui.FramesRun+5; stage=70; break;
                    case 70:
                        // Render the same UI at a narrower desktop aspect ratio without changing the user's Game view.
                        testedPanel=ui.GetComponent<UIDocument>().panelSettings;
                        layoutTexture=new RenderTexture(1200,900,24); layoutTexture.Create(); testedPanel.targetTexture=layoutTexture;
                        nextFrame=ui.FramesRun+10; stage=8; break;
                    case 8:
                        var bounds=root.Q("life-screen").worldBound; var action=root.Q<Button>("life-start-amateur").worldBound;
                        Pass(action.xMax<=bounds.xMax+1&&action.yMax<=bounds.yMax+1,"Primary action remains visible at 4:3 desktop aspect ratio");
                        CaptureTexture(layoutTexture,Output+"/amateur_profile_4x3.png"); CleanCapture(); nextFrame=ui.FramesRun+5; stage++; break;
                    case 9:
                        ui.ReloadProfile(); Press(root.Q<Button>("begin-life"));
                        Pass(root.Q("life-summary")!=null&&FighterLife.Evaluate(ui.Draft.life).Money==40,"Completed story reloads into amateur summary");
                        // Simulate write failure: a parent path that is a file cannot contain a save.
                        string blocker=Output+"/write-blocker"; File.WriteAllText(blocker,"test"); string good=ui.validationSavePath;
                        ui.validationSavePath=blocker+"/save.json";
                        Press(root.Q<Button>("life-start-amateur"));
                        Pass(!ui.Draft.life.amateurStarted&&SceneManager.GetActiveScene().name=="CareerStart","Failed save prevents amateur start and scene transition");
                        ui.validationSavePath=good; File.Delete(blocker);
                        nextFrame=ui.FramesRun+2; stage++; break;
                    case 10:
                        Press(root.Q<Button>("life-start-amateur")); stage++; break;
                }
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void AssertCardsFit(VisualElement root)
        {
            var view=root.Q<ScrollView>(className:"life-choice-scroll");
            var hints=root.Q("life-cards").Query<Label>(className:"life-select-hint").ToList();
            Pass(hints.All(h=>h.worldBound.yMax<=view.contentViewport.worldBound.yMax+1), "Choice prices and action hints fit inside the visible card area");
        }
        static void AssertHiddenOutcomes(VisualElement root)
        {
            var cards=root.Q("life-cards");
            Assert(cards != null, "Choice cards must exist");
            Assert(!cards.Query<Label>().ToList().Any(l=>l.ClassListContains("life-effect-gain")||l.ClassListContains("life-effect-loss")), "Choice cards leaked stat changes");
            Assert(!root.Query<Label>().ToList().Any(l=>l.name!=null && l.name.StartsWith("stat-preview-")), "Sidebar leaked a projected stat change");
            Assert(cards.Query<Label>(className:"life-choice-price").ToList().Count==cards.childCount, "Every card must disclose its price");
        }
        static void CaptureTexture(RenderTexture texture,string path)
        {
            var old=RenderTexture.active; RenderTexture.active=texture; var image=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
            try{image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(image);}
        }
        static void CleanCapture()
        { if(testedPanel)testedPanel.targetTexture=null; if(layoutTexture){layoutTexture.Release();UnityEngine.Object.DestroyImmediate(layoutTexture);} testedPanel=null;layoutTexture=null; }
        static void Finish(string problem)
        {
            EditorApplication.update-=Check; Application.logMessageReceived-=Log; CleanCapture(); if(problem!=null)errors.Add(problem);
            File.WriteAllText(Output+"/playmode-validation.txt","Life Story Play Mode: "+(errors.Count==0?"PASS":"FAIL")+"\n"+string.Join("\n",results.Select(r=>"PASS: "+r))+"\n"+string.Join("\n",errors));
            Debug.Log("[MMA Pixel UI] "+(errors.Count==0?"PASS":"FAIL")+" / "+results.Count+" checks / "+string.Join("; ",errors)); EditorApplication.ExitPlaymode();
        }
    }
}
