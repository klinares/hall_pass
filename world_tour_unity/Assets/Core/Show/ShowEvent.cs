using System.Collections.Generic;

namespace WorldTour.Core.Shows
{
    /// <summary>What just happened in a show. The Game layer reacts (sound, dog pose, burst, toast, save).</summary>
    public enum ShowEventKind
    {
        /// <summary>A song is up (JS show()). Index, Slot. For math the level and problem are set by now.</summary>
        ItemStarted,
        /// <summary>The mode set up its hint button (JS setupHints). Amount = max hints.</summary>
        HintsReady,
        /// <summary>A hint was shown and counted. Level = which hint (1-based); Amount = fans it cost (see <see cref="ShowEvent.Amount"/>).</summary>
        HintUsed,
        /// <summary>A wrong try. Tries = wrong tries on this song so far. The streak is now 0.</summary>
        WrongTry,
        /// <summary>Answered right. Win, Amount = fans gained, Streak.</summary>
        Answered,
        /// <summary>The answer was shown after the last wrong try (JS reveal()).</summary>
        Revealed,
        /// <summary>A missed song was put back in the queue. Index = where it will play.</summary>
        Requeued,
        /// <summary>A miss that was not put back: Tag = "bonus" (it was a bonus replay), "no-bonus-left" (two already used, or an encore) or "final-song" (the last song in the queue; it waits for the encore and review, QUESTIONS 10).</summary>
        RequeueSkipped,
        /// <summary>
        /// For saved progress (JS record(): recordMath / recordWord). Clean, Missed, Keep, Hinted. Keep means the
        /// item was missed earlier in this show, so a clean replay must not clear it from review.
        /// </summary>
        ItemRecorded,
        /// <summary>The skill record was updated (JS recordSkill). Correct, Hinted, Moved (1, 0, -1), Level after.</summary>
        SkillRecorded,
        /// <summary>The level moved. Moved = 1 or -1, Level = the new level.</summary>
        LevelChanged,
        /// <summary>A tagged mistake was counted (JS ctx.mistake / noteMistake). Tag.</summary>
        MistakeNoted,
        /// <summary>Fans to add to her balance and lifetime (JS P.addFans). Amount. Never negative.</summary>
        FansAwarded,
        /// <summary>The last song is done. Summary.</summary>
        ShowFinished
    }

    /// <summary>One show event. Only the fields named on its <see cref="ShowEventKind"/> are meaningful.</summary>
    public sealed class ShowEvent
    {
        public ShowEventKind Kind;
        public int Index;
        public ShowSlot Slot;
        /// <summary>
        /// Fans: gained (Answered, FansAwarded), max hints (HintsReady), or for HintUsed the drop in what a right
        /// answer would earn now: a hint never takes fans already won, it lowers this song's reward
        /// (e.g. from 1000 x streak to 500). 0 when the song's reward was already lowered.
        /// </summary>
        public int Amount;
        public int Tries;
        public int Level;
        public int Moved;
        public int Streak;
        public WinKind Win;
        public bool Clean;
        public bool Missed;
        public bool Keep;
        public bool Hinted;
        public bool Correct;
        public string Tag;
        public ShowSummary Summary;

        public override string ToString()
        {
            return Kind + " i=" + Index + " amt=" + Amount;
        }
    }

    /// <summary>The ticket's setlist tag for a song (ticket.js:21).</summary>
    public enum SetlistTag
    {
        /// <summary>"nailed it".</summary>
        Ok,
        /// <summary>"used a hint".</summary>
        Hint,
        /// <summary>"rehearse again".</summary>
        Miss
    }

    /// <summary>One line of the ticket's setlist: a main (non-bonus) song in order.</summary>
    public sealed class SetlistEntry
    {
        public ShowItem Item;
        public SetlistTag Tag;
    }

    /// <summary>
    /// What the end of a show hands on: the fields of JS <c>P.finishShow(...)</c> (show.js:221) plus what
    /// <c>renderTicket</c> reads from G (ticket.js).
    /// </summary>
    public sealed class ShowSummary
    {
        // finishShow fields
        public string Mode;
        public string Venue;
        /// <summary>Main songs (bonus replays not counted).</summary>
        public int N;
        /// <summary>Clean wins, bonus replays included (JS G.firstTry).</summary>
        public int First;
        /// <summary>Distinct songs a hint was used on (JS G.hinted.size).</summary>
        public int Hinted;
        public int Fans;
        public int Secs;
        public int Best;
        public bool Sold;
        public bool Encore;

        // ticket fields
        public double Pct;
        public bool Vip;
        /// <summary>Wins with hints and no wrong try (JS G.hintOnly).</summary>
        public int HintOnly;
        /// <summary>Every hint used (JS G.hintTotal).</summary>
        public int HintTotal;
        /// <summary>A math set (the ticket says "math set" instead of the week's label).</summary>
        public bool IsMath;
        /// <summary>
        /// Songs missed at least once (the engine's MissedKids), bonus replays or not. Unity only (QUESTIONS 10 addendum):
        /// the "clean run" sticker needs no misses and no hints, and First alone can't tell, because a clean bonus replay
        /// counts as a first try.
        /// </summary>
        public int Missed;
        public List<SetlistEntry> Setlist = new List<SetlistEntry>();
        /// <summary>Main songs missed at least once (JS missedPrim). The encore replays these.</summary>
        public List<ShowItem> MissedItems = new List<ShowItem>();
        /// <summary>ticket.js:75: an encore is offered when something was missed and this was not an encore.</summary>
        public bool CanEncore;
    }
}
