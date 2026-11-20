using System;
using System.Collections.Generic;
using System.Globalization;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Game.Modes
{
    /// <summary>One drawn note in a typed beat problem's picture.</summary>
    public struct BeatTypedBlock
    {
        /// <summary>Start and length as fractions of the row.</summary>
        public Rational Start, Len;
        /// <summary>The label markup ([[2/8]]).</summary>
        public string Label;
        /// <summary>Faded: the part that gets cut.</summary>
        public bool Gone;
        /// <summary>The shade (3: the first note, 4: the second).</summary>
        public int Shade;
    }

    public sealed class BeatTypedRow
    {
        public readonly List<BeatTypedBlock> Blocks = new List<BeatTypedBlock>();
        /// <summary>Cells numbered 1..Numbers under this row (0: none). Never past the row's last note.</summary>
        public int Numbers;
    }

    /// <summary>
    /// The picture for a typed add / subtract beat problem (beat.js typed()) at ladder stage n. The web drew the two notes
    /// end to end on the grid from the start, so the sum (or the solid remainder) could be counted off before the worked
    /// solution. Here (answer 16; intended difference 33), before the worked step each note gets its own row:
    /// the addends (or the whole and the part to cut, faded) each starting at the row's left edge, labelled with their own
    /// fraction, and from hint 2 with that fraction in the common denominator (the step the hint text names) and their
    /// own cells numbered. Combining them is left to her. The worked step draws the web's picture: the notes end to end
    /// with the grid numbered (subtraction: the remainder, labelled with its own length, then the faded cut).
    /// </summary>
    public static class BeatTypedPicture
    {
        public static Rational A(Problem p) { return p.Params["a"].AsRational(); }
        public static Rational B(Problem p) { return p.Params["b"].AsRational(); }
        public static bool Plus(Problem p) { return p.Params["op"].String == "+"; }
        public static int Grid(Problem p) { return p.Params["grid"].Int; }

        /// <summary>A fraction rewritten in the grid's denominator: [[2/8]].</summary>
        public static string InGrid(Rational f, int grid)
        {
            long n = f.N * grid / f.D;
            return "[[" + n.ToString(CultureInfo.InvariantCulture) + "/" + grid.ToString(CultureInfo.InvariantCulture) + "]]";
        }

        public static string Plain(Rational f) { return "[[" + Rational.Str(f) + "]]"; }

        /// <summary>True when stage n draws the web's end-to-end picture (the worked step).</summary>
        public static bool Joined(int stage) { return stage >= 3; }

        /// <summary>The separate rows for stages 0 to 2. Null at the worked step (use <see cref="JoinedBlocks"/>).</summary>
        public static List<BeatTypedRow> Rows(Problem p, int stage)
        {
            if (Joined(stage)) return null;
            Rational a = A(p), b = B(p);
            bool plus = Plus(p);
            int g = Grid(p);
            var rows = new List<BeatTypedRow>();
            foreach (var x in new[] { new { f = a, gone = false, shade = 3 }, new { f = b, gone = !plus, shade = 4 } })
            {
                var row = new BeatTypedRow();
                row.Blocks.Add(new BeatTypedBlock
                {
                    Start = Rational.R(0, 1), Len = x.f, Gone = x.gone, Shade = x.shade,
                    Label = stage >= 2 ? InGrid(x.f, g) : Plain(x.f),
                });
                row.Numbers = stage >= 2 ? (int)(x.f.N * g / x.f.D) : 0;
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>The worked step's notes, end to end (beat.js typed draw()); the remainder is labelled with its own length.</summary>
        public static List<BeatTypedBlock> JoinedBlocks(Problem p)
        {
            Rational a = A(p), b = B(p);
            bool plus = Plus(p);
            Rational first = plus ? a : Rational.Sub(a, b);
            return new List<BeatTypedBlock>
            {
                new BeatTypedBlock { Start = Rational.R(0, 1), Len = first, Shade = 3, Label = Plain(first) },
                new BeatTypedBlock { Start = first, Len = b, Shade = 4, Gone = !plus, Label = Plain(b) },
            };
        }

        /// <summary>beat.js: rows for the joined picture (the sum can run past one measure).</summary>
        public static int JoinedRows(Problem p)
        {
            return Plus(p) ? Math.Max(1, (int)Math.Ceiling(Rational.Num(A(p)) + Rational.Num(B(p)) - 1e-9)) : 1;
        }
    }
}
