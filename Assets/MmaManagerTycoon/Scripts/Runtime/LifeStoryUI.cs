using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MmaManagerTycoon
{
    // This screen shares the saved fighter profile with character creation.
    // Choices, rather than cached totals, are saved so rewards cannot drift or repeat on reload.
    public sealed class LifeStoryUI
    {
        readonly CareerStartUI owner;
        readonly VisualElement host;
        VisualElement screen, rail, main, cards, timeline;
        Label feedback;
        Button confirm;
        int selected = -1;
        bool busy;
        public VisualElement Screen => screen;
        public bool Visible => screen != null && screen.parent != null;
        public LifeStoryUI(CareerStartUI owner, StyleSheet sheet)
        { this.owner = owner; host = owner.Root; if (sheet != null) host.styleSheets.Add(sheet); }
        static Label L(string text, string cls, string name = null)
        { var label = new Label(text) { name = name }; label.AddToClassList(cls); return label; }
        static VisualElement V(string cls, string name = null)
        { var element = new VisualElement { name = name }; element.AddToClassList(cls); return element; }
        static Button B(string text, Action action, string cls, string name = null)
        { var button = new Button(action) { text = text, name = name }; button.AddToClassList(cls); return button; }
        LifeProgress Progress => owner.Draft.life;
        public void Show()
        {
            host.Q("screen").style.display = DisplayStyle.None;
            selected = -1; Render();
        }
        public void Hide()
        { screen?.RemoveFromHierarchy(); host.Q("screen").style.display = DisplayStyle.Flex; owner.RefreshLifeButton(); }
        public void Render()
        {
            screen?.RemoveFromHierarchy();
            screen = V("life-screen", "life-screen"); host.Add(screen);
            var state = FighterLife.Evaluate(Progress);
            int age = Progress.pendingReceipt ? state.Decisions - 1 : state.Age;
            var header = V("life-header"); screen.Add(header);
            var brand = V("life-brand"); header.Add(brand);
            brand.Add(L("MMA", "life-logo")); brand.Add(L("/   ORIGINS", "life-section"));
            var navigation = V("life-nav"); header.Add(navigation);
            navigation.Add(B("Your choices  /  " + state.Decisions, ShowHistory, "life-quiet", "life-history"));
            navigation.Add(B("Edit identity", Hide, "life-quiet", "life-profile"));
            var body = V("life-body"); screen.Add(body);
            rail = V("life-rail"); body.Add(rail); RenderStats(state, null);
            main = V("life-main"); body.Add(main);
            timeline = V("life-timeline"); main.Add(timeline);
            for (int i = 0; i < 17; i++)
            {
                var year = L(i.ToString("00"), "life-year");
                year.EnableInClassList("past", i < state.Decisions);
                year.EnableInClassList("current", i == age); timeline.Add(year);
            }
            if (state.Complete && !Progress.pendingReceipt) RenderSummary(state);
            else if (Progress.pendingReceipt) RenderReceipt(state);
            else RenderDecision(state);
            var footer = V("life-footer"); screen.Add(footer);
            footer.Add(L("EVERY CHOICE LEAVES A MARK.", "life-footnote"));
            footer.Add(L("AGES 0–16  /  " + state.Decisions + " OF 17 DECISIONS SAVED", "life-footnote"));
        }
        void RenderStats(LifeState state, int? projectedMoney)
        {
            rail.Clear();
            rail.Add(L(owner.Draft.fighterName, "life-fighter-name"));
            rail.Add(L(string.IsNullOrEmpty(owner.Draft.nickname) ? "YOUR FUTURE FIGHTER" : "“" + owner.Draft.nickname + "”", "life-small-gold"));
            var overview = V("life-overview"); rail.Add(overview);
            var age = V("life-age-block"); overview.Add(age);
            age.Add(L("AGE", "life-eyebrow")); age.Add(L((Progress.pendingReceipt ? state.Decisions - 1 : state.Age).ToString("00"), "life-age", "life-age"));
            var cash = V("life-cash-block"); overview.Add(cash);
            cash.Add(L("AVAILABLE FUNDS", "life-eyebrow")); cash.Add(L("$" + state.Money, "life-money", "life-money"));
            if (projectedMoney.HasValue) cash.Add(L("After choice: $" + projectedMoney.Value, projectedMoney.Value < 0 ? "life-loss" : "life-muted", "life-money-preview"));
            rail.Add(L("MAIN STYLE", "life-eyebrow"));
            rail.Add(L(state.Style == "Undecided" ? "Chosen at age 14" : state.Style, "life-style", "life-style"));
            var stats = new ScrollView(ScrollViewMode.Vertical) { name = "life-stats" }; stats.AddToClassList("life-stats"); rail.Add(stats);
            for (int i = 0; i < FighterLife.StatNames.Length; i++)
            {
                if (i == 0 || i == 6 || i == 10) stats.Add(L(i == 0 ? "TECHNIQUE" : i == 6 ? "PHYSICAL" : "MINDSET", "life-stat-group"));
                var row = V("life-stat-row", "stat-row-" + i); stats.Add(row);
                row.Add(L(FighterLife.StatNames[i], "life-stat-name"));
                var value = V("life-stat-values"); row.Add(value);
                value.Add(L(state.Stats[i].ToString(), "life-stat-value", "stat-value-" + i));

            }
            rail.Add(L("Development ratings / 100\nStarting foundations: 10 in every stat.", "life-stat-note"));
        }
        void Intro(string kicker, string title, string story)
        {
            main.Add(L(kicker, "life-eyebrow")); main.Add(L(title, "life-title")); main.Add(L(story, "life-story"));
        }
        void RenderDecision(LifeState state)
        {
            var chapter = FighterLife.Chapters[state.Age];
            Intro(chapter.Kicker, chapter.Title, chapter.Story);
            main.Add(L("CHOOSE YOUR PATH  /  YOUR STORY, YOUR CALL", "life-choice-guide"));
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("life-choice-scroll"); main.Add(scroll);
            cards = V("life-cards", "life-cards"); scroll.Add(cards);
            for (int i = 0; i < chapter.Choices.Length; i++)
            {
                int index = i; var choice = chapter.Choices[i];
                bool affordable = state.Money + choice.Cash >= 0;
                var card = B("", () => Select(index), "life-card", "life-choice-" + i); cards.Add(card);
                if (i == chapter.Choices.Length - 1) card.style.marginRight = 0;
                card.EnableInClassList("locked", !affordable);
                card.Add(L("0" + (i + 1) + (choice.Style != null ? "  /  MAIN STYLE" : "  /  LIFE CHOICE"), "life-card-number"));
                card.Add(L(choice.Title, "life-card-title")); card.Add(L(choice.Description, "life-card-description"));
                var effects = V("life-effects"); card.Add(effects);
                effects.Add(L(choice.Cash < 0 ? "COST  $" + -choice.Cash : choice.Cash > 0 ? "EARN  $" + choice.Cash : "NO COST", "life-choice-price"));
                card.Add(L(affordable ? "Select this path  →" : "Needs $" + -choice.Cash + "  /  You have $" + state.Money, affordable ? "life-select-hint" : "life-locked-hint"));
                // Keep prices readable even when this path is unaffordable.
                card.tooltip = affordable ? "Select this path, then confirm your decision." : "You cannot afford this choice. Pick a free option or a cheaper path.";
            }
            feedback = L("Choose a path. See how it shapes your fighter after you confirm.", "life-feedback", "life-feedback"); main.Add(feedback);
            var actions = V("life-actions"); main.Add(actions);
            actions.Add(L("Changes apply only when you confirm.\nEvery confirmed choice is saved automatically.", "life-muted"));
            confirm = B("CONFIRM DECISION  →", Commit, "life-primary", "life-confirm"); confirm.SetEnabled(false); actions.Add(confirm);
        }
        void Select(int index)
        {
            if (busy || Progress.pendingReceipt) return;
            var state = FighterLife.Evaluate(Progress); if (state.Complete) return;
            selected = index;
            var choice = FighterLife.Chapters[state.Age].Choices[index];
            int remainingMoney = state.Money + choice.Cash;
            for (int i = 0; i < cards.childCount; i++) cards[i].EnableInClassList("selected", i == selected);
            RenderStats(state, remainingMoney);
            bool available = FighterLife.CanChoose(Progress, selected);
            confirm.SetEnabled(available);
            feedback.text = available ? "Selected: " + choice.Title + "  •  Funds: $" + state.Money + " → $" + remainingMoney : "You need $" + -choice.Cash + " for this path. Choose another option; no debt is allowed.";
            feedback.EnableInClassList("life-loss", !available);
        }
        void RenderEffects(VisualElement parent, LifeState before, LifeChoice choice, bool receipt)
        {
            var after = FighterLife.Preview(before, choice);
            parent.Add(L(choice.Cash == 0 ? "No cash change" : (choice.Cash > 0 ? "+$" + choice.Cash : "−$" + -choice.Cash), choice.Cash == 0 ? "life-effect-neutral" : choice.Cash > 0 ? "life-effect-gain" : "life-effect-loss"));
            foreach (var change in choice.Changes)
            {
                int stat = (int)change.Stat, delta = after.Stats[stat] - before.Stats[stat];
                string text = (delta > 0 ? "+" : delta < 0 ? "−" : "") + Math.Abs(delta) + "  " + FighterLife.StatNames[stat];
                if (receipt) text += "   /   " + before.Stats[stat] + " → " + after.Stats[stat];
                if (delta != change.Amount) text += " (limit reached)";
                parent.Add(L(text, delta > 0 ? "life-effect-gain" : delta < 0 ? "life-effect-loss" : "life-effect-neutral"));
            }
        }
        void Commit()
        {
            if (busy || !FighterLife.CanChoose(Progress, selected)) return;
            busy = true;
            FighterLife.Choose(Progress, selected);
            if (!owner.PersistDraft())
            {
                Progress.choices.RemoveAt(Progress.choices.Count - 1); Progress.pendingReceipt = false;
                feedback.text = "Couldn’t save. Nothing was charged or applied. Please try again."; feedback.AddToClassList("life-loss"); busy = false; return;
            }
            selected = -1; busy = false; Render();
        }
        void RenderReceipt(LifeState after)
        {
            int age = after.Decisions - 1;
            var beforeProgress = new LifeProgress { choices = Progress.choices.Take(age).ToList() };
            var before = FighterLife.Evaluate(beforeProgress); var choice = FighterLife.Chapters[age].Choices[Progress.choices[age]];
            Intro("AGE " + age + "  /  DECISION SAVED", "A choice that shapes you.", "Your decision is saved. Here is exactly what changed before you move on.");
            var result = new ScrollView(ScrollViewMode.Vertical) { name = "life-receipt" }; result.AddToClassList("life-receipt"); main.Add(result);
            result.Add(L(choice.Title, "life-result-title")); result.Add(L(choice.Description, "life-story"));
            if (choice.Style != null) result.Add(L("MAIN STYLE SET  /  " + choice.Style.ToUpperInvariant(), "life-style-badge"));
            var effects = V("life-receipt-effects"); result.Add(effects); RenderEffects(effects, before, choice, true);
            result.Add(L("FUNDS   $" + before.Money + " → $" + after.Money, "life-result-money", "life-receipt-money"));
            feedback = L("Saved automatically. Your changes will not be applied again on reload.", "life-feedback", "life-feedback"); main.Add(feedback);
            var actions = V("life-actions"); main.Add(actions);
            actions.Add(L(after.Complete ? "Your amateur foundations are ready." : "Next chapter  /  Age " + after.Age, "life-muted"));
            actions.Add(B(after.Complete ? "SEE YOUR FIGHTER  →" : "CONTINUE TO AGE " + after.Age + "  →", Continue, "life-primary", "life-continue"));
        }
        void Continue()
        {
            if (busy || !Progress.pendingReceipt) return;
            Progress.pendingReceipt = false;
            if (!owner.PersistDraft()) { Progress.pendingReceipt = true; feedback.text = "Couldn’t save your progress. Please try again."; return; }
            selected = -1; Render();
        }
        static void AddFact(VisualElement parent, string title, string value)
        {
            var fact = V("life-summary-fact"); parent.Add(fact);
            fact.Add(L(title, "life-eyebrow")); fact.Add(L(value, "life-fact-value"));
        }
        void RenderSummary(LifeState state)
        {
            Intro("AGE 16  /  AMATEUR CAREER", "Built by your decisions.", "A childhood of choices. One fighter who is yours. Your balance, main style and every development stat carry into your amateur profile.");
            var summary = new ScrollView(ScrollViewMode.Vertical) { name = "life-summary" }; summary.AddToClassList("life-receipt"); main.Add(summary);
            summary.Add(L(owner.Draft.fighterName, "life-result-title"));
            summary.Add(L(string.IsNullOrEmpty(owner.Draft.nickname) ? "A new name in the gym." : "“" + owner.Draft.nickname + "”", "life-story"));
            var facts = V("life-summary-facts"); summary.Add(facts);
            AddFact(facts, "MAIN STYLE", state.Style);
            AddFact(facts, "STARTING FUNDS", "$" + state.Money);
            AddFact(facts, "AMATEUR RECORD", "0–0");
            summary.Add(L("YOUR STRONGEST FOUNDATIONS", "life-eyebrow"));
            foreach (int stat in Enumerable.Range(0, 17).OrderByDescending(i => state.Stats[i]).Take(3))
                summary.Add(L(FighterLife.StatNames[stat] + "   " + state.Stats[stat] + " / 100", "life-strength"));
            summary.Add(L("All 17 ratings are shown in your fighter panel. Your first stop is the training gym.", "life-muted"));
            feedback = L("Your complete life story is saved and can be reviewed at any time.", "life-feedback", "life-feedback"); main.Add(feedback);
            var actions = V("life-actions"); main.Add(actions);
            actions.Add(B("REVIEW YOUR CHOICES", ShowHistory, "life-quiet"));
            actions.Add(B(Progress.amateurStarted ? "RETURN TO TRAINING  →" : "BEGIN AMATEUR CAREER  →", EnterTraining, "life-primary", "life-start-amateur"));
        }
        void EnterTraining()
        {
            if (busy || !FighterLife.Evaluate(Progress).Complete || Progress.pendingReceipt) return;
            bool wasStarted = Progress.amateurStarted; Progress.amateurStarted = true;
            if (!owner.PersistDraft()) { Progress.amateurStarted = wasStarted; feedback.text = "Couldn’t save your career. Please try again."; return; }
            busy = true;
            CareerTrainingSession.Open(owner.Draft, owner.ProfilePath);
        }
        void CloseHistory(VisualElement overlay)
        {
            overlay.RemoveFromHierarchy(); main.SetEnabled(true); rail.SetEnabled(true);
            screen.Q(className: "life-header").SetEnabled(true); screen.Q<Button>("life-history").Focus();
        }
        void ShowHistory()
        {
            if (screen.Q("life-history-overlay") != null) return;
            main.SetEnabled(false); rail.SetEnabled(false); screen.Q(className: "life-header").SetEnabled(false);
            var overlay = V("life-history-overlay", "life-history-overlay"); screen.Add(overlay);
            var panel = V("life-history-panel"); overlay.Add(panel);
            var heading = V("life-actions"); panel.Add(heading); heading.Add(L("The choices behind the fighter.", "life-result-title"));
            heading.Add(B("Close  ×", () => { CloseHistory(overlay); }, "life-quiet", "life-close-history"));
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("life-history-list"); panel.Add(scroll);
            var before = new LifeState();
            for (int age = 0; age < Progress.choices.Count; age++)
            {
                var choice = FighterLife.Chapters[age].Choices[Progress.choices[age]];
                var item = V("life-history-item"); scroll.Add(item);
                item.Add(L("AGE " + age + "  /  " + choice.Title, "life-history-title"));
                var effects = V("life-history-effects"); item.Add(effects); RenderEffects(effects, before, choice, true);
                before = FighterLife.Preview(before, choice);
            }
            if (Progress.choices.Count == 0) scroll.Add(L("Your story starts with your first choice.", "life-story"));
            overlay.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Escape) { CloseHistory(overlay); e.StopPropagation(); } });
            heading.Q<Button>().Focus();
        }
    }
}
