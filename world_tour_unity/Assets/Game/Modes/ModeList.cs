using System.Collections.Generic;
using System.Linq;
using WorldTour.Core.Maths;
using WorldTour.Core.Meta;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// Every playable mode, in setlist order, ported from world_tour/modes/index.js MODES (ids, names, kinds and math
    /// skills from each mode file). The journal's milestones read this list ("every mode", masters), so it must be the
    /// whole list even while only some modes are built. Math modes switched off in math_config.json are left out, as
    /// index.js does.
    /// </summary>
    public static class ModeList
    {
        private static readonly ModeInfo[] Spelling =
        {
            new ModeInfo("slots", "missing letters", ModeInfo.Spelling),
            new ModeInfo("vowels", "missing vowels", ModeInfo.Spelling),
            new ModeInfo("remix", "remix", ModeInfo.Spelling),
            new ModeInfo("fanmail", "fan mail", ModeInfo.Spelling),
            new ModeInfo("studio", "studio session", ModeInfo.Spelling),
            new ModeInfo("lyric", "lyric writer", ModeInfo.Spelling),
            new ModeInfo("prefix", "prefix mixer", ModeInfo.Spelling),
        };

        private static readonly ModeInfo[] MathModes =
        {
            new ModeInfo("merch", "merch table", ModeInfo.Math, "dec"),
            new ModeInfo("beat", "beat maker", ModeInfo.Math, "frac"),
            new ModeInfo("tickets", "ticket sales", ModeInfo.Math, "md"),
            new ModeInfo("stage", "stage designer", ModeInfo.Math, "xy"),
            new ModeInfo("truck", "pack the truck", ModeInfo.Math, "vol"),
            new ModeInfo("budget", "tour budget", ModeInfo.Math, "ops"),
            new ModeInfo("soundcheck", "soundcheck", ModeInfo.Math, "flu"),
        };

        /// <summary>index.js MODES: the spelling modes, then the math modes whose skill is switched on.</summary>
        public static List<ModeInfo> All(MathConfig config)
        {
            var list = new List<ModeInfo>(Spelling);
            list.AddRange(MathModes.Where(m => config == null || config.IsEnabled(m.Skill)));
            return list;
        }

        /// <summary>index.js modeById, or null.</summary>
        public static ModeInfo ById(string id, MathConfig config)
        {
            return All(config).FirstOrDefault(m => m.Id == id);
        }
    }
}
