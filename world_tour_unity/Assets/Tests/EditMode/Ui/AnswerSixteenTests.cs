using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;
using WorldTour.Game.Modes;

namespace WorldTour.Game.EditTests
{
    /// <summary>
    /// QUESTIONS 16 / answer 16: at hints 1 and 2 no picture shows the answer or lets her count it out. Phase 4 fix round 1
    /// (tester's B1, B2/P8, B3): each test sweeps the 3,000-seed set at every level and checks, from the geometry the
    /// picture is drawn from, that the hint-2 picture neither marks the answer nor lets it be counted, and that the worked
    /// step still draws it.
    /// </summary>
    public class AnswerSixteenTests
    {
        private static IEnumerable<long> Seeds(int n) { for (int i = 0; i < n; i++) yield return 1 + i * 7919L; }

        private sealed class NullView : IStageView, IBuilderHost
        {
            public void Paint() { }
            public void Tapped() { }
            public void Popped() { }
            public void Wobbled() { }
            public void Say(string line, string pose) { }
            public void Mistake(string tag) { }
            public void LockHints() { }
            public void Next(float seconds) { }
            public void ShowNext() { }
        }

        // ---------- B1: pack the truck, the missing height ----------

        /// <summary>Is an isometric point the corner (x, y, h) of the box's top face, with whole x in 0..l and y in 0..w?</summary>
        private static bool OnTop(IsoPt p, int l, int w, int h)
        {
            double xmy = p.X / Math.Cos(Math.PI / 6), xpy = 2 * (p.Y + h);
            double x = (xmy + xpy) / 2, y = (xpy - xmy) / 2;
            return Math.Abs(x - Math.Round(x)) < 1e-6 && Math.Abs(y - Math.Round(y)) < 1e-6 && x > -1e-6 && x < l + 1e-6 && y > -1e-6 && y < w + 1e-6;
        }

        [Test]
        public void B1_TruckMissingHeight_Hint2ShowsOneLayer_NoLinesUpTheSides_HeightHidden()
        {
            int n = 0;
            foreach (int level in new[] { 1, 2, 3 })
                foreach (long seed in Seeds(3000))
                {
                    Problem p = Volume.Generate(level, seed);
                    if (p.Kind != "missing") continue;
                    var pic = new TruckPicture(p);
                    IsoBox b = pic.Boxes[0];
                    string at = "vol L" + level + " seed " + seed;
                    for (int stage = 0; stage <= 2; stage++)
                    {
                        if (stage > 0) pic.Step(stage);
                        IsoScene s = pic.Scene();
                        Assert.AreEqual("?", s.Labels.First(x => x.Side == "hd").Text, at + ": the height stays hidden at stage " + stage);
                        Assert.IsFalse(s.Labels.Any(x => x.Text == b.H.ToString(System.Globalization.CultureInfo.InvariantCulture) && x.Side == "hd"), at);
                        Assert.AreEqual(stage == 2 ? (b.L - 1) + (b.W - 1) : 0, s.Lines.Count, at + ": one layer's grid at hint 2, nothing before");
                        foreach (IsoLine l in s.Lines)
                            Assert.IsTrue(OnTop(l.A, b.L, b.W, b.H) && OnTop(l.B, b.L, b.W, b.H), at + ": every hint-2 line lies on the top face (no layer can be counted)");
                    }
                    pic.Step(3);
                    IsoScene w3 = pic.Scene();
                    Assert.AreEqual(2 * (b.L - 1) + 2 * (b.W - 1) + 2 * (b.H - 1), w3.Lines.Count, at + ": every line at the worked step");
                    Assert.AreEqual(b.H.ToString(System.Globalization.CultureInfo.InvariantCulture), w3.Labels.First(x => x.Side == "hd").Text, at);
                    n++;
                }
            Assert.Greater(n, 1000);
        }

        // ---------- B2 / P8: beat maker, typed sum and difference ----------

        [Test]
        public void B2_BeatTypedSumAndDiff_BeforeTheWorkedStep_SeparateRows_NothingCombined()
        {
            int n = 0;
            foreach (int level in new[] { 2, 3 })
                foreach (long seed in Seeds(3000))
                {
                    Problem p = Fractions.Generate(level, seed);
                    if (p.Kind != "sum" && p.Kind != "diff") continue;
                    Rational a = BeatTypedPicture.A(p), b = BeatTypedPicture.B(p);
                    int g = BeatTypedPicture.Grid(p);
                    Rational ans = BeatTypedPicture.Plus(p) ? Rational.Add(a, b) : Rational.Sub(a, b);
                    string at = "frac L" + level + " " + p.Kind + " seed " + seed;
                    var allowed = new HashSet<string> { BeatTypedPicture.Plain(a), BeatTypedPicture.Plain(b), BeatTypedPicture.InGrid(a, g), BeatTypedPicture.InGrid(b, g) };
                    for (int stage = 0; stage <= 2; stage++)
                    {
                        List<BeatTypedRow> rows = BeatTypedPicture.Rows(p, stage);
                        Assert.IsNotNull(rows, at);
                        Assert.AreEqual(2, rows.Count, at + ": one row per note");
                        CollectionAssert.AreEqual(new[] { a, b }, rows.Select(r => r.Blocks.Single().Len).ToArray(), at + ": only the given notes are drawn");
                        foreach (BeatTypedRow r in rows)
                        {
                            BeatTypedBlock x = r.Blocks.Single();
                            Assert.AreEqual(0, Rational.Num(x.Start), 1e-12, at + ": each note starts at the row's left edge (nothing end to end)");
                            Assert.IsTrue(allowed.Contains(x.Label), at + ": a label is a given note's own fraction: " + x.Label);
                            int cells = (int)(x.Len.N * g / x.Len.D);
                            Assert.LessOrEqual(r.Numbers, cells, at + ": no cell numbers past the note");
                            Assert.AreEqual(stage == 2 ? cells : 0, r.Numbers, at);
                        }
                        Assert.AreEqual(stage == 2, rows.All(r => r.Blocks[0].Label == BeatTypedPicture.InGrid(r.Blocks[0].Len, g)), at + ": hint 2 labels them in the common denominator");
                        // the answer is never drawn as a block or a label unless it is one of the given notes
                        bool given = Rational.Cmp(ans, a) == 0 || Rational.Cmp(ans, b) == 0;
                        if (!given)
                        {
                            Assert.IsFalse(rows.Any(r => Rational.Cmp(r.Blocks[0].Len, ans) == 0), at + ": no block has the answer's length");
                            Assert.IsFalse(rows.Any(r => r.Blocks[0].Label == BeatTypedPicture.Plain(ans) || r.Blocks[0].Label == BeatTypedPicture.InGrid(ans, g)), at + ": no label is the answer");
                        }
                        Assert.IsTrue(rows.All(r => r.Numbers < (int)(ans.N * g / ans.D) || !BeatTypedPicture.Plus(p)), at + ": no row counts up to the sum");
                    }
                    Assert.IsNull(BeatTypedPicture.Rows(p, 3), at);
                    List<BeatTypedBlock> joined = BeatTypedPicture.JoinedBlocks(p);
                    if (BeatTypedPicture.Plus(p))
                        Assert.AreEqual(0, Rational.Cmp(Rational.Add(joined[0].Len, joined[1].Len), ans), at + ": the worked step joins them");
                    else
                    {
                        Assert.AreEqual(0, Rational.Cmp(joined[0].Len, ans), at + ": the worked step shows the remainder");
                        Assert.AreEqual(BeatTypedPicture.Plain(ans), joined[0].Label, at + ": labelled with its own length (the web wrote the whole's fraction on it)");
                        Assert.IsTrue(joined[1].Gone);
                    }
                    n++;
                }
            Assert.Greater(n, 1000);
        }

        // ---------- B3: stage designer, a hint-2 route onto the answer ----------

        [Test]
        public void B3_Stage_Hint2NeverDrawsARouteOrNumberOnTheAnswer_EveryKind()
        {
            int n = 0, hidden = 0;
            foreach (int level in new[] { 1, 2, 3 })
                foreach (long seed in Seeds(3000))
                {
                    Problem p = Coords.Generate(level, seed);
                    var v = new NullView();
                    var g = new StageGame(StageSetup.From(p), v, v);
                    GridPt a = g.Setup.Answer;
                    string at = p.Kind + " L" + level + " seed " + seed;
                    for (int stage = 1; stage <= 2; stage++)
                    {
                        g.Step(stage);
                        StageScene s = g.Scene();
                        if (s.Path != null)
                            // a read problem's route starts at the gear she is reading (it is on the stage already); it may not end there
                            foreach (GridPt q in s.Path.Points.Where(q => !(g.Setup.Read && q.Equals(s.Path.From)))) Assert.AreNotEqual(a, q, at + ": the hint-" + stage + " route touches the answer");
                        if (s.Highlight.HasValue)
                        {
                            Assert.IsNotNull(s.Path, at + ": a big number only with its route");
                            Assert.AreNotEqual(0, a.Y, at + ": the big bottom-line number is the answer point itself when y = 0");
                        }
                        if (!g.Setup.Read) { Assert.IsNull(s.Light, at); Assert.IsNull(s.Ghost, at); }
                        else Assert.IsNull(s.Ghost, at);
                    }
                    if (g.Scene().Path == null) hidden++;
                    g.Step(3);
                    Assert.AreEqual(a, g.Scene().Path.End, at + ": the worked route still ends on the answer");
                    n++;
                }
            Assert.AreEqual(9000, n);
            Assert.Greater(hidden, 300, "the y = 0 problems are in the sweep");
        }
    }
}
