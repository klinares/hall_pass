using System.Collections.Generic;
using WorldTour.Core.Data;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;

namespace WorldTour.Game.Map
{
    /// <summary>One venue's state on the map (map.js:81-90).</summary>
    public struct VenueState
    {
        public Venue Venue;
        public int Index;
        /// <summary>Unlocked (map.js:81 <c>P.unlocked</c>).</summary>
        public bool Open;
        /// <summary>The newest unlocked venue: "you're here" and the bus (map.js:81).</summary>
        public bool Here;
        /// <summary>Unlocked but not yet shown on the map: this visit plays its unlock moment (map.js:92).</summary>
        public bool Fresh;
    }

    /// <summary>
    /// Everything the home screen reads from the save, worked out once per visit (map.js:15-24). Pure: no Unity
    /// objects, so the rules are tested in edit mode. <see cref="Build"/> does not change the save;
    /// <see cref="Commit"/> does what map.js does to it on mount (award stickers, mark unlocks seen).
    /// </summary>
    public sealed class MapModel
    {
        public Venue Newest, Next;
        /// <summary>Index of the newest unlocked venue (map.js:22 <c>reachedIdx</c>).</summary>
        public int Reached;
        /// <summary>How far the route was drawn before this visit's unlocks: the newest unlocked venue that isn't fresh (0 at least).</summary>
        public int ReachedBefore;
        public int Streak, Best;
        public int? Away;
        public bool Sleepy;
        public long Lifetime, Balance;
        public int Stickers, StickersTotal;
        public List<VenueState> Venues = new List<VenueState>();
        /// <summary>Unlocked venues not shown yet, in unlock order (map.js:23 <c>unseenUnlocks</c>).</summary>
        public List<string> Fresh = new List<string>();

        /// <summary>Lifetime fans still needed for the next venue, or 0 when every venue is open.</summary>
        public long ToGo { get { return Next != null ? Next.Fans - Lifetime : 0; } }

        public static MapModel Build(SaveBlob s, string today)
        {
            var m = new MapModel
            {
                Newest = ProgressRules.NewestVenue(s),
                Next = ProgressRules.NextVenue(s),
                Streak = ProgressRules.DayStreak(s, today),
                Best = s.Streak.Best,
                Away = ProgressRules.DaysAway(s, today),
                Lifetime = s.Fans.Lifetime,
                Balance = s.Fans.Balance,
                Stickers = s.Journal.Stickers.Count,
                StickersTotal = Milestones.All.Count,
            };
            m.Sleepy = m.Away != null && m.Away.Value >= 3;
            m.Reached = MapLayout.IndexOf(m.Newest.Id);
            m.Fresh = ProgressRules.UnseenUnlocks(s);
            m.ReachedBefore = 0;
            for (int i = 0; i < Core.Data.Venues.All.Count; i++)
            {
                Venue v = Core.Data.Venues.All[i];
                bool open = ProgressRules.Unlocked(s, v.Id);
                bool fresh = m.Fresh.Contains(v.Id);
                if (open && !fresh && i > m.ReachedBefore) m.ReachedBefore = i;
                m.Venues.Add(new VenueState { Venue = v, Index = i, Open = open, Here = v.Id == m.Newest.Id, Fresh = fresh });
            }
            if (m.ReachedBefore > m.Reached) m.ReachedBefore = m.Reached;
            return m;
        }

        /// <summary>
        /// What map.js does to the save on mount: award any stickers earned outside a show (map.js:17, with every mode
        /// as <c>setModes(MODES)</c>), then mark this visit's unlocks seen (map.js:95). Returns true when the save
        /// changed and should be written.
        /// </summary>
        public static bool Commit(SaveBlob s, IEnumerable<string> fresh, string today, out List<Milestone> stickers)
        {
            stickers = ProgressRules.AwardStickers(s, ModeCatalog.Infos, today);
            bool changed = stickers.Count > 0;
            if (fresh != null) foreach (string id in fresh) changed |= ProgressRules.MarkSeen(s, id);
            return changed;
        }
    }
}
