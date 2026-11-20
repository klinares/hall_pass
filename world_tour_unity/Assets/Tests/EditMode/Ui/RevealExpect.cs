using System;
using System.Collections.Generic;
using System.Linq;
using WorldTour.Game.Modes;

namespace WorldTour.Game.EditTests
{
    /// <summary>
    /// The web game's worked line ran into " it'll come back around." with no full stop ("so 9 it'll come back around.");
    /// Unity puts one in (MathLines.ComeBackAfter; intended difference 30). The JS-match tests map the web's reveal lines
    /// to the Unity form before comparing, and change nothing else.
    /// </summary>
    internal static class RevealExpect
    {
        public static string Map(string entry, string prefix)
        {
            if (entry == null || !entry.StartsWith(prefix, StringComparison.Ordinal) || !entry.EndsWith(MathLines.ComeBack, StringComparison.Ordinal)) return entry;
            string worked = entry.Substring(prefix.Length, entry.Length - prefix.Length - MathLines.ComeBack.Length);
            return prefix + MathLines.ComeBackAfter(worked);
        }

        public static List<string> Map(IEnumerable<string> entries, string prefix)
        {
            return entries.Select(e => Map(e, prefix)).ToList();
        }
    }
}
