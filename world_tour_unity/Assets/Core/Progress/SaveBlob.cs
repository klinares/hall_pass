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
        /// <summary>
        /// The save format version. 1 = the web game's store.js format; 2 = phase 5: adds <c>journal.seen</c> and keeps
        /// keys this version doesn't know. Migrate writes this version, or the stored one if it is newer (so a save from a
        /// newer game is never stamped back down and re-migrated by it).
        /// </summary>
        public const int Version = 2;

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
        /// <summary>Top-level keys this version doesn't know, kept in their stored order and written back after the known ones (null = none).</summary>
        public JsonValue Extra;

        /// <summary>store.js <c>defaults()</c>, with today's stamp passed in.</summary>
        public static SaveBlob Defaults(string today)
        {
            return new SaveBlob { Created = today };
        }
    }

    public sealed class SettingsData
    {
        /// <summary>Keys this version doesn't know, kept in stored order (null = none).</summary>
        public JsonValue Extra;
        public bool Sound = true;
        /// <summary>Phase 10A: the music and ambience slider, 0 to 100 in steps of 10. Null (not in the save) means the default, 40. Written only when set.</summary>
        public int? Ambience;
        /// <summary>Phase 10A: the effects slider, 0 to 100 in steps of 10. Null means the default, 100. Written only when set.</summary>
        public int? Effects;
        public string Theme = "tour-pink";
        /// <summary>"auto" or "reduced".</summary>
        public string Motion = "auto";
        /// <summary>Phase 7.1, the manager's office: spelling plays one level lower at the top and needs 8 in a row to move up. Written only when true.</summary>
        public bool GentleSpelling;
        /// <summary>Phase 7.1, the manager's office: the same for math. Written only when true.</summary>
        public bool GentleMath;
    }

    /// <summary>Balance is what she can spend backstage; lifetime only ever grows and unlocks venues.</summary>
    public sealed class FansData
    {
        /// <summary>Keys this version doesn't know, kept in stored order (null = none).</summary>
        public JsonValue Extra;
        public long Balance;
        public long Lifetime;
    }

    /// <summary>The day streak.</summary>
    public sealed class StreakData
    {
        /// <summary>Keys this version doesn't know, kept in stored order (null = none).</summary>
        public JsonValue Extra;
        public int Days;
        public int Best;
        /// <summary>The last day she finished a show, or null.</summary>
        public string Last;
    }

    public sealed class VenuesData
    {
        /// <summary>Keys this version doesn't know, kept in stored order (null = none).</summary>
        public JsonValue Extra;
        public List<string> Unlocked = new List<string> { Data.Venues.First };
        /// <summary>Unlocks she has already seen on the map.</summary>
        public List<string> Seen = new List<string> { Data.Venues.First };
    }

    /// <summary>One word's record (progress.js recordWord): attempts, correct, first-try, hinted, missed, last day.</summary>
    public sealed class WordStat
    {
        /// <summary>Keys this version doesn't know, kept in stored order (null = none).</summary>
        public JsonValue Extra;
        public int A;
        public int C;
        public int First;
        public int H;
        public int Miss;
        public string Last;
    }

    public sealed class ReviewData
    {
        /// <summary>Keys this version doesn't know, kept in stored order (null = none).</summary>
        public JsonValue Extra;
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
        /// <summary>Keys this version doesn't know, kept in stored order (null = none).</summary>
        public JsonValue Extra;
        public string Skill;
        public int Level;
        public long Seed;
        public JsonValue Prob;
    }

    /// <summary>One finished show (progress.js finishShow: <c>{ d, ...summary }</c>).</summary>
    public sealed class ShowRecord
    {
        /// <summary>Keys this version doesn't know, kept in stored order (null = none).</summary>
        public JsonValue Extra;
        public string D;
        public string Mode;
        public string Venue;
        public int N;
        public int First;
        public int Hinted;
        public int Fans;
        public int Secs;
        public int Best;
        /// <summary>Songs missed at least once. Unity only (QUESTIONS 10 addendum), written only when above 0 so saves without misses match the JS byte for byte; a record without it reads as 0.</summary>
        public int Missed;
        /// <summary>Written only when true (JS <c>sold: ... || undefined</c>).</summary>
        public bool Sold;
        /// <summary>Written only when true (JS <c>encore: G.encore || undefined</c>).</summary>
        public bool Encore;
    }

    public sealed class InventoryData
    {
        /// <summary>Keys this version doesn't know, kept in stored order (null = none).</summary>
        public JsonValue Extra;
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
        /// <summary>Keys this version doesn't know, kept in stored order (null = none).</summary>
        public JsonValue Extra;
        /// <summary>Milestone id to the day it was earned. Kept forever.</summary>
        public Dictionary<string, string> Stickers = new Dictionary<string, string>();
        /// <summary>Sticker key to where she placed it on the page.</summary>
        public Dictionary<string, Spot> Layout = new Dictionary<string, Spot>();
        /// <summary>
        /// Version 2: the sticker keys (milestone ids and bought "sticker:" ids) she has already seen peel onto the journal
        /// page, in the order they peeled. Only ever grows. A version 1 save starts with none, so everything already in her
        /// book peels on at her first visit.
        /// </summary>
        public List<string> Seen = new List<string>();
    }

    /// <summary>A sticker's place on the journal page, as fractions of the page, plus its tilt.</summary>
    public sealed class Spot
    {
        /// <summary>Keys this version doesn't know, kept in stored order (null = none).</summary>
        public JsonValue Extra;
        public double X;
        public double Y;
        public double R;
    }
}
