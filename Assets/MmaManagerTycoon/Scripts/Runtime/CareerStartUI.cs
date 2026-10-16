using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace MmaManagerTycoon
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class CareerStartUI : MonoBehaviour
    {
        public Camera previewCamera;
        public FighterAppearance appearance;
        public StyleSheet styles;
        public StyleSheet lifeStyles;
        public LifeStoryUI LifeUI { get; private set; }
        public string ProfilePath => SavePath;
        public int FramesRun { get; private set; }
        public bool Ready { get; private set; }
        public bool Saved { get; private set; }
        public CareerProfile Draft { get; private set; } = new CareerProfile();
        public VisualElement Root => GetComponent<UIDocument>().rootVisualElement;
#if UNITY_EDITOR
        [NonSerialized] public string validationSavePath;
#endif
        string SavePath
        {
            get
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(validationSavePath)) return validationSavePath;
#endif
                return CareerProfiles.DefaultPath;
            }
        }
        TextField nameInput, nicknameInput, countrySearch;
        Label status;
        VisualElement overlay, form, portrait;
        Button countryButton;
        RenderTexture previewTexture;
        bool filling;
        CareerCreationPresentation presentation;

        void Start()
        {
            Application.runInBackground = true;
            var root = Root;
            root.styleSheets.Add(styles);
            root.styleSheets.Add(Resources.Load<StyleSheet>("CareerPixel"));
            nameInput = root.Q<TextField>("fighter-name");
            nicknameInput = root.Q<TextField>("nickname");
            countrySearch = root.Q<TextField>("country-search");
            nameInput.textEdition.placeholder = "Enter your fighter’s name";
            nicknameInput.textEdition.placeholder = "e.g. The Outlaw";
            countrySearch.textEdition.placeholder = "Search by country name or code";
            nameInput.tabIndex = 1; nicknameInput.tabIndex = 2;
            nameInput.RegisterValueChangedCallback(e => { Draft.fighterName = e.newValue; Changed(); });
            nicknameInput.RegisterValueChangedCallback(e => { Draft.nickname = e.newValue; Changed(); });
            status = root.Q<Label>("status");
            overlay = root.Q("country-overlay");
            form = root.Q(className: "form-panel");
            portrait = root.Q("portrait-panel");
            countryButton = root.Q<Button>("nationality");
            countryButton.tabIndex = 3;
            countryButton.clicked += OpenCountries;
            root.Q<Button>("close-country").clicked += CloseCountries;
            countrySearch.RegisterValueChangedCallback(e => PopulateCountries(e.newValue));
            overlay.RegisterCallback<PointerDownEvent>(e => { if (e.target == overlay) CloseCountries(); });
            overlay.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Escape) { CloseCountries(); e.StopPropagation(); }
                // Keep keyboard focus inside the country picker until it closes.
                if (e.keyCode == KeyCode.Tab)
                {
                    var focusable = overlay.Query<VisualElement>().ToList().Where(v => v.focusable && v.enabledInHierarchy && v.tabIndex >= 0).ToList();
                    int index = focusable.IndexOf(Root.focusController.focusedElement as VisualElement);
                    if (focusable.Count > 0) focusable[(index + (e.shiftKey ? -1 : 1) + focusable.Count) % focusable.Count].Focus();
                    e.StopPropagation();
                }
            }, TrickleDown.TrickleDown);
            var swatches = root.Q("skin-tones");
            for (int i = 0; i < FighterAppearance.ToneCount; i++)
            {
                int selected = i;
                var button = new Button(() => { Draft.skinTone = selected; appearance.SetSkinTone(selected); Changed(); });
                button.name = "tone-" + i;
                button.tooltip = FighterAppearance.ToneNames[i] + " skin tone";
                button.tabIndex = 4 + i;
                button.AddToClassList("tone");
                button.style.backgroundColor = FighterAppearance.Swatch(i);
                var check = new Label { name = "check-" + i, pickingMode = PickingMode.Ignore };
                check.AddToClassList("tone-check"); button.Add(check); swatches.Add(button);
            }
            root.Q<Button>("save-fighter").tabIndex = 9;
            root.Q<Button>("save-fighter").clicked += Save;
            previewTexture = new RenderTexture(720, 800, 24, RenderTextureFormat.ARGB32) { name = "Career fighter portrait", filterMode = FilterMode.Point, antiAliasing = 1 };
            previewTexture.Create();
            previewCamera.targetTexture = previewTexture;
            var image = root.Q<Image>("fighter-preview");
            image.image = previewTexture; image.scaleMode = ScaleMode.ScaleAndCrop;
            var actionRow = new VisualElement(); actionRow.AddToClassList("career-actions");
            var saveButton = root.Q<Button>("save-fighter");
            var parent = saveButton.parent; int at = parent.IndexOf(saveButton); saveButton.RemoveFromHierarchy(); parent.Insert(at, actionRow);
            actionRow.Add(saveButton);
            var begin = new Button(OpenLifeStory) { name = "begin-life", text = "BEGIN LIFE STORY →", tabIndex = 10 };
            begin.AddToClassList("primary-button"); actionRow.Add(begin);
            LifeUI = new LifeStoryUI(this, lifeStyles);
            presentation = new CareerCreationPresentation(this);
            ReloadProfile();
            root.RegisterCallback<GeometryChangedEvent>(_ => root.EnableInClassList("compact", root.resolvedStyle.height < 850));
            Ready = true;
        }
        void Update() { FramesRun++; presentation?.Tick(); }
        void OnDestroy()
        {
            presentation?.Dispose();
            if (previewCamera) previewCamera.targetTexture = null;
            if (previewTexture) { previewTexture.Release(); Destroy(previewTexture); }
        }
        public void ReloadProfile()
        {
            string message = null;
            try { Draft = CareerProfiles.Load(SavePath) ?? new CareerProfile(); Saved = File.Exists(SavePath); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { Draft = new CareerProfile(); Saved = false; message = "Couldn’t load your saved fighter. You can create a new profile."; }
            filling = true;
            nameInput.SetValueWithoutNotify(Draft.fighterName);
            nicknameInput.SetValueWithoutNotify(Draft.nickname);
            filling = false;
            appearance.SetSkinTone(Draft.skinTone);
            Refresh();
            LifeUI?.Hide();
            SetStatus(message ?? (Saved ? "Saved fighter loaded. Edit any detail below." : "Your fighter can be edited after saving."), message != null, Saved);
        }
        void Changed()
        {
            if (filling) return;
            Saved = false; Refresh();
            SetStatus("Unsaved changes. Save when your fighter is ready.", false, false);
        }
        void Refresh()
        {
            var root = Root;
            string name = CareerProfiles.Clean(Draft.fighterName), nick = CareerProfiles.Clean(Draft.nickname);
            root.Q<Label>("preview-name").text = name.Length > 0 ? name : "Your fighter";
            root.Q<Label>("preview-nickname").text = nick.Length > 0 ? "“" + nick + "”" : "Make a name for yourself.";
            var country = CareerProfiles.FindCountry(Draft.nationalityCode);
            countryButton.text = country == null ? "Select nationality                         +" : country.Name + "   /   " + country.Code;
            root.Q<Label>("preview-country").text = country == null ? "CHOOSE YOUR NATIONALITY" : country.Code + "  /  " + country.Name.ToUpperInvariant();
            root.Q<Label>("tone-label").text = FighterAppearance.ToneNames[Draft.skinTone];
            for (int i = 0; i < FighterAppearance.ToneCount; i++)
            {
                root.Q<Button>("tone-" + i).EnableInClassList("selected", Draft.skinTone == i);
                root.Q<Label>("check-" + i).text = Draft.skinTone == i ? "•" : "";
            }
            root.Q<Button>("save-fighter").text = Saved ? "SAVED  ✓" : "SAVE FIGHTER";
            RefreshLifeButton();
            presentation?.Refresh();
        }
        void SetStatus(string text, bool error, bool saved)
        { status.text = text; status.EnableInClassList("error", error); status.EnableInClassList("saved", saved); presentation?.Status(error, saved); }
        void Save() { PersistDraft(); }
        public bool PersistDraft()
        {
            string error = CareerProfiles.Validate(Draft);
            if (error != null)
            {
                SetStatus(error, true, false);
                if (CareerProfiles.Clean(Draft.fighterName).Length == 0) nameInput.Focus(); else countryButton.Focus();
                return false;
            }
            try
            {
                CareerProfiles.Save(Draft, SavePath); Saved = true;
                nameInput.SetValueWithoutNotify(Draft.fighterName); nicknameInput.SetValueWithoutNotify(Draft.nickname);
                Refresh(); SetStatus("Fighter saved. This is where your career begins.", false, true);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { SetStatus("Couldn’t save your fighter. Your changes are still here; try again.", true, false); return false; }
        }
        public void RefreshLifeButton()
        {
            var button = Root.Q<Button>("begin-life");
            if (button == null) return;
            var life = Draft.life;
            button.text = life == null || life.choices.Count == 0 ? "Begin your story  →" : FighterLife.Evaluate(life).Complete && !life.pendingReceipt ? "Amateur profile  →" : "Continue your story  →";
        }
        void OpenLifeStory()
        {
            bool created = Draft.life == null;
            if (created) Draft.life = new LifeProgress();
            if (!PersistDraft()) { if (created) Draft.life = null; return; }
            if (presentation != null) presentation.Exit(() => LifeUI.Show()); else LifeUI.Show();
        }
        void OpenCountries()
        {
            overlay.RemoveFromClassList("hidden"); form.SetEnabled(false); portrait.SetEnabled(false);
            countrySearch.SetValueWithoutNotify(""); PopulateCountries(""); countrySearch.Focus(); presentation?.OpenCountries();
        }
        void CloseCountries()
        { overlay.AddToClassList("hidden"); form.SetEnabled(true); portrait.SetEnabled(true); countryButton.Focus(); }
        void PopulateCountries(string query)
        {
            var list = Root.Q<ScrollView>("country-list"); list.Clear();
            string search = (query ?? "").Trim();
            var countries = CareerProfiles.Countries.Where(c => c.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || c.Code.Equals(search, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var country in countries)
            {
                var row = new Button(() => { Draft.nationalityCode = country.Code; Changed(); CloseCountries(); }) { text = country.Name + "   /   " + country.Code, name = "country-" + country.Code };
                row.AddToClassList("country-row"); CareerCreationPresentation.StyleCountry(row, country, Draft.nationalityCode == country.Code); list.Add(row);
            }
            Root.Q<Label>("country-count").text = countries.Count == 0 ? "No matches. Try another country name." : countries.Count + " countries and territories  •  Esc to close";
            list.scrollOffset = Vector2.zero;
        }
    }
}
