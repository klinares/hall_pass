using System.Collections.Generic;

namespace WorldTour.Core.Maths.Gen
{
    /// <summary>
    /// One generated problem: the object every generator in world_tour/math/gen/ returns, shared by all the
    /// generators, the show engine and the review queue (which stores the whole problem, not a seed).
    /// Fields match the JS keys one to one. The shared fields are typed; <see cref="Params"/> and
    /// <see cref="Work"/> differ by skill, so they are JSON trees built in the same shape as the JS.
    /// </summary>
    public sealed class Problem
    {
        /// <summary>JS <c>skill</c>: "dec", "md", "frac", "xy", "vol", "ops", "flu".</summary>
        public string Skill;
        public int Level;
        /// <summary>JS <c>kind</c>, the problem's variety within the skill ("change", "mul", "takeout", ...).</summary>
        public string Kind;
        /// <summary>The seed passed in, unchanged (the RNG uses it modulo 2^32, as the JS does).</summary>
        public long Seed;
        /// <summary>JS <c>params</c>: what the mode's screen draws. Always an object.</summary>
        public JsonValue Params;
        /// <summary>The question, lowercase, with <c>&lt;b&gt;</c> tags and <c>[[n/d]]</c> fraction markup as in the JS.</summary>
        public string Prompt;
        /// <summary>The short label on the card.</summary>
        public string Label;
        public Answer Answer;
        /// <summary>JS <c>accept.simplest</c>: only the simplest form counts (and the prompt says so).</summary>
        public bool Simplest;
        /// <summary>JS <c>choices</c>: multiple-choice options, or null for typed and builder problems.</summary>
        public List<Choice> Choices;
        /// <summary>JS <c>traps</c>: tagged wrong answers, already through cleanTraps.</summary>
        public List<Trap> Traps = new List<Trap>();
        /// <summary>The hint ladder: nudge, partial step, worked solution. Always three.</summary>
        public List<string> Steps = new List<string>();
        /// <summary>
        /// JS <c>work</c>: the worked hint's drawing (column sum, area model, chunks). A C# null means the JS
        /// object has no such key (fractions); <see cref="JsonValue.Null"/> means the key is there and null.
        /// </summary>
        public JsonValue Work;
        /// <summary>JS <c>builder</c>: true when the mode is won by building (beat maker, pack the truck). Null when the JS has no such key.</summary>
        public bool? Builder;

        /// <summary>A value in this problem's answer units as the JS writes it: a number, <c>{ n, d }</c>, <c>{ x, y }</c> or null.</summary>
        public static JsonValue ValueJson(MathValue v)
        {
            switch (v.Kind)
            {
                case ValueKind.Number: return JsonValue.Num(v.Number);
                case ValueKind.Fraction: return JsonValue.Obj().Set("n", JsonValue.Num(v.N)).Set("d", JsonValue.Num(v.D));
                case ValueKind.Point: return JsonValue.Obj().Set("x", JsonValue.Num(v.X)).Set("y", JsonValue.Num(v.Y));
                default: return JsonValue.Null;
            }
        }

        /// <summary>The whole problem as the JSON object the JS generator returns.</summary>
        public JsonValue ToJson()
        {
            var o = JsonValue.Obj()
                .Set("skill", Skill)
                .Set("level", Level)
                .Set("kind", Kind)
                .Set("seed", Seed)
                .Set("params", Params ?? JsonValue.Null)
                .Set("prompt", Prompt)
                .Set("label", Label)
                .Set("answer", Answer == null ? JsonValue.Null
                    : JsonValue.Obj().Set("type", Answer.TypeName(Answer.Type)).Set("value", ValueJson(Answer.Value)))
                .Set("accept", JsonValue.Obj().Set("simplest", Simplest));
            if (Choices == null) o.Set("choices", JsonValue.Null);
            else
            {
                var cs = new List<JsonValue>();
                foreach (Choice c in Choices)
                    cs.Add(JsonValue.Obj().Set("value", c.IsText ? JsonValue.Str(c.Text) : ValueJson(c.Value)).Set("correct", c.Correct).Set("why", c.Why));
                o.Set("choices", JsonValue.Arr(cs));
            }
            var ts = new List<JsonValue>();
            foreach (Trap t in Traps) ts.Add(JsonValue.Obj().Set("value", ValueJson(t.Value)).Set("why", t.Why));
            o.Set("traps", JsonValue.Arr(ts));
            var ss = new List<JsonValue>();
            foreach (string s in Steps) ss.Add(JsonValue.Str(s));
            o.Set("steps", JsonValue.Arr(ss));
            if (Work != null) o.Set("work", Work);
            if (Builder.HasValue) o.Set("builder", Builder.Value);
            return o;
        }

        /// <summary>Canonical JSON (see <see cref="JsonValue.ToCanonicalJson"/>); equal to Tools/gen_vectors.mjs output for the same problem.</summary>
        public string ToCanonicalJson() => ToJson().ToCanonicalJson();
    }
}
