using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Game.Modes
{
    /// <summary>tickets.js's copy, word for word.</summary>
    public static class TicketsLines
    {
        private static readonly string Apos = ((char)0x2019).ToString();
        public static readonly string Minus = ((char)0x2212).ToString();
        public static readonly string Times = ((char)0x00D7).ToString();
        public static readonly string Dot = ((char)0x00B7).ToString();
        public static readonly string Ellipsis = ((char)0x2026).ToString();

        /// <summary>tickets.js:57 the desk tag.</summary>
        public const string DeskTag = "box office";
        /// <summary>tickets.js:63 the keypad's check key.</summary>
        public const string SellIt = "sell it";
        /// <summary>tickets.js:62 the prompt card's label.</summary>
        public static string PromptLabel(string kind) { return kind == "rows" ? "seating chart" : "box office"; }
        /// <summary>tickets.js:73 the dog's first line.</summary>
        public static string Intro(string kind)
        {
            return kind == "rows" ? "careful: only full rows count." : "work it out on paper if you want. i" + Apos + "ll wait.";
        }
        /// <summary>tickets.js:31 / 37 the seat rows' line.</summary>
        public static string RowsLine(long q, long d, long rem, bool worked)
        {
            return worked
                ? "<b>" + TicketsWork.F(q) + "</b> full rows of " + d + ", and " + rem + " fans in a row that isn" + Apos + "t full."
                : d + " seats per row. does the last row fill up?";
        }

        /// <summary>tickets.js:52 the letterboard: "1,234", "x", "56".</summary>
        public static string[] Board(Problem p)
        {
            JsonValue pr = p.Params;
            string op = pr["op"].String;
            return op == Times ? new[] { TicketsWork.F(pr["a"].Long), op, TicketsWork.F(pr["b"].Long) }
                               : new[] { TicketsWork.F(pr["N"].Long), op, TicketsWork.F(pr["d"].Long) };
        }
    }

    /// <summary>A row of the chunking sheet (tickets.js chunks(): .crow, .crow.left, .crow.top, .crow.total).</summary>
    public struct ChunkRow
    {
        public string Text, Note;
        /// <summary>"top", "take", "left" or "total".</summary>
        public string Style;
    }

    /// <summary>
    /// The worked paper under the box office board (tickets.js area(), chunks(), rows()), for ladder step n (2 or 3): the
    /// area model for x, chunking for /, full rows and leftovers for the remainder problems.
    /// Unity (QUESTIONS 8's rule on the pictures; intended differences 16 and 17): at step 2 the picture never shows the
    /// answer. An area model with one part (a round factor, 70) shows "other x 7 x 10" in its cell instead of the product,
    /// which is the answer; the seat rows show one full row, a gap and the part row instead of every full row (which
    /// counts out the answer).
    /// </summary>
    public sealed class TicketsWork
    {
        public static string F(long n) { return n.ToString("#,##0", CultureInfo.InvariantCulture); }

        public string Kind;
        // area
        public List<string> ColHeads = new List<string>(), RowHeads = new List<string>();
        public List<List<string>> Cells = new List<List<string>>();
        /// <summary>The sum under the area model (worked step only), or null.</summary>
        public string Sum;
        // chunks
        public List<ChunkRow> Rows = new List<ChunkRow>();
        // seat rows
        public int Full;
        /// <summary>Unity: a gap between the full row and the part row (step 2), drawn as an ellipsis.</summary>
        public bool Gap;
        /// <summary>"... 23 more full rows", or null.</summary>
        public string More;
        /// <summary>How much of the last row is full (rem / d).</summary>
        public double Part;
        public string Line;

        /// <summary>tickets.js onStep: the paper for step n, or null below step 2 or when the problem has no work.</summary>
        public static TicketsWork Draw(Problem p, int n)
        {
            if (n < 2 || p.Work == null || p.Work.IsNull) return null;
            JsonValue w = p.Work;
            string kind = w["kind"].String;
            var t = new TicketsWork { Kind = kind };
            if (kind == "area")
            {
                List<long> rows = w["rowParts"].Items.Select(x => x.Long).ToList(), cols = w["colParts"].Items.Select(x => x.Long).ToList();
                t.ColHeads = cols.Select(F).ToList();
                t.RowHeads = rows.Select(F).ToList();
                bool one = rows.Count == 1 && cols.Count == 1;
                foreach (long r in rows)
                    t.Cells.Add(cols.Select(c => n >= 3 || !one ? F(r * c)
                        : c % 10 == 0 ? F(r * (c / 10)) + " " + TicketsLines.Times + " 10" : "?").ToList());
                if (n >= 3)
                    t.Sum = string.Join(" + ", rows.SelectMany(r => cols.Select(c => F(r * c)))) + " = <b>" + F(rows.Sum(r => cols.Sum(c => r * c))) + "</b>";
                return t;
            }
            if (kind == "chunks")
            {
                long d = w["d"].Long;
                var all = w["chunks"].Items.ToList();
                List<JsonValue> shown = n >= 3 ? all : w.Has("lower") ? new List<JsonValue> { w["lower"] } : all.Take(1).ToList();
                t.Rows.Add(new ChunkRow { Text = F(w["N"].Long), Note = "start", Style = "top" });
                long run = 0;
                foreach (JsonValue c in shown)
                {
                    run += c["c"].Long;
                    t.Rows.Add(new ChunkRow { Text = TicketsLines.Minus + " " + F(c["take"].Long), Note = d + " " + TicketsLines.Times + " " + F(c["c"].Long), Style = "take" });
                    t.Rows.Add(new ChunkRow { Text = F(c["left"].Long), Note = "left " + TicketsLines.Dot + " total " + F(run), Style = "left" });
                }
                if (n >= 3)
                    t.Rows.Add(new ChunkRow { Text = string.Join(" + ", all.Select(c => F(c["c"].Long))) + " = " + F(all.Sum(c => c["c"].Long)), Style = "total" });
                return t;
            }
            long q = w["q"].Long, dd = w["d"].Long, rem = w["rem"].Long;
            if (n >= 3)
            {
                t.Full = (int)Math.Min(q, 12);
                if (q > 12) t.More = TicketsLines.Ellipsis + " " + F(q - 12) + " more full rows";
            }
            else { t.Full = 1; t.Gap = true; }
            t.Part = (double)rem / dd;
            t.Line = TicketsLines.RowsLine(q, dd, rem, n >= 3);
            return t;
        }

        /// <summary>Every number-bearing text on the paper, for the claim tests and the log.</summary>
        public IEnumerable<string> Texts()
        {
            foreach (var r in Cells) foreach (string c in r) yield return c;
            if (Sum != null) yield return Sum;
            foreach (ChunkRow r in Rows) { yield return r.Text; if (r.Note != null) yield return r.Note; }
            if (More != null) yield return More;
            if (Line != null) yield return Line;
        }
    }
}
