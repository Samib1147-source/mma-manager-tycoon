using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MmaManagerTycoon
{
    // Presentation only. Profile validation, saving, life progression and animations
    // remain owned by their existing systems.
    public sealed class CareerCreationPresentation : IDisposable
    {
        readonly CareerStartUI owner;
        readonly VisualElement screen;
        readonly Label countryValue;
        readonly Camera camera;
        readonly int cameraMask, fighterLayer;
        readonly Color cameraBackground;
        readonly Vector3 cameraPosition;
        readonly float cameraSize;
        readonly CameraClearFlags cameraClear;
        bool visible = true, leaving;
        IVisualElementScheduledItem reveal, exit;

        public CareerCreationPresentation(CareerStartUI owner)
        {
            this.owner = owner; screen = owner.Root.Q("screen");
            owner.Root.styleSheets.Add(Resources.Load<StyleSheet>("CreationTheme"));
            owner.Root.Q("stage-art").Add(new CareerStageGraphic(false));
            owner.Root.Q("career-emblem").Add(new CareerStageGraphic(true));
            var nationality = owner.Root.Q<Button>("nationality"); nationality.text = "";
            countryValue = new Label { name = "nationality-value", pickingMode = PickingMode.Ignore };
            countryValue.AddToClassList("cr-nationality-value"); nationality.Add(countryValue);
            var arrow = new Label("⌄") { pickingMode = PickingMode.Ignore }; arrow.AddToClassList("cr-nationality-arrow"); nationality.Add(arrow);
            owner.Root.Q<Button>("begin-life").AddToClassList("cr-primary");
            owner.Root.Q<Button>("save-fighter").AddToClassList("cr-secondary");

            // Isolate the existing animated fighter in the creator's camera only.
            // This is runtime composition, not an edit to the fighter or gym assets.
            camera = owner.previewCamera; cameraMask = camera.cullingMask;
            cameraBackground = camera.backgroundColor; cameraClear = camera.clearFlags;
            cameraPosition = camera.transform.position; cameraSize = camera.orthographicSize;
            fighterLayer = owner.appearance.gameObject.layer; owner.appearance.gameObject.layer = 31;
            camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            var bounds = owner.appearance.GetComponent<SpriteRenderer>().bounds;
            camera.orthographicSize = bounds.extents.y * 1.07f;
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, cameraPosition.z);
            owner.Root.Q<Image>("fighter-preview").scaleMode = ScaleMode.ScaleToFit;
            Reveal();
        }
        public void Tick()
        {
            bool now = screen.resolvedStyle.display != DisplayStyle.None;
            if (now && !visible) Reveal();
            visible = now;
        }
        void Reveal()
        {
            leaving = false; reveal?.Pause(); screen.RemoveFromClassList("cr-leaving"); screen.AddToClassList("cr-entering");
            reveal = screen.schedule.Execute(() => screen.RemoveFromClassList("cr-entering")).StartingIn(30);
        }
        public void Exit(Action completed)
        {
            if (leaving) return; leaving = true; screen.SetEnabled(false); screen.AddToClassList("cr-leaving");
            exit = screen.schedule.Execute(() => { completed(); visible = false; screen.SetEnabled(true); screen.RemoveFromClassList("cr-leaving"); leaving = false; }).StartingIn(150);
        }
        public void Refresh()
        {
            string name = CareerProfiles.Clean(owner.Draft.fighterName);
            var label = owner.Root.Q<Label>("preview-name"); label.text = string.IsNullOrEmpty(name) ? "YOUR FIGHTER" : name.ToUpperInvariant(); label.tooltip = name;
            label.style.fontSize = name.Length > 24 ? 38 : name.Length > 18 ? 46 : 60;
            string nick = CareerProfiles.Clean(owner.Draft.nickname);
            owner.Root.Q<Label>("preview-nickname").text = nick.Length > 0 ? "“" + nick + "”" : "Your story is unwritten";
            var country = CareerProfiles.FindCountry(owner.Draft.nationalityCode);
            owner.Root.Q<Button>("nationality").text = "";
            countryValue.text = country == null ? "Choose a nationality" : country.Name;
            countryValue.tooltip = country == null ? "" : country.Name + " / " + country.Code;
            owner.Root.Q<Label>("preview-country").text = country == null ? "Nationality unselected" : country.Code + "  /  " + country.Name;
            owner.Root.Q<Button>("save-fighter").text = owner.Saved ? "Saved  ✓" : "Save fighter";
            for (int i = 0; i < FighterAppearance.ToneCount; i++)
                owner.Root.Q<Label>("check-" + i).text = owner.Draft.skinTone == i ? "✓" : "";
            ClearValidation();
        }
        public void ClearValidation()
        {
            owner.Root.Q("fighter-name").RemoveFromClassList("cr-invalid");
            owner.Root.Q("nationality").RemoveFromClassList("cr-invalid");
        }
        public void Status(bool error, bool saved)
        {
            var indicator = owner.Root.Q("save-indicator"); indicator.EnableInClassList("cr-saved", saved); indicator.EnableInClassList("cr-error", error);
            if (error)
            {
                owner.Root.Q("fighter-name").EnableInClassList("cr-invalid", CareerProfiles.Clean(owner.Draft.fighterName).Length == 0);
                owner.Root.Q("nationality").EnableInClassList("cr-invalid", CareerProfiles.FindCountry(owner.Draft.nationalityCode) == null);
            }
        }
        public void OpenCountries()
        {
            var dialog = owner.Root.Q("country-dialog"); dialog.AddToClassList("cr-opening");
            dialog.schedule.Execute(() => dialog.RemoveFromClassList("cr-opening")).StartingIn(25);
        }
        public static void StyleCountry(Button row, CareerProfiles.Country country, bool selected)
        {
            row.text = ""; row.tooltip = country.Name; row.EnableInClassList("cr-current-country", selected);
            var code = new Label(country.Code) { pickingMode = PickingMode.Ignore }; code.AddToClassList("cr-country-code"); row.Add(code);
            var name = new Label(country.Name) { pickingMode = PickingMode.Ignore }; name.AddToClassList("cr-country-name"); row.Add(name);
            if (selected) { var hint = new Label("Selected") { pickingMode = PickingMode.Ignore }; hint.AddToClassList("cr-country-selected"); row.Add(hint); }
        }
        public void Dispose()
        {
            reveal?.Pause(); exit?.Pause();
            if (camera) { camera.cullingMask = cameraMask; camera.backgroundColor = cameraBackground; camera.clearFlags = cameraClear; camera.transform.position = cameraPosition; camera.orthographicSize = cameraSize; }
            if (owner.appearance) owner.appearance.gameObject.layer = fighterLayer;
        }
    }

    // Resolution-independent rings, spotlight and floor shadow. Drawn by UI Toolkit;
    // no raster backgrounds, material assets or third-party animation dependencies.
    public sealed class CareerStageGraphic : VisualElement
    {
        readonly bool emblem;
        public CareerStageGraphic(bool emblem)
        {
            this.emblem = emblem; pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute; style.left = 0; style.right = 0; style.top = 0; style.bottom = 0;
            generateVisualContent += Draw;
        }
        static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var value); return value; }
        void Glow(MeshGenerationContext context, Vector2 center, Vector2 radius, Color middle, Color edge)
        {
            const int segments = 64;
            var mesh = context.Allocate(segments + 2, segments * 3);
            mesh.SetNextVertex(new Vertex { position = new Vector3(center.x, center.y, Vertex.nearZ), tint = middle });
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                mesh.SetNextVertex(new Vertex { position = new Vector3(center.x + Mathf.Cos(a) * radius.x, center.y + Mathf.Sin(a) * radius.y, Vertex.nearZ), tint = edge });
            }
            for (ushort i = 1; i <= segments; i++) { mesh.SetNextIndex(0); mesh.SetNextIndex(i); mesh.SetNextIndex((ushort)(i + 1)); }
        }
        void Octagon(Painter2D p, Vector2 center, float radius, Color color, float width)
        {
            p.strokeColor = color; p.lineWidth = width; p.BeginPath();
            for (int i = 0; i < 8; i++) { float a = (22.5f + 45 * i) * Mathf.Deg2Rad; var pt = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius; if (i == 0) p.MoveTo(pt); else p.LineTo(pt); }
            p.ClosePath(); p.Stroke();
        }
        void Draw(MeshGenerationContext context)
        {
            float w = contentRect.width, h = contentRect.height; if (w < 1 || h < 1) return;
            var p = context.painter2D;
            if (emblem)
            {
                Octagon(p, new Vector2(w/2,h/2), w*.46f, C("#c74f3c"), 2);
                p.strokeColor = C("#f1e9db"); p.lineWidth = 2; p.BeginPath(); p.MoveTo(new Vector2(w*.36f,h*.66f)); p.LineTo(new Vector2(w*.36f,h*.34f)); p.LineTo(new Vector2(w*.5f,h*.5f)); p.LineTo(new Vector2(w*.64f,h*.34f)); p.LineTo(new Vector2(w*.64f,h*.66f)); p.Stroke(); return;
            }
            var center = new Vector2(w*.64f,h*.43f);
            Glow(context, center, new Vector2(w*.48f,h*.54f), new Color(.30f,.25f,.18f,.32f), Color.clear);
            Octagon(p, center, Mathf.Min(w*.36f,h*.39f), new Color(.65f,.54f,.39f,.19f), 1);
            Octagon(p, center, Mathf.Min(w*.36f,h*.39f)+14, new Color(.65f,.54f,.39f,.07f), 1);
            // Asymmetric corner lines suggest the geometry of a cage without a boxed panel.
            p.lineWidth = 2; p.strokeColor = new Color(.78f,.31f,.23f,.52f); p.BeginPath();
            p.MoveTo(new Vector2(w*.86f,h*.12f)); p.LineTo(new Vector2(w*.97f,h*.23f)); p.LineTo(new Vector2(w*.97f,h*.37f)); p.Stroke();
            Glow(context, new Vector2(w*.67f,h*.70f), new Vector2(w*.24f,16), new Color(0,0,0,.7f), Color.clear);
        }
    }
}
