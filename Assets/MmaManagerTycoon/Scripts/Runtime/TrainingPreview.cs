using UnityEngine;
using UnityEngine.InputSystem;

namespace MmaManagerTycoon
{
    public enum TrainingDrill { Jab, LowKick, HighKick, LeadHook }

    public sealed class TrainingPreview : MonoBehaviour
    {
        public Animator fighter;
        public Transform bag;
        public SpriteRenderer fighterSprite;
        public bool Training { get; private set; }
        public bool Paused { get; private set; }
        public TrainingDrill SelectedDrill { get; private set; }
        public int Impacts { get; private set; }
        public int JabImpacts { get; private set; }
        public int KickImpacts { get; private set; }
        public int HighKickImpacts { get; private set; }
        public int LeadHookImpacts { get; private set; }
        public float LastImpactPhase { get; private set; }
        public string LastImpactSprite { get; private set; }
        public float PreviewElapsed { get; private set; }
        public int FramesRun { get; private set; }
        float swing, velocity;
        // Per drill: sheet prefix, the frame where the strike lands and the bag
        // impulse it carries. Indexed by TrainingDrill.
        static readonly string[] DrillPrefix = { "jab_", "low_kick_", "high_kick_", "lead_hook_" };
        static readonly int[] DrillLandingFrame = { 5, 9, 13, 9 };
        static readonly float[] DrillImpulse = { 12f, 17f, 28f, 22f };
        bool armed = true;
        int impactState;
        GUIStyle title, subtitle, body, button;
        FighterAppearance appearance;

        void Awake() { Application.runInBackground = true; appearance = fighterSprite ? fighterSprite.GetComponent<FighterAppearance>() : null; }
        public void StartDrill(TrainingDrill drill)
        {
            if (!fighter) return;
            SelectedDrill = drill;
            // The strike selects are mutually exclusive; the controller reads
            // them as an explicit drill id rather than a bare flag.
            fighter.SetBool("LowKick", drill == TrainingDrill.LowKick);
            fighter.SetBool("HighKick", drill == TrainingDrill.HighKick);
            fighter.SetBool("LeadHook", drill == TrainingDrill.LeadHook);
            SetTraining(true);
        }
        public void SetTraining(bool active)
        {
            if (!fighter) return;
            Training = active;
            fighter.SetBool("Training", active);
        }
        public void SetPaused(bool paused)
        {
            Paused = paused;
            if (fighter) fighter.speed = paused ? 0 : 1;
        }
        void Update()
        {
            FramesRun++;
            PreviewElapsed += Mathf.Min(Time.unscaledDeltaTime, .05f);
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.cKey.wasPressedThisFrame && appearance) appearance.SetSkinTone((appearance.SkinTone + 1) % FighterAppearance.ToneCount);
                if (keyboard.digit1Key.wasPressedThisFrame) StartDrill(TrainingDrill.Jab);
                if (keyboard.digit2Key.wasPressedThisFrame) StartDrill(TrainingDrill.LowKick);
                if (keyboard.digit3Key.wasPressedThisFrame) StartDrill(TrainingDrill.HighKick);
                if (keyboard.digit4Key.wasPressedThisFrame) StartDrill(TrainingDrill.LeadHook);
                if (keyboard.spaceKey.wasPressedThisFrame) SetTraining(!Training);
                if (keyboard.pKey.wasPressedThisFrame) SetPaused(!Paused);
            }
            if (Paused || !fighter || !bag) return;
            var state = fighter.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash != impactState)
            {
                impactState = state.fullPathHash;
                armed = true;
            }
            bool lowKick = state.IsName("LowKick");
            bool highKick = state.IsName("HighKick");
            int drill = state.IsName("LeadHook") ? (int)TrainingDrill.LeadHook
                : highKick ? (int)TrainingDrill.HighKick
                : lowKick ? (int)TrainingDrill.LowKick
                : state.IsName("Jab") ? (int)TrainingDrill.Jab : -1;
            if (drill >= 0)
            {
                // The bag takes the hit on the frame where the strike lands:
                // jab 5/16 (0.25 s), low kick 9/24 (0.45 s),
                // high kick 13/28 (0.65 s, head level),
                // lead hook 9/20 (0.45 s, head level). Reading the displayed
                // frame needs nothing from the clip timeline, so a drill switch,
                // a pause or a looping state cannot move or swallow the hit. Any
                // frame before the landing re-arms it, so a frame the renderer
                // steps straight past at a low frame rate still lands.
                string frame = fighterSprite && fighterSprite.sprite ? fighterSprite.sprite.name : "";
                int index = FrameIndex(frame, DrillPrefix[drill]);
                if (index >= 0)
                {
                    if (index < DrillLandingFrame[drill]) armed = true;
                    else if (armed)
                    {
                        armed = false;
                        velocity -= DrillImpulse[drill];
                        Impacts++;
                        LastImpactPhase = state.normalizedTime;
                        LastImpactSprite = frame;
                        if (drill == (int)TrainingDrill.HighKick) HighKickImpacts++;
                        else if (drill == (int)TrainingDrill.LowKick) KickImpacts++;
                        else if (drill == (int)TrainingDrill.LeadHook) LeadHookImpacts++;
                        else JabImpacts++;
                    }
                }
            }
            float dt = Mathf.Min(Time.deltaTime, .05f);
            velocity += (-30f * swing - 5f * velocity) * dt;
            swing += velocity * dt;
            bag.localRotation = Quaternion.Euler(0, 0, Mathf.Clamp(swing, -4, 4));
        }
        // Frame number inside a strike sheet, or -1 when the sprite belongs to a
        // different sheet.
        static int FrameIndex(string frame, string prefix)
        {
            if (string.IsNullOrEmpty(frame) || frame.Length <= prefix.Length) return -1;
            for (int i = 0; i < prefix.Length; i++) if (frame[i] != prefix[i]) return -1;
            return int.TryParse(frame.Substring(prefix.Length), out int index) ? index : -1;
        }
        void OnGUI()
        {
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold };
                title.normal.textColor = new Color(.93f,.94f,.91f);
                subtitle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
                subtitle.normal.textColor = new Color(.74f,.40f,.36f);
                body = new GUIStyle(GUI.skin.label) { fontSize = 17 };
                body.normal.textColor = new Color(.67f,.72f,.75f);
                button = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
                button.normal.textColor = new Color(.95f,.93f,.88f);
            }
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1280f,Screen.height/720f,1));
            GUI.Label(new Rect(42,24,650,25),"MMA MANAGER TYCOON  /  TRAINING PREVIEW",subtitle);
            GUI.Label(new Rect(40,56,700,48),"From guard to bag work",title);
            GUI.Label(new Rect(42,106,900,30),"Punches, hooks, low kicks, head kicks, and responsive bag work",body);
            if (appearance)
            {
                GUI.Label(new Rect(840,24,400,28),"SKIN TONE  /  " + appearance.SkinToneName,subtitle);
                for(int i=0;i<FighterAppearance.ToneCount;i++)
                {
                    GUI.backgroundColor=FighterAppearance.Swatch(i);
                    string label=appearance.SkinTone==i?"["+(i+1)+"]":(i+1).ToString();
                    if(GUI.Button(new Rect(840+i*74,62,66,38),new GUIContent(label,FighterAppearance.ToneNames[i]),button))appearance.SetSkinTone(i);
                }
                GUI.backgroundColor=Color.white;
                GUI.Label(new Rect(840,104,390,26),"C  Cycle skin tones",body);
            }
            var state = fighter ? fighter.GetCurrentAnimatorStateInfo(0) : default;
            string activity = state.IsName("HighKick") ? "HIGH ROUNDHOUSE KICK" : state.IsName("LowKick") ? "LOW ROUNDHOUSE KICK" : state.IsName("LeadHook") ? "LEAD HOOK" : state.IsName("Jab") ? "STRAIGHT PUNCH" : "BREATHING IDLE";
            GUI.Label(new Rect(42,562,1000,30),Paused ? "PAUSED" : (Training ? "BAG WORK  /  " : "GUARD / RECOVERY  /  ") + activity,subtitle);
            GUI.backgroundColor = new Color(.47f,.22f,.24f);
            if (GUI.Button(new Rect(40,610,142,48),"1  PUNCH",button)) StartDrill(TrainingDrill.Jab);
            if (GUI.Button(new Rect(190,610,164,48),"2  LOW KICK",button)) StartDrill(TrainingDrill.LowKick);
            if (GUI.Button(new Rect(362,610,178,48),"3  HIGH KICK",button)) StartDrill(TrainingDrill.HighKick);
            if (GUI.Button(new Rect(548,610,128,48),"4  HOOK",button)) StartDrill(TrainingDrill.LeadHook);
            GUI.backgroundColor = new Color(.26f,.32f,.37f);
            if (GUI.Button(new Rect(684,610,192,48),Training ? "RETURN TO GUARD" : "RESUME DRILL",button)) SetTraining(!Training);
            if (GUI.Button(new Rect(884,610,112,48),Paused ? "RESUME" : "PAUSE",button)) SetPaused(!Paused);
            GUI.backgroundColor = Color.white;
            GUI.Label(new Rect(1004,610,260,48),"SPACE  Start / stop\nP  Pause / resume",body);
            GUI.Label(new Rect(42,677,1100,28),"Switches finish the current strike. Training preview; management systems are not included yet.",body);
            GUI.matrix = old;
        }
    }
}
