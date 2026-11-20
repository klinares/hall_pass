using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Maths;
using WorldTour.Game.Modes;

namespace WorldTour.Game.EditTests
{
    /// <summary>
    /// remix's rules (world_tour/modes/spelling/remix.js) as plain classes. The deals were printed by remix.js:19-22 and
    /// util.js tilt, copied verbatim into b25_deal.mjs (the 2.5 builder's scratchpad) with Math.random = mulberry32(seed).
    /// </summary>
    public class RemixRulesTests
    {
        private static readonly string Rsq = ((char)0x2019).ToString();

        public static readonly object[][] JsDeals =
        {
            new object[] { "because", 1, 1, "aubeesc", "", "-2 -2.8 -0.1 -3.5 -0.8 2.1 -1.7", 0.19087357842363417 },
            new object[] { "because", 2, 2, "tacseube", "t", "-2.9 1.4 -1.1 3.8 -3.7 3.3 -3.5 -3", 0.19193362118676305 },
            new object[] { "because", 3, 3, "slteabeuc", "lt", "-1.4 1.5 -1.7 1.4 0.2 2.7 0.3 -3.9 3", 0.31039701169356704 },
            new object[] { "illegal", 3, 42, "elaiodllg", "od", "0.9 -4 -0.2 2.7 -3.6 0.7 -3.7 -1.9 -3.5", 0.18568900716491044 },
            new object[] { "irrational", 2, 7, "oanlairctri", "c", "-1.7 0.3 -1.6 3.8 -2 3 -2.5 -2.9 -2.8 -1.7 0.4", 0.7618736950680614 },
            new object[] { "friend", 1, 99, "ndfrei", "", "1.8 -2 -2.5 -0.3 2.1 -0.1", 0.2729956773109734 },
            new object[] { "ab", 1, 5, "ba", "", "-0.3 -1.6", 0.8965497829485685 },
            new object[] { "committee", 3, 20261120, "iolmmdetcet", "dl", "-1.4 -0.7 2.8 2 -3 -3.7 0 3.7 -0.4 1.9 -0.8", 0.798214323585853 },
        };

        [Test, TestCaseSource(nameof(JsDeals))]
        public void Deal_MatchesTheWebGame(string word, int level, int seed, string mix, string decoys, string tilts, double nextAfter)
        {
            var r = new Rng(seed);
            RemixDeal d = RemixRules.Deal(word, level, r.Next);
            Assert.AreEqual(mix, new string(d.Mix.ToArray()));
            Assert.AreEqual(decoys, new string(d.Decoys.ToArray()));
            float[] want = tilts.Split(' ').Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray();
            Assert.AreEqual(want.Length, d.Tilts.Count);
            for (int i = 0; i < want.Length; i++) Assert.AreEqual(want[i], d.Tilts[i], 1e-4, "tilt " + i);
            Assert.AreEqual(nextAfter, r.Next(), 0.0, "used exactly as many random numbers as the JS");
        }

        [Test]
        public void Deal_NeverSpellsTheWord_DecoysByLevel()
        {
            var r = new Rng(77);
            foreach (string w in new[] { "because", "friend", "island", "necessary", "separate", "ab", "aab" })
                for (int level = 1; level <= 3; level++)
                    for (int k = 0; k < 50; k++)
                    {
                        RemixDeal d = RemixRules.Deal(w, level, r.Next);
                        Assert.AreNotEqual(w, new string(d.Mix.ToArray()), w);
                        Assert.AreEqual(level - 1, d.Decoys.Count);
                        Assert.IsTrue(d.Decoys.All(c => w.IndexOf(c) < 0 && RemixRules.DecoyLetters.IndexOf(c) >= 0));
                        Assert.AreEqual(d.Decoys.Count, d.Decoys.Distinct().Count());
                        CollectionAssert.AreEquivalent(w.Concat(d.Decoys), d.Mix);
                        Assert.IsTrue(d.Tilts.All(t => t >= -4 && t <= 4));
                    }
        }

        [Test]
        public void Deal_ARackThatCantDiffer_IsAcceptedNotLoopedOn()
        {
            // the JS loops forever on these; nothing in a real week is this short
            Assert.AreEqual("a", new string(RemixRules.Deal("a", 1, new Rng(1).Next).Mix.ToArray()));
            Assert.AreEqual("aa", new string(RemixRules.Deal("aa", 1, new Rng(1).Next).Mix.ToArray()));
            Assert.AreNotEqual("a", new string(RemixRules.Deal("a", 2, new Rng(1).Next).Mix.ToArray()), "a decoy makes it possible");
        }

        [Test]
        public void HintMax_AtLeastTwo_OnePerThreeLetters()
        {
            Assert.AreEqual(2, RemixRules.HintMax("cat"));
            Assert.AreEqual(2, RemixRules.HintMax("friend"));
            Assert.AreEqual(3, RemixRules.HintMax("because"));
            Assert.AreEqual(4, RemixRules.HintMax("irrational"));
            Assert.AreEqual(5, RemixRules.HintMax("responsibility".Substring(0, 13)));
        }

        // ---------- the board ----------

        private static RemixBoard Board(string word, string mix)
        {
            return new RemixBoard(word, mix.ToList());
        }

        [Test]
        public void Board_PickUndoClear_AndCompletion()
        {
            var b = Board("cat", "tac");
            Assert.AreEqual(PickResult.Placed, b.Pick(2));                // c
            Assert.AreEqual(PickResult.Ignored, b.Pick(2), "a used tile");
            Assert.AreEqual(PickResult.Placed, b.Pick(1));                // a
            Assert.IsTrue(b.Undo());
            Assert.AreEqual("c", b.Built);
            Assert.IsTrue(b.Clear());
            Assert.IsFalse(b.Clear(), "nothing to clear");
            Assert.IsFalse(b.Undo());
            b.Pick(2); b.Pick(1);
            Assert.AreEqual(PickResult.Complete, b.Pick(0));
            Assert.IsTrue(b.IsRight);
            Assert.AreEqual(PickResult.Ignored, b.Pick(0), "full");
            b.Busy = true;
            Assert.IsFalse(b.Undo(), "busy");
            b.Busy = false;
            b.ResetAfterMiss();
            Assert.AreEqual("", b.Built);
        }

        [Test]
        public void Board_AfterAWrongTry_TilesAreClickableAgain_AndAPickLands()
        {
            // RemixMode.Check: a full wrong word -> Busy while the slots wobble -> FinishMissReset -> Paint, which sets
            // each LetterTile.interactable from TileInteractable. The tiles' click and drag go through Pick.
            var b = Board("cat", "tac");
            b.Pick(0); b.Pick(1); b.Pick(2);
            Assert.IsFalse(b.IsRight);
            b.Busy = true;
            for (int i = 0; i < b.Mix.Count; i++) Assert.IsFalse(b.TileInteractable(i), "no picks while the miss plays out");
            Assert.AreEqual(PickResult.Ignored, b.Pick(0));
            b.FinishMissReset();
            Assert.IsFalse(b.Busy);
            Assert.AreEqual("", b.Built);
            for (int i = 0; i < b.Mix.Count; i++) Assert.IsTrue(b.TileInteractable(i), "tile " + i + " must be clickable after the reset");
            Assert.AreEqual(PickResult.Placed, b.Pick(2), "a clicked tile lands in the first slot");
            Assert.AreEqual("c", b.Built);
            Assert.IsFalse(b.TileInteractable(2), "a tile in a slot is not clickable");
            b.Pick(1);
            Assert.AreEqual(PickResult.Complete, b.Pick(0));
            Assert.IsTrue(b.IsRight);
        }

        [Test]
        public void Board_WrongOrder_IsNotRight()
        {
            var b = Board("cat", "tac");
            b.Pick(0); b.Pick(1);
            Assert.AreEqual(PickResult.Complete, b.Pick(2));
            Assert.AreEqual("tac", b.Built);
            Assert.IsFalse(b.IsRight);
        }

        [Test]
        public void Hint_ClearsWrongLetters_GlowsTheNext_ThenIsFreeWhileGlowing()
        {
            var b = Board("because", "aubeesc");
            b.Pick(2);                                                    // b (right)
            b.Pick(0);                                                    // a (wrong: next is e)
            RemixHint h = b.Hint();
            Assert.IsTrue(h.Counted);
            Assert.IsTrue(h.HadWrong);
            Assert.AreEqual("b", b.Built, "the right start is kept");
            Assert.AreEqual(3, h.Glow, "the first unused e");
            Assert.AreEqual("i cleared the wrong letters. tap the glowing tile next.", h.Line);

            RemixHint again = b.Hint();
            Assert.IsFalse(again.Counted, "pressing again while one glows is free");
            Assert.AreEqual("that glowing tile is still your next letter. tap it.", again.Line);
            Assert.AreEqual(3, b.Glow);

            b.Pick(3);
            Assert.AreEqual(-1, b.Glow, "a pick repaints and the glow goes");
            RemixHint next = b.Hint();
            Assert.IsTrue(next.Counted);
            Assert.IsFalse(next.HadWrong);
            Assert.AreEqual(6, next.Glow, "c");
            Assert.AreEqual("tap the glowing tile. that" + Rsq + "s your next letter.", next.Line);
        }

        [Test]
        public void Hint_WhileBusy_DoesNothing()
        {
            var b = Board("cat", "tac");
            b.Busy = true;
            RemixHint h = b.Hint();
            Assert.IsFalse(h.Counted);
            Assert.IsNull(h.Line);
            Assert.AreEqual(-1, b.Glow);
        }

        [Test]
        public void Hint_OnAnEmptyBoard_GlowsTheFirstLetter()
        {
            var b = Board("cat", "tca");
            RemixHint h = b.Hint();
            Assert.AreEqual(1, h.Glow);
            Assert.IsFalse(h.HadWrong);
        }

        [Test]
        public void Keys_PreferTheGlowingTile_ThenTheFirstUnused()
        {
            var b = Board("because", "aubeesc");
            Assert.AreEqual(2, b.TileForKey('b'));
            Assert.AreEqual(2, b.TileForKey('B'), "either case");
            Assert.AreEqual(-1, b.TileForKey('z'));
            Assert.AreEqual(3, b.TileForKey('e'));
            b.Pick(2);
            RemixHint h = b.Hint();                                       // glows tile 3 (the first e)
            Assert.AreEqual(3, h.Glow);
            b.Pick(3);
            b.Pick(6);                                                    // c
            Assert.AreEqual(0, b.TileForKey('a'));
            Assert.AreEqual(4, b.TileForKey('e'), "the remaining e");
        }

        // ---------- lines ----------

        [Test]
        public void Ladder_TryOneTwo_AsTheWebGame()
        {
            string ldq = ((char)0x201C).ToString(), rdq = ((char)0x201D).ToString();
            int draws = 0;
            Func<double> low = () => { draws++; return .2; };
            Func<double> high = () => { draws++; return .7; };
            Assert.AreEqual("not quite. it" + Rsq + "s <b>7 letters</b> and starts with <b>" + ldq + "b" + rdq + "</b>.", RemixLines.Ladder("because", "be-cause", 1, low));
            Assert.AreEqual(0, draws, "no random value on the first try");
            Assert.AreEqual("keep going, natalia. sound it out: <b>be - cause</b>", RemixLines.Ladder("because", "be-cause", 2, low));
            Assert.AreEqual("keep going. sound it out: <b>be - cause</b>", RemixLines.Ladder("because", "be-cause", 2, high));
            Assert.AreEqual(2, draws, "one value on the second try");
            Assert.AreEqual("keep going. sound it out: <b>ir - ___ - ___ - al</b>", RemixLines.Ladder("irrational", "ir-ra-tion-al", 3, high));
            Assert.AreEqual(2, draws, "none after the second try");
        }

        [Test]
        public void Lines_DecoyNote_HintButton_Reveal()
        {
            Assert.IsNull(RemixLines.DecoyNote(0));
            Assert.AreEqual("one letter doesn" + Rsq + "t belong. leave it on the rack.", RemixLines.DecoyNote(1));
            Assert.AreEqual("two letters don" + Rsq + "t belong. leave them on the rack.", RemixLines.DecoyNote(2));
            Assert.AreEqual("hint <size=70%>(3)</size>", RemixLines.HintButton(3));
            Assert.AreEqual("no hints left", RemixLines.HintButton(0));
            Assert.AreEqual("it" + Rsq + "s <b>because</b>. look closely, it" + Rsq + "ll come back around.", RemixLines.Reveal("because"));
        }

        [Test]
        public void Lines_AreLowercase()
        {
            foreach (string s in new[] { RemixLines.HintCleared, RemixLines.HintGlow, RemixLines.HintStillGlowing, RemixLines.DecoyNote(2), RemixLines.Sub, RemixLines.DeskTag })
                Assert.AreEqual(s.ToLowerInvariant(), s);
        }
    }
}
