using System;
using System.Globalization;

namespace WorldTour.Core.Maths.Gen
{
    /// <summary>
    /// What the snack table's picture shows at a ladder stage, as plain data the Game layer draws. An area diagram
    /// (rows x columns of one whole) for multiplication and a tape diagram for division.
    /// Stage 0 is the problem as posed, 1 and 2 are the hints, 3 the worked solution. At stages 0 to 2 the picture never
    /// shades the product (<see cref="Overlap"/> is false, except the shaded-region problem, whose whole question it is)
    /// and never cuts the whole into every little piece, so the product can't be counted off it (the pieces are the
    /// player's to work out): the area diagram cuts only the part that is taken, the tape cuts one whole or one part.
    /// </summary>
    public sealed class FmPicture
    {
        /// <summary>"none" (only the expression is drawn), "area" or "tape".</summary>
        public string Kind = "none";

        // ----- area: the whole is a Rows x Cols grid; columns come from the part, rows from the fraction taken of it -----
        public int Cols = 1, Rows = 1;
        /// <summary>Columns shaded light (the part the problem starts from).</summary>
        public int ColsOn;
        /// <summary>Rows cut inside the shaded columns only (hint 2): the part that is taken is cut, the rest of the tray is not.</summary>
        public bool CutShadedOnly;
        /// <summary>Every row line drawn across the whole tray.</summary>
        public bool CutAll;
        /// <summary>The product, shaded dark: RowsOn rows x OverlapCols columns, from the top left.</summary>
        public bool Overlap;
        public int RowsOn, OverlapCols;
        /// <summary>Counts written beside the grid: how many rows and columns the tray is cut into ("" for none).</summary>
        public string ColsLabel = "", RowsLabel = "";

        // ----- tape: Wholes bars, each cut into Parts equal pieces when cut -----
        public int Wholes = 1, Parts = 1;
        /// <summary>How many of the bars are drawn cut into pieces (the rest are plain).</summary>
        public int WholesCut;
        /// <summary>The first piece of the first bar is lightly shaded (the unit fraction in question).</summary>
        public bool MarkFirst;
        /// <summary>The first piece is cut into this many shares (0 = not cut); with <see cref="SplitAll"/> every piece is.</summary>
        public int Split;
        public bool SplitAll;
        /// <summary>One piece (or share) shaded dark: the worked answer.</summary>
        public bool Shade;
        /// <summary>The pieces of every cut bar are numbered 1, 2, 3...</summary>
        public bool Numbered;
        /// <summary>The label over the bar (the whole's size), or "".</summary>
        public string Total = "";

        /// <summary>A line under the picture, with [[n/d]] markup, or "".</summary>
        public string Caption = "";
    }

    public static class FracMulPicture
    {
        static string S(long v) { return v.ToString(CultureInfo.InvariantCulture); }

        public static FmPicture For(Problem p, int stage)
        {
            stage = Math.Max(0, Math.Min(3, stage));
            JsonValue q = p.Params;
            string answer = "[[" + Rational.Str(Answers.ValueOf(p.Answer)) + "]]";
            switch (p.Kind)
            {
                case "unitof":
                {
                    long whole = q["whole"].Long, d = q["parts"].Long;
                    var t = new FmPicture { Kind = "tape", Wholes = 1, Parts = (int)d, Total = S(whole) };
                    t.WholesCut = stage >= 1 ? 1 : 0;
                    // hint 2 cuts the bar only: the first piece IS the answer, so it is not marked until the worked step
                    if (stage >= 3) { t.Shade = true; t.Caption = S(whole) + " " + Glyph.Divide + " " + S(d) + " = " + answer; }
                    return t;
                }
                case "share":
                {
                    int d = q["parts"].Int, n = q["split"].Int;
                    var t = new FmPicture { Kind = "tape", Wholes = 1, Parts = d, WholesCut = 1, MarkFirst = true };
                    if (stage >= 2) t.Split = n;
                    if (stage >= 3) { t.SplitAll = true; t.Shade = true; t.Caption = "one share is " + answer + " of the tray"; }
                    return t;
                }
                case "howmany":
                {
                    int w = q["wholes"].Int, d = q["parts"].Int;
                    var t = new FmPicture { Kind = "tape", Wholes = w, Parts = d };
                    if (stage >= 2) { t.WholesCut = 1; t.MarkFirst = true; }
                    if (stage >= 3) { t.WholesCut = w; t.Numbered = true; t.Caption = answer + " servings"; }
                    return t;
                }
                case "part":
                case "unitnon":
                case "nonnon":
                {
                    int cols = q["cols"].Int, colsOn = q["colsOn"].Int, rows = q["rows"].Int, rowsOn = q["rowsOn"].Int;
                    var a = new FmPicture { Kind = "area", Cols = 1, Rows = 1 };
                    if (stage >= 1) { a.Cols = cols; a.ColsOn = colsOn; }
                    if (stage >= 2) { a.Rows = rows; a.CutShadedOnly = true; a.RowsLabel = S(rows) + " rows"; }
                    if (stage >= 3) { a.CutAll = true; a.CutShadedOnly = false; a.Overlap = true; a.RowsOn = rowsOn; a.OverlapCols = colsOn; a.ColsLabel = S(cols) + " columns"; a.Caption = answer + " of the tray"; }
                    return a;
                }
                case "shaded":
                {
                    int cols = q["cols"].Int, colsOn = q["colsOn"].Int, rows = q["rows"].Int, rowsOn = q["rowsOn"].Int;
                    var a = new FmPicture { Kind = "area", Cols = cols, Rows = rows, CutAll = true, Overlap = true, RowsOn = rowsOn, OverlapCols = colsOn };
                    if (stage >= 2) { a.RowsLabel = S(rows) + " rows"; a.ColsLabel = S(cols) + " columns"; }
                    if (stage >= 3) { a.RowsLabel = S(rowsOn) + " of " + S(rows) + " rows"; a.ColsLabel = S(colsOn) + " of " + S(cols) + " columns"; }
                    return a;
                }
                default:
                    return new FmPicture();
            }
        }
    }
}
