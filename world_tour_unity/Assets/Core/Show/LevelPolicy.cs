using System;
using WorldTour.Core.Maths;

namespace WorldTour.Core.Shows
{
    /// <summary>
    /// The numbers one skill's level moves by (phase 7.1): the lowest and highest level, where a new skill starts, and how
    /// many right answers in a row go up and misses in a row go down. Math skills read theirs from math_config.json
    /// (LEVELS, LEVEL_UP_AFTER, LEVEL_DOWN_AFTER); spelling keeps the web game's rules. "Gentle" (the manager's office
    /// switch, per subject) lowers the cap by one level and asks for 8 right in a row to move up.
    /// A saved level above the cap is never rewritten to the cap: <see cref="Effective"/> says which level is played.
    /// </summary>
    public sealed class LevelPolicy
    {
        /// <summary>Right answers in a row to move up in gentle mode.</summary>
        public const int GentleUpAfter = 8;

        public readonly int Floor, Cap, Start, UpAfter, DownAfter;

        public LevelPolicy(int floor, int cap, int start, int upAfter, int downAfter)
        {
            Floor = floor;
            Cap = Math.Max(floor, cap);
            Start = Math.Max(floor, Math.Min(start, Cap));
            UpAfter = Math.Max(1, upAfter);
            DownAfter = Math.Max(1, downAfter);
        }

        /// <summary>The web game's rules (levels 1 to 3, up after 4, down after 2), which spelling keeps and tests use.</summary>
        public static readonly LevelPolicy Legacy = new LevelPolicy(LevelRules.MinLevel, LevelRules.MaxLevel, LevelRules.MinLevel, LevelRules.UpAfter, LevelRules.DownAfter);

        /// <summary>Spelling: levels 1 to 3, up after 4, down after 2; gentle caps at 2 and needs 8.</summary>
        public static LevelPolicy ForSpelling(bool gentle) => gentle ? Legacy.Gentler() : Legacy;

        /// <summary>
        /// A math skill: the config's cap and start for it (a null config means no limits: cap 3, start 0, up after 6,
        /// down after 2), then gentle on top. The floor is the warm-up level 0.
        /// </summary>
        public static LevelPolicy ForMath(MathConfig config, string skill, bool gentle)
        {
            LevelLimit lim = config != null ? config.LimitsFor(skill) : new LevelLimit();
            int cap = Clamp(lim.MAX_LEVEL);
            var p = new LevelPolicy(MathConfig.LowestLevel, cap, Clamp(lim.START_LEVEL),
                config != null ? config.LEVEL_UP_AFTER : MathConfig.DefaultUpAfter,
                config != null ? config.LEVEL_DOWN_AFTER : MathConfig.DefaultDownAfter);
            return gentle ? p.Gentler() : p;
        }

        private static int Clamp(int level) => Math.Max(MathConfig.LowestLevel, Math.Min(MathConfig.HighestLevel, level));

        /// <summary>One level lower at the top, and at least 8 in a row to go up. The floor, start and down rule stay.</summary>
        public LevelPolicy Gentler() => new LevelPolicy(Floor, Cap - 1, Start, Math.Max(UpAfter, GentleUpAfter), DownAfter);

        /// <summary>The level actually played for a saved level: kept between the floor and the cap.</summary>
        public int Effective(int saved) => Math.Max(Floor, Math.Min(saved, Cap));
    }
}
