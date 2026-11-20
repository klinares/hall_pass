using System;
using System.Collections.Generic;
using WorldTour.Core.Shows;

namespace WorldTour.Game.Mascot
{
    /// <summary>What riff does for one show event: an optional pose, an optional reaction, an optional line.</summary>
    public struct DogCue
    {
        public bool HasPose;
        public DogPose Pose;
        public DogMotion Motion;
        /// <summary>
        /// False (the web rule, show.js sayDog): the reaction plays only when the pose actually changes.
        /// True: it plays every time (the wobble on each wrong try).
        /// </summary>
        public bool MotionAlways;
        /// <summary>The bubble text, or null to leave the bubble as it is.</summary>
        public string Line;

        public static readonly DogCue Nothing = new DogCue();
        public static DogCue Of(DogPose pose, DogMotion motion = DogMotion.None, string line = null, bool always = false) =>
            new DogCue { HasPose = true, Pose = pose, Motion = motion, Line = line, MotionAlways = always };
        public override string ToString() => (HasPose ? DogPoses.Id(Pose) : "-") + "/" + Motion + (MotionAlways ? "!" : "") + "/" + (Line ?? "");
    }

    /// <summary>
    /// What a cue may read besides the event: the random source, the current word (null for math), and the mode's
    /// reveal line for a word (show.js reveal's default; the director passes RemixLines.Reveal, which owns the text).
    /// </summary>
    public struct DogCueInput
    {
        public Func<double> Random;
        public string Word;
        public Func<string, string> RevealLine;
    }

    /// <summary>
    /// The one table from show events to riff's reactions (2.5 and 2.6 reuse it). It follows js/show.js:
    /// <code>
    /// ItemStarted  first song      idle                 dogShowStart        (show.js:82, shown when the show mounts)
    /// ItemStarted  bonus replay    idle                 "this one again..." (show.js:198)
    /// ItemStarted  other songs     idle                 next-song pick      (show.js:198)
    /// HintUsed                     think                (the mode says the hint)               (show.js:126)
    /// WrongTry                     oops, wobble always  (the mode says the ladder)             (parts.js:59; wobble: show.js:168 wobbles the play area)
    /// Answered     clean           cheer, hop           dogWin(streak)      (show.js:163)
    /// Answered     hints, no miss  cheer, hop           dogHintWin
    /// Answered     after a miss    cheer, hop           dogAfterMiss
    /// Revealed                     oops                 RevealLine(word) for a word; math modes say their own (show.js:178)
    /// ShowFinished pct >= .7       cheer, hop           (the ticket has its own text)          (ticket.js:80 always cheers)
    /// ShowFinished pct &lt; .7       idle (calm)
    /// Nudge (15 s, timer in 2.5)   think                dogIdleHint         (show.js:115)
    /// </code>
    /// Every other event leaves riff as he is. A hop plays only when the pose changes to cheer, as in the web.
    /// </summary>
    public static class DogCues
    {
        /// <summary>The finish line below which riff stays calm: ticket.js "what a show" starts at .7 (copy.js finishHead).</summary>
        public const double FinishCheerPct = .7;

        public static readonly IReadOnlyDictionary<ShowEventKind, Func<ShowEvent, DogCueInput, DogCue>> Table =
            new Dictionary<ShowEventKind, Func<ShowEvent, DogCueInput, DogCue>>
            {
                [ShowEventKind.ItemStarted] = (e, c) => DogCue.Of(DogPose.Idle, DogMotion.None,
                    e.Index == 0 ? DogLines.PickShowStart(c.Random)
                    : e.Slot != null && e.Slot.Bonus ? DogLines.BonusSong : DogLines.PickNextSong(c.Random)),
                [ShowEventKind.HintUsed] = (e, c) => DogCue.Of(DogPose.Think),
                [ShowEventKind.WrongTry] = (e, c) => DogCue.Of(DogPose.Oops, DogMotion.Wobble, null, always: true),
                [ShowEventKind.Answered] = (e, c) => DogCue.Of(DogPose.Cheer, DogMotion.Hop, DogLines.ForWin(e.Win, e.Streak, c.Random)),
                [ShowEventKind.Revealed] = (e, c) => DogCue.Of(DogPose.Oops, DogMotion.None, c.Word != null && c.RevealLine != null ? c.RevealLine(c.Word) : null),
                [ShowEventKind.ShowFinished] = (e, c) => e.Summary != null && e.Summary.Pct >= FinishCheerPct
                    ? DogCue.Of(DogPose.Cheer, DogMotion.Hop) : DogCue.Of(DogPose.Idle),
            };

        public static DogCue For(ShowEvent e, DogCueInput input)
        {
            if (e == null) return DogCue.Nothing;
            return Table.TryGetValue(e.Kind, out var f) ? f(e, input) : DogCue.Nothing;
        }

        /// <summary>show.js armNudge: after 15 s on a song with hints left, the bulb glows and riff says this.</summary>
        public static DogCue Nudge() => DogCue.Of(DogPose.Think, DogMotion.None, DogLines.IdleHint);
    }

    /// <summary>
    /// show.js sayDog and coachPose: the bubble text changes when a line is given, the pose changes only when it
    /// is different, and the reaction is played on a change (or always, when the cue says so).
    /// </summary>
    public sealed class DogCoach
    {
        public DogPose Pose { get; private set; }
        public string Line { get; private set; }

        public DogCoach(DogPose pose = DogPose.Idle, string line = null) { Pose = pose; Line = line; }

        /// <summary>Applies a cue; returns the reaction to play now (None if nothing should move).</summary>
        public DogMotion Apply(DogCue cue, out bool poseChanged)
        {
            poseChanged = false;
            if (cue.Line != null) Line = cue.Line;
            if (cue.HasPose && cue.Pose != Pose) { Pose = cue.Pose; poseChanged = true; }
            if (cue.Motion == DogMotion.None) return DogMotion.None;
            return poseChanged || cue.MotionAlways ? cue.Motion : DogMotion.None;
        }

        /// <summary>A mode's own line (ctx.say(html, pose)): web sayDog with an optional pose and no reaction except a hop into cheer.</summary>
        public DogMotion Say(string line, DogPose? pose, out bool poseChanged)
        {
            var cue = new DogCue { Line = line, HasPose = pose.HasValue, Pose = pose ?? DogPose.Idle, Motion = pose == DogPose.Cheer ? DogMotion.Hop : DogMotion.None };
            return Apply(cue, out poseChanged);
        }
    }
}
