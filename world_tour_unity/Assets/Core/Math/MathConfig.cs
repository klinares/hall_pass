using System;
using System.Collections.Generic;

namespace WorldTour.Core.Maths
{
    /// <summary>
    /// The one switchboard for math, ported from world_tour/math/config.js and stored as
    /// StreamingAssets/math_config.json (written by Tools/config_to_json.mjs). Field names match the
    /// JS exports and the JSON keys exactly, so JsonUtility in the Game layer can fill it.
    /// Core never parses the JSON itself.
    /// </summary>
    /// <remarks>
    /// Defaults matter: JsonUtility leaves a field at its initial value when the key is missing.
    /// A missing skill switch stays true, as the JS treats a missing key as shown
    /// (<c>ENABLED_SKILLS[m.skill] !== false</c>). A missing bound stays -1, which
    /// <see cref="Validate"/> reports.
    /// </remarks>
    [Serializable]
    public class MathConfig
    {
        public SkillSwitches ENABLED_SKILLS = new SkillSwitches();
        public GradeBounds BOUNDS = new GradeBounds();

        /// <summary>Set a skill to false to hide its mode. Unknown skill ids count as shown, as in the JS.</summary>
        public bool IsEnabled(string skill)
        {
            if (ENABLED_SKILLS == null) return true;
            switch (skill)
            {
                case "dec": return ENABLED_SKILLS.dec;
                case "md": return ENABLED_SKILLS.md;
                case "frac": return ENABLED_SKILLS.frac;
                case "xy": return ENABLED_SKILLS.xy;
                case "vol": return ENABLED_SKILLS.vol;
                case "ops": return ENABLED_SKILLS.ops;
                case "flu": return ENABLED_SKILLS.flu;
                default: return true;
            }
        }

        /// <summary>Readable problems with the loaded file, one per entry; empty when it is fine.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (ENABLED_SKILLS == null) errors.Add("math_config.json: ENABLED_SKILLS is missing.");
            if (BOUNDS == null)
            {
                errors.Add("math_config.json: BOUNDS is missing.");
                return errors;
            }
            Positive(errors, "moneyCents", BOUNDS.moneyCents);
            Positive(errors, "maxDenominator", BOUNDS.maxDenominator);
            Positive(errors, "coordMax", BOUNDS.coordMax);
            Positive(errors, "maxVolume", BOUNDS.maxVolume);
            Positive(errors, "storyDollars", BOUNDS.storyDollars);
            return errors;
        }

        private static void Positive(List<string> errors, string name, int value)
        {
            if (value == -1) errors.Add("math_config.json: BOUNDS." + name + " is missing.");
            else if (value <= 0) errors.Add("math_config.json: BOUNDS." + name + " must be a whole number above 0 (it is " + value + ").");
        }

        /// <summary>The values in world_tour/math/config.js, plus storyDollars (Unity only, QUESTIONS 9.4), for tests and as a fallback.</summary>
        public static MathConfig WebDefaults() => new MathConfig
        {
            ENABLED_SKILLS = new SkillSwitches(),
            BOUNDS = new GradeBounds { moneyCents = 10000, maxDenominator = 12, coordMax = 10, maxVolume = 1000, storyDollars = 150 }
        };
    }

    /// <summary>JS <c>ENABLED_SKILLS</c>.</summary>
    [Serializable]
    public class SkillSwitches
    {
        public bool dec = true;   // merch table: decimals and money (5.NBT.7)
        public bool md = true;    // ticket sales: multiply and divide (5.NBT.5-6)
        public bool frac = true;  // beat maker: add and subtract fractions (5.NF.1-2)
        public bool xy = true;    // stage designer: coordinate plane (5.G.1-2)
        public bool vol = true;   // pack the truck: volume (5.MD.3-5)
        public bool ops = true;   // tour budget: order of operations (5.OA.1-2)
        public bool flu = true;   // soundcheck: facts and powers of ten (5.NBT.1-2)
    }

    /// <summary>JS <c>BOUNDS</c>: grade-level bounds the generators and tests hold to. -1 means not set.</summary>
    [Serializable]
    public class GradeBounds
    {
        public int moneyCents = -1;      // money under $100
        public int maxDenominator = -1;
        public int coordMax = -1;
        public int maxVolume = -1;
        public int storyDollars = -1;    // whole dollars in tour budget stories, up to $150 (Unity only; QUESTIONS 9.4)
    }
}
