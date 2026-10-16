using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MmaManagerTycoon.Editor
{
    [InitializeOnLoad]
    public static class CareerUIInstaller
    {
        const string Root = "Assets/MmaManagerTycoon";
        const string UI = Root + "/CareerUI";
        const string ScenePath = UI + "/CareerStart.unity";
        const string TrainingPath = Root + "/TrainingPreview/GymInteriorPreview.unity";
        const string Output = "/Users/souhilbenammour/Documents/Codex/2026-10-14/cre/outputs/CareerUI";
        const string Pending = "MMA.CareerUI.Check";
        static int stage, nextFrame;
        static double started;
        static string idleFrame;
        static readonly List<string> checks = new List<string>();
        static readonly List<string> errors = new List<string>();
        static CareerUIInstaller() { EditorApplication.update += Run; EditorApplication.playModeStateChanged += PlayChanged; }
        static void Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= Run;
            bool request = File.Exists(Output + "/revalidate.request");
            if (File.Exists(Output + "/integration.json") && !request) return;
            try
            {
                if (request) File.Delete(Output + "/revalidate.request"); else Build();
                PolishPortrait();
                SessionState.SetString(Pending + ".previous", EditorSceneManager.playModeStartScene ? AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) : "");
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
                SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { File.WriteAllText(Output + "/error.txt", e.ToString()); Debug.LogException(e); }
        }
        static void PolishPortrait()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (var sr in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>()))
            {
                if (sr.GetComponent<FighterAppearance>())
                { sr.transform.position = new Vector3(-.75f, 1.02f, 0); sr.transform.localScale = Vector3.one * 1.35f; }
                else sr.color = new Color(.45f, .43f, .36f, sr.color.a);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        [MenuItem("MMA Career/Open Fighter Creation")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }
        static void Build()
        {
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) throw new Exception("Save your open scene before installing the career UI.");
            var original = File.ReadAllBytes(TrainingPath);
            var source = SceneManager.GetSceneByPath(TrainingPath);
            if (!source.IsValid() || !source.isLoaded) source = EditorSceneManager.OpenScene(TrainingPath, OpenSceneMode.Additive);
            var sourceRoots = source.GetRootGameObjects();
            var training = sourceRoots.SelectMany(r => r.GetComponentsInChildren<TrainingPreview>()).Single();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            FighterAppearance appearance = null;
            foreach (var sr in sourceRoots.SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>()))
            {
                if (sr.transform == training.bag) continue;
                var clone = UnityEngine.Object.Instantiate(sr.gameObject);
                clone.name = sr.name;
                SceneManager.MoveGameObjectToScene(clone, scene);
                foreach (Transform child in clone.GetComponentsInChildren<Transform>()) child.gameObject.layer = 30;
                if (sr == training.fighterSprite)
                {
                    clone.transform.position = new Vector3(-.75f, .67f, 0);
                    clone.transform.localScale = Vector3.one * 1.5f;
                    appearance = clone.GetComponent<FighterAppearance>();
                }
            }
            var portraitCamera = new GameObject("Live fighter portrait camera").AddComponent<Camera>();
            portraitCamera.orthographic = true; portraitCamera.orthographicSize = 2.15f;
            portraitCamera.transform.position = new Vector3(-.75f, 1.75f, -10);
            portraitCamera.cullingMask = 1 << 30; portraitCamera.allowHDR = false; portraitCamera.allowMSAA = false;
            portraitCamera.clearFlags = CameraClearFlags.SolidColor; portraitCamera.backgroundColor = new Color(.10f,.10f,.09f);
            portraitCamera.depth = -1;
            var main = new GameObject("Main Camera").AddComponent<Camera>();
            main.tag = "MainCamera"; main.cullingMask = 0; main.clearFlags = CameraClearFlags.SolidColor;
            main.backgroundColor = new Color(.06f,.07f,.07f); main.gameObject.AddComponent<AudioListener>();
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = "Career UI Panel"; panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1600, 900); panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight; panel.match = 1;
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(UI + "/CareerTheme.tss");
            AssetDatabase.CreateAsset(panel, UI + "/CareerPanel.asset");
            var uiObject = new GameObject("Career start screen");
            var document = uiObject.AddComponent<UIDocument>(); document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UI + "/CareerStart.uxml");
            var controller = uiObject.AddComponent<CareerStartUI>(); controller.previewCamera = portraitCamera; controller.appearance = appearance;
            controller.styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(UI + "/CareerStart.uss");
            if (!document.visualTreeAsset || !panel.themeStyleSheet || !controller.styles || !appearance) throw new Exception("Career UI dependency missing.");
            EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!original.SequenceEqual(File.ReadAllBytes(TrainingPath))) throw new Exception("Training scene changed during installation.");
            var previous = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            previous.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = previous.ToArray();
            File.WriteAllText(Output + "/integration.json", "{\"scene\":\"" + ScenePath + "\",\"trainingSceneUnchanged\":true,\"livePreview\":true,\"uiToolkit\":true,\"playMode\":\"pending\"}");
        }
        static void PlayChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                stage = -1; nextFrame = 15; started = EditorApplication.timeSinceStartup; checks.Clear(); errors.Clear();
                Application.logMessageReceived += Log; EditorApplication.update += Check;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                Application.logMessageReceived -= Log; EditorApplication.update -= Check;
                string previous = SessionState.GetString(Pending + ".previous", "");
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
                SessionState.SetBool(Pending, false);
            }
        }
        static void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
        static void Require(bool value, string message)
        { if (!value) throw new Exception(message); checks.Add(message); }
        static void Press(Button button)
        { if (button == null) throw new Exception("Button not found"); button.Focus(); using (var e = NavigationSubmitEvent.GetPooled()) { e.target = button; button.SendEvent(e); } }
        static void Check()
        {
            if (EditorApplication.timeSinceStartup - started > 120) { Finish("Timed out waiting for career UI. Paused: " + EditorApplication.isPaused); return; }
            var ui = UnityEngine.Object.FindAnyObjectByType<CareerStartUI>();
            if (!ui || !ui.Ready || ui.FramesRun < nextFrame) return;
            try
            {
                var root = ui.Root;
                switch (stage)
                {
                    case -1:
                        ScreenCapture.CaptureScreenshot(Output + "/career_empty_unity.png");
                        nextFrame = ui.FramesRun + 8; stage++; break;
                    case 0:
                        ui.validationSavePath = Output + "/validation-profile.json";
                        if (File.Exists(ui.validationSavePath)) File.Delete(ui.validationSavePath);
                        ui.ReloadProfile();
                        Require(CareerProfiles.Countries.Count > 150, "Nationality list contains over 150 countries and territories");
                        Press(root.Q<Button>("save-fighter"));
                        Require(!File.Exists(ui.validationSavePath) && root.Q<Label>("status").ClassListContains("error"), "Empty name cannot save; inline validation shown (" + root.Q<Label>("status").text + ")");
                        root.Q<TextField>("fighter-name").value = "  Alex Morgan  ";
                        root.Q<TextField>("nickname").value = "The Outlaw";
                        Press(root.Q<Button>("save-fighter"));
                        Require(!File.Exists(ui.validationSavePath), "Nationality is required");
                        Press(root.Q<Button>("nationality"));
                        root.Q<TextField>("country-search").value = "France";
                        Require(root.Q<Button>("country-FR") != null && root.Query<Button>(className: "country-row").ToList().Count == 1, "Country search filters results");
                        Press(root.Q<Button>("country-FR"));
                        Require(ui.Draft.nationalityCode == "FR" && root.Q("country-overlay").ClassListContains("hidden"), "Country selection updates profile and closes picker");
                        idleFrame = ui.appearance.GetComponent<SpriteRenderer>().sprite.name;
                        for (int i = 0; i < 5; i++)
                        {
                            Press(root.Q<Button>("tone-" + i));
                            Require(ui.Draft.skinTone == i && ui.appearance.SkinTone == i && root.Q<Button>("tone-" + i).ClassListContains("selected"), "Skin tone " + i + " updates live preview and selected swatch");
                        }
                        Press(root.Q<Button>("tone-2"));
                        Press(root.Q<Button>("save-fighter"));
                        var saved = CareerProfiles.Load(ui.validationSavePath);
                        Require(ui.Saved && saved.fighterName == "Alex Morgan" && saved.nickname == "The Outlaw" && saved.nationalityCode == "FR" && saved.skinTone == 2, "Save button writes normalized identity and appearance");
                        root.Q<TextField>("fighter-name").value = "Changed";
                        Require(!ui.Saved, "Editing a saved fighter marks unsaved changes");
                        ui.ReloadProfile();
                        Require(root.Q<TextField>("fighter-name").value == "Alex Morgan", "Saved profile reloads into form");
                        root.Q<TextField>("nickname").value = "";
                        Press(root.Q<Button>("save-fighter"));
                        Require(CareerProfiles.Load(ui.validationSavePath).nickname == "", "Optional nickname can be cleared and saved over existing profile");
                        root.Q<TextField>("nickname").value = "The Outlaw";
                        Press(root.Q<Button>("save-fighter"));
                        nextFrame = ui.FramesRun + 5; stage++; break;
                    case 1:
                        Require(ui.appearance.GetComponent<SpriteRenderer>().sprite.name != idleFrame, "Fighter idle animation advances while customizing");
                        var screen = root.Q("screen").worldBound; var save = root.Q<Button>("save-fighter").worldBound;
                        Require(save.xMin >= screen.xMin && save.xMax <= screen.xMax && save.yMax < screen.yMax, "Save button stays inside the visible screen");
                        root.Q<Button>("save-fighter").Blur();
                        ScreenCapture.CaptureScreenshot(Output + "/career_start_unity.png");
                        nextFrame = ui.FramesRun + 5; stage++; break;
                    case 2:
                        Press(root.Q<Button>("nationality")); root.Q<TextField>("country-search").value = "zzznomatch";
                        Require(root.Query<Button>(className: "country-row").ToList().Count == 0 && root.Q<Label>("country-count").text.StartsWith("No matches"), "Country search handles no results");
                        root.Q<TextField>("country-search").value = "united";
                        nextFrame = ui.FramesRun + 4; stage++; break;
                    case 3:
                        ScreenCapture.CaptureScreenshot(Output + "/nationality_picker_unity.png");
                        nextFrame = ui.FramesRun + 4; stage++; break;
                    case 4:
                        Press(root.Q<Button>("close-country"));
                        Require(root.Q("country-overlay").ClassListContains("hidden") && root.Q<Button>("nationality").enabledInHierarchy, "Closing picker restores form interaction");
                        File.WriteAllText(ui.validationSavePath, "{broken"); ui.ReloadProfile();
                        Require(!ui.Saved && root.Q<Label>("status").ClassListContains("error"), "Corrupt save handled without crashing");
                        File.Delete(ui.validationSavePath);
                        Finish(null); break;
                }
            }
            catch (Exception e) { Finish(e.ToString()); }
        }
        static void Finish(string problem)
        {
            EditorApplication.update -= Check; Application.logMessageReceived -= Log;
            if (problem != null) errors.Add(problem);
            string report = "Career UI Play Mode: " + (errors.Count == 0 ? "PASS" : "FAIL") + "\n" + string.Join("\n", checks.Select(c => "PASS: " + c)) + "\n" + string.Join("\n", errors);
            File.WriteAllText(Output + "/playmode-validation.txt", report); Debug.Log("[MMA Career UI] " + report);
            EditorApplication.ExitPlaymode();
        }
    }
}
