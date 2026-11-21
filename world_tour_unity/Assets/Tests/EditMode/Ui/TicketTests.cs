using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using WorldTour.Core.Data;
using WorldTour.Core.Meta;
using WorldTour.Core.Shows;
using WorldTour.Game.Ticket;

namespace WorldTour.Game.EditTests
{
    /// <summary>
    /// The ticket screen's pure parts (task 2.6): the barcode, the copy, the percent thresholds and the setlist tags,
    /// checked against the real world_tour/screens/ticket.js through ticket_vectors.txt (Tools/ticket_vectors.mjs).
    /// </summary>
    public class TicketTests
    {
        /// <summary>Next to this file; WT_TICKET_VECTORS overrides it (for running the assembly outside the Editor).</summary>
        static string VectorPath
        {
            get { return Environment.GetEnvironmentVariable("WT_TICKET_VECTORS") ?? Path.Combine(Application.dataPath, "Tests", "EditMode", "Ui", "ticket_vectors.txt"); }
        }

        sealed class Case
        {
            public string Name;
            public readonly Dictionary<string, string> In = new Dictionary<string, string>();
            public readonly Dictionary<string, string> Out = new Dictionary<string, string>();
        }

        static List<Case> s_Cases;
        static List<KeyValuePair<string, string>> s_Seeds;

        static void Load()
        {
            if (s_Cases != null) return;
            s_Cases = new List<Case>();
            s_Seeds = new List<KeyValuePair<string, string>>();
            string[] lines = File.ReadAllLines(VectorPath);
            Case cur = null;
            string seed = null;
            foreach (string raw in lines)
            {
                if (raw.Length == 0 || raw[0] == '#') continue;
                if (raw.StartsWith("case ", StringComparison.Ordinal)) { cur = new Case { Name = raw }; continue; }
                if (raw.StartsWith("barcode", StringComparison.Ordinal)) { seed = Unescape(raw.Length > 8 ? raw.Substring(8) : ""); continue; }
                if (raw == "end")
                {
                    if (cur != null) s_Cases.Add(cur);
                    cur = null;
                    seed = null;
                    continue;
                }
                int eq = raw.IndexOf('=');
                if (eq < 0) throw new InvalidDataException("ticket_vectors.txt: a line without '=': " + raw);
                string k = raw.Substring(0, eq), v = Unescape(raw.Substring(eq + 1));
                if (seed != null && k == "bars") s_Seeds.Add(new KeyValuePair<string, string>(seed, v));
                else if (cur != null && k.StartsWith("in.", StringComparison.Ordinal)) cur.In[k.Substring(3)] = v;
                else if (cur != null && k.StartsWith("out.", StringComparison.Ordinal)) cur.Out[k.Substring(4)] = v;
            }
        }

        static string Unescape(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 5 < s.Length && s[i + 1] == 'u')
                {
                    sb.Append((char)int.Parse(s.Substring(i + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    i += 5;
                }
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }

        static int I(Case c, string k) { return int.Parse(c.In[k], CultureInfo.InvariantCulture); }

        /// <summary>The summary the engine would hand over for this case's inputs.</summary>
        static TicketData DataOf(Case c)
        {
            bool encore = c.In["encore"] == "1";
            int n = I(c, "n"), first = I(c, "first"), hintOnly = I(c, "hintOnly");
            var s = new ShowSummary
            {
                Mode = c.In["mode"], Venue = c.In["venue"], N = n, First = first, HintOnly = hintOnly, Best = I(c, "best"),
                HintTotal = I(c, "hintTotal"), Fans = I(c, "fans"), Encore = encore, IsMath = false,
                Pct = Scoring.Percent(first, hintOnly, n)
            };
            s.Vip = s.Pct >= Scoring.VipAt;
            s.Sold = Scoring.SoldOut(encore, n, s.Pct);
            foreach (string song in c.In["songs"].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] p = song.Split(':');
                var item = new WordItem(p[0]);
                bool missed = p[1] == "1", hinted = p[2] == "1";
                s.Setlist.Add(new SetlistEntry { Item = item, Tag = TicketText.TagOf(missed, hinted) });
                if (missed) s.MissedItems.Add(item);
            }
            s.CanEncore = s.MissedItems.Count > 0 && !encore;
            return new TicketData
            {
                Summary = s, WeekLabel = c.In["week"], ModeName = c.In["modeName"], Date = c.In["date"],
                NewStickers = c.In["stickers"].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(Milestones.ById).ToList(),
                Unlocks = c.In["unlocks"].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(Venues.ById).ToList()
            };
        }

        static Dictionary<string, string> Printed(TicketModel m)
        {
            return new Dictionary<string, string>
            {
                ["pctRound"] = TicketText.PctRound(m.Pct).ToString(CultureInfo.InvariantCulture),
                ["eyebrow"] = m.Eyebrow,
                ["head"] = m.Head,
                ["sub"] = IntendedCopy.ToWeb(m.Sub),
                ["top"] = TicketText.Top,
                ["venue"] = m.LiveAt,
                ["fansNum"] = m.FansNumber,
                ["fans"] = m.FansNumber + " " + TicketText.FansWord,
                ["rows"] = string.Join("|", m.Rows.Select(r => r.Key + ":" + r.Value)),
                ["class"] = m.Class,
                ["bars"] = TicketBarcode.Format(m.Bars),
                ["barSeed"] = m.BarcodeSeed,
                ["date"] = m.Date,
                ["stamp"] = m.ShowStamp ? TicketText.Stamp : "",
                ["stkTitle"] = m.Stickers.Count > 0 ? TicketText.StickersTitle : "",
                ["stkNames"] = m.StickerNames,
                ["stkArt"] = Math.Min(TicketText.StickersShown, m.Stickers.Count).ToString(CultureInfo.InvariantCulture),
                ["unlockTitle"] = m.Unlock != null ? TicketText.UnlockTitle : "",
                ["unlock"] = m.Unlock != null ? m.Unlock.Name : "",
                ["setTitle"] = TicketText.SetlistTitle,
                ["setlist"] = string.Join("|", m.Setlist.Select(x => x.TagClass + ":" + x.Text + ":" + x.TagText)),
                ["button"] = (m.Primary == TicketPrimary.Encore ? "encore" : "again") + ":" + m.PrimaryLabel,
                ["back"] = TicketText.BackToMap,
                ["bubble"] = m.Coach,
            };
        }

        // ---------- against the real ticket.js ----------

        [Test]
        public void Vectors_AreThere()
        {
            Load();
            Assert.GreaterOrEqual(s_Cases.Count, 60);
            Assert.GreaterOrEqual(s_Seeds.Count, 200);
        }

        [Test]
        public void EveryPrintedLine_MatchesTheRealTicketJs()
        {
            Load();
            int checks = 0;
            foreach (Case c in s_Cases)
            {
                TicketModel m = TicketModel.Build(DataOf(c));
                Assert.AreEqual(double.Parse(c.Out["pct"], CultureInfo.InvariantCulture), m.Pct, 0.0, c.Name + " pct (show.js:219)");
                Dictionary<string, string> mine = Printed(m);
                foreach (KeyValuePair<string, string> kv in c.Out)
                {
                    if (kv.Key == "pct") continue;
                    Assert.IsTrue(mine.ContainsKey(kv.Key), "no C# value for out." + kv.Key);
                    Assert.AreEqual(kv.Value, mine[kv.Key], c.Name + " out." + kv.Key);
                    checks++;
                }
            }
            Assert.Greater(checks, 1000);
        }

        [Test]
        public void Barcode_MatchesTheRealBarcodeFunction_ForEverySeed()
        {
            Load();
            foreach (KeyValuePair<string, string> kv in s_Seeds)
                Assert.AreEqual(kv.Value, TicketBarcode.Format(TicketBarcode.Bars(kv.Key)), "seed '" + kv.Key + "'");
        }

        // ---------- barcode ----------

        [Test]
        public void Barcode_IsDeterministicPerShow_AndChangesWithTheShow()
        {
            string a = TicketBarcode.Seed("2026-11-20", "remix", 12345);
            Assert.AreEqual("2026-11-20remix12345", a);
            Assert.AreEqual(TicketBarcode.Format(TicketBarcode.Bars(a)), TicketBarcode.Format(TicketBarcode.Bars(a)));
            int differ = 0;
            for (int f = 0; f < 50; f++)
                if (TicketBarcode.Format(TicketBarcode.Bars(TicketBarcode.Seed("2026-11-20", "remix", f))) != TicketBarcode.Format(TicketBarcode.Bars(TicketBarcode.Seed("2026-11-21", "remix", f)))) differ++;
            Assert.Greater(differ, 25, "most shows on different days get different bars");
        }

        [Test]
        public void Barcode_BarsStayInsideOneRowOfTheViewBox()
        {
            for (int f = 0; f < 500; f++)
            {
                List<BarcodeBar> bars = TicketBarcode.Bars(TicketBarcode.Seed("2026-11-20", "fanmail", f * 37));
                int last = -1;
                foreach (BarcodeBar b in bars)
                {
                    Assert.That(b.Width, Is.InRange(1, 4));
                    Assert.Less(b.X, TicketBarcode.ViewWidth, "a bar starts inside the viewBox");
                    Assert.Greater(b.X, last, "bars never overlap");
                    last = b.X + b.Width;
                }
            }
        }

        [Test]
        public void Barcode_RoundsLikeJsDoubles_NotLikeAnExactMultiply()
        {
            // 4294967295 * 1103515245 is past 2^53: the JS double result differs from the exact uint product
            uint exact = unchecked(4294967295u * 1103515245u + 12345u);
            Assert.AreNotEqual(exact, TicketBarcode.Lcg(4294967295u));
            Assert.AreEqual(3191463936u, TicketBarcode.Lcg(4294967295u), "node: (4294967295 * 1103515245 + 12345) >>> 0");
            Assert.AreEqual(unchecked(1u * 1103515245u + 12345u), TicketBarcode.Lcg(1u), "small values are exact either way");
        }

        // ---------- percent and copy ----------

        [Test]
        public void Percent_PicksTheHeadSubAndVip_AtTheJsThresholds()
        {
            Assert.AreEqual("sold out", TicketText.Head(.9));
            Assert.AreEqual("what a show", TicketText.Head(.8999));
            Assert.AreEqual("what a show", TicketText.Head(.7));
            Assert.AreEqual("rehearsal night", TicketText.Head(.6999));
            Assert.IsTrue(TicketText.IsVip(.9));
            Assert.IsFalse(TicketText.IsVip(Scoring.Percent(8, 1, 10)), "8.5 of 10 is not vip");
            Assert.AreEqual("natalia, you" + (char)0x2019 + "re so ready for that test.", TicketText.Sub(1));
            Assert.AreEqual(85, TicketText.PctRound(Scoring.Percent(8, 1, 10)));
            Assert.AreEqual(5, TicketText.PctRound(.045), "JS Math.round(.045 * 100) is 5 (ties go up)");
        }

        [Test]
        public void Stamp_FollowsVip_NotSold()
        {
            // ticket.js:49: an encore at 100% gets the stamp although show.js never records it as sold out
            var s = new ShowSummary { Mode = "remix", Venue = "coffee", N = 2, First = 2, Encore = true, Pct = 1 };
            s.Sold = Scoring.SoldOut(true, 2, 1);
            TicketModel m = TicketModel.Build(new TicketData { Summary = s, WeekLabel = "w", Date = "2026-11-20" });
            Assert.IsTrue(m.ShowStamp);
            Assert.IsFalse(m.Sold);
            StringAssert.Contains("vip=true sold=false", m.LogLine);
        }

        [Test]
        public void Fmt_GroupsThousands_LikeToLocaleStringEnUs()
        {
            Assert.AreEqual("0", TicketText.Fmt(0));
            Assert.AreEqual("999", TicketText.Fmt(999));
            Assert.AreEqual("1,000", TicketText.Fmt(1000));
            Assert.AreEqual("1,234,567", TicketText.Fmt(1234567));
        }

        [Test]
        public void StickerNames_ShowThreeThenCountTheRest()
        {
            Assert.AreEqual("", TicketText.StickerNames(new string[0]));
            Assert.AreEqual("a", TicketText.StickerNames(new[] { "a" }));
            Assert.AreEqual("a, b, c", TicketText.StickerNames(new[] { "a", "b", "c" }));
            Assert.AreEqual("a, b, c + 2 more", TicketText.StickerNames(new[] { "a", "b", "c", "d", "e" }));
        }

        [Test]
        public void Coach_PicksTheEncoreLine_ThenVip_ThenGoodShow()
        {
            Assert.AreEqual("the encore replays the one you missed. half fans, zero pressure.", TicketText.Coach(true, 1, true));
            Assert.AreEqual("the encore replays the 3 you missed. half fans, zero pressure.", TicketText.Coach(true, 3, false));
            Assert.AreEqual("that" + (char)0x2019 + "s a vip night. framing this ticket.", TicketText.Coach(false, 0, true));
            Assert.AreEqual("good show. the bus leaves when you" + (char)0x2019 + "re ready.", TicketText.Coach(false, 2, false));
        }

        // ---------- setlist tags ----------

        [Test]
        public void SetlistTags_MapToTheJsClassesAndWords()
        {
            Assert.AreEqual("ok", TicketText.TagClass(SetlistTag.Ok));
            Assert.AreEqual("hint", TicketText.TagClass(SetlistTag.Hint));
            Assert.AreEqual("miss", TicketText.TagClass(SetlistTag.Miss));
            Assert.AreEqual("nailed it", TicketText.TagText(SetlistTag.Ok));
            Assert.AreEqual("used a hint", TicketText.TagText(SetlistTag.Hint));
            Assert.AreEqual("rehearse again", TicketText.TagText(SetlistTag.Miss));
            Assert.AreEqual(SetlistTag.Miss, TicketText.TagOf(true, true), "a miss beats a hint");
            Assert.AreEqual(SetlistTag.Hint, TicketText.TagOf(false, true));
            Assert.AreEqual(SetlistTag.Ok, TicketText.TagOf(false, false));
            Assert.AreEqual("01", TicketText.SongNumber(1));
            Assert.AreEqual("10", TicketText.SongNumber(10));
            Assert.AreEqual("100", TicketText.SongNumber(100));
        }

        [Test]
        public void RealShow_WithAMissAndAHint_GivesTheTicketItsTagsAndEncore()
        {
            var words = Enumerable.Range(0, 5).Select(i => (ShowItem)new WordItem("word" + i)).ToList();
            var e = new ShowEngine(new ShowSetup { ModeId = "remix", VenueId = "coffee", Items = words, ClockMs = () => 0 });
            e.Start();
            int song = 0;
            while (!e.Finished)
            {
                e.SetupHints(3);
                if (!e.IsBonus && song == 1) { e.Miss(); e.Win(); }
                else if (!e.IsBonus && song == 3) { e.Hint(); e.Win(); }
                else e.Win();
                if (!e.IsBonus) song++;
                e.Next();
            }
            TicketModel m = TicketModel.Build(new TicketData { Summary = e.Summary, WeekLabel = "week 1", ModeName = "remix", Date = "2026-11-20" });
            Assert.AreEqual(new[] { "ok", "miss", "ok", "hint", "ok" }, m.Setlist.Select(x => x.TagClass).ToArray());
            Assert.AreEqual(5, m.Setlist.Count, "the bonus replay is not on the setlist");
            Assert.AreEqual(TicketPrimary.Encore, m.Primary);
            Assert.AreEqual("encore: 1 to rehearse", m.PrimaryLabel);
            Assert.AreEqual("week 1 " + TicketText.Star + " remix", m.Eyebrow);
            Assert.AreEqual("live at the corner coffee shop", m.LiveAt);
            StringAssert.StartsWith("WT_TICKET_SHOWN pct=", m.LogLine);
            StringAssert.EndsWith(" stickers=0 unlocks=0 encore=true", m.LogLine);
        }

        [Test]
        public void Encore_HasNoEncoreButton_AndSaysEncoreInTheEyebrow()
        {
            var s = new ShowSummary { Mode = "remix", Venue = "coffee", N = 1, Encore = true, Pct = 0 };
            s.Setlist.Add(new SetlistEntry { Item = new WordItem("a"), Tag = SetlistTag.Miss });
            s.MissedItems.Add(s.Setlist[0].Item);
            s.CanEncore = false;
            TicketModel m = TicketModel.Build(new TicketData { Summary = s, WeekLabel = "w", ModeName = "remix", Date = "2026-11-20" });
            Assert.AreEqual(TicketPrimary.PlayAgain, m.Primary);
            Assert.AreEqual("play again", m.PrimaryLabel);
            Assert.AreEqual("w " + TicketText.Star + " remix encore", m.Eyebrow);
            Assert.AreEqual("good show. the bus leaves when you" + (char)0x2019 + "re ready.", m.Coach);
        }

        [Test]
        public void Unlock_IsTheLastVenueUnlocked_AndTheLogCountsThemAll()
        {
            var s = new ShowSummary { Mode = "remix", Venue = "coffee", N = 1, Pct = 1 };
            TicketModel m = TicketModel.Build(new TicketData
            {
                Summary = s, WeekLabel = "w", Date = "2026-11-20",
                Unlocks = new[] { Venues.ById("records"), Venues.ById("rec") },
                NewStickers = new[] { Milestones.All[0], Milestones.All[1], Milestones.All[2], Milestones.All[3] }
            });
            Assert.AreEqual("rec", m.Unlock.Id);
            StringAssert.Contains(" stickers=4 unlocks=2 ", m.LogLine);
            StringAssert.EndsWith(" + 1 more", m.StickerNames);
        }
    }
}
