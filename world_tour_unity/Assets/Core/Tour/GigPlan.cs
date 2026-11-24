using System;
using System.Collections.Generic;
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
    }
}
