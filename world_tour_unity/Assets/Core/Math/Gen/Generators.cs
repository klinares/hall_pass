using System;
using System.Collections.Generic;

namespace WorldTour.Core.Maths.Gen
{
    /// <summary>
    /// Skill id to generator, ported from world_tour/math/gen/index.js. The same (skill, level, seed) gives the
    /// same problem as the web game, field for field.
    /// </summary>
    public static class Generators
    {
        /// <summary>Every skill, in the JS GENERATORS order: dec, md, frac, xy, vol, ops, flu.</summary>
        public static readonly IReadOnlyList<string> Skills = new[]
        {
            Decimals.Skill, MultDiv.Skill, Fractions.Skill, Coords.Skill, Volume.Skill, OrderOps.Skill, Fluency.Skill
        };

        /// <summary>Skills with no JS twin (Unity only), kept out of <see cref="Skills"/> so the JS vectors stay one to one: fm, snack table.</summary>
        public static readonly IReadOnlyList<string> UnitySkills = new[] { FracMul.Skill };

        /// <summary>JS: <c>generate(skill, level, seed)</c>. Throws ArgumentException for a skill with no generator.</summary>
        public static Problem Generate(string skill, int level, long seed)
        {
            switch (skill)
            {
                case Decimals.Skill: return Decimals.Generate(level, seed);
                case MultDiv.Skill: return MultDiv.Generate(level, seed);
                case Fractions.Skill: return Fractions.Generate(level, seed);
                case Coords.Skill: return Coords.Generate(level, seed);
                case Volume.Skill: return Volume.Generate(level, seed);
                case OrderOps.Skill: return OrderOps.Generate(level, seed);
                case Fluency.Skill: return Fluency.Generate(level, seed);
                case FracMul.Skill: return FracMul.Generate(level, seed);
                default: throw new ArgumentException("no generator for skill '" + skill + "'", nameof(skill));
            }
        }
    }
}
