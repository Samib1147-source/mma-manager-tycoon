using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MmaManagerTycoon
{
    public enum FighterStat { Boxing, Kickboxing, Wrestling, BJJ, TakedownDefense, SubmissionDefense, Cardio, Strength, Speed, Durability, FightIQ, Discipline, Motivation, Confidence, Coachability, Loyalty, Professionalism }
    [Serializable]
    public sealed class LifeProgress
    {
        public int version = 1;
        public List<int> choices = new List<int>();
        public bool pendingReceipt;
        public bool amateurStarted;
    }
    public sealed class LifeState
    {
        public int Money = 100;
        public int[] Stats = Enumerable.Repeat(10, 17).ToArray();
        public string Style = "Undecided";
        public int Decisions;
        public int Age => Math.Min(Decisions, 16);
        public bool Complete => Decisions == 17;
        public LifeState Copy() => new LifeState { Money = Money, Stats = (int[])Stats.Clone(), Style = Style, Decisions = Decisions };
    }
    public readonly struct StatChange
    {
        public readonly FighterStat Stat;
        public readonly int Amount;
        public StatChange(FighterStat stat, int amount) { Stat = stat; Amount = amount; }
    }
    public sealed class LifeChoice
    {
        public string Title, Description, Style;
        public int Cash;
        public StatChange[] Changes;
        public LifeChoice(string title, string description, int cash, StatChange[] changes, string style = null)
        { Title = title; Description = description; Cash = cash; Changes = changes; Style = style; }
    }
    public sealed class LifeChapter
    {
        public string Kicker, Title, Story;
        public LifeChoice[] Choices;
        public LifeChapter(string kicker, string title, string story, params LifeChoice[] choices)
        { Kicker = kicker; Title = title; Story = story; Choices = choices; }
    }
    public static class FighterLife
    {
        public static readonly string[] StatNames = { "Boxing", "Kickboxing", "Wrestling", "Brazilian Jiu-Jitsu", "Takedown defense", "Submission defense", "Cardio", "Strength", "Speed", "Durability", "Fight IQ", "Discipline", "Motivation", "Confidence", "Coachability", "Loyalty", "Professionalism" };
        static StatChange G(FighterStat stat, int value) => new StatChange(stat, value);
        static LifeChoice C(string title, string story, int cash, params StatChange[] changes) => new LifeChoice(title, story, cash, changes);
        static LifeChoice S(string title, string story, params StatChange[] changes) => new LifeChoice(title, story, 0, changes, title);
        public static readonly LifeChapter[] Chapters = {
            new LifeChapter("FAMILY / FIRST FOUNDATIONS", "A small beginning.", "Your story begins with a $100 family fund. In these early years, choose how your family spends time and money.",
                C("A shelf of stories", "Your family chooses a few books for quiet evenings spent reading together.", -20, G(FighterStat.FightIQ,2), G(FighterStat.Coachability,1)),
                C("A home full of play", "Songs, floor games and time together fill the days at home.", 0, G(FighterStat.Speed,2), G(FighterStat.Confidence,1)),
                C("Build a little safety net", "Family gifts go into the fund for another day.",25,G(FighterStat.Discipline,2),G(FighterStat.Motivation,-1))),
            new LifeChapter("FAMILY / AGE 1", "Finding your feet.", "Your family is settling into a routine. What becomes part of everyday life?",
                C("Playgroup mornings", "Your family signs up for mornings with the local playgroup.",-15,G(FighterStat.Confidence,2),G(FighterStat.Coachability,2)),
                C("Park afternoons", "Spend afternoons at the park with your family.",0,G(FighterStat.Cardio,2),G(FighterStat.Speed,1)),
                C("Keep a steady routine", "Your family sets regular times for meals, naps and play.",0,G(FighterStat.Discipline,3),G(FighterStat.Confidence,-1))),
            new LifeChapter("FAMILY / AGE 2", "Curiosity takes over.", "Everything is a new challenge. Your family picks a favourite way to help you explore.",
                C("Puzzles together", "Sit down together with a box of simple puzzles.",-15,G(FighterStat.FightIQ,3),G(FighterStat.Coachability,1)),
                C("Chase and catch", "Take a borrowed ball outside for a game on the grass.",0,G(FighterStat.Speed,2),G(FighterStat.Cardio,1)),
                C("Save the birthday gifts", "Put birthday money in the fund and make your own fun at home.",20,G(FighterStat.Discipline,2),G(FighterStat.Motivation,-1))),
            new LifeChapter("FAMILY / AGE 3", "Learning to join in.", "You start spending more time with other children. What does your family encourage?",
                C("Community movement class", "Join an instructor and other children for movement games.",-25,G(FighterStat.Speed,3),G(FighterStat.Coachability,2)),
                C("Make-believe with friends", "Make up stories and imaginary adventures with your friends.",0,G(FighterStat.Confidence,2),G(FighterStat.Loyalty,2)),
                C("Little responsibilities", "Take on a few small jobs, starting with tidying your toys.",0,G(FighterStat.Discipline,3),G(FighterStat.Motivation,-1))),
            new LifeChapter("FAMILY / AGE 4", "The first challenge.", "School is getting closer. Your family helps you prepare in a way that suits you.",
                C("Try a balance course", "Try stepping stones and balance beams at a supervised session.",-20,G(FighterStat.Speed,3),G(FighterStat.Confidence,2)),
                C("Learn through library games", "Join the free games and activities at your local library.",0,G(FighterStat.FightIQ,2),G(FighterStat.Coachability,2)),
                C("Make saving a game", "Put family gift money aside for something you can choose later.",15,G(FighterStat.Discipline,2),G(FighterStat.Motivation,-1))),
            new LifeChapter("SCHOOL / AGE 5", "A bigger world.", "Your first school year brings new friendships, rules and choices. Where do you put your energy?",
                C("Join the after-school games", "Spend afternoons at a club for running, jumping and team games.",-25,G(FighterStat.Cardio,3),G(FighterStat.Speed,2)),
                C("Be the welcoming friend", "Invite a new pupil to sit with you and join your games.",0,G(FighterStat.Loyalty,3),G(FighterStat.Confidence,2)),
                C("Focus on classroom routines", "Make homework and classroom tasks part of your daily routine.",0,G(FighterStat.Discipline,3),G(FighterStat.FightIQ,2),G(FighterStat.Cardio,-1))),
            new LifeChapter("SCHOOL / AGE 6", "Your first team.", "A school activity fair gives you a few ways to spend your afternoons.",
                C("Join the football club", "Sign up for the school football club and its weekly matches.",-30,G(FighterStat.Cardio,4),G(FighterStat.Loyalty,2)),
                C("Try the free running group", "Meet a teacher and classmates for the free playground running group.",0,G(FighterStat.Cardio,3),G(FighterStat.Discipline,2)),
                C("Choose the puzzle club", "Spend afternoons solving puzzles with other pupils.",0,G(FighterStat.FightIQ,4),G(FighterStat.Speed,-1))),
            new LifeChapter("SCHOOL / AGE 7", "Inside a martial arts gym.", "A community gym runs an open day. All activities are supervised, non-contact basics. What catches your attention?",
                C("Book the boxing basics course", "Sign up for supervised footwork and pad games at the gym.",-35,G(FighterStat.Boxing,4),G(FighterStat.Coachability,2)),
                C("Try the free grappling session", "Take a place in the free session for balance games and breakfalls.",0,G(FighterStat.Wrestling,3),G(FighterStat.SubmissionDefense,1)),
                C("Watch and ask questions", "Stay beside the mats, watch the session and talk to the coaches.",0,G(FighterStat.FightIQ,3),G(FighterStat.Motivation,2))),
            new LifeChapter("SCHOOL / AGE 8", "Time has a price.", "You want more time for your interests, and the family fund could use a boost. How do you balance things?",
                C("Help with extra chores", "Agree on an allowance for doing extra jobs around the house.",30,G(FighterStat.Discipline,3),G(FighterStat.Cardio,-1)),
                C("Practice with a friend", "Meet a friend after school for movement games and simple drills.",0,G(FighterStat.Speed,3),G(FighterStat.Loyalty,2)),
                C("Take a junior movement course", "Book a place on the coach-led junior movement course.",-30,G(FighterStat.Strength,2),G(FighterStat.Durability,2),G(FighterStat.Coachability,2))),
            new LifeChapter("SCHOOL / AGE 9", "Handling a setback.", "Your school team has a rough day. The result stings, but your response is yours to choose.",
                C("Ask what you can improve", "Ask the coach to talk through the game with you.",0,G(FighterStat.Coachability,4),G(FighterStat.FightIQ,2),G(FighterStat.Confidence,-1)),
                C("Lift your teammates", "Get the team together after the result and talk it over.",0,G(FighterStat.Loyalty,3),G(FighterStat.Motivation,2)),
                C("Book a skills workshop", "Join a practical workshop with a small group and an instructor.",-25,G(FighterStat.Confidence,4),G(FighterStat.Speed,2))),
            new LifeChapter("GROWING UP / AGE 10", "A little responsibility.", "The gym is becoming a familiar place. There is more than one way to belong.",
                C("Help at the community fundraiser", "Help run a community event in exchange for a thank-you payment.",40,G(FighterStat.Professionalism,3),G(FighterStat.Loyalty,2),G(FighterStat.Cardio,-1)),
                C("Join junior technical classes", "Book a place in the gym’s junior non-contact classes.",-40,G(FighterStat.Boxing,3),G(FighterStat.Kickboxing,3),G(FighterStat.Discipline,2)),
                C("Use the free open session", "Drop into the free beginner session at the community gym.",0,G(FighterStat.Wrestling,2),G(FighterStat.Coachability,3))),
            new LifeChapter("GROWING UP / AGE 11", "Choose your circle.", "Your friends are developing different interests. What do you build your afternoons around?",
                C("Become the reliable training partner", "Arrange regular times to practise with the same partner.",0,G(FighterStat.Discipline,3),G(FighterStat.Loyalty,3),G(FighterStat.Motivation,-1)),
                C("Keep sport social", "Join your friends for a different sport each week.",0,G(FighterStat.Motivation,3),G(FighterStat.Confidence,2)),
                C("Attend a junior grappling clinic", "Spend a day on the mats at a junior grappling clinic.",-45,G(FighterStat.BJJ,4),G(FighterStat.SubmissionDefense,3))),
            new LifeChapter("GROWING UP / AGE 12", "Earn it or train it.", "A supervised weekend community event offers paid tasks. Your training group meets at the same time.",
                C("Take the event work", "Accept paid tasks at the supervised weekend event.",60,G(FighterStat.Professionalism,3),G(FighterStat.Discipline,2),G(FighterStat.Cardio,-2)),
                C("Keep the training commitment", "Stick with your usual training group this weekend.",0,G(FighterStat.Cardio,4),G(FighterStat.Discipline,2),G(FighterStat.Strength,2)),
                C("Invest in a skills camp", "Use part of the fund to book a supervised junior camp.",-60,G(FighterStat.Boxing,3),G(FighterStat.Wrestling,3),G(FighterStat.FightIQ,3))),
            new LifeChapter("GROWING UP / AGE 13", "Getting serious.", "You are thinking about a future in MMA. How do you prepare for a bigger commitment?",
                C("Study the sport", "Watch fight footage and technical breakdowns with your coach.",0,G(FighterStat.FightIQ,4),G(FighterStat.Coachability,2)),
                C("Build your conditioning", "Follow a regular, supervised exercise schedule at the gym.",0,G(FighterStat.Cardio,4),G(FighterStat.Durability,3),G(FighterStat.Motivation,-1)),
                C("Work the club’s weekend event", "Accept paid work helping the club run its weekend event.",70,G(FighterStat.Professionalism,3),G(FighterStat.Loyalty,2),G(FighterStat.Cardio,-2))),
            new LifeChapter("TURNING POINT / AGE 14", "Find your fighting identity.", "Choose your main style. This becomes your foundation, not a restriction: you can still develop every MMA skill. All four foundations are available without a fee.",
                S("Boxing", "Make the boxing gym your home. Work the bag, pads and ring drills.",G(FighterStat.Boxing,9),G(FighterStat.Speed,3),G(FighterStat.Kickboxing,-2)),
                S("Muay Thai", "Join the Muay Thai group for pad rounds, kicks and clinch practice.",G(FighterStat.Kickboxing,9),G(FighterStat.Durability,3),G(FighterStat.Speed,-1)),
                S("Wrestling", "Choose the wrestling room for mat drills, shots and sprawls.",G(FighterStat.Wrestling,9),G(FighterStat.TakedownDefense,4),G(FighterStat.Boxing,-2)),
                S("Brazilian Jiu-Jitsu", "Join the jiu-jitsu team for positional rounds and ground technique.",G(FighterStat.BJJ,9),G(FighterStat.SubmissionDefense,4),G(FighterStat.Cardio,-1))),
            new LifeChapter("THE FINAL STRETCH / AGE 15", "Make room for the dream.", "Your chosen style gives your training direction. Now decide how you will support the next step.",
                C("Take a supervised weekend job", "Accept a supervised job with regular weekend hours.",90,G(FighterStat.Professionalism,4),G(FighterStat.Discipline,2),G(FighterStat.Cardio,-2)),
                C("Train consistently with your coach", "Set aside regular sessions with your coach each week.",0,G(FighterStat.FightIQ,3),G(FighterStat.Coachability,3),G(FighterStat.Cardio,2)),
                C("Join the development programme", "Book a place in the gym’s small-group development programme.",-70,G(FighterStat.Strength,4),G(FighterStat.TakedownDefense,3),G(FighterStat.SubmissionDefense,3))),
            new LifeChapter("AMATEUR HORIZON / AGE 16", "The next chapter is yours.", "You are ready to begin your amateur journey in the game. Choose how to prepare with the money and habits you have built.",
                C("Invest in a coached preparation block", "Set aside part of your fund for a dedicated coaching block.",-100,G(FighterStat.FightIQ,4),G(FighterStat.Coachability,3),G(FighterStat.Confidence,3)),
                C("Buy your own training kit", "Choose your own gloves, wraps and other essentials for training.",-60,G(FighterStat.Durability,3),G(FighterStat.Confidence,3),G(FighterStat.Professionalism,2)),
                C("Take the club-supported route", "Accept a free club place and borrowed kit, and help around the gym.",0,G(FighterStat.Discipline,2),G(FighterStat.Loyalty,3),G(FighterStat.Professionalism,2)))
        };
        public static LifeState Evaluate(LifeProgress progress)
        {
            var state = new LifeState();
            if (progress == null) return state;
            if (progress.version != 1 || progress.choices == null || progress.choices.Count > Chapters.Length) throw new ArgumentException("Invalid life-story save.");
            foreach (int index in progress.choices)
            {
                var chapter = Chapters[state.Decisions];
                if (index < 0 || index >= chapter.Choices.Length) throw new ArgumentException("Invalid saved decision.");
                var choice = chapter.Choices[index];
                if (state.Money + choice.Cash < 0) throw new ArgumentException("Saved decision exceeds the available money.");
                Apply(state, choice); state.Decisions++;
            }
            if (progress.amateurStarted && !state.Complete || progress.pendingReceipt && state.Decisions == 0) throw new ArgumentException("Invalid life-story stage.");
            return state;
        }
        public static LifeState Preview(LifeState state, LifeChoice choice)
        { var result = state.Copy(); Apply(result, choice); return result; }
        static void Apply(LifeState state, LifeChoice choice)
        {
            state.Money += choice.Cash;
            foreach (var gain in choice.Changes) state.Stats[(int)gain.Stat] = Mathf.Clamp(state.Stats[(int)gain.Stat] + gain.Amount, 0, 100);
            if (choice.Style != null) state.Style = choice.Style;
        }
        public static bool CanChoose(LifeProgress progress, int choice)
        {
            var state = Evaluate(progress);
            return !state.Complete && !(progress?.pendingReceipt ?? false) && choice >= 0 && choice < Chapters[state.Age].Choices.Length && state.Money + Chapters[state.Age].Choices[choice].Cash >= 0;
        }
        public static void Choose(LifeProgress progress, int choice)
        {
            if (!CanChoose(progress, choice)) throw new InvalidOperationException("This decision is not available.");
            progress.choices.Add(choice); progress.pendingReceipt = true;
        }
    }
}
