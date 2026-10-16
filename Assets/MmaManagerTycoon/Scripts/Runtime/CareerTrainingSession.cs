using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MmaManagerTycoon
{
    public sealed class CareerTrainingSession : MonoBehaviour
    {
        public CareerProfile Profile { get; private set; }
        public LifeState State => FighterLife.Evaluate(Profile.life);
        string path;
        UIDocument document;
        PanelSettings panel;
        bool returning;
        public static void Open(CareerProfile profile, string path)
        {
            var session = new GameObject("Amateur career session").AddComponent<CareerTrainingSession>();
            session.Profile = profile; session.path = path;
            DontDestroyOnLoad(session.gameObject);
            SceneManager.sceneLoaded += session.Loaded;
            SceneManager.LoadSceneAsync("GymInteriorPreview");
        }
        void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "CareerStart")
            {
#if UNITY_EDITOR
                var ui = FindAnyObjectByType<CareerStartUI>(); if (ui) ui.validationSavePath = path;
#endif
                Destroy(gameObject); return;
            }
            if (scene.name != "GymInteriorPreview") return;
            var training = FindAnyObjectByType<TrainingPreview>();
            if (training) training.fighterSprite.GetComponent<FighterAppearance>().SetSkinTone(Profile.skinTone);
            panel = Instantiate(Resources.Load<PanelSettings>("LifeTrainingPanel"));
            document = gameObject.AddComponent<UIDocument>(); document.panelSettings = panel;
            var root = document.rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("CareerPixel"));
            root.style.justifyContent = Justify.FlexStart; root.style.alignItems = Align.Center;
            root.pickingMode = PickingMode.Ignore;
            var bar = new VisualElement(); bar.style.flexDirection = FlexDirection.Row; bar.style.alignItems = Align.Center;
            bar.style.backgroundColor = new Color(.055f,.065f,.065f,.96f); bar.style.paddingLeft = 22; bar.style.paddingRight = 22;
            bar.style.paddingTop = 10; bar.style.paddingBottom = 10; bar.style.marginTop = 190;
            root.Add(bar);
            var label = new Label(Profile.fighterName + "   /   AGE 16   /   " + State.Style + "   /   $" + State.Money + "   /   AMATEUR 0–0");
            label.style.color = new Color(.91f,.85f,.73f); label.style.fontSize = 18; label.style.marginRight = 24; bar.Add(label);
            var back = new Button(() => { if (returning) return; returning = true; SceneManager.LoadSceneAsync("CareerStart"); }) { text = "Fighter profile  →", name = "career-return-profile" };
            back.AddToClassList("career-return");
            back.style.backgroundColor = new Color(.83f,.65f,.41f); back.style.color = new Color(.08f,.08f,.07f); back.style.height = 32; back.style.paddingLeft = 16; back.style.paddingRight = 16; bar.Add(back);
        }
        void OnDestroy()
        { SceneManager.sceneLoaded -= Loaded; if (panel) Destroy(panel); }
    }
}
