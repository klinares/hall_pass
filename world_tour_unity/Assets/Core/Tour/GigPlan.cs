using System;
using System.Collections.Generic;
using System.Linq;
using WorldTour.Core.Data;

namespace WorldTour.Core.Tour
{
    /// <summary>
    /// Phase 8.1: which mode each crew member in a place offers as a gig, and how long a gig runs. Plain rules, no
    /// UnityEngine, so edit-mode tests cover them. The game layer passes in the enabled math modes (math_config.json) and
    /// this week's available spelling modes, in setlist order.
    /// </summary>
    public static class GigPlan
    {
        /// <summary>A gig is 3 to 5 questions (the long show stays 10).</summary>
        public const int MinLength = 3, MaxLength = 5;

        /// <summary>The place's position on the tour (0 = the coffee shop); an unknown id counts as 0.</summary>
        public static int PlaceIndex(string placeId)
        {
            for (int i = 0; i < Venues.All.Count; i++) if (Venues.All[i].Id == placeId) return i;
            return 0;
        }

        /// <summary>
        /// One mode id per crew member, the same every visit. Crew alternate math, spelling, math... and each list is
        /// rotated by the place's index, so neighbouring places lead with different modes. A mode is not repeated inside
        /// a place until every mode has been used. An empty string means nothing to offer (both lists empty).
        /// </summary>
        public static string[] Assign(string placeId, int crewCount, IReadOnlyList<string> mathModes, IReadOnlyList<string> spellingModes)
        {
            var result = new string[Math.Max(0, crewCount)];
            var math = mathModes ?? new string[0];
            var spell = spellingModes ?? new string[0];
            var used = new HashSet<string>();
            int p = PlaceIndex(placeId), mathPicks = 0, spellPicks = 0;
            for (int k = 0; k < result.Length; k++)
            {
                bool wantMath = k % 2 == 0;
                if (wantMath && math.Count == 0) wantMath = false;
                else if (!wantMath && spell.Count == 0) wantMath = true;
                IReadOnlyList<string> list = wantMath ? math : spell;
                if (list.Count == 0) { result[k] = ""; continue; }
                int start = p + (wantMath ? mathPicks : spellPicks);
                string pick = null;
                for (int i = 0; i < list.Count && pick == null; i++)
                {
                    string c = list[(start + i) % list.Count];
                    if (!used.Contains(c)) pick = c;
                }
                if (pick == null) pick = list[start % list.Count];   // more crew than modes: repeat
                used.Add(pick);
                if (wantMath) mathPicks++; else spellPicks++;
                result[k] = pick;
            }
            return result;
        }

        /// <summary>
        /// The math mode(s) for the unit she is working on now, best first (5th grade: fractions, "snack table"). It is always one crew
        /// member's first offer and the stage show's default set while it is switched on. Edit this list when the unit changes.
        /// </summary>
        public static readonly string[] CurrentUnit = { "snack" };

        /// <summary>The first <see cref="CurrentUnit"/> mode that is in the enabled math list, or null.</summary>
        public static string UnitMode(IReadOnlyList<string> mathModes)
        {
            if (mathModes == null) return null;
            foreach (string u in CurrentUnit) for (int i = 0; i < mathModes.Count; i++) if (mathModes[i] == u) return u;
            return null;
        }

        /// <summary>
        /// What each crew member can offer, in order: the first entry is the gig they pitch (<see cref="Assign"/>, with the current unit's mode
        /// forced into the last math slot when it would otherwise be missing), the rest is every other enabled math mode and this week's spelling
        /// mode, dealt round-robin from the first crew member, so every mode is on offer somewhere in the place and "another gig" walks the list.
        /// Same result every visit.
        /// </summary>
        public static string[][] Offers(string placeId, int crewCount, IReadOnlyList<string> mathModes, IReadOnlyList<string> spellingModes)
        {
            string[] firsts = Assign(placeId, crewCount, mathModes, spellingModes);
            var math = mathModes ?? new string[0];
            var spell = spellingModes ?? new string[0];
            string unit = UnitMode(math);
            if (unit != null && !firsts.Contains(unit))
            {
                for (int k = firsts.Length - 1; k >= 0; k--)
                    if (firsts[k] != "" && math.Contains(firsts[k])) { firsts[k] = unit; break; }
            }
            var pools = new List<string>[firsts.Length];
            for (int k = 0; k < pools.Length; k++) pools[k] = new List<string>();
            for (int k = 0; k < firsts.Length; k++) if (firsts[k] != "") pools[k].Add(firsts[k]);
            if (pools.Length > 0)
            {
                int deal = 0;
                foreach (string m in math.Concat(spell))
                {
                    if (firsts.Contains(m)) continue;
                    pools[deal % pools.Length].Add(m);
                    deal++;
                }
            }
            return pools.Select(p => p.ToArray()).ToArray();
        }

        /// <summary>The number of questions in one gig, 3 to 5, from a [0, 1) random source.</summary>
        public static int Length(Func<double> random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            int span = MaxLength - MinLength + 1;
            int n = MinLength + (int)Math.Floor(random() * span);
            return Math.Min(MaxLength, Math.Max(MinLength, n));
        }
    }

    /// <summary>How big the crowd in the place is for her fans (the 3D placeholder figures; the real art keeps the rule).</summary>
    public static class CrowdRules
    {
        public const int Min = 6, Max = 36;

        /// <summary>6 figures at the start, growing with the square root of lifetime fans, 36 at most. Never fewer as fans grow.</summary>
        public static int Count(long lifetimeFans)
        {
            if (lifetimeFans < 0) lifetimeFans = 0;
            int n = Min + (int)Math.Floor(Math.Sqrt(lifetimeFans / 1000.0) * 2.0);
            return Math.Min(Max, Math.Max(Min, n));
        }

        /// <summary>Phase 8.3: the fewest figures any place's crowd starts with.</summary>
        public const int VenueMin = 4;

        /// <summary>
        /// Phase 8.3: the full crowd for a place, scaled by the venue's crowd factor (venues.js <c>crowd</c>, 0.15 to 1): 15 at the coffee shop to 44 at the arena.
        /// </summary>
        public static int Capacity(string venueId)
        {
            return (int)Math.Round(10.0 + 34.0 * Venues.ById(venueId).Crowd);
        }

        /// <summary>The lifetime fans at which a place's crowd is full: the next stop's unlock number (the arena, the last one, fills at 1.2 million).</summary>
        public static long FullAtFans(string venueId)
        {
            for (int i = 0; i < Venues.All.Count; i++)
                if (Venues.All[i].Id == venueId) return i + 1 < Venues.All.Count ? Venues.All[i + 1].Fans : Venues.All[i].Fans * 4 / 3;
            return Venues.All[1].Fans;
        }

        /// <summary>
        /// Phase 8.3: how many figures stand in this place for these lifetime fans. A few (<see cref="VenueMin"/>) with no fans, growing with the square root of
        /// her fans (so the first thousands count most) to the place's <see cref="Capacity"/> at <see cref="FullAtFans"/>. Never fewer as fans grow, never more than the capacity.
        /// </summary>
        public static int CountFor(long lifetimeFans, string venueId)
        {
            if (lifetimeFans < 0) lifetimeFans = 0;
            int cap = Capacity(venueId);
            double fill = Math.Sqrt(Math.Min(1.0, lifetimeFans / (double)FullAtFans(venueId)));
            int n = (int)Math.Round(cap * fill);
            return Math.Min(cap, Math.Max(VenueMin, n));
        }
    }
}
