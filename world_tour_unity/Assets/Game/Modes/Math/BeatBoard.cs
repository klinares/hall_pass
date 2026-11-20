using System;
using System.Collections.Generic;
using System.Linq;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Game.Modes
{
    /// <summary>A note on the beat track, as beat.js blocks() builds it.</summary>
    public struct BeatBlock
    {
        /// <summary>Length as a fraction of a measure.</summary>
        public Rational Len;
        /// <summary>The note's denominator (2 = half, 12 = twelfth); 0 for a typed problem's drawn blocks.</summary>
        public int D;
        /// <summary>Written by the generator (true) or placed by her (false).</summary>
        public bool Pre;
        /// <summary>Its index in params.pre (Pre) or in the placed list.</summary>
        public int Ref;
        /// <summary>She can tap it to take it back (placed) or out (written, in a take-out problem).</summary>
        public bool Tap;
        /// <summary>Typed problems only: drawn faded, the part that gets cut (.note.gone).</summary>
        public bool Gone;
        /// <summary>The label markup: [[1/4]].</summary>
        public string Label;
    }

    /// <summary>One drawn piece of a block in one row (beat.js layout()): a block that crosses a bar line is two pieces.</summary>
    public struct BeatPiece
    {
        /// <summary>The block's position in the drawn list.</summary>
        public int Block;
        public int Row;
        /// <summary>Start and width as fractions of the row.</summary>
        public double X, W;
        /// <summary>The block's first piece (the label goes on it).</summary>
        public bool First;
        /// <summary>The block is split over two rows (.note.cut: square ends).</summary>
        public bool Cut;
    }

    public enum BeatPlay { Ignored, Filled, TooLong, TooShort, Empty }
    public enum BeatAdd { Ignored, Added, NoRoom }

    /// <summary>
    /// beat.js's builder (render, without the drawing): fill the measure with note blocks, or take written notes out until
    /// it's exactly one whole. Taps and drops add a block at the end; tapping a placed block takes it back (or, in a
    /// take-out problem, takes a written one out); undo takes back the last; "play it" wins when the notes add to the
    /// target exactly (fractions.js filled), and a measure that is short or spills over is a miss tagged too-short or
    /// too-long. The worked step (3) fills it the right way: decompose(target - written) or the fewest written notes that
    /// add up to the extra (cheapestCut).
    /// </summary>
    public sealed class BeatBoard
    {
        public readonly Problem Problem;
        public readonly Rational Target;
        public readonly int Grid, Bars;
        public readonly bool Removable;
        public readonly List<int> Pre, Palette;

        private readonly List<int> _placed = new List<int>();
        private List<int> _removed = new List<int>();

        public IReadOnlyList<int> Placed { get { return _placed; } }
        public IReadOnlyList<int> Removed { get { return _removed; } }
        /// <summary>No more changes (played, worked solution shown, or over).</summary>
        public bool Done { get; set; }
        /// <summary>The highest ladder step shown (2 and up draws the grid numbers).</summary>
        public int Stage { get; private set; }

        public BeatBoard(Problem p)
        {
            Problem = p ?? throw new ArgumentNullException(nameof(p));
            JsonValue pr = p.Params;
            Target = pr["target"].AsRational();
            Grid = pr["grid"].Int;
            Bars = pr["bars"].Int;
            Removable = pr["removable"].BoolValue;
            Pre = pr["pre"].AsLongs().Select(x => (int)x).ToList();
            Palette = pr["palette"].AsLongs().Select(x => (int)x).ToList();
        }

        private static Rational Unit(long d) { return Rational.R(1, d); }

        /// <summary>beat.js:83-92 blocks(): the written notes still in, then the placed ones, end to end.</summary>
        public List<BeatBlock> Blocks()
        {
            var list = new List<BeatBlock>();
            for (int i = 0; i < Pre.Count; i++)
            {
                if (_removed.Contains(i)) continue;
                list.Add(new BeatBlock { Len = Unit(Pre[i]), D = Pre[i], Pre = true, Ref = i, Tap = !Done && Removable, Label = "[[1/" + Pre[i] + "]]" });
            }
            for (int i = 0; i < _placed.Count; i++)
                list.Add(new BeatBlock { Len = Unit(_placed[i]), D = _placed[i], Pre = false, Ref = i, Tap = !Done, Label = "[[1/" + _placed[i] + "]]" });
            return list;
        }

        public Rational Total() { return Rational.Sum(Blocks().Select(b => b.Len)); }

        /// <summary>beat.js:96 the rows drawn: the bars, plus one more while the notes spill past them.</summary>
        public int Rows()
        {
            return Math.Max(Bars, Math.Min(Bars + 1, (int)Math.Ceiling(Rational.Num(Total()) - 1e-9)));
        }

        /// <summary>beat.js:98 .track.over: the notes run past the target.</summary>
        public bool Over() { return Rational.Num(Total()) > Rational.Num(Target) + 1e-9; }

        /// <summary>beat.js:116-121 add(d): one block on the end, unless the page (bars + 1 measures) is full.</summary>
        public BeatAdd Add(int d)
        {
            if (Done || Removable) return BeatAdd.Ignored;
            if (Rational.Num(Rational.Sum(new[] { Total(), Unit(d) })) > Bars + 1 + 1e-9) return BeatAdd.NoRoom;
            _placed.Add(d);
            return BeatAdd.Added;
        }

        /// <summary>beat.js:99-106 a tap on the drawn block at <paramref name="index"/>: take it back, or take it out.</summary>
        public bool Tap(int index)
        {
            if (Done) return false;
            List<BeatBlock> bl = Blocks();
            if (index < 0 || index >= bl.Count || !bl[index].Tap) return false;
            BeatBlock b = bl[index];
            if (!b.Pre) _placed.RemoveAt(b.Ref);
            else _removed.Add(b.Ref);
            return true;
        }

        /// <summary>beat.js:126-131 undo / put back.</summary>
        public bool Undo()
        {
            if (Done) return false;
            if (Removable) { if (_removed.Count > 0) _removed.RemoveAt(_removed.Count - 1); }
            else if (_placed.Count > 0) _placed.RemoveAt(_placed.Count - 1);
            return true;
        }

        /// <summary>fractions.js filled(): the notes add to the target exactly.</summary>
        public bool Filled() { return Fractions.Filled(Problem.Params, _placed, _removed); }

        /// <summary>
        /// beat.js:152-174 "play it": filled wins; otherwise a miss, too long or too short. QUESTIONS 15 (an intended
        /// difference): in a fill problem, playing before she has added any note is not an attempt (Empty): no miss, no
        /// try, no fans lost, the ladder stays put. The web counted it as a too-short miss.
        /// </summary>
        public BeatPlay Play()
        {
            if (Done) return BeatPlay.Ignored;
            if (!Removable && _placed.Count == 0 && !Filled()) return BeatPlay.Empty;
            if (Filled()) { Done = true; return BeatPlay.Filled; }
            return Rational.Num(Total()) > Rational.Num(Target) ? BeatPlay.TooLong : BeatPlay.TooShort;
        }

        /// <summary>beat.js:135-147 the ladder's step n: the grid numbers from 2, the worked fill at 3.</summary>
        public void Step(int n)
        {
            Stage = Math.Max(Stage, n);
            if (n >= 3)
            {
                if (Removable) _removed = CheapestCut(Pre, Problem.Answer.Value);
                else
                {
                    _placed.Clear();
                    _placed.AddRange(Fractions.Decompose(Rational.Sub(Target, Rational.Sum(Pre.Select(d => Unit(d)))), Palette));
                }
                Done = true;
            }
        }

        /// <summary>beat.js:181-191 cheapestCut: the fewest written notes that add up to exactly the extra.</summary>
        public static List<int> CheapestCut(IReadOnlyList<int> pre, MathValue over)
        {
            int n = pre.Count;
            double target = over.N / over.D;
            int bestMask = -1, bestK = 0;
            for (int mask = 1; mask < 1 << n; mask++)
            {
                double s = 0; int k = 0;
                for (int i = 0; i < n; i++) if ((mask & (1 << i)) != 0) { s += 1.0 / pre[i]; k++; }
                if (Math.Abs(s - target) < 1e-9 && (bestMask < 0 || k < bestK)) { bestMask = mask; bestK = k; }
            }
            var list = new List<int>();
            if (bestMask < 0) return list;
            for (int i = 0; i < n; i++) if ((bestMask & (1 << i)) != 0) list.Add(i);
            return list;
        }

        /// <summary>
        /// beat.js:18-34 layout(blocks, rows): lay blocks end to end, one row per whole; a block crossing a bar line is
        /// drawn in two pieces. Pieces past the last row are dropped.
        /// </summary>
        public static List<BeatPiece> Layout(IReadOnlyList<Rational> lens, int rows)
        {
            var output = new List<BeatPiece>();
            double at = 0;
            for (int i = 0; i < lens.Count; i++)
            {
                double len = Rational.Num(lens[i]);
                double s = at, left = len;
                bool first = true;
                while (left > 1e-9)
                {
                    int row = (int)Math.Floor(s + 1e-9);
                    if (row >= rows) break;
                    double room = row + 1 - s, piece = Math.Min(room, left);
                    output.Add(new BeatPiece { Block = i, Row = row, X = s - row, W = piece, First = first, Cut = piece < len - 1e-9 });
                    s += piece; left -= piece; first = false;
                }
                at += len;
            }
            return output;
        }

        /// <summary>beat.js:37-40 where the bar line sits in row r: the target's end, as a fraction of the row (null = none).</summary>
        public static double? BarEnd(Rational? target, int row)
        {
            if (!target.HasValue) return null;
            return Math.Max(0, Math.Min(1, Rational.Num(target.Value) - row));
        }

        /// <summary>The notes for the beat sound (snd.beat): length and denominator, in order.</summary>
        public List<KeyValuePair<double, int>> Notes()
        {
            return Blocks().Select(b => new KeyValuePair<double, int>(Rational.Num(b.Len), b.D)).ToList();
        }

        /// <summary>For tests and the playthrough: the placed notes that would fill it (decompose of target - written).</summary>
        public List<int> FillSolution()
        {
            return Fractions.Decompose(Rational.Sub(Target, Rational.Sum(Pre.Select(d => Unit(d)))), Palette);
        }
    }
}
