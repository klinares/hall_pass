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
    /// Stage designer and pack the truck (world_tour/modes/math/stage.js, truck.js) as plain classes: StageGame,
    /// TruckBuild, TruckPicture and IsoGeometry, with the shared hint ladder (MathLadder). builder_vectors.json was printed by Tools/builder_vectors.mjs, which runs
    /// the web game's real modes (and parts.js's ladder) against a small fake DOM: every scripted press is replayed here
    /// and the dog's lines, misses, mistakes, wins, reveals, feedback and what's on screen must match. The pictures that
    /// go with each hint are also checked for what they claim (QUESTIONS 8/9: before the worked solution no picture
    /// hands over the answer, and no picture says anything false) across the 3,000-seed volume set.
    /// </summary>
    public class MathBuilderTests
    {
        private static readonly string Rsq = ((char)0x2019).ToString();

        // ---------- the JS vectors ----------

        private static JsonValue s_Vectors;

        private static JsonValue Vectors
        {
            get
            {
                if (s_Vectors != null) return s_Vectors;
                string assets;
                try { assets = UnityEngine.Application.dataPath; }
                catch (Exception) { assets = Environment.GetEnvironmentVariable("WT_ASSETS"); }
                s_Vectors = StrictJson.Parse(File.ReadAllText(Path.Combine(assets, "Tests", "EditMode", "Ui", "builder_vectors.json")));
                return s_Vectors;
            }
        }

        public static IEnumerable<int> StageCases() { return Enumerable.Range(0, 54); }
        public static IEnumerable<int> BuildCases() { return Enumerable.Range(0, 24); }
        public static IEnumerable<int> TypedCases() { return Enumerable.Range(0, 40); }

        /// <summary>The show's ctx (for the shared ladder too) and the views, recording what the JS fake ctx records.</summary>
        private sealed class Rec : IBuilderHost, IMathSong, IStageView, ITruckView
        {
            public readonly List<string> Ev = new List<string>();
            public bool NextShown, ButtonsLocked;
            public int Tries;
            private Func<int, bool> _hint;
            public void Say(string line, string pose) { Ev.Add("say|" + line + "|" + (pose ?? "null")); }
            public void Mistake(string tag) { Ev.Add("mistake|" + tag); }
            public void LockHints() { Ev.Add("lockHints"); }
            public void Next(float seconds) { Ev.Add("next|" + Math.Round(seconds * 1000).ToString(CultureInfo.InvariantCulture)); }
            public void ShowNext() { NextShown = true; }
            public void SetupHints(int max, Func<int, bool> apply) { Assert.AreEqual(2, max, "parts.js setupHints(2)"); _hint = apply; }
            public int Miss() { Tries++; Ev.Add("miss|" + Tries); return Tries; }
            public void Win() { Ev.Add("win|0"); }
            public void Reveal(string line) { Ev.Add("reveal|" + line); }
            /// <summary>The bulb (show.js: apply(n) returns false when there was nothing new).</summary>
            public bool Hint() { bool ok = _hint != null && _hint(0); Ev.Add("hint|" + (ok ? "true" : "false")); return ok; }
            public void Paint() { }
            public void Tapped() { Ev.Add("tap"); }
            public void Tapped(int x, int y) { Ev.Add("tap"); }
            public void Popped() { Ev.Add("pop"); }
            public void Wobbled() { Ev.Add("wobble"); }
            public void LockButtons() { ButtonsLocked = true; }
        }

        private static List<string> JsEvents(JsonValue ev)
        {
            var l = new List<string>();
            foreach (JsonValue e in ev.Items)
            {
                var parts = e.Items.Select(x => x.Kind == JsonKind.String ? x.String : x.Kind == JsonKind.Null ? "null"
                    : x.Kind == JsonKind.Bool ? (x.BoolValue ? "true" : "false") : JsNumber.Str(x.Number)).ToList();
                l.Add(string.Join("|", parts));
            }
            return l;
        }

        private static double[] Nums(string route)
        {
            // "M1.1 6.5 H4.1 V6.5"
            return route.Replace("M", " ").Replace("H", " ").Replace("V", " ").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        }

        // ---------- stage designer, replayed ----------

        [Test, TestCaseSource(nameof(StageCases))]
        public void Stage_ReplaysTheWebGame(int index)
        {
            JsonValue v = Vectors["stage"][index];
            int level = (int)v["level"].Long; long seed = v["seed"].Long;
            Problem p = Coords.Generate(level, seed);
            Assert.AreEqual(v["kind"].String, p.Kind);
            var rec = new Rec();
            var game = new StageGame(StageSetup.From(p), rec, rec);
            game.Ladder = new BuilderLadder(rec, p, game.Step);
            int max = game.Setup.Max;
            bool revealed = false;
            var acts = v["actions"].Items; var snaps = v["snaps"].Items;
            for (int i = 0; i < acts.Count; i++)
            {
                JsonValue a = acts[i];
                string what = a[0].String;
                switch (what)
                {
                    case "begin": game.Begin(); break;
                    case "tap": game.TapAt(a[1].Number, a[2].Number); break;
                    case "nudge": game.Nudge((int)a[1].Number, (int)a[2].Number); break;
                    case "pick": game.Pick(a[1].String == "x", (int)a[2].Number); break;
                    case "set": game.Set(); break;
                    case "hint": rec.Hint(); break;
                    default: Assert.Fail("unknown action " + what); break;
                }
                string at = "case " + index + " (" + p.Kind + " L" + level + " seed " + seed + ") press " + i + " " + what;
                JsonValue s = snaps[i];
                // intended difference 18: a move problem's "set" before the light moved says NeedMove instead of the web's NeedSpot
                List<string> js = JsEvents(s["ev"]).Select(e => p.Kind == "move" && e == "say|" + StageLines.NeedSpot + "|think"
                    ? "say|" + StageLines.NeedMove(game.Setup.Gear) + "|think" : RevealExpect.Map(e, "reveal|")).ToList();
                CollectionAssert.AreEqual(js, rec.Ev, at + ": events");
                if (js.Any(e => e.StartsWith("reveal|", StringComparison.Ordinal))) revealed = true;
                rec.Ev.Clear();
                Assert.AreEqual(s["pair"].String, game.PairText(), at + ": pair");
                Assert.AreEqual(s["setDisabled"].BoolValue, !game.SetEnabled, at + ": set button");
                Assert.AreEqual(s["nx"].BoolValue, rec.NextShown, at + ": next button");
                StageScene sc = game.Scene();
                var lamps = new List<string>();
                Action<string, GridPt?> lamp = (cls, g) => { if (g.HasValue) lamps.Add(cls + " " + JsNumber.Str(Math.Round(StageGame.PlotX(max, g.Value.X), 4)) + " " + JsNumber.Str(Math.Round(StageGame.PlotY(max, g.Value.Y), 4))); };
                lamp("start", sc.Start); lamp("set", sc.Light); lamp("ghost", sc.Ghost);
                CollectionAssert.AreEqual(s["lamps"].Items.Select(l => l[0].String + " " + JsNumber.Str(l[1].Number) + " " + JsNumber.Str(l[2].Number)).ToList(), lamps, at + ": lamps");
                // intended difference 29 (answer 16): before the worked step, a route that ends on the answer is not drawn
                bool hidden = false;
                if (!s["route"].IsNull && !revealed)
                {
                    double[] r0 = Nums(s["route"].String);
                    GridPt ans = game.Setup.Answer;
                    hidden = Math.Abs(r0[2] - StageGame.PlotX(max, ans.X)) < 1e-9 && Math.Abs(r0[3] - StageGame.PlotY(max, ans.Y)) < 1e-9;
                }
                if (hidden) { Assert.IsNull(sc.Path, at + ": a hint-2 route onto the answer is not drawn"); Assert.IsNull(sc.Highlight, at + ": nor the big number"); }
                else if (s["route"].IsNull) Assert.IsNull(sc.Path, at + ": no route");
                else
                {
                    Assert.IsNotNull(sc.Path, at + ": route");
                    double[] r = Nums(s["route"].String);
                    StagePath pa = sc.Path;
                    double[] mine = { StageGame.PlotX(max, pa.From.X), StageGame.PlotY(max, pa.From.Y), StageGame.PlotX(max, pa.ToX), StageGame.PlotY(max, pa.ToY) };
                    for (int k = 0; k < 4; k++) Assert.AreEqual(r[k], mine[k], 1e-9, at + ": route " + k);
                }
                if (!hidden && !revealed && game.Setup.Answer.Y == 0 && !s["on"].IsNull) Assert.IsNull(sc.Highlight, at + ": no big number on an answer on the bottom line");
                else if (!hidden) Assert.AreEqual(s["on"].IsNull ? null : s["on"].String, sc.Highlight.HasValue ? sc.Highlight.Value.ToString(CultureInfo.InvariantCulture) : null, at + ": highlighted number");
                // The pickers: the JS leaves them as she set them when the worked solution is shown; Unity shows the answer there (DECISIONS, 4.2).
                if (game.Setup.Read && !revealed)
                    CollectionAssert.AreEqual(s["pvals"].Items.Select(x => x.String).ToList(),
                        new[] { game.PickX.HasValue ? game.PickX.Value.ToString(CultureInfo.InvariantCulture) : "?", game.PickY.HasValue ? game.PickY.Value.ToString(CultureInfo.InvariantCulture) : "?" }, at + ": pickers");
            }
        }

        [Test]
        public void CoordinatePairs_KeepTogetherInWrappingText()
        {
            // P6 (fix round 2): TMP <nobr>, since the font has no U+00A0
            Assert.AreEqual("put the amp at <nobr>(2, 0)</nobr>. tap that spot", MathText.Pairs("put the amp at (2, 0). tap that spot"));
            Assert.AreEqual("<nobr>(10, 3)</nobr> and <nobr>(4, 4)</nobr>", MathText.Pairs("(10, 3) and (4, 4)"));
            StringAssert.Contains("<nobr>(1, 5)</nobr>", MathText.ToTmp("across 1, up 5. that's <b>(1, 5)</b>."));
            Assert.IsFalse(MathText.ToTmp("(2, 0)").Contains(((char)0x00A0).ToString()));
            Assert.AreEqual("1/2 + 1/4", MathText.Plain(MathText.Pairs("[[1/2]] + [[1/4]]")));
        }

        [Test]
        public void Stage_SetBeforeAnything_IsNotATry_InEveryKind()
        {
            // QUESTIONS 15 / answer 15: "set" with the light never moved (or the numbers never set) asks her to do something first
            foreach (string kind in new[] { "place", "read", "move" })
            {
                Problem p = Seeds(400).SelectMany(s => new[] { Coords.Generate(1, s), Coords.Generate(3, s) }).First(x => x.Kind == kind);
                var rec = new Rec();
                var g = new StageGame(StageSetup.From(p), rec, rec);
                g.Ladder = new BuilderLadder(rec, p, g.Step);
                string line = kind == "read" ? StageLines.NeedBoth : kind == "move" ? StageLines.NeedMove(g.Setup.Gear) : StageLines.NeedSpot;
                Assert.AreEqual(StageSet.NeedInput, g.Set(), kind);
                Assert.AreEqual(StageSet.NeedInput, g.Set(), kind);
                CollectionAssert.AreEqual(new[] { "say|" + line + "|think", "say|" + line + "|think" }, rec.Ev, kind + ": no wobble, no miss, no ladder line");
                Assert.AreEqual(0, rec.Tries, kind);
                Assert.AreEqual(0, g.Ladder.Shown, kind);
                Assert.IsFalse(g.Done, kind);
                if (kind == "read") { g.Pick(true, 1); Assert.AreEqual(StageSet.NeedInput, g.Set(), "one number is not enough"); Assert.AreEqual(0, rec.Tries); }
            }
            Assert.AreEqual("move the amp first.", StageLines.NeedMove("amp"));
        }

        [Test]
        public void Stage_Vectors_CoverEveryKindAndOutcome()
        {
            var kinds = new HashSet<string>(); int wins = 0, reveals = 0, traps = 0, needs = 0, offGrid = 0;
            foreach (JsonValue v in Vectors["stage"].Items)
            {
                kinds.Add(v["kind"].String);
                foreach (JsonValue s in v["snaps"].Items)
                    foreach (string e in JsEvents(s["ev"]))
                    {
                        if (e.StartsWith("win|", StringComparison.Ordinal)) wins++;
                        if (e.StartsWith("reveal|", StringComparison.Ordinal)) reveals++;
                        if (e.StartsWith("mistake|", StringComparison.Ordinal)) traps++;
                        if (e.Contains("first.|think")) needs++;
                    }
                var acts = v["actions"].Items; var snaps = v["snaps"].Items;
                for (int i = 1; i < acts.Count; i++)
                    if (acts[i][0].String == "tap" && snaps[i]["ev"].Count == 0) offGrid++;
            }
            CollectionAssert.AreEquivalent(new[] { "place", "read", "move" }, kinds);
            Assert.Greater(wins, 20); Assert.Greater(reveals, 5); Assert.Greater(traps, 10); Assert.Greater(needs, 3); Assert.Greater(offGrid, 3);
        }

        // ---------- pack the truck builder, replayed ----------

        [Test, TestCaseSource(nameof(BuildCases))]
        public void TruckBuild_ReplaysTheWebGame(int index)
        {
            JsonValue v = Vectors["truckBuild"][index];
            Problem p = Volume.Generate(2, v["seed"].Long);
            Assert.AreEqual("build", p.Kind);
            var rec = new Rec();
            var game = new TruckBuild(p, rec, rec);
            game.Ladder = new BuilderLadder(rec, p, game.Step);
            var acts = v["actions"].Items; var snaps = v["snaps"].Items;
            for (int i = 0; i < acts.Count; i++)
            {
                JsonValue a = acts[i];
                string what = a[0].String;
                switch (what)
                {
                    case "begin": game.Begin(); break;
                    case "spot": game.Add((int)a[1].Number, (int)a[2].Number); break;
                    case "undo": game.Undo(); break;
                    case "clear": game.Clear(); break;
                    case "go": game.Load(); break;
                    case "hint": rec.Hint(); break;
                    default: Assert.Fail("unknown action " + what); break;
                }
                string at = "case " + index + " (seed " + p.Seed + ", " + game.L + "x" + game.W + " to " + game.Target + ") press " + i + " " + what;
                JsonValue s = snaps[i];
                CollectionAssert.AreEqual(RevealExpect.Map(JsEvents(s["ev"]), "reveal|"), rec.Ev, at + ": events");
                rec.Ev.Clear();
                Assert.AreEqual(s["cnt"].String, TruckLines.Loaded(game.Total), at + ": count line");
                var spots = new List<string>();
                for (int y = 0; y < game.W; y++) for (int x = 0; x < game.L; x++) spots.Add(game.At(x, y) + (game.At(x, y) > 0 ? "+" : ""));
                CollectionAssert.AreEqual(s["spots"].Items.Select(x => x.String).ToList(), spots, at + ": spots");
                Assert.AreEqual(s["goDisabled"].BoolValue, rec.ButtonsLocked, at + ": load button");
                Assert.AreEqual(s["undoDisabled"].BoolValue, rec.ButtonsLocked, at + ": undo button");
                Assert.AreEqual(s["nx"].BoolValue, rec.NextShown, at + ": next button");
                IsoScene sc = game.Scene();
                Assert.AreEqual((int)s["cubes"].Number, sc.Cubes, at + ": crates drawn");
                Assert.AreEqual((int)s["lit"].Number, sc.LitCubes, at + ": lit crates");
                var vb = s["viewBox"].Items.Select(x => x.Number).ToArray();
                CollectionAssert.AreEqual(vb, new[] { sc.X0, sc.Y0, sc.W, sc.H }, at + ": view box");
            }
        }

        [Test]
        public void TruckBuild_Vectors_CoverRoofUnevenEmptyAndReveal()
        {
            string all = string.Join("\n", Vectors["truckBuild"].Items.SelectMany(v => v["snaps"].Items).SelectMany(s => JsEvents(s["ev"])));
            StringAssert.Contains("mistake|uneven-stacks", all);
            StringAssert.Contains("say|right count, but the stacks aren" + Rsq + "t all the same height. ", all);
            StringAssert.Contains("say|that stack" + Rsq + "s at the roof. 6 is the max.|think", all);
            StringAssert.Contains("reveal|", all);
            StringAssert.Contains("win|0", all);
            Assert.IsFalse(Vectors["truckBuild"].Items.Any(v => v["actions"].Items.Select((a, i) => new { a, i }).Any(x => x.a[0].String == "go" && x.i > 0
                && v["snaps"][x.i - 1]["cnt"].String == "0 loaded" && !JsEvents(v["snaps"][x.i]["ev"]).Contains("lockHints") && JsEvents(v["snaps"][x.i]["ev"]).Any(e => e.StartsWith("miss|", StringComparison.Ordinal)))),
                "no replay script loads an empty floor (intended difference 15 is tested on its own)");
        }

        // ---------- the typed truck problems' pictures ----------

        private static IsoFill FillOf(string poly, string group)
        {
            if (poly == "truckfloor") return IsoFill.Floor;
            bool lit = group.Split(' ').Contains("lit"), soft = group.Split(' ').Contains("b");
            switch (poly)
            {
                case "ft": return lit || soft ? IsoFill.TopLit : IsoFill.Top;
                case "fr": return soft ? IsoFill.RightSoft : IsoFill.Right;
                default: return IsoFill.Left;
            }
        }

        private static void SameScene(JsonValue js, IsoScene sc, string at, bool topLinesOnly = false)
        {
            CollectionAssert.AreEqual(js["viewBox"].Items.Select(x => x.Number).ToArray(), new[] { sc.X0, sc.Y0, sc.W, sc.H }, at + ": view box");
            var polys = js["polys"].Items;
            Assert.AreEqual(polys.Count, sc.Polys.Count, at + ": faces");
            for (int i = 0; i < polys.Count; i++)
            {
                Assert.AreEqual(FillOf(polys[i][0].String, polys[i][1].String), sc.Polys[i].Fill, at + ": face " + i + " fill");
                var pts = polys[i][2].Items;
                Assert.AreEqual(pts.Count, sc.Polys[i].Points.Length, at + ": face " + i + " corners");
                for (int k = 0; k < pts.Count; k++)
                {
                    // crates' corners are written with toFixed(3), solids' raw
                    Assert.AreEqual(pts[k][0].Number, sc.Polys[i].Points[k].X, 6e-4, at + ": face " + i + " x" + k);
                    Assert.AreEqual(pts[k][1].Number, sc.Polys[i].Points[k].Y, 6e-4, at + ": face " + i + " y" + k);
                }
            }
            var lines = js["lines"].Items;
            if (topLinesOnly) lines = new List<JsonValue>();                    // intended difference 31: checked by the claim test
            else Assert.AreEqual(lines.Count, sc.Lines.Count, at + ": unit lines");
            for (int i = 0; i < lines.Count; i++)
            {
                IsoLine l = sc.Lines[i];
                double[] m = { l.A.X, l.A.Y, l.B.X, l.B.Y };
                for (int k = 0; k < 4; k++) Assert.AreEqual(lines[i][k].Number, m[k], 1e-4, at + ": line " + i);
            }
            var texts = js["texts"].Items;
            Assert.AreEqual(texts.Count, sc.Labels.Count, at + ": labels");
            for (int i = 0; i < texts.Count; i++)
            {
                Assert.AreEqual("dim " + sc.Labels[i].Side, texts[i][0].String, at + ": label " + i + " side");
                Assert.AreEqual(texts[i][1].Number, sc.Labels[i].At.X, 1e-9, at + ": label " + i + " x");
                Assert.AreEqual(texts[i][2].Number, sc.Labels[i].At.Y, 1e-9, at + ": label " + i + " y");
                Assert.AreEqual(texts[i][3].String, sc.Labels[i].Text, at + ": label " + i + " text");
            }
        }

        [Test, TestCaseSource(nameof(TypedCases))]
        public void TruckTyped_PictureAtEachLadderStage_MatchesTheWebGame(int index)
        {
            JsonValue v = Vectors["truckTyped"][index];
            Problem p = Volume.Generate((int)v["level"].Long, v["seed"].Long);
            Assert.AreEqual(v["kind"].String, p.Kind);
            var pic = new TruckPicture(p);
            string at = "case " + index + " (" + p.Kind + " seed " + p.Seed + ")";
            var pics = v["pics"].Items;
            for (int stage = 0; stage < 4; stage++)
            {
                if (stage > 0) pic.Step(stage);
                SameScene(pics[stage], pic.Scene(), at + " stage " + stage, p.Kind == "missing" && stage == 2);
            }
            Assert.AreEqual(v["caption"].IsNull ? null : v["caption"].String, p.Kind == "count" ? TruckLines.Caption : null, at + ": caption");
            Assert.AreEqual(v["tag"].String, TruckLines.DeskTag, at + ": desk tag");
            Assert.AreEqual(v["lbl"].String, TruckLines.PromptLabel, at + ": prompt label");
            Assert.AreEqual(v["intro"].String, TruckLines.Intro(p.Kind), at + ": first line");
        }

        // ---------- small rules ----------

        [Test]
        public void Stage_Snap_RoundsHalvesUp_LikeJsMathRound_AndRefusesOffGrid()
        {
            GridPt g;
            Assert.IsTrue(StageGame.Snap(6, StageGame.Pad + 2.5, 6.5 - 3.5, out g)); Assert.AreEqual(new GridPt(3, 4), g);
            Assert.IsTrue(StageGame.Snap(6, StageGame.Pad - .5, 6.5, out g)); Assert.AreEqual(new GridPt(0, 0), g, "-0.5 rounds to 0, as Math.round");
            Assert.IsFalse(StageGame.Snap(6, StageGame.Pad - .51, 6.5, out g));
            Assert.IsFalse(StageGame.Snap(6, StageGame.Pad + 6.5, 3, out g));
            Assert.IsTrue(StageGame.Snap(10, StageGame.Pad + 10.49, .5 - .49, out g)); Assert.AreEqual(new GridPt(10, 10), g);
            Assert.AreEqual(-2, StageGame.JsRound(-2.5)); Assert.AreEqual(3, StageGame.JsRound(2.5)); Assert.AreEqual(0, StageGame.JsRound(-.5));
        }

        [Test]
        public void Stage_ToPlot_MapsTheDrawnBoxBackToPlotUnits()
        {
            foreach (int max in new[] { 6, 10 })
            {
                double side = StageGame.Side(max);
                // a 400 x 300 box: the plot is 300 square, centred; y up from the bottom-left
                double scale = 300 / side;
                for (int x = 0; x <= max; x++)
                    for (int y = 0; y <= max; y++)
                    {
                        double bx = 50 + StageGame.PlotX(max, x) * scale, by = (side - StageGame.PlotY(max, y)) * scale;
                        double ux, uy; GridPt g;
                        StagePlotLayout.ToPlot(max, 400, 300, bx, by, out ux, out uy);
                        Assert.IsTrue(StageGame.Snap(max, ux, uy, out g));
                        Assert.AreEqual(new GridPt(x, y), g);
                    }
            }
        }

        [Test]
        public void Stage_PlotLayout_MatchesStageJsPlot()
        {
            // stage.js:17-22 for max 6: 7 vertical + 7 horizontal lines, x labels 0..6 at Y(0)+.62, y labels 1..6 at X(0)-.45
            List<StagePlotLayout.Line> lines = StagePlotLayout.Lines(6);
            Assert.AreEqual(14, lines.Count);
            Assert.AreEqual(2, lines.Count(l => l.Axis));
            Assert.AreEqual(1.1, lines[0].X1, 1e-12); Assert.AreEqual(6.5, lines[0].Y1, 1e-12); Assert.AreEqual(.5, lines[0].Y2, 1e-12);
            List<StagePlotLayout.Label> labels = StagePlotLayout.Labels(6, 3);
            Assert.AreEqual(7 + 6 + 2, labels.Count);
            Assert.AreEqual(1, labels.Count(l => l.On));
            StagePlotLayout.Label on = labels.First(l => l.On);
            Assert.AreEqual("3", on.Text); Assert.AreEqual(4.1, on.X, 1e-12); Assert.AreEqual(6.5 + StagePlotLayout.BottomLabelDrop, on.Y, 1e-12);   // stage.js .62, lowered (P5)
            StagePlotLayout.Label y6 = labels.First(l => !l.Letter && l.X < 1 && l.Text == "6");
            Assert.AreEqual(.65, y6.X, 1e-12); Assert.AreEqual(.67, y6.Y, 1e-12);
            Assert.AreEqual(7.35, labels.First(l => l.Text == "x").X, 1e-12);
            Assert.AreEqual(.3, labels.First(l => l.Text == "y").Y, 1e-12);
        }

        [Test]
        public void Stage_LinesAndLabels_AreTheWebGames()
        {
            Assert.AreEqual("stage plot " + (char)0x2736 + " amp", StageLines.DeskTag("amp"));
            Assert.AreEqual("set the mic stand", StageLines.SetLabel(false, "mic stand"));
            Assert.AreEqual("lock it in", StageLines.SetLabel(true, "amp"));
            Assert.AreEqual("( ?, 4 )", StageLines.Pair(null, 4));
            var place = StageSetup.From(Coords.Generate(1, 5));
            Assert.AreEqual(place.Read ? "across first, then up." : place.Start.HasValue ? "move it, then set it." : "tap the spot. the arrows fine-tune it.", StageLines.Intro(place));
        }

        [Test]
        public void Truck_Lines_AreTheWebGames()
        {
            Assert.AreEqual("the trailer " + (char)0x2736 + " 24 crates", TruckLines.BuildDeskTag(24));
            Assert.AreEqual("12 loaded", TruckLines.Loaded(12));
            Assert.AreEqual("that stack" + Rsq + "s at the roof. 6 is the max.", TruckLines.Roof(6));
            Assert.AreEqual("spot 1, 3", TruckLines.Spot(0, 2));
            Assert.AreEqual("length " + (char)0xD7 + " width " + (char)0xD7 + " height.", TruckLines.Intro("lwh"));
        }

        // ---------- what the pictures claim (QUESTIONS 8 and 9) ----------

        private static IEnumerable<long> Seeds(int n) { for (int i = 0; i < n; i++) yield return 1 + i * 7919L; }

        [Test]
        public void Stage_HintPictures_NeverPlaceTheAnswerBeforeTheWorkedStep_AndAgreeWithTheText()
        {
            int checkedProblems = 0, readOnBottom = 0;
            foreach (int level in new[] { 1, 2, 3 })
                foreach (long seed in Seeds(3000))
                {
                    Problem p = Coords.Generate(level, seed);
                    var rec = new Rec();
                    var g = new StageGame(StageSetup.From(p), rec, rec);
                    GridPt a = g.Setup.Answer, from = g.Setup.Start ?? new GridPt(0, 0);
                    string at = p.Kind + " L" + level + " seed " + seed;
                    for (int n = 1; n <= 2; n++)
                    {
                        g.Step(n);
                        StageScene s = g.Scene();
                        Assert.IsFalse(g.Done, at + ": step " + n + " leaves the stage open");
                        Assert.IsNull(s.Ghost, at + ": no light of hers is set for her");
                        if (!g.Setup.Read) Assert.IsNull(s.Light, at + ": the answer's light isn't shown before the worked step");
                        else Assert.AreEqual(g.Setup.Point, s.Light, at + ": the gear to read stays where it is");
                        if (g.Setup.Read) { Assert.IsNull(g.PickX, at + ": the pickers stay hers"); Assert.IsNull(g.PickY, at); }
                    }
                    StagePath r2 = g.Scene().Path;
                    bool onAnswer = g.Setup.Read ? a.Y == 0 : from.Y == a.Y;
                    if (g.Setup.Read && a.Y == 0) readOnBottom++;
                    if (onAnswer)
                    {
                        // answer 16 (intended difference 29): the route would end on the answer, so hint 2 draws nothing
                        Assert.IsNull(r2, at + ": no hint-2 route onto the answer");
                        Assert.IsNull(g.Scene().Highlight, at + ": no big number on the answer");
                    }
                    else
                    {
                        Assert.AreEqual(a.Y == 0 ? (int?)null : a.X, g.Scene().Highlight, at + ": step 2 marks the answer's x (not when it is on the bottom line)");
                        Assert.AreEqual(a.X, r2.ToX, at + ": step 2's route goes to the answer's x");
                        Assert.AreNotEqual(a, r2.End, at + ": and stops short of the answer");
                        if (g.Setup.Read)
                        {
                            // "follow the gear straight down to the bottom line: it's at x across."
                            Assert.AreEqual(a, r2.From); Assert.AreEqual(0, r2.ToY);
                            StringAssert.Contains("<b>" + a.X + "</b> across", p.Steps[1], at);
                        }
                        else
                        {
                            // place: "go across to x on the bottom line"; move: "dx right: sx + dx = x"
                            Assert.AreEqual(from, r2.From); Assert.AreEqual(from.Y, r2.ToY);
                            StringAssert.Contains("<b>" + a.X + "</b>", p.Steps[1], at);
                        }
                    }
                    g.Step(3);
                    StageScene w = g.Scene();
                    Assert.IsTrue(g.Done, at);
                    Assert.AreEqual(a, w.Path.End, at + ": the worked route ends on the answer");
                    Assert.AreEqual(from, w.Path.From, at + ": and starts where the step text starts");
                    Assert.AreEqual(a, g.Setup.Read ? w.Ghost : w.Light, at + ": the answer is set on the stage");
                    Assert.AreEqual(StageLines.Pair(a.X, a.Y), g.Setup.Read ? g.PairText() : StageLines.Pair(w.Light.Value.X, w.Light.Value.Y), at);
                    StringAssert.Contains("<b>(" + a.X + ", " + a.Y + ")</b>", p.Steps[2], at + ": the worked text names the same point");
                    checkedProblems++;
                }
            Assert.AreEqual(9000, checkedProblems);
            // read problems on the bottom line: step 2 names x and the gear is on the line, so y = 0 is all that's left (FYI in the 4.2 hand-back)
            TestContext.WriteLine("read problems with y = 0: " + readOnBottom);
        }

        [Test]
        public void TruckTyped_HintPictures_HideTheMissingSideUntilTheWorkedStep_AndLabelTheTrueSides()
        {
            int n = 0;
            foreach (int level in new[] { 1, 2, 3 })
                foreach (long seed in Seeds(3000))
                {
                    Problem p = Volume.Generate(level, seed);
                    if (p.Kind == "build") continue;
                    var pic = new TruckPicture(p);
                    string at = p.Kind + " L" + level + " seed " + seed;
                    for (int stage = 0; stage <= 3; stage++)
                    {
                        if (stage > 0) pic.Step(stage);
                        IsoScene s = pic.Scene();
                        if (p.Kind == "count")
                        {
                            IsoBox b = pic.Boxes[0];
                            Assert.AreEqual(b.L * b.W * b.H, s.Cubes, at + ": every crate is drawn");
                            Assert.AreEqual(stage >= 2 ? b.L * b.W : 0, s.LitCubes, at + ": hint 2 lights exactly the top layer");
                            Assert.AreEqual(0, s.Labels.Count, at + ": a count problem has no numbers on the picture");
                            continue;
                        }
                        var boxes = p.Kind == "two" ? pic.Boxes : pic.Boxes.Take(1).ToList();
                        Assert.AreEqual(3 * boxes.Count, s.Labels.Count, at);
                        for (int k = 0; k < boxes.Count; k++)
                        {
                            IsoBox b = boxes[k];
                            Assert.AreEqual(b.L.ToString(CultureInfo.InvariantCulture), s.Labels[3 * k].Text, at + ": length label");
                            Assert.AreEqual(b.W.ToString(CultureInfo.InvariantCulture), s.Labels[3 * k + 1].Text, at + ": width label");
                            string h = p.Kind == "missing" && stage < 3 ? "?" : b.H.ToString(CultureInfo.InvariantCulture);
                            Assert.AreEqual(h, s.Labels[3 * k + 2].Text, at + ": height label at stage " + stage);
                        }
                        int unit = boxes.Sum(b => 2 * (b.L - 1) + 2 * (b.W - 1) + 2 * (b.H - 1));
                        // the missing height: hint 2 draws one layer (the top face's grid) only (answer 16, intended difference 31)
                        int top = boxes.Sum(b => (b.L - 1) + (b.W - 1));
                        Assert.AreEqual(stage >= 3 || (stage == 2 && p.Kind != "missing") ? unit : stage == 2 ? top : 0, s.Lines.Count, at + ": unit lines from hint 2");
                    }
                    n++;
                }
            Assert.Greater(n, 6000);
        }

        [Test]
        public void TruckBuild_EveryProblemFits_TheUnevenLineNeverQuotesTheWorkedStep_AndTheWorkedFillIsTrue()
        {
            int n = 0;
            foreach (long seed in Seeds(3000))
            {
                Problem p = Volume.Generate(2, seed);
                if (p.Kind != "build") continue;
                var rec = new Rec();
                var g = new TruckBuild(p, rec, rec);
                string at = "seed " + seed;
                Assert.AreEqual(g.L * g.W * g.Height, g.Target, at + ": the target is a whole number of full layers");
                Assert.LessOrEqual(g.Height, g.MaxH, at + ": the answer fits under the roof");
                foreach (int shown in new[] { 1, 2 })
                {
                    string line = TruckLines.Uneven(p.Steps, shown);
                    Assert.IsFalse(line.Contains(p.Steps[2]), at + ": the uneven line never quotes the worked step");
                    Assert.IsFalse(line.Contains("<b>"), at + ": nor any bolded answer");
                }
                g.Step(1); g.Step(2);
                Assert.AreEqual(0, g.Total, at + ": hints 1 and 2 load nothing for her");
                g.Step(3);
                Assert.AreEqual(g.Target, g.Total, at + ": the worked fill loads exactly the target");
                Assert.IsTrue(g.Even && g.At(0, 0) == g.Height, at + ": every stack at the answer's height");
                StringAssert.Contains("<b>" + g.Height + "</b> crates high on every spot", p.Steps[2], at + ": as the worked text says");
                Assert.AreEqual(g.Target, g.Scene().Cubes, at);
                n++;
            }
            Assert.Greater(n, 1000);
        }

        [Test]
        public void TruckBuild_RoofUndoClearAndTheEmptyFloor()
        {
            Problem p = Seeds(400).Select(s => Volume.Generate(2, s)).First(x => x.Kind == "build");
            var rec = new Rec();
            var g = new TruckBuild(p, rec, rec);
            g.Ladder = new BuilderLadder(rec, p, g.Step);
            for (int k = 0; k < 6; k++) Assert.AreEqual(TruckAdd.Added, g.Add(0, 0));
            Assert.AreEqual(TruckAdd.AtRoof, g.Add(0, 0));
            Assert.AreEqual(6, g.Total);
            Assert.AreEqual(TruckAdd.Ignored, g.Add(g.L, 0));
            Assert.IsTrue(g.Undo()); Assert.AreEqual(5, g.Total);
            Assert.IsTrue(g.Clear()); Assert.AreEqual(0, g.Total);
            Assert.IsFalse(g.Undo(), "nothing to undo after clear");
            // QUESTIONS 15 (intended difference 15): an empty floor is no try; she's asked to load something first
            Assert.AreEqual(TruckCheck.Empty, g.Load());
            Assert.AreEqual(0, rec.Tries);
            Assert.AreEqual(0, ladderShown(g));
            CollectionAssert.AreEqual(new[] { "say|" + TruckLines.EmptyFloor + "|think" }, rec.Ev.Skip(rec.Ev.Count - 1).ToList());
        }

        private static int ladderShown(TruckBuild g) { return g.Ladder.Shown; }

        [Test]
        public void TruckBuild_EmptyFloor_IsAMissOnTheWeb_ButNotHere()
        {
            JsonValue v = Vectors["truckEmpty"];
            List<string> js = JsEvents(v["snaps"][1]["ev"]);
            Assert.IsTrue(js.Contains("miss|1"), "the web counts it: " + string.Join(", ", js));
            Problem p = Volume.Generate(2, v["seed"].Long);
            var rec = new Rec();
            var g = new TruckBuild(p, rec, rec);
            g.Ladder = new BuilderLadder(rec, p, g.Step);
            g.Begin(); rec.Ev.Clear();
            Assert.AreEqual(TruckCheck.Empty, g.Load());
            Assert.AreEqual(TruckCheck.Empty, g.Load());
            CollectionAssert.AreEqual(new[] { "say|load a crate or two first.|think", "say|load a crate or two first.|think" }, rec.Ev,
                "no wobble, no miss, no ladder line");
            Assert.AreEqual(0, rec.Tries);
            Assert.AreEqual(0, g.Ladder.Shown);
            Assert.IsFalse(rec.ButtonsLocked);
            // the line states no number, so it can't give anything away
            Assert.IsFalse(TruckLines.EmptyFloor.Any(char.IsDigit));
        }
    }
}
