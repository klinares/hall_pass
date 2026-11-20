using System.Globalization;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Core.Shows
{
    /// <summary>
    /// One thing a show asks: a week's word or a math problem. Ported from world_tour/js/items.js
    /// ("a show item is a week's word ({ w, s, h, ... }) or a math problem ({ skill, level, seed, prob })").
    /// The engine only needs <see cref="Kid"/>; everything else is for the mode that draws it.
    /// </summary>
    public abstract class ShowItem
    {
        /// <summary>JS <c>kid(v)</c>: the item's identity inside a show (missed, hinted, encore all key on it).</summary>
        public abstract string Kid { get; }

        /// <summary>JS <c>labelOf(v)</c>: the word, or the problem's label (empty until the problem exists).</summary>
        public abstract string Label { get; }
    }

    /// <summary>A spelling word. <see cref="Data"/> carries the full week record (h, x, prefix) for the mode.</summary>
    public sealed class WordItem : ShowItem
    {
        /// <summary>JS <c>w</c>: the word.</summary>
        public readonly string W;
        /// <summary>JS <c>s</c>: syllables joined by '-', or null.</summary>
        public readonly string S;
        /// <summary>Whatever the Game layer wants to keep with the word (the week's word record). The engine never reads it.</summary>
        public readonly object Data;

        public WordItem(string w, string s = null, object data = null)
        {
            W = w ?? "";
            S = s;
            Data = data;
        }

        public override string Kid { get { return W; } }
        public override string Label { get { return W; } }
        public override string ToString() { return W; }
    }

    /// <summary>
    /// A math problem slot. A fresh one has no level and no problem: the show fills both in when it is
    /// played (JS: "fresh ones, which take her level at the moment they're played"). A review one arrives
    /// with the stored problem. A bonus replay or encore reuses the same object, so the same problem.
    /// </summary>
    public sealed class MathItem : ShowItem
    {
        public readonly string Skill;
        /// <summary>Null until the show plays it (JS <c>level: null</c>).</summary>
        public int? Level;
        public readonly long Seed;
        /// <summary>Null until generated, unless it came from the review queue.</summary>
        public Problem Prob;

        public MathItem(string skill, int? level, long seed, Problem prob = null)
        {
            Skill = skill;
            Level = level;
            Seed = seed;
            Prob = prob;
        }

        /// <summary>JS <c>`${skill}:${level}:${seed}`</c>; a level not yet set prints "null", as the JS template does.</summary>
        public override string Kid
        {
            get
            {
                string lv = Level.HasValue ? Level.Value.ToString(CultureInfo.InvariantCulture) : "null";
                return Skill + ":" + lv + ":" + Seed.ToString(CultureInfo.InvariantCulture);
            }
        }

        public override string Label { get { return Prob != null ? Prob.Label ?? "" : ""; } }
        public override string ToString() { return Kid; }
    }

    /// <summary>JS queue entry state: '' (not played or in play), 'done', 'miss'.</summary>
    public enum SlotState { None, Done, Miss }

    /// <summary>One entry in the show's queue (JS <c>{ v, bonus, state }</c>).</summary>
    public sealed class ShowSlot
    {
        public readonly ShowItem Item;
        /// <summary>True for a re-inserted miss ("bonus song, one more try"). Bonus slots don't count toward the show's n.</summary>
        public readonly bool Bonus;
        public SlotState State;

        public ShowSlot(ShowItem item, bool bonus)
        {
            Item = item;
            Bonus = bonus;
        }
    }
}
