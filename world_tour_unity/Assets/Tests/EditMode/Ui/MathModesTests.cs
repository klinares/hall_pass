using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Data;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;
using WorldTour.Game.Modes;

namespace WorldTour.Game.EditTests
{
    /// <summary>
    /// Task 4.1: the shared math parts (world_tour/modes/math/parts.js keypad, ladder, typedAnswer, rich), the merch table
    /// (merch.js) and the beat maker (beat.js). The cross-check vectors in math_vectors.txt come from the web game's own
    /// code (Tools/math/math_vectors.mjs: the real parts.js and generators, and beat.js layout/cheapestCut and merch.js
    /// tag/receipt copied out verbatim at run time).
    /// </summary>
    public class MathModesTests
    {
        private static string VectorPath
        {
            get
            {
                string assets;
                try { assets = UnityEngine.Application.dataPath; }
                catch (Exception) { assets = Environment.GetEnvironmentVariable("WT_ASSETS"); }
                return Path.Combine(assets, "Tests", "EditMode", "Ui", "math_vectors.txt");
            }
        }

        private static List<JsonValue> s_Vectors;

        private static IEnumerable<JsonValue> Vectors(string t)
        {
            if (s_Vectors == null)
                s_Vectors = File.ReadAllLines(VectorPath).Where(l => l.Length > 0).Select(l => StrictJson.Parse(l)).ToList();
            return s_Vectors.Where(v => v["t"].String == t);
        }

        private static AnswerType TypeOf(string s)
        {
            switch (s)
            {
                case "money": return AnswerType.Money;
                case "decimal": return AnswerType.Decimal;
                case "fraction": return AnswerType.Fraction;
                default: return AnswerType.Int;
            }
        }

        private static string Readout(string text)
        {
            ReadoutShape r = KeypadText.Shape(text);
            if (r.Kind == ReadoutKind.Empty) return "E";
            if (r.Kind == ReadoutKind.Plain) return "P:" + r.Text;
            return "F:" + (r.Whole ?? "") + ":" + r.Num + ":" + r.Den;
        }

        private static Rational Frac(string s)
        {
            string[] p = s.Split('/');
            return Rational.R(long.Parse(p[0], CultureInfo.InvariantCulture), long.Parse(p[1], CultureInfo.InvariantCulture));
        }

        // ---------- the keypad ----------

        [Test]
        public void Vectors_AreThere()
        {
            Assert.GreaterOrEqual(Vectors("K").Count(), 300);
            Assert.GreaterOrEqual(Vectors("L").Count(), 400);
            Assert.GreaterOrEqual(Vectors("C").Count(), 60);
            Assert.AreEqual(300, Vectors("M").Count());
            Assert.GreaterOrEqual(Vectors("S").Count(), 400);
            Assert.GreaterOrEqual(Vectors("B").Count(), 140);
        }

        [Test]
        public void Keypad_TextAndReadout_MatchWireKeypad()
        {
            int n = 0;
            foreach (JsonValue v in Vectors("K"))
            {
                var k = new KeypadText(TypeOf(v["type"].String));
                foreach (char c in v["keys"].String) k.Press(c == '<' ? "back" : c.ToString());
                Assert.AreEqual(v["text"].String, k.Text, v.ToString());
                Assert.AreEqual(v["readout"].String, Readout(k.Text), v.ToString());
                n++;
            }
            Assert.Greater(n, 0);
        }

        [Test]
        public void Keypad_Rules()
        {
            var money = new KeypadText(AnswerType.Money);
            Assert.IsTrue(money.Press("."));
            Assert.AreEqual("0.", money.Text, "a point first becomes 0.");
            money.Press("5"); money.Press("0");
            Assert.IsFalse(money.Press("1"), "no third money decimal, and no tap");
            Assert.AreEqual("0.50", money.Text);
            Assert.IsTrue(money.Press("/"), "the bar key is a no-op for money, but still a key");
            Assert.AreEqual("0.50", money.Text);
            var f = new KeypadText(AnswerType.Fraction);
            f.Press("/");
            Assert.AreEqual("", f.Text, "no bar before a digit");
            foreach (string c in new[] { "1", " ", "3", "/", "4" }) f.Press(c);
            Assert.AreEqual("1 3/4", f.Text);
            Assert.AreEqual("F:1:3:4", Readout(f.Text));
            Assert.AreEqual("F:1::", Readout("1 "), "and: the whole, then two empty boxes");
            Assert.AreEqual("F::3:", Readout("3/"));
            f.Lock();
            Assert.IsFalse(f.Press("5"));
            Assert.AreEqual("1 3/4", f.Text);
            var d = new KeypadText(AnswerType.Int);
            for (int i = 0; i < 12; i++) d.Press("7");
            Assert.AreEqual(9, d.Text.Length, "nine characters at most");
        }

        // ---------- the beat track ----------

        [Test]
        public void Layout_MatchesBeatJs()
        {
            int n = 0;
            foreach (JsonValue v in Vectors("L"))
            {
                List<Rational> lens = v["lens"].Items.Select(x => Frac(x.String)).ToList();
                List<BeatPiece> got = BeatBoard.Layout(lens, v["rows"].Int);
                IReadOnlyList<JsonValue> want = v["pieces"].Items;
                Assert.AreEqual(want.Count, got.Count, v.ToString());
                for (int i = 0; i < want.Count; i++)
                {
                    JsonValue w = want[i];
                    Assert.AreEqual(w[0].Int, got[i].Block, v.ToString());
                    // bit for bit (JS String(n) reads back to the same double; -1.1e-16 shows the float steps are the same)
                    Assert.AreEqual(double.Parse(w[1].String, CultureInfo.InvariantCulture), got[i].X, 0.0, v.ToString());
                    Assert.AreEqual(double.Parse(w[2].String, CultureInfo.InvariantCulture), got[i].W, 0.0, v.ToString());
                    Assert.AreEqual(w[3].BoolValue, got[i].First, v.ToString());
                    Assert.AreEqual(w[4].BoolValue, got[i].Cut, v.ToString());
                    Assert.AreEqual(w[5].Int, got[i].Row, v.ToString());
                }
                n++;
            }
            Assert.Greater(n, 0);
        }

        [Test]
        public void CheapestCut_MatchesBeatJs()
        {
            foreach (JsonValue v in Vectors("C"))
            {
                List<int> pre = v["pre"].AsLongs().Select(x => (int)x).ToList();
                List<int> got = BeatBoard.CheapestCut(pre, MathValue.Frac(v["n"].Number, v["d"].Number));
                CollectionAssert.AreEqual(v["cut"].AsLongs().Select(x => (int)x).ToList(), got, v.ToString());
            }
        }

        // ---------- merch ----------

        [Test]
        public void Merch_TagsAndReceipt_MatchMerchJs()
        {
            foreach (JsonValue v in Vectors("M"))
            {
                Problem p = Decimals.Generate(v["level"].Int, v["seed"].Long);
                CollectionAssert.AreEqual(v["tags"].Items.Select(x => x.String).ToList(), MerchRules.Lines(p).Select(MerchRules.Tag).ToList(), v.ToString());
                if (v["r2"].IsNull) { Assert.IsTrue(p.Work != null && p.Work.IsNull, "the JS work is null (decimal x decimal draws no receipt): " + v); continue; }
                Assert.AreEqual(Rows(v["r2"]), Rows(MerchRules.Receipt(p.Work, false)), v.ToString());
                Assert.AreEqual(Rows(v["r3"]), Rows(MerchRules.Receipt(p.Work, true)), v.ToString());
            }
        }

        private static string Rows(JsonValue j) { return string.Join("|", j.Items.Select(x => x.String)); }
        private static string Rows(List<MerchRules.ReceiptRow> rows) { return string.Join("|", rows.Select(r => (r.Sum ? "S" : "R") + "~" + r.Op + "~" + r.Amount)); }

        [Test]
        public void Merch_Lines_ReadTheProblem()
        {
            Problem p = Decimals.Generate(3, 7);
            List<MerchLine> lines = MerchRules.Lines(p);
            Assert.That(lines.Count, Is.InRange(1, 2));
            Assert.AreEqual("2.5", MerchRules.Tenths(25));
            Assert.AreEqual("1.1", MerchRules.Tenths(11));
            for (int seed = 1; seed <= 300; seed++)
                for (int level = 1; level <= 3; level++)
                {
                    Problem q = Decimals.Generate(level, seed);
                    foreach (MerchLine l in MerchRules.Lines(q)) Assert.IsFalse(string.IsNullOrEmpty(l.Icon), "every line has a sticker id");
                    Assert.AreEqual(q.Kind == "change", MerchRules.Paid(q) > 0);
                }
        }

        // ---------- the typed round and the ladder ----------

        private sealed class Song : IMathSong
        {
            public readonly List<string> Log = new List<string>();
            int _tries, _hints, _max;
            Func<int, bool> _apply;
            public void SetupHints(int max, Func<int, bool> apply) { _max = max; _apply = apply; Log.Add("setup:" + max); }
            public int Miss() { _tries++; Log.Add("miss:" + _tries); return _tries; }
            public void Mistake(string tag) { Log.Add("mistake:" + tag); }
            public void Win() { Log.Add("win"); }
            public void Reveal(string line) { Log.Add("reveal:" + MathText.Plain(line)); }
            public void Say(string line, string pose) { Log.Add("say:" + pose + ":" + MathText.Plain(line)); }
            public void LockHints() { Log.Add("lock"); }
            public void Hint()
            {
                if (_hints >= _max) Log.Add("h:none");
                else if (!_apply(_hints + 1)) Log.Add("h:free");
                else { _hints++; Log.Add("h:" + _hints); }
            }
        }

        private sealed class Pad : IMathPadView
        {
            public readonly KeypadText K;
            private readonly List<string> _log;
            public Pad(KeypadText k, List<string> log) { K = k; _log = log; }
            public void Nudge() { }
            public void LockRight() { K.Lock(); }
            public void Wobble() { }
            public void ShowAnswer(string text) { K.Set(text); K.Lock(); }
            public void Clear() { K.Clear(); }
            // the web's showNext falls back to ctx.next(3200) when the page has no next button (the vectors' fake page)
            public void ShowNext() { _log.Add("next:3200"); }
        }

        [Test]
        public void TypedRound_MatchesTypedAnswer()
        {
            int n = 0;
            foreach (JsonValue v in Vectors("S"))
            {
                int level = v["level"].Int;
                long seed = v["seed"].Long;
                Problem p = v["skill"].String == "dec" ? Decimals.Generate(level, seed) : Fractions.Generate(level, seed);
                var song = new Song();
                var pad = new Pad(new KeypadText(p.Answer.Type), song.Log);
                var round = new TypedMathRound(song, p, pad, s => song.Log.Add("next:" + Math.Round(s * 1000).ToString(CultureInfo.InvariantCulture)),
                    step => song.Log.Add("step:" + step));
                foreach (JsonValue a in v["script"].Items)
                {
                    string act = a.String;
                    if (act == "h") song.Hint();
                    else foreach (char c in act)
                        {
                            if (c == '\n') { if (!pad.K.Locked) round.Submit(pad.K.Text); }
                            else pad.K.Press(c == '<' ? "back" : c.ToString());
                        }
                    song.Log.Add("pad=" + pad.K.Text);
                }
                CollectionAssert.AreEqual(RevealExpect.Map(v["log"].Items.Select(x => x.String), "reveal:"), song.Log, v.ToString());
                n++;
            }
            Assert.Greater(n, 0);
        }

        [Test]
        public void BuilderLadder_MatchesLadder()
        {
            foreach (JsonValue v in Vectors("B"))
            {
                Problem p = Fractions.Generate(v["level"].Int, v["seed"].Long);
                var song = new Song();
                var ladder = new MathLadder(song, p, step => song.Log.Add("step:" + step));
                foreach (JsonValue a in v["script"].Items)
                {
                    string act = a.String;
                    if (act == "h") song.Hint();
                    else if (act == "right") ladder.Right();
                    else song.Log.Add("over=" + (ladder.Wrong(null, act) ? "true" : "false"));
                }
                CollectionAssert.AreEqual(RevealExpect.Map(v["log"].Items.Select(x => x.String), "reveal:"), song.Log, v.ToString());
            }
        }

        [Test]
        public void TypedRound_UnreadableInput_IsNeverAMiss()
        {
            // QUESTIONS 7: empty, unreadable and not-yet-simplest input only asks her to retype
            Problem p = Enumerable.Range(1, 400).Select(s => Fractions.Generate(3, s)).First(q => q.Kind == "diff" && q.Answer.Value.D <= 6);
            var song = new Song();
            var pad = new Pad(new KeypadText(p.Answer.Type), song.Log);
            var round = new TypedMathRound(song, p, pad, s => { });
            Rational v = Rational.R((long)p.Answer.Value.N, (long)p.Answer.Value.D);
            Assert.AreEqual(TypedMathResult.Nudge, round.Submit(""));
            Assert.AreEqual(TypedMathResult.Nudge, round.Submit("1/"));
            Assert.AreEqual(TypedMathResult.Nudge, round.Submit((v.N * 2) + "/" + (v.D * 2)), "simplest form is asked for");
            Assert.IsFalse(song.Log.Any(l => l.StartsWith("miss")), "a nudge is not a miss");
            Assert.AreEqual(TypedMathResult.Right, round.Submit(Rational.Str(v)));
        }

        // ---------- the beat builder ----------

        private static IEnumerable<Problem> Builders(int count = 300)
        {
            for (int level = 1; level <= 3; level++)
                for (int seed = 1; seed <= count; seed++)
                {
                    Problem p = Fractions.Generate(level, seed);
                    if (p.Builder == true) yield return p;
                }
        }

        [Test]
        public void Beat_TheRightNotesFillIt_AndPlayWins()
        {
            int fills = 0, cuts = 0;
            foreach (Problem p in Builders())
            {
                var b = new BeatBoard(p);
                if (b.Removable)
                {
                    Assert.AreEqual(BeatPlay.TooLong, b.Play(), "a take-out starts too full");
                    List<int> cut = BeatBoard.CheapestCut(b.Pre, p.Answer.Value);
                    Assert.IsNotEmpty(cut);
                    foreach (int i in cut.OrderByDescending(x => x))
                        Assert.IsTrue(b.Tap(b.Blocks().FindIndex(x => x.Pre && x.Ref == i)));
                    Assert.IsTrue(b.Filled(), p.Seed.ToString());
                    Assert.AreEqual(BeatPlay.Filled, b.Play());
                    Assert.IsTrue(b.Done);
                    Assert.IsFalse(b.Tap(0), "done: no more taps");
                    cuts++;
                }
                else
                {
                    Assert.AreEqual(BeatPlay.Empty, b.Play(), "nothing added yet: not an attempt (QUESTIONS 15)");
                    Assert.IsFalse(b.Done);
                    foreach (int d in b.FillSolution()) Assert.AreEqual(BeatAdd.Added, b.Add(d));
                    Assert.IsTrue(b.Filled());
                    Assert.AreEqual(BeatPlay.Filled, b.Play());
                    Assert.AreEqual(BeatAdd.Ignored, b.Add(b.Palette[0]), "done: no more notes");
                    fills++;
                }
            }
            Assert.Greater(fills, 50);
            Assert.Greater(cuts, 20);
        }

        [Test]
        public void Beat_AddTapUndo_AndThePageLimit()
        {
            Problem p = Builders().First(q => !new BeatBoard(q).Removable);
            var b = new BeatBoard(p);
            int pre = b.Pre.Count;
            int smallest = b.Palette.Max();
            int added = 0;
            BeatAdd r;
            while ((r = b.Add(smallest)) == BeatAdd.Added) added++;
            Assert.AreEqual(BeatAdd.NoRoom, r, "beat.js:117 no more room past bars + 1 measures");
            Assert.LessOrEqual(Rational.Num(b.Total()), b.Bars + 1 + 1e-9);
            Assert.AreEqual(b.Bars + 1, b.Rows(), "a spilling page draws one more row");
            Assert.IsTrue(b.Over());
            Assert.AreEqual(BeatPlay.TooLong, b.Play());
            Assert.IsFalse(b.Tap(0), "written notes can't be taken back in a fill");
            Assert.IsTrue(b.Tap(pre), "a placed note can");
            Assert.AreEqual(added - 1, b.Placed.Count);
            Assert.IsTrue(b.Undo());
            Assert.AreEqual(added - 2, b.Placed.Count);
            while (b.Placed.Count > 0) b.Undo();
            Assert.IsTrue(b.Undo(), "undo on nothing is still a key (the web plays its tap)");
            Assert.AreEqual(b.Bars, b.Rows());
        }

        [Test]
        public void Beat_PlayOnAnEmptyMeasure_IsNotAMiss()
        {
            // QUESTIONS 15 (intended difference): no miss, no try, no fans, the ladder doesn't move; riff asks for a note
            Problem p = Builders().First(q => !new BeatBoard(q).Removable);
            var b = new BeatBoard(p);
            Assert.AreEqual(BeatPlay.Empty, b.Play());
            Assert.AreEqual(BeatPlay.Empty, b.Play(), "as often as she likes");
            Assert.AreEqual("add a note or two first.", MathLines.AddANoteFirst);
            b.Add(b.Palette.Max());
            Assert.AreNotEqual(BeatPlay.Empty, b.Play(), "with a note placed it is a real try");
            var t = new BeatBoard(Builders().First(q => new BeatBoard(q).Removable));
            Assert.AreEqual(BeatPlay.TooLong, t.Play(), "a take-out starts too full: playing it untouched is still a try");
        }

        [Test]
        public void Beat_WorkedStep_FillsItTheRightWay()
        {
            foreach (Problem p in Builders(120))
            {
                var b = new BeatBoard(p);
                if (!b.Removable) b.Add(b.Palette[0]);
                else b.Tap(0);
                b.Step(1);
                Assert.AreEqual(1, b.Stage);
                Assert.IsFalse(b.Done);
                b.Step(2);
                b.Step(3);
                Assert.IsTrue(b.Done);
                Assert.IsTrue(b.Filled(), "the worked solution fills it exactly: seed " + p.Seed);
            }
        }

        [Test]
        public void Beat_BarLine_SitsAtTheTarget()
        {
            Assert.IsNull(BeatBoard.BarEnd(null, 0));
            Assert.AreEqual(1.0, BeatBoard.BarEnd(Rational.R(1), 0));
            Assert.AreEqual(0.0, BeatBoard.BarEnd(Rational.R(1), 1));
            Assert.AreEqual(1.0, BeatBoard.BarEnd(Rational.R(3, 2), 0));
            Assert.AreEqual(0.5, BeatBoard.BarEnd(Rational.R(3, 2), 1));
        }

        // ---------- stacked fractions ----------

        [Test]
        public void Rich_ParsesAndFlattensLikeTheWeb()
        {
            List<MathSeg> s = MathText.Parse("add [[1 1/2]] and [[3/4]], then [[2]] more");
            Assert.AreEqual(5, s.Count);
            Assert.AreEqual("add ", s[0].Text);
            Assert.IsTrue(s[1].IsFrac);
            Assert.AreEqual("1", s[1].Whole); Assert.AreEqual("1", s[1].Num); Assert.AreEqual("2", s[1].Den);
            Assert.IsNull(s[3].Whole);
            Assert.AreEqual(", then 2 more", s[4].Text, "[[2]] is just 2 (rich())");
            Assert.AreEqual("add 1 1/2 and 3/4, then 2 more", MathText.Plain("add [[1 1/2]] and [[3/4]], then [[2]] more"));
        }

        [Test]
        public void Rich_EveryFractionIsStacked_NeverSlashText()
        {
            foreach (Problem p in Enumerable.Range(1, 200).SelectMany(seed => new[] { Fractions.Generate(2, seed), Fractions.Generate(3, seed) }))
                foreach (string line in p.Steps.Concat(new[] { p.Prompt, p.Label }))
                {
                    string t = MathText.ToTmp(line);
                    StringAssert.DoesNotContain("[[", t, line);
                    foreach (MathSeg g in MathText.Parse(line).Where(x => x.IsFrac))
                        StringAssert.DoesNotContain(g.Num + "/" + g.Den, t, "a fraction left as slash text: " + line);
                }
            string one = MathText.Stacked(null, "3", "12");
            StringAssert.Contains("<noparse>__</noparse>", one, "the bar is as wide as the wider number");
            StringAssert.Contains("<voffset=", one);
            StringAssert.DoesNotContain("/", one.Replace("</", "<"));
            string mixed = MathText.Stacked("1", "1", "2");
            StringAssert.Contains("<b>1</b><space=0.08em><size=", mixed, "the whole number is bold, full size, before the fraction");
            Assert.AreEqual(MathText.Stacked(null, "1", "2"), MathText.ToTmp("[[1/2]]"));
        }

        [Test]
        public void Copy_IsTheWebGames()
        {
            Assert.AreEqual(28, MathLines.Trap.Count, "parts.js TRAP");
            Assert.AreEqual("not full yet. there" + (char)0x2019 + "s still a gap before the bar line.", MathLines.Trap["too-short"]);
            Assert.AreEqual(" it" + (char)0x2019 + "ll come back around.", MathLines.ComeBack);
            Assert.AreEqual("2.5 ounces " + (char)0x00D7 + " $1.20", MerchRules.Tag(new MerchLine { Unit = "ounce", Amount = 25, Each = 120, Qty = 1 }));
            Assert.AreEqual("3 " + (char)0x00D7 + " $2.15", MerchRules.Tag(new MerchLine { Qty = 3, Each = 215 }));
            Assert.AreEqual("$4.05", MerchRules.Tag(new MerchLine { Qty = 1, Each = 405 }));
            Assert.AreEqual("4.60", TypedMathRound.ShownText(Answer.Money(460)));
            Assert.AreEqual("1 1/2", TypedMathRound.ShownText(Answer.Fraction(Rational.R(3, 2))));
            Assert.AreEqual("1250.00", TypedMathRound.ShownText(Answer.Money(125000)), "no commas on the readout");
        }
    }
}
