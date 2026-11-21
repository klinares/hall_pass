using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using WorldTour.Core.Data;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;
using WorldTour.Game.Modes;

namespace WorldTour.Game.EditTests
{
    /// <summary>
    /// Ticket sales, tour budget and soundcheck (world_tour/modes/math/tickets.js, budget.js, soundcheck.js) as plain
    /// classes: TicketsWork, BudgetRound, SoundcheckRound and SoundcheckMeter, with the shared TypedMathRound and
    /// MathLadder. typed_vectors.json was printed by Tools/typed_vectors.mjs, which runs the web game's real modes (and
    /// parts.js) on a small fake DOM with a fake clock and Math.random seeded: every scripted press is replayed here and
    /// the dog's lines, misses, mistakes, wins (with soundcheck's bonus), reveals, feedback and what's on screen must
    /// match, except where an intended difference says otherwise (16, 17: the ticket papers at step 2). The papers and
    /// sheets that go with each hint are checked for what they claim across the 3,000-seed volume set (QUESTIONS 8, 9).
    /// </summary>
    public class MathTypedModesTests
    {
        private static JsonValue s_Vectors;

        private static JsonValue Vectors
        {
            get
            {
                if (s_Vectors != null) return s_Vectors;
                string assets;
                try { assets = UnityEngine.Application.dataPath; }
                catch (Exception) { assets = Environment.GetEnvironmentVariable("WT_ASSETS"); }
                s_Vectors = StrictJson.Parse(File.ReadAllText(Path.Combine(assets, "Tests", "EditMode", "Ui", "typed_vectors.json")));
                return s_Vectors;
            }
        }

        public static IEnumerable<int> TicketCases() { return Enumerable.Range(0, 48); }
        public static IEnumerable<int> BudgetCases() { return Enumerable.Range(0, 36); }
        public static IEnumerable<int> SoundcheckCases() { return Enumerable.Range(0, 54); }

        private static readonly Regex Tags = new Regex("<[^>]+>");
        private static string Plain(string s) { return s == null ? null : Tags.Replace(s, ""); }

        /// <summary>The show's ctx, the keypad and the views, recording what the JS fake ctx records.</summary>
        private sealed class Rec : IMathSong, IMathPadView, IBudgetView, ISoundcheckView
        {
            public readonly List<string> Ev = new List<string>();
            public int Tries;
            public bool NextShown, PadShown;
            public string Ro = "";
            public bool[] Off = new bool[4], Good = new bool[4], GoneB = new bool[4];
            private Func<int, bool> _hint;

            public void SetupHints(int max, Func<int, bool> apply) { Assert.AreEqual(2, max); _hint = apply; }
            public int Miss() { Tries++; Ev.Add("miss|" + Tries); return Tries; }
            public void Mistake(string tag) { Ev.Add("mistake|" + tag); }
            public void Win() { Ev.Add("win|0"); }
            public void Reveal(string line) { Ev.Add("reveal|" + line); }
            public void Say(string line, string pose) { Ev.Add("say|" + line + "|" + (pose ?? "null")); }
            public void LockHints() { Ev.Add("lockHints"); }
            public bool Hint() { bool ok = _hint != null && _hint(0); Ev.Add("hint|" + (ok ? "true" : "false")); return ok; }

            // keypad
            public void Nudge() { Ev.Add("nudge"); }
            public void LockRight() { Ev.Add("pop"); }
            public void Wobble() { Ev.Add("wobble"); }
            public void ShowAnswer(string text) { Ro = text; }
            public void Clear() { Ro = ""; }
            public void ShowNext() { NextShown = true; }

            // choices (budget, soundcheck)
            public void Paint() { }
            public void SlapSheet() { }
            public void FreezeChoices() { for (int i = 0; i < Off.Length; i++) Off[i] = true; }
            void IBudgetView.Good(int i, bool pop) { Good[i] = true; if (pop) Ev.Add("pop"); }
            void ISoundcheckView.Good(int i, bool pop) { Good[i] = true; if (pop) Ev.Add("pop"); }
            public void Gone(int i) { GoneB[i] = true; Off[i] = true; Ev.Add("wobble"); }
            public void ShowPad() { PadShown = true; }
            public void ShowAnswerOnPad(string text) { Ro = text; }
            public void Ticked() { Ev.Add("tick"); }

            public List<string> Choices(int n)
            {
                return Enumerable.Range(0, n).Select(i => (Off[i] ? "off" : "on") + (Good[i] ? " good" : "") + (GoneB[i] ? " gone" : "")).ToList();
            }
        }

        private static List<string> JsEvents(JsonValue ev, bool dropTaps)
        {
            var l = new List<string>();
            foreach (JsonValue e in ev.Items)
            {
                var parts = e.Items.Select(x => x.Kind == JsonKind.String ? x.String : x.Kind == JsonKind.Null ? "null"
                    : x.Kind == JsonKind.Bool ? (x.BoolValue ? "true" : "false") : JsNumber.Str(x.Number)).ToList();
                string s = string.Join("|", parts);
                if (dropTaps && s == "tap") continue;                                 // the keypad's key clicks
                l.Add(s);
            }
            return l;
        }

        private static string F(long n) { return TicketsWork.F(n); }

        /// <summary>
        /// The web game's hint text where Unity's differs by an intended difference: soundcheck's level 3 decimal-point
        /// hint 2 (answer 14's place-value wording, IntendedDifferences 9.1/14, still waiting in the web game). Any other
        /// difference fails. Returns (web, unity) pairs to swap in the web's events.
        /// </summary>
        private static List<KeyValuePair<string, string>> StepSwaps(JsonValue head, Problem p, string at)
        {
            var l = new List<KeyValuePair<string, string>>();
            var js = head["steps"].Items.Select(x => x.String).ToList();
            Assert.AreEqual(js.Count, p.Steps.Count, at);
            for (int k = 0; k < js.Count; k++)
            {
                if (js[k] == p.Steps[k]) continue;
                // polish 6.2B item 7: hint 3 of a level 3 soundcheck groups thousands in its bold answer (8,400 for the web's 8400)
                bool commas = p.Skill == Fluency.Skill && p.Level == 3 && k == 2
                    && System.Text.RegularExpressions.Regex.Replace(p.Steps[k], "<b>([^<]*)</b>", m => "<b>" + m.Groups[1].Value.Replace(",", "") + "</b>") == js[k];
                Assert.IsTrue(commas || (p.Skill == Fluency.Skill && (p.Kind == "decpow" || p.Kind == "decdiv") && k == 1),
                    at + ": hint " + (k + 1) + " differs from the web game's and isn't an intended difference: " + js[k] + " / " + p.Steps[k]);
                l.Add(new KeyValuePair<string, string>(js[k], p.Steps[k]));
            }
            return l;
        }

        private static List<string> Swap(List<string> ev, List<KeyValuePair<string, string>> swaps)
        {
            if (swaps.Count == 0) return ev;
            return ev.Select(e => swaps.Aggregate(e, (acc, kv) => acc.Replace(kv.Key, kv.Value))).ToList();
        }

        // ---------- ticket sales, replayed ----------

        [Test, TestCaseSource(nameof(TicketCases))]
        public void Tickets_ReplaysTheWebGame(int index)
        {
            JsonValue v = Vectors["tickets"][index];
            Problem p = MultDiv.Generate((int)v["level"].Long, v["seed"].Long);
            Assert.AreEqual(v["kind"].String, p.Kind);
            string at0 = "case " + index + " (" + p.Kind + " L" + p.Level + " seed " + p.Seed + ")";
            JsonValue head = v["head"];
            CollectionAssert.AreEqual(head["board"].Items.Select(x => x.String).ToList(), TicketsLines.Board(p), at0 + ": board");
            Assert.AreEqual(head["lbl"].String, TicketsLines.PromptLabel(p.Kind), at0);
            Assert.AreEqual(head["check"].String, TicketsLines.SellIt, at0);
            Assert.IsEmpty(StepSwaps(head, p, at0));

            var rec = new Rec();
            int stage = 0;
            TicketsWork work = null;
            var round = new TypedMathRound(rec, p, rec, s => rec.Ev.Add("next|" + Math.Round(s * 1000)), n =>
            {
                stage = Math.Max(stage, n);
                if (n >= 2) work = TicketsWork.Draw(p, n);
            });
            var acts = v["actions"].Items; var snaps = v["snaps"].Items;
            for (int i = 0; i < acts.Count; i++)
            {
                string what = acts[i][0].String;
                if (what == "begin") rec.Say(TicketsLines.Intro(p.Kind), "idle");
                else if (what == "hint") rec.Hint();
                else if (what == "type") { string t = acts[i][1].String; rec.Ro = t; round.Submit(t); }
                string at = at0 + " press " + i + " " + what;
                JsonValue s = snaps[i];
                CollectionAssert.AreEqual(RevealExpect.Map(JsEvents(s["ev"], true), "reveal|"), rec.Ev, at + ": events");
                rec.Ev.Clear();
                Assert.AreEqual(s["ro"].String, rec.Ro, at + ": readout");
                Assert.AreEqual(s["nx"].BoolValue, rec.NextShown, at + ": next button");
                SamePaper(s["work"], work, stage, p, at);
            }
        }

        /// <summary>The JS paper against ours; at step 2 the two intended differences (16, 17) are pinned instead.</summary>
        private static void SamePaper(JsonValue js, TicketsWork w, int stage, Problem p, string at)
        {
            if (js.IsNull) { Assert.IsNull(w, at + ": no paper yet"); return; }
            Assert.IsNotNull(w, at + ": paper");
            string kind = js["kind"].String;
            Assert.AreEqual(kind, w.Kind, at);
            long ans = (long)p.Answer.Value.Number;
            Assert.AreEqual(Plain(js["asum"].IsNull ? null : js["asum"].String), Plain(kind == "remainder" ? w.Line : w.Sum), at + ": the line under it");
            if (kind == "area")
            {
                CollectionAssert.AreEqual(js["heads"].Items.Select(x => x.String).ToList(), w.ColHeads.Concat(w.RowHeads).ToList(), at + ": heads");
                List<string> cells = w.Cells.SelectMany(r => r).ToList(), jsCells = js["cells"].Items.Select(x => x.String).ToList();
                if (stage == 2 && cells.Count == 1)
                {
                    Assert.AreEqual(F(ans), jsCells[0], at + ": the web's one-part area model shows the answer at step 2");
                    StringAssert.EndsWith(" " + TicketsLines.Times + " 10", cells[0], at + ": intended difference 16");
                }
                else CollectionAssert.AreEqual(jsCells, cells, at + ": cells");
            }
            else if (kind == "chunks")
            {
                var rows = js["rows"].Items;
                Assert.AreEqual(rows.Count, w.Rows.Count, at + ": chunk rows");
                for (int i = 0; i < rows.Count; i++)
                {
                    string style = w.Rows[i].Style == "take" ? "" : w.Rows[i].Style;
                    Assert.AreEqual(rows[i][0].String, style, at + ": row " + i + " style");
                    Assert.AreEqual(rows[i][1].String, w.Rows[i].Text, at + ": row " + i);
                    Assert.AreEqual(rows[i][2].IsNull ? null : rows[i][2].String, w.Rows[i].Note, at + ": row " + i + " note");
                }
            }
            else
            {
                Assert.AreEqual(js["part"].Number, w.Part, 1e-12, at + ": the last row");
                if (stage == 2)
                {
                    int jsRows = (int)js["full"].Number + (js["more"].IsNull ? 0 : int.Parse(Regex.Match(js["more"].String, @"[\d,]+").Value.Replace(",", ""), CultureInfo.InvariantCulture));
                    Assert.AreEqual(ans, jsRows, at + ": the web's step-2 seat rows count out the answer");
                    Assert.AreEqual(1, w.Full, at + ": intended difference 17"); Assert.IsTrue(w.Gap); Assert.IsNull(w.More);
                }
                else
                {
                    Assert.AreEqual((int)js["full"].Number, w.Full, at + ": full rows");
                    Assert.AreEqual(js["more"].IsNull ? null : js["more"].String, w.More, at + ": more rows");
                }
            }
        }

        [Test]
        public void Tickets_Vectors_CoverEveryKindAndBothDifferences()
        {
            var kinds = new HashSet<string>(Vectors["tickets"].Items.Select(v => v["kind"].String));
            CollectionAssert.AreEquivalent(new[] { "mul", "div", "rows" }, kinds);
            int oneCell = 0, rows2 = 0;
            foreach (JsonValue v in Vectors["tickets"].Items)
                foreach (JsonValue s in v["snaps"].Items)
                {
                    if (s["work"].IsNull) continue;
                    if (s["work"]["kind"].String == "area" && s["work"]["cells"].Count == 1 && s["work"]["asum"].IsNull) oneCell++;
                    if (s["work"]["kind"].String == "remainder" && !s["work"]["asum"].String.Contains("full rows of")) rows2++;
                }
            Assert.Greater(oneCell, 0, "a one-part area model at step 2 is replayed");
            Assert.Greater(rows2, 0, "seat rows at step 2 are replayed");
        }

        // ---------- tour budget, replayed ----------

        [Test, TestCaseSource(nameof(BudgetCases))]
        public void Budget_ReplaysTheWebGame(int index)
        {
            JsonValue v = Vectors["budget"][index];
            Problem p = OrderOps.Generate((int)v["level"].Long, v["seed"].Long);
            Assert.AreEqual(v["kind"].String, p.Kind);
            string at0 = "case " + index + " (" + p.Kind + " L" + p.Level + " seed " + p.Seed + ")";
            var rec = new Rec();
            var game = new BudgetRound(p, rec, rec);
            var round = new TypedMathRound(rec, p, rec, s => rec.Ev.Add("next|" + Math.Round(s * 1000)), game.Step);
            game.Ladder = round.Ladder;
            if (!game.Story) rec.PadShown = true;
            JsonValue head = v["head"];
            Assert.AreEqual(head["lbl"].String, BudgetLines.PromptLabel(game.Story), at0);
            Assert.AreEqual(head["prompt"].String, game.Story ? p.Prompt : IntendedCopy.ToWeb(BudgetLines.WhatComes), at0);
            Assert.AreEqual(head["check"].String, BudgetLines.BalanceIt, at0);
            Assert.IsEmpty(StepSwaps(head, p, at0));
            CollectionAssert.AreEqual(head["choices"].Items.Select(x => x.String).ToList(), game.Story ? p.Choices.Select(c => c.Text).ToList() : new List<string>(), at0);
            int nChoices = game.Story ? p.Choices.Count : 0;
            var acts = v["actions"].Items; var snaps = v["snaps"].Items;
            for (int i = 0; i < acts.Count; i++)
            {
                string what = acts[i][0].String;
                if (what == "begin") rec.Say(BudgetLines.Intro(game.Story), "idle");
                else if (what == "hint") rec.Hint();
                else if (what == "pick") game.Pick((int)acts[i][1].Number);
                else if (what == "type") { string t = acts[i][1].String; rec.Ro = t; round.Submit(t); }
                string at = at0 + " press " + i + " " + what;
                JsonValue s = snaps[i];
                CollectionAssert.AreEqual(RevealExpect.Map(JsEvents(s["ev"], true), "reveal|"), rec.Ev, at + ": events");
                rec.Ev.Clear();
                Assert.AreEqual(s["ro"].String, rec.Ro, at + ": readout");
                Assert.AreEqual(s["nx"].BoolValue, rec.NextShown, at + ": next button");
                Assert.AreEqual(s["pad"].BoolValue, rec.PadShown, at + ": keypad shown");
                CollectionAssert.AreEqual(s["choices"].Items.Select(x => x.String).ToList(), rec.Choices(nChoices), at + ": choices");
                List<SheetLine> sheet = game.Sheet();
                var js = s["sheet"].Items;
                Assert.AreEqual(js.Count, sheet.Count, at + ": sheet lines");
                for (int k = 0; k < js.Count; k++)
                {
                    string cls = sheet[k].Question ? "q" : sheet[k].Final ? "final" : "";
                    Assert.AreEqual(js[k][0].String, cls, at + ": line " + k + " style");
                    Assert.AreEqual(js[k][1].String, sheet[k].Text, at + ": line " + k);
                    // intended difference 32: a story's first step is highlighted at hint 2 (the web's pattern found nothing there)
                    if (game.Story && js[k][2].String == "" && sheet[k].Mark != "")
                    {
                        Match fm = Regex.Match(p.Steps[1], "first: ([^<]*?) = ");
                        Assert.IsTrue(fm.Success, at); Assert.AreEqual(fm.Groups[1].Value, sheet[k].Mark, at + ": line " + k + " highlighter");
                    }
                    else Assert.AreEqual(js[k][2].String, sheet[k].Mark, at + ": line " + k + " highlighter");
                }
            }
        }

        // ---------- soundcheck, replayed ----------

        [Test, TestCaseSource(nameof(SoundcheckCases))]
        public void Soundcheck_ReplaysTheWebGame_MeterAndBonusToo(int index)
        {
            JsonValue v = Vectors["soundcheck"][index];
            Problem p = Fluency.Generate((int)v["level"].Long, v["seed"].Long);
            Assert.AreEqual(v["kind"].String, p.Kind);
            string at0 = "case " + index + " (" + p.Kind + " L" + p.Level + " seed " + p.Seed + ")";
            JsonValue head = v["head"];
            Assert.AreEqual(head["tag"].String, SoundcheckLines.DeskTag, at0);
            CollectionAssert.AreEqual(head["labels"].Items.Select(x => x.String).ToList(), p.Choices.Select(c => SoundcheckLines.Label(p, c)).ToList(), at0 + ": choice labels");

            var swaps = StepSwaps(head, p, at0);
            var rec = new Rec();
            var rng = new Rng(v["rseed"].Long);
            var game = new SoundcheckRound(p, new BonusSong(rec, x => rec.Ev.Add("win|" + x)), rec, rng.Next, s => rec.Ev.Add("next|" + Math.Round(s * 1000)));
            long clockMs = 0;
            bool ticked = false;
            var acts = v["actions"].Items; var snaps = v["snaps"].Items;
            for (int i = 0; i < acts.Count; i++)
            {
                string what = acts[i][0].String;
                if (what == "begin") rec.Say(IntendedCopy.ToWeb(SoundcheckLines.Intro), "idle");
                else if (what == "hint") rec.Hint();
                else if (what == "pick") game.Pick((int)acts[i][1].Number);
                else if (what == "wait")
                {
                    long end = clockMs + (long)acts[i][1].Number;
                    // setInterval(100): a tick at every 100 ms since the song was drawn, while the meter runs
                    for (long t = (clockMs / 100 + 1) * 100; t <= end; t += 100) { if (game.Meter.Running) ticked = true; game.Meter.Tick(t / 1000.0); }
                    clockMs = end;
                }
                string at = at0 + " press " + i + " " + what;
                JsonValue s = snaps[i];
                CollectionAssert.AreEqual(RevealExpect.Map(Swap(JsEvents(s["ev"], false), swaps), "reveal|"), rec.Ev, at + ": events");
                rec.Ev.Clear();
                Assert.AreEqual(s["nx"].BoolValue, rec.NextShown, at + ": next button");
                CollectionAssert.AreEqual(s["choices"].Items.Select(x => x.String).ToList(), rec.Choices(p.Choices.Count), at + ": choices");
                bool outNow = !game.Meter.Beatable;
                Assert.AreEqual(s["out"].BoolValue, outNow, at + ": meter out");
                Assert.AreEqual(s["label"].String, outNow ? SoundcheckLines.Out : game.Meter.Shown + SoundcheckLines.SecFor, at + ": meter label");
                if (!s["width"].IsNull) { Assert.IsTrue(ticked, at); Assert.AreEqual(double.Parse(s["width"].String.TrimEnd('%'), CultureInfo.InvariantCulture), game.Meter.Fill * 100, 1e-9, at + ": meter"); }
                else Assert.IsFalse(ticked, at + ": the meter hasn't ticked yet");
            }
        }

        [Test]
        public void Soundcheck_Vectors_CoverBonusOutOfTimeCrossOutAndReveal()
        {
            string all = string.Join("\n", Vectors["soundcheck"].Items.SelectMany(v => v["snaps"].Items).SelectMany(s => JsEvents(s["ev"], false)));
            StringAssert.Contains("win|300", all);
            StringAssert.Contains("win|0", all);
            StringAssert.Contains("say|" + SoundcheckLines.Beat + "|cheer", all);
            StringAssert.Contains("tick", all);
            StringAssert.Contains("reveal|", all);
            Assert.IsTrue(Vectors["soundcheck"].Items.Any(v => v["snaps"].Items.Any(s => s["out"].BoolValue)), "a meter runs out");
        }

        // ---------- small rules ----------

        [Test]
        public void Soundcheck_Meter_OnlyDecidesTheBonus()
        {
            var m = new SoundcheckMeter(8);
            Assert.AreEqual(8, m.Shown); Assert.AreEqual(300, m.BonusNow);
            m.Tick(.1); Assert.AreEqual(8, m.Shown); Assert.AreEqual(98.75, m.Fill * 100, 1e-9);
            m.Tick(7.9); Assert.AreEqual(1, m.Shown); Assert.AreEqual(300, m.BonusNow);
            Assert.IsTrue(m.Tick(8)); Assert.IsFalse(m.Beatable); Assert.AreEqual(0, m.BonusNow); Assert.AreEqual(0, m.Shown);
            Assert.IsFalse(m.Tick(9), "it stops once out");

            // running out is no miss and no lost fans; a right answer after it still wins, with no bonus
            Problem p = Fluency.Generate(1, 5);
            var rec = new Rec();
            var g = new SoundcheckRound(p, new BonusSong(rec, x => rec.Ev.Add("win|" + x)), rec, new Rng(1).Next, s => { });
            g.Meter.Tick(20);
            Assert.AreEqual(0, rec.Tries);
            Assert.IsFalse(rec.Ev.Any(e => e.StartsWith("miss", StringComparison.Ordinal) || e.StartsWith("say", StringComparison.Ordinal)));
            Assert.AreEqual(SoundcheckPick.Right, g.Pick(g.RightIndex));
            CollectionAssert.Contains(rec.Ev, "win|0");

            // a miss and a hint leave the meter running
            var rec2 = new Rec();
            var g2 = new SoundcheckRound(p, new BonusSong(rec2, x => rec2.Ev.Add("win|" + x)), rec2, new Rng(2).Next, s => { });
            g2.Pick(Enumerable.Range(0, p.Choices.Count).First(i => !p.Choices[i].Correct));
            rec2.Hint();
            Assert.IsTrue(g2.Meter.Running);
            g2.Meter.Tick(1);
            Assert.AreEqual(SoundcheckPick.Right, g2.Pick(g2.RightIndex));
            CollectionAssert.Contains(rec2.Ev, "win|300");
        }

        [Test]
        public void Lines_AreTheWebGames()
        {
            Assert.AreEqual("work it out on paper if you want. i" + (char)0x2019 + "ll wait.", TicketsLines.Intro("mul"));
            Assert.AreEqual("careful: only full rows count.", TicketsLines.Intro("rows"));
            Assert.AreEqual("parentheses first. then " + (char)0xD7 + " and " + (char)0xF7 + ". then + and " + (char)0x2212 + ".", BudgetLines.Intro(false));
            Assert.AreEqual("yes: <b>3 + 4</b>. now work it out.", BudgetLines.Yes("3 + 4"));
            Assert.AreEqual(" sec for +300 bonus fans", SoundcheckLines.SecFor);
            Assert.AreEqual("beat the meter. <b>+300</b> bonus fans.", SoundcheckLines.Beat);
        }

        // ---------- what the papers and sheets claim (QUESTIONS 8 and 9) ----------

        private static IEnumerable<long> Seeds(int n) { for (int i = 0; i < n; i++) yield return 1 + i * 7919L; }

        private static bool HasNumber(string text, long n)
        {
            string t = Plain(text) ?? "";
            return Regex.Matches(t, @"\d[\d,]*").Cast<Match>().Any(m => m.Value.Replace(",", "") == n.ToString(CultureInfo.InvariantCulture));
        }

        [Test]
        public void Tickets_Step2Paper_NeverShowsTheAnswer_AgreesWithTheText_AndTheWorkedPaperDoes()
        {
            int n = 0, oneCell = 0;
            foreach (int level in new[] { 1, 2, 3 })
                foreach (long seed in Seeds(3000))
                {
                    Problem p = MultDiv.Generate(level, seed);
                    long ans = (long)p.Answer.Value.Number;
                    string at = p.Kind + " L" + level + " seed " + seed;
                    TicketsWork w2 = TicketsWork.Draw(p, 2), w3 = TicketsWork.Draw(p, 3);
                    Assert.IsNull(TicketsWork.Draw(p, 1), at + ": nothing at step 1");
                    if (w2.Kind == "area")
                    {
                        foreach (string c in w2.Cells.SelectMany(r => r))
                        {
                            Assert.IsFalse(HasNumber(c, ans), at + ": a step-2 cell shows the answer: " + c);
                            // every cell is in the step-2 text: "9 x 90 = 810, 9 x 3 = 27" or "36 x 7 = 252. now make that ten times bigger"
                            StringAssert.Contains(c.Split(' ')[0], p.Steps[1], at + ": the cell agrees with the text");
                        }
                        if (w2.Cells.Count == 1 && w2.Cells[0].Count == 1) oneCell++;
                        Assert.IsNull(w2.Sum, at);
                        Assert.IsTrue(HasNumber(w3.Sum, ans) && w3.Sum.Contains("<b>" + F(ans) + "</b>"), at + ": the worked sum is the answer");
                    }
                    else if (w2.Kind == "chunks")
                    {
                        Assert.AreEqual(3, w2.Rows.Count, at + ": one chunk at step 2");
                        string total = w2.Rows[2].Note;
                        Assert.IsFalse(HasNumber(total.Substring(total.IndexOf("total", StringComparison.Ordinal)), ans), at + ": the running total at step 2 isn't the answer");
                        StringAssert.Contains("running total: " + total.Substring(total.IndexOf("total ", StringComparison.Ordinal) + 6) + ".", p.Steps[1], at + ": the same running total as the text");
                        StringAssert.Contains(w2.Rows[2].Text, p.Steps[1], at + ": the same amount left as the text");
                        Assert.AreEqual("total", w3.Rows.Last().Style);
                        StringAssert.EndsWith(" = " + F(ans), w3.Rows.Last().Text, at + ": the worked total is the answer");
                    }
                    else
                    {
                        Assert.AreEqual(1, w2.Full, at); Assert.IsTrue(w2.Gap); Assert.IsNull(w2.More);
                        // the web's line names the seats per row; skip when that number happens to equal the answer (d = q)
                        if ((long)p.Work["d"].Long != ans) Assert.IsFalse(HasNumber(w2.Line, ans), at + ": the step-2 line doesn't name the rows");
                        Assert.AreEqual(Math.Min(ans, 12), w3.Full, at);
                        Assert.AreEqual(ans, w3.Full + (w3.More == null ? 0 : long.Parse(Regex.Match(w3.More, @"[\d,]+").Value.Replace(",", ""), CultureInfo.InvariantCulture)), at + ": the worked rows count to the answer");
                        StringAssert.StartsWith("<b>" + F(ans) + "</b> full rows", w3.Line, at);
                    }
                    n++;
                }
            Assert.AreEqual(9000, n);
            Assert.Greater(oneCell, 100, "one-part area models are covered");
        }

        [Test]
        public void Budget_Step2Sheet_HighlightsTheTextsFirstStep_AndLeavesTheAnswerToHer()
        {
            int n = 0;
            foreach (int level in new[] { 1, 2, 3 })
                foreach (long seed in Seeds(3000))
                {
                    Problem p = OrderOps.Generate(level, seed);
                    string at = p.Kind + " L" + level + " seed " + seed;
                    List<string> st = p.Params["states"].Items.Select(x => x.String).ToList();
                    string ans = JsNumber.Str(p.Answer.Value.Number);
                    Assert.AreEqual(1, BudgetRound.Sheet(p, 1).Count, at);
                    List<SheetLine> s2 = BudgetRound.Sheet(p, 2);
                    Assert.AreEqual(2, s2.Count, at);
                    Assert.Greater(st.Count, 2, at + ": the second line is never the last");
                    Assert.AreNotEqual("= " + ans, s2[1].Text, at + ": step 2 doesn't reach the answer");
                    if (p.Kind == "story")
                    {
                        // "it's <b>(28 + 10) / 2</b>. first: 28 + 10 = 38.": the first step is highlighted (P3, intended difference 32;
                        // the web's pattern found nothing here), and never the final value
                        Match f = Regex.Match(p.Steps[1], @"first: (.*) = ([^ .<]+)\.");
                        Assert.IsTrue(f.Success && s2[0].Mark == f.Groups[1].Value, at + ": the highlighter is on the story's first step: " + s2[0].Mark);
                        Assert.AreNotEqual(ans, s2[0].Mark, at);
                        Assert.IsTrue(f.Success, at + ": " + p.Steps[1]);
                        StringAssert.Contains(f.Groups[2].Value, s2[1].Text, at + ": the text's first result is on the next line");
                    }
                    else
                    {
                        Match m = Regex.Match(p.Steps[1], "<b>(.*?) = ([^<]*)</b>");
                        Assert.IsTrue(m.Success, at + ": " + p.Steps[1]);
                        Assert.AreEqual(m.Groups[1].Value, s2[0].Mark, at + ": the highlighter is on the step the text names");
                        StringAssert.Contains(m.Groups[2].Value.Trim().TrimEnd('.'), s2[1].Text, at + ": and its result is on the next line");
                    }
                    List<SheetLine> s3 = BudgetRound.Sheet(p, 3);
                    Assert.AreEqual("= " + ans, s3.Last().Text, at + ": the worked sheet ends on the answer");
                    Assert.IsTrue(s3.Last().Final);
                    n++;
                }
            Assert.AreEqual(9000, n);
        }

        [Test]
        public void Soundcheck_Hint2_CrossesOutAWrongChoice_AndNeverTheRightOne()
        {
            int crossed = 0;
            foreach (int level in new[] { 1, 2, 3 })
                foreach (long seed in Seeds(1000))
                {
                    Problem p = Fluency.Generate(level, seed);
                    var rec = new Rec();
                    var g = new SoundcheckRound(p, new BonusSong(rec, x => { }), rec, new Rng(seed).Next, s => { });
                    rec.Hint(); rec.Hint();
                    string at = p.Kind + " L" + level + " seed " + seed;
                    Assert.IsFalse(g.IsOff(g.RightIndex), at + ": the right choice stays");
                    int off = Enumerable.Range(0, p.Choices.Count).Count(g.IsOff);
                    Assert.LessOrEqual(off, 1, at);
                    Assert.GreaterOrEqual(p.Choices.Count - off, 2, at + ": at least two choices are left to think about");
                    crossed += off;
                    if (p.Kind == "decpow" || p.Kind == "decdiv")
                        StringAssert.Contains(" moves every digit ", p.Steps[1], at + ": answer 14's place-value hint");
                }
            Assert.Greater(crossed, 2000);
        }
    }
}
