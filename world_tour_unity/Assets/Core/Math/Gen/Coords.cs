using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WorldTour.Core.Maths.Gen
{
    /// <summary>
    /// STAGE DESIGNER: the first quadrant of the coordinate plane (5.G.1-2). Ported from
    /// world_tour/math/gen/coords.js; every RNG call happens in the same order, so problems match the JS.
    /// L0 (Unity only, warm-up): place or read a light, axes to 4.
    /// L1: place a light at a point, or read where one is, axes to 6.
    /// L2: the same with axes to 10.
    /// L3: "move it 3 right and 2 up": tap where it ends up, axes to 10.
    /// </summary>
    public static class Coords
    {
        public const string Skill = "xy";

        /// <summary>JS GEAR: [name, icon].</summary>
        private static readonly string[][] Gear =
        {
            new[] { "spotlight", "lights" }, new[] { "mic stand", "mic" }, new[] { "amp", "amp" },
            new[] { "speaker", "crate" }, new[] { "drum riser", "star" }
        };

        private static readonly string[] KindsL3 = { "move", "move", "read" };
        private static readonly string[] KindsL12 = { "place", "read" };

        private static string S(long v) => v.ToString(CultureInfo.InvariantCulture);
        private static string Pt(int x, int y) => "(" + S(x) + ", " + S(y) + ")";
        private static JsonValue PtJson(int x, int y) => JsonValue.Obj().Set("x", x).Set("y", y);

        public static Problem Generate(int level, long seed) => Guard.Guarded(Build, level, seed);

        private static Problem Build(int level, long seed, Rng r)
        {
            int max = level == 0 ? 4 : level == 1 ? 6 : 10;
            string kind = level == 3 ? r.Pick(KindsL3) : r.Pick(KindsL12);
            string[] gi = r.Pick(Gear);
            string gear = gi[0], icon = gi[1];
            int px, py, sx = 0, sy = 0, dx = 0, dy = 0;
            bool hasStart = false;

            if (kind == "move")
            {
                for (;;)
                {
                    sx = r.Int(0, max);
                    sy = r.Int(0, max);
                    hasStart = true;
                    dx = r.Int(-4, 4);
                    dy = r.Int(-4, 4);
                    if (dx == 0 || dy == 0) continue;
                    px = sx + dx;
                    py = sy + dy;
                    if (px < 0 || py < 0 || px > max || py > max || px == py) continue;
                    break;
                }
            }
            else
            {
                // JS rand(): a point off the diagonal (which also rules out the origin)
                do { px = r.Int(0, max); py = r.Int(0, max); } while (px == py || (px == 0 && py == 0));
            }

            Answer answer = Answer.Point(px, py);
            Func<int, string, string, string> words = (n, pos, neg) => S(Math.Abs(n)) + " " + (n > 0 ? pos : neg);
            string prompt, label;
            List<string> steps;
            string p = Pt(px, py);
            if (kind == "place")
            {
                prompt = "put the " + gear + " at " + p + ". tap that spot on the stage.";
                label = "place " + p;
                steps = new List<string>
                {
                    "the first number goes across, the second goes up. start at (0, 0).",
                    "go across to <b>" + S(px) + "</b> on the bottom line. now go up from there.",
                    "across " + S(px) + ", up " + S(py) + ". that" + Glyph.Apos + "s <b>" + p + "</b>."
                };
            }
            else if (kind == "read")
            {
                prompt = "where is the " + gear + "? set the two numbers.";
                label = "read " + p;
                steps = new List<string>
                {
                    "the first number is how far across, the second is how far up.",
                    "follow the " + gear + " straight down to the bottom line: it" + Glyph.Apos + "s at <b>" + S(px) + "</b> across.",
                    "across " + S(px) + ", up " + S(py) + ": <b>" + p + "</b>."
                };
            }
            else
            {
                string start = Pt(sx, sy);
                prompt = "the " + gear + " is at " + start + ". move it " + words(dx, "right", "left") + " and " + words(dy, "up", "down") + ". tap where it ends up.";
                label = start + " moved";
                steps = new List<string>
                {
                    "right and left change the first number. up and down change the second.",
                    words(dx, "right", "left") + ": " + S(sx) + " " + (dx > 0 ? "+" : Glyph.Minus) + " " + S(Math.Abs(dx)) + " = <b>" + S(px) + "</b>. now the up-and-down part.",
                    S(sy) + " " + (dy > 0 ? "+" : Glyph.Minus) + " " + S(Math.Abs(dy)) + " = " + S(py) + ". it lands on <b>" + p + "</b>."
                };
            }

            var traps = new List<Trap> { new Trap(MathValue.Pt(py, px), "swapped") };
            if (kind == "move")
            {
                traps.Add(new Trap(MathValue.Pt(sx + dy, sy + dx), "moves-swapped"));
                traps.Add(new Trap(MathValue.Pt(sx - dx, sy - dy), "wrong-direction"));
            }

            return new Problem
            {
                Skill = Skill, Level = level, Kind = kind, Seed = seed,
                Params = JsonValue.Obj()
                    .Set("max", max).Set("gear", gear).Set("icon", icon)
                    .Set("start", hasStart ? PtJson(sx, sy) : JsonValue.Null)
                    .Set("dx", dx).Set("dy", dy)
                    .Set("point", kind == "read" ? PtJson(px, py) : JsonValue.Null),
                Prompt = prompt, Label = label, Answer = answer,
                Simplest = false, Choices = null,
                Traps = Mistakes.CleanTraps(answer, traps.Where(t => t.Value.X <= max && t.Value.Y <= max)),
                Steps = steps
            };
        }
    }
}
