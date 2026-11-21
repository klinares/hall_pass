using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Shows;
using WorldTour.Game.Mascot;

namespace WorldTour.Mascot.Tests
{
    public class MascotLineTests
    {
        static readonly string R = ((char)0x2019).ToString();     // the web's apostrophe

        sealed class Counter { public int N; readonly double _v; public Counter(double v) { _v = v; } public double Next() { N++; return _v; } }

        static string[] AllPooled() =>
            DogLines.Cheers.Concat(DogLines.ShowStart).Concat(DogLines.Win).Concat(DogLines.HintWin).Concat(DogLines.AfterMiss)
                .Concat(DogLines.NextSong).Concat(new[] { DogLines.BonusSong, DogLines.IdleHint }).ToArray();

        [Test]
        public void ExactTextWithCurlyApostrophes()
        {
            Assert.AreEqual("sound check done. let" + R + "s go.", DogLines.ShowStart[0]);
            Assert.AreEqual("i" + R + "ll be right here if you need a hint.", DogLines.ShowStart[3]);
            Assert.AreEqual("that one" + R + "s going on the poster.", DogLines.Win[2]);
            Assert.AreEqual("this one again. you" + R + "ve got it now.", DogLines.BonusSong);
            Assert.AreEqual("crowd" + R + "s warmed up.", DogLines.NextSong[2]);
            Assert.AreEqual(7, DogLines.Cheers.Count); Assert.AreEqual(4, DogLines.ShowStart.Count); Assert.AreEqual(5, DogLines.Win.Count);
            Assert.AreEqual(3, DogLines.HintWin.Count); Assert.AreEqual(3, DogLines.AfterMiss.Count); Assert.AreEqual(4, DogLines.NextSong.Count);
            foreach (var s in AllPooled()) { Assert.IsFalse(s.Contains("'"), s); Assert.AreEqual(s.ToLowerInvariant(), s, "lowercase: " + s); }
        }

        [Test]
        public void PickUsesFloorOfRandomTimesLength()
        {
            Assert.AreEqual("clean.", DogLines.PickWin(2, () => 0));
            Assert.AreEqual("easy work.", DogLines.PickWin(2, () => .999));
            Assert.AreEqual("nailed it.", DogLines.PickWin(0, () => .2));          // floor(.2 * 5) = 1
            var c = new Counter(.5);
            Assert.AreEqual("4 in a row. the crowd is losing it.", DogLines.PickWin(4, c.Next));
            Assert.AreEqual(0, c.N);
            Assert.AreEqual("got there. streak" + R + "s safe.", DogLines.ForWin(WinKind.HintOnly, 0, () => 0));
            Assert.AreEqual("second take was the keeper.", DogLines.ForWin(WinKind.AfterMiss, 0, () => .9));
        }

        [Test]
        public void ToastFollowsShowJs()
        {
            var c = new Counter(0);
            Assert.AreEqual("3 in a row!", DogLines.Toast(true, 3, c.Next)); Assert.AreEqual(0, c.N);
            Assert.AreEqual("iconic", DogLines.Toast(true, 2, () => 0));
            Assert.AreEqual("main character", DogLines.Toast(false, 9, () => .99));
        }

        [Test]
        public void HomeLines()
        {
            var c = new Counter(0);
            Assert.AreEqual("oh hey. i napped for 3 days. ready when you are.", DogLines.Home("record store", "12,000", 5, 3, c.Next));
            Assert.AreEqual(0, c.N);
            Assert.AreEqual("next stop: <b>record store</b>. 12,000 fans to go.", DogLines.Home("record store", "12,000", 0, 2, () => 0));
            Assert.AreEqual("we made it to the arena. every show from here is a victory lap.", DogLines.Home(null, "0", 0, null, () => .2));
            Assert.AreEqual("2 days in a row. don" + R + "t break the chain.", DogLines.Home(null, "0", 2, 0, () => .7));
            Assert.AreEqual("one show a day keeps the streak alive.", DogLines.Home(null, "0", 1, 0, () => .7));
        }

        static string WebFile(string rel)
        {
            foreach (var root in new[] { Path.Combine(Directory.GetCurrentDirectory(), ".."), Directory.GetCurrentDirectory() })
            {
                string p = Path.GetFullPath(Path.Combine(root, "world_tour", "js", rel));
                if (File.Exists(p)) return File.ReadAllText(p);
            }
            return null;
        }

        [Test]
        public void EveryLineIsInTheWebSource()
        {
            string copy = WebFile("copy.js"), show = WebFile("show.js");
            if (copy == null || show == null) Assert.Ignore("world_tour/js not found next to the project");
            foreach (var s in DogLines.Cheers.Concat(DogLines.ShowStart).Concat(DogLines.Win).Concat(DogLines.HintWin).Concat(DogLines.AfterMiss))
                StringAssert.Contains("'" + s + "'", copy);
            foreach (var s in DogLines.NextSong) StringAssert.Contains("'" + s + "'", show);
            StringAssert.Contains("`" + DogLines.BonusSong + "`", show);
            StringAssert.Contains("`" + DogLines.IdleHint.Replace("tap hint", "tap the bulb") + "`", copy);   // 7.4 intended copy difference: "tap hint" (IntendedDifferences.md)
            StringAssert.Contains("${streak} in a row. the crowd is losing it.", copy);
            StringAssert.Contains("`${G.streak} in a row!`", show);
            StringAssert.Contains("export const NAME = '" + DogLines.Name + "'", copy);
            // (the ladder and reveal lines are RemixLines'; RemixRulesTests checks them)
            foreach (var f in new[] { "`oh hey. i napped for ${away} days. ready when you are.`", "`next stop: <b>${next}</b>. ${toGo} fans to go.`",
                                      "'we made it to the arena. every show from here is a victory lap.'", "`${streak} days in a row. don" + R + "t break the chain.`",
                                      "'one show a day keeps the streak alive.'" })
                StringAssert.Contains(f, copy);
        }
    }
}
