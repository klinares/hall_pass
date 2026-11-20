using System.Collections.Generic;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Progress
{
    /// <summary>
    /// The whole save: one versioned blob with the fields of world_tour/js/store.js <c>defaults()</c>.
    /// Read and written by <see cref="SaveJson"/> through JsonValue (no reflection), so High stripping in the
    /// web player can't remove anything it needs.
    /// </summary>
    public sealed class SaveBlob
    {
        /// <summary>store.js KEY: the one storage key the whole save lives under.</summary>
        public const string Key = "worldtour.v1";
        /// <summary>store.js <c>v: 1</c>. Migrate always writes this version.</summary>
        public const int Version = 1;

        public int V = Version;
        /// <summary>The day the save was made ("yyyy-mm-dd").</summary>
        public string Created;
        /// <summary>Milliseconds since 1970 at the last save (JS Date.now); 0 = never saved.</summary>
        public long Updated;
        public SettingsData Settings = new SettingsData();
        public FansData Fans = new FansData();
        public StreakData Streak = new StreakData();
        public VenuesData Venues = new VenuesData();
        public Dictionary<string, SkillRecord> Skills = new Dictionary<string, SkillRecord>();
        public Dictionary<string, WordStat> Words = new Dictionary<string, WordStat>();
        public ReviewData Review = new ReviewData();
        public List<ShowRecord> Shows = new List<ShowRecord>();
        /// <summary>Seconds played per day ("yyyy-mm-dd" to seconds).</summary>
        public Dictionary<string, int> Time = new Dictionary<string, int>();
        public InventoryData Inventory = new InventoryData();
        public JournalData Journal = new JournalData();

        /// <summary>store.js <c>defaults()</c>, with today's stamp passed in.</summary>
        public static SaveBlob Defaults(string today)
        {
            return new SaveBlob { Created = today };
        }
    }

    public sealed class SettingsData
    {
        public bool Sound = true;
        public string Theme = "tour-pink";
        /// <summary>"auto" or "reduced".</summary>
        public string Motion = "auto";
    }

    /// <summary>Balance is what she can spend backstage; lifetime only ever grows and unlocks venues.</summary>
    public sealed class FansData
    {
        public long Balance;
        public long Lifetime;
    }

    /// <summary>The day streak.</summary>
    public sealed class StreakData
    {
        public int Days;
        public int Best;
        /// <summary>The last day she finished a show, or null.</summary>
        public string Last;
    }

    public sealed class VenuesData
    {
        public List<string> Unlocked = new List<string> { Data.Venues.First };
        /// <summary>Unlocks she has already seen on the map.</summary>
        public List<string> Seen = new List<string> { Data.Venues.First };
    }

    /// <summary>One word's record (progress.js recordWord): attempts, correct, first-try, hinted, missed, last day.</summary>
    public sealed class WordStat
    {
        public int A;
        public int C;
        public int First;
        public int H;
        public int Miss;
        public string Last;
    }

    public sealed class ReviewData
    {
        /// <summary>Words missed and not yet cleared.</summary>
        public List<string> Words = new List<string>();
        /// <summary>Missed math problems, stored whole (js/review.js), oldest first, at most <see cref="Review.Max"/>.</summary>
        public List<ReviewEntry> Math = new List<ReviewEntry>();
    }

    /// <summary>
    /// One saved math problem: <c>{ skill, level, seed, prob }</c>. <see cref="Prob"/> is the problem's JSON exactly
    /// as stored (the generator's object), or null for an old seed-only entry. Kept as JSON so a damaged or
    /// hand-edited problem survives loading and is judged by <see cref="Review.Usable"/> as the JS does.
    /// </summary>
    public sealed class ReviewEntry
    {
        public string Skill;
        public int Level;
        public long Seed;
        public JsonValue Prob;
    }

    /// <summary>One finished show (progress.js finishShow: <c>{ d, ...summary }</c>).</summary>
    public sealed class ShowRecord
    {
        public string D;
        public string Mode;
        public string Venue;
        public int N;
        public int First;
        public int Hinted;
        public int Fans;
        public int Secs;
        public int Best;
        /// <summary>Written only when true (JS <c>sold: ... || undefined</c>).</summary>
        public bool Sold;
        /// <summary>Written only when true (JS <c>encore: G.encore || undefined</c>).</summary>
        public bool Encore;
    }

    public sealed class InventoryData
    {
        public List<string> Owned = new List<string> { "theme:tour-pink", "guitar:classic", "dog:none" };
        /// <summary>Slot to item (without the slot prefix).</summary>
        public Dictionary<string, string> Equipped = DefaultEquipped();

        public static Dictionary<string, string> DefaultEquipped()
        {
            return new Dictionary<string, string> { { "theme", "tour-pink" }, { "guitar", "classic" }, { "dog", "none" } };
        }
    }

    public sealed class JournalData
    {
        /// <summary>Milestone id to the day it was earned. Kept forever.</summary>
        public Dictionary<string, string> Stickers = new Dictionary<string, string>();
        /// <summary>Sticker key to where she placed it on the page.</summary>
        public Dictionary<string, Spot> Layout = new Dictionary<string, Spot>();
    }

    /// <summary>A sticker's place on the journal page, as fractions of the page, plus its tilt.</summary>
    public sealed class Spot
    {
        public double X;
        public double Y;
        public double R;
    }
}
