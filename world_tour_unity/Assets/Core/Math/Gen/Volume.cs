using System.Collections.Generic;
using System.Globalization;

namespace WorldTour.Core.Maths.Gen
{
    /// <summary>
    /// PACK THE TRUCK: volume of rectangular prisms (5.MD.3-5). Ported from world_tour/math/gen/volume.js;
    /// every RNG call happens in the same order, so problems match the JS.
    /// L0 (Unity only, warm-up): count the crates in a single visible layer, 2 to 5 across and 2 to 4 deep.
    /// L1: count unit crates in a small stack, all visible.
    /// L2: l x w x h from labeled sides, or load the truck floor to an exact count.
    /// L3: two boxes stacked (add the volumes), or find a missing side.
    /// </summary>
    public static class Volume
    {
        public const string Skill = "vol";

        private static readonly string[] KindsL2 = { "lwh", "build" };
        private static readonly string[] KindsL3 = { "two", "missing" };

        // JS: f = n => n.toLocaleString('en-US'), for whole numbers
        private static string F(long n) => n.ToString("#,##0", CultureInfo.InvariantCulture);
        private static string S(long n) => n.ToString(CultureInfo.InvariantCulture);
        private static JsonValue Box(int l, int w, int h) => JsonValue.Obj().Set("l", l).Set("w", w).Set("h", h);

        public static Problem Generate(int level, long seed) => Guard.Guarded(Build, level, seed);

        private static Problem Build(int level, long seed, Rng r)
        {
            string kind = level <= 1 ? "count" : level == 2 ? r.Pick(KindsL2) : r.Pick(KindsL3);
            Answer answer;
            string prompt, label;
            List<string> steps;
            var traps = new List<Trap>();
            var boxes = new List<JsonValue>();
            JsonValue parameters = JsonValue.Obj();
            string x = " " + Glyph.Times + " ";

            if (kind == "count")
            {
                // level 0 is one flat layer (h = 1) with rows of at least 2, so nothing is hidden behind anything
                int l = level == 0 ? r.Int(2, 5) : r.Int(2, 4), w = level == 0 ? r.Int(2, 4) : r.Int(1, 3), h = level == 0 ? 1 : r.Int(1, 3);
                if (w * h == 1) h = 2;
                int V = l * w * h;
                boxes.Add(Box(l, w, h));
                answer = Answer.Int(V);
                prompt = "how many crates are in this stack? every crate is 1 cubic unit.";
                label = "count " + S(l) + Glyph.Times + S(w) + Glyph.Times + S(h);
                steps = new List<string>
                {
                    level == 0
                        ? "this is one layer. count the crates in one row, then count how many rows."
                        : "count the crates in the top layer, then count how many layers.",
                    // Unity (QUESTIONS 9.2, an intended difference from the JS): with one layer, "that's l*w crates"
                    // is the answer, so hint 2 describes the layer without the total.
                    h == 1
                        ? "the top layer is " + S(l) + " across and " + S(w) + " deep. how many crates is that? there is 1 layer."
                        : "the top layer is " + S(l) + " by " + S(w) + ": that" + Glyph.Apos + "s " + S(l * w) + " crates. there are " + S(h) + " layers.",
                    level == 0
                        ? S(l) + x + S(w) + " = <b>" + S(V) + "</b> crates, and with 1 layer that is " + S(V) + " cubic units."
                        : S(l * w) + x + S(h) + " = <b>" + S(V) + "</b> cubic units."
                };
                traps.Add(new Trap(MathValue.Num(l + w + h), "added-sides"));
                traps.Add(new Trap(MathValue.Num(level == 0 ? l : l * w), level == 0 ? "one-row" : "one-layer"));
                traps.Add(new Trap(MathValue.Num(l * h + l * w), "visible-faces"));
            }
            else if (kind == "lwh")
            {
                int l, w, h;
                do { l = r.Int(3, 12); w = r.Int(2, 8); h = r.Int(2, 6); } while (l * w * h > 1000);
                int V = l * w * h;
                boxes.Add(Box(l, w, h));
                answer = Answer.Int(V);
                prompt = "the gear case is " + S(l) + " units long, " + S(w) + " wide, and " + S(h) + " tall. what" + Glyph.Apos + "s its volume in cubic units?";
                label = S(l) + x + S(w) + x + S(h);
                steps = new List<string>
                {
                    "volume = length " + Glyph.Times + " width " + Glyph.Times + " height. find the floor first: " + S(l) + x + S(w) + ".",
                    "the floor is " + S(l) + x + S(w) + " = " + S(l * w) + " square units. stack that " + S(h) + " high.",
                    S(l * w) + x + S(h) + " = <b>" + F(V) + "</b> cubic units."
                };
                traps.Add(new Trap(MathValue.Num(l + w + h), "added-sides"));
                traps.Add(new Trap(MathValue.Num(l * w), "one-layer"));
                traps.Add(new Trap(MathValue.Num(2 * (l * w + l * h + w * h)), "surface-area"));
            }
            else if (kind == "build")
            {
                int l = r.Int(2, 4), w = r.Int(2, 3), h = r.Int(2, 4);
                int V = l * w * h;
                boxes.Add(Box(l, w, h));
                answer = Answer.Int(h);
                parameters.Set("floor", JsonValue.Obj().Set("l", l).Set("w", w)).Set("target", V).Set("maxH", 6);
                prompt = "the truck floor fits " + S(l) + " by " + S(w) + " crates. load exactly " + S(V) + " crates, with every stack the same height.";
                label = "load " + S(V) + " crates";
                steps = new List<string>
                {
                    "every layer is one crate on each spot of the floor. how many crates is one layer?",
                    "one layer is " + S(l) + x + S(w) + " = " + S(l * w) + " crates. how many layers of " + S(l * w) + " make " + S(V) + "?",
                    S(l) + x + S(w) + x + S(h) + " = " + S(V) + ": <b>" + S(h) + "</b> crates high on every spot."
                };
            }
            else if (kind == "two")
            {
                int al, aw, ah, bl, bw, bh;
                do
                {
                    al = r.Int(4, 10); aw = r.Int(3, 8); ah = r.Int(1, 4);
                    bl = r.Int(2, al - 1); bw = r.Int(2, aw - 1 != 0 ? aw - 1 : 2); bh = r.Int(1, 4);
                } while (bw > aw || al * aw * ah + bl * bw * bh > 1000);
                int VA = al * aw * ah, VB = bl * bw * bh;
                boxes.Add(Box(al, aw, ah));
                boxes.Add(Box(bl, bw, bh));
                answer = Answer.Int(VA + VB);
                prompt = "the stage is a big block " + S(al) + x + S(aw) + x + S(ah) + ", with a drum riser " + S(bl) + x + S(bw) + x + S(bh) + " on top. what" + Glyph.Apos + "s the total volume?";
                label = "stage + riser";
                steps = new List<string>
                {
                    "find each block" + Glyph.Apos + "s volume on its own, then add them.",
                    "the stage: " + S(al) + x + S(aw) + x + S(ah) + " = " + F(VA) + ". now the riser.",
                    F(VA) + " + " + S(bl) + x + S(bw) + x + S(bh) + " = " + F(VA) + " + " + F(VB) + " = <b>" + F(VA + VB) + "</b> cubic units."
                };
                traps.Add(new Trap(MathValue.Num(VA), "one-box"));
                traps.Add(new Trap(MathValue.Num(VB), "one-box"));
                traps.Add(new Trap(MathValue.Num((al + bl) * (aw + bw) * (ah + bh)), "added-sides"));
            }
            else
            {
                int l, w, h;
                do { l = r.Int(3, 12); w = r.Int(2, 9); h = r.Int(2, 10); } while (l * w * h > 1000);
                int V = l * w * h;
                boxes.Add(Box(l, w, h));
                answer = Answer.Int(h);
                parameters.Set("hideH", true);
                prompt = "a speaker case holds " + F(V) + " cubic units. it" + Glyph.Apos + "s " + S(l) + " long and " + S(w) + " wide. how tall is it?";
                label = "missing height of " + F(V);
                steps = new List<string>
                {
                    "length " + Glyph.Times + " width " + Glyph.Times + " height = " + F(V) + ". you know two of the three.",
                    "the floor is " + S(l) + x + S(w) + " = " + S(l * w) + ". what times " + S(l * w) + " makes " + F(V) + "?",
                    F(V) + " " + Glyph.Divide + " " + S(l * w) + " = <b>" + S(h) + "</b>."
                };
                traps.Add(new Trap(MathValue.Num(V - l * w), "subtracted"));
                traps.Add(new Trap(MathValue.Num((double)V / l), "divided-once"));
            }

            parameters.Set("boxes", JsonValue.Arr(boxes));
            return new Problem
            {
                Skill = Skill, Level = level, Kind = kind, Seed = seed,
                Params = parameters, Prompt = prompt, Label = label, Answer = answer,
                Simplest = false, Choices = null,
                Traps = Mistakes.CleanTraps(answer, traps), Steps = steps,
                Builder = kind == "build"
            };
        }
    }
}
