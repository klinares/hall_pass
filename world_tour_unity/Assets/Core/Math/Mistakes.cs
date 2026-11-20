using System;
using System.Collections.Generic;

namespace WorldTour.Core.Maths
{
    /// <summary>JS trap <c>{ value, why }</c>: a wrong answer a kid might type, tagged with the mistake it models.</summary>
    public readonly struct Trap
    {
        public readonly MathValue Value;
        public readonly string Why;

        public Trap(MathValue value, string why)
        {
            Value = value;
            Why = why;
        }

        public override string ToString() => Value + ":" + (Why ?? "null");
    }

    /// <summary>
    /// JS multiple-choice option <c>{ value, correct, why }</c>; Why is null on the correct one.
    /// The value is either a number, fraction or point (<see cref="Value"/>), or text (<see cref="Text"/>) for
    /// choices that are words or expressions, such as the tour budget's "6 x (4 + 9)". A text choice has
    /// Value = MathValue.Null; a value choice has Text = null.
    /// </summary>
    public readonly struct Choice
    {
        public readonly MathValue Value;
        public readonly bool Correct;
        public readonly string Why;
        /// <summary>The choice's text when the JS value is a string; null for a value choice.</summary>
        public readonly string Text;

        public Choice(MathValue value, bool correct, string why)
        {
            Value = value;
            Correct = correct;
            Why = why;
            Text = null;
        }

        /// <summary>A choice whose JS value is a string.</summary>
        public Choice(string text, bool correct, string why)
        {
            Value = MathValue.Null;
            Correct = correct;
            Why = why;
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        public bool IsText => Text != null;

        public override string ToString() => (IsText ? "\"" + Text + "\"" : Value.ToString()) + ":" + (Correct ? "true" : "false") + ":" + (Why ?? "null");
    }

    /// <summary>
    /// Distractors and traps, ported from world_tour/math/mistakes.js. A trap lets the game answer that
    /// mistake directly, and the office can count it.
    /// </summary>
    public static class Mistakes
    {
        /// <summary>
        /// JS <c>cleanTraps(answer, traps)</c>: keep traps that are valid, non-negative, differ from the answer
        /// and from each other, in order. Values are in the answer's own units (cents, integers, {n,d}, {x,y}).
        /// A kept fraction comes back reduced. Drops null values, negatives, non-whole numbers and d &lt;= 0.
        /// </summary>
        public static List<Trap> CleanTraps(Answer answer, IEnumerable<Trap> traps)
        {
            if (answer == null) throw new ArgumentNullException(nameof(answer));
            if (traps == null) throw new ArgumentNullException(nameof(traps));
            bool point = answer.Type == AnswerType.Point;
            var output = new List<Trap>();
            foreach (Trap t in traps)
            {
                MathValue v = t.Value;
                if (v.Kind == ValueKind.Null) continue;
                if (point) { if (v.X < 0 || v.Y < 0) continue; }
                else if (v.IsObject) { if (!MathValue.IsInteger(v.N) || !MathValue.IsInteger(v.D) || v.D <= 0 || v.N < 0) continue; }
                else if (!MathValue.IsInteger(v.Number) || v.Number < 0) continue;
                if (Same(point, v, answer.Value)) continue;
                bool seen = false;
                foreach (Trap o in output) if (Same(point, o.Value, v)) { seen = true; break; }
                if (seen) continue;
                output.Add(new Trap(v.IsObject && !point ? MathValue.Frac(MathValue.ToRational(v.N, v.D)) : v, t.Why));
            }
            return output;
        }

        // JS: point: a.x === b.x && a.y === b.y; object: eq(a, b) (cross-multiplied, in doubles); else a === b.
        private static bool Same(bool point, MathValue a, MathValue b)
        {
            if (point) return a.X == b.X && a.Y == b.Y;
            if (a.IsObject) return a.N * b.D == b.N * a.D;
            return b.Kind == ValueKind.Number && a.Number == b.Number;
        }

        /// <summary>
        /// JS <c>choiceSet(rng, answer, traps, fill, n = 4)</c>: the answer plus up to n - 1 tagged distractors,
        /// shuffled. <paramref name="fill"/> supplies extra plausible values (tagged "near") when the tagged ones run short.
        /// </summary>
        public static List<Choice> ChoiceSet(Rng rng, Answer answer, IEnumerable<Trap> traps, IEnumerable<MathValue> fill, int n = 4)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (fill == null) throw new ArgumentNullException(nameof(fill));
            List<Trap> tagged = Take(CleanTraps(answer, traps), 0, n - 1);
            var pool = new List<Trap>(tagged);
            foreach (MathValue v in fill) pool.Add(new Trap(v, "near"));
            List<Trap> extra = Take(CleanTraps(answer, pool), tagged.Count, int.MaxValue);
            var wrong = new List<Trap>(tagged);
            wrong.AddRange(extra);
            wrong = Take(wrong, 0, n - 1);
            var all = new List<Choice> { new Choice(answer.Value, true, null) };
            foreach (Trap w in wrong) all.Add(new Choice(w.Value, false, w.Why));
            return rng.Shuffle(all);
        }

        // JS Array.prototype.slice(start, end) for start >= 0; a negative end counts from the back.
        private static List<Trap> Take(List<Trap> list, int start, int end)
        {
            if (end < 0) end = Math.Max(list.Count + end, 0);
            end = Math.Min(end, list.Count);
            var r = new List<Trap>();
            for (int i = start; i < end; i++) r.Add(list[i]);
            return r;
        }
    }
}
