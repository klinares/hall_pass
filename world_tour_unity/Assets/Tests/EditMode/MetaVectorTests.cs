using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using WorldTour.Core.Maths;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// Checks Core/Meta against the real web game: world_tour_unity/Tools/meta_vectors.mjs runs world_tour/js/meta.js
    /// on sample saves and writes meta_vectors.txt next to this file. Each seed builds a save with mulberry32
    /// (Rng matches the JS bit for bit) and runs a scripted mix of buy, equip, earn, place, finished shows and fan
    /// gains; the whole trace must match. <see cref="Trace"/> is the C# twin of the script's <c>trace(seed)</c>:
    /// the two recipes must stay step-for-step identical (same Rng calls in the same order).
    /// </summary>
    public class MetaVectorTests
    {
        private static Dictionary<string, string> _file;

        /// <summary>The vector file: "spots", "mins" and one entry per seed.</summary>
        internal static Dictionary<string, string> Vectors()
        {
            if (_file != null) return _file;
            string path = Path.Combine(Application.dataPath, "Tests", "EditMode", "meta_vectors.txt");
            var d = new Dictionary<string, string>();
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                int tab = line.IndexOf('\t');
                if (tab <= 0) throw new InvalidDataException("meta_vectors.txt: a line without a tab");
                d[line.Substring(0, tab)] = line.Substring(tab + 1);
            }
            if (!d.ContainsKey("spots") || !d.ContainsKey("mins") || !d.ContainsKey("1"))
                throw new InvalidDataException("meta_vectors.txt is empty or malformed");
            return _file = d;
        }

        public static readonly int[] Seeds = Enumerable.Range(1, 160).ToArray();

        [Test]
        public void Vectors_CoverEverySeed()
        {
            Assert.AreEqual(Seeds.Length + 2, Vectors().Count);
        }

        [TestCaseSource(nameof(Seeds))]
        public void Trace_MatchesJs(int seed)
        {
            string want = Vectors()[seed.ToString(CultureInfo.InvariantCulture)];
            string got = Trace(seed);
            if (got != want) Assert.Fail("seed " + seed + " differs at " + FirstDiff(got, want) + "\n  c#: " + Around(got, want) + "\n  js: " + Around(want, got));
        }

        [Test]
        public void DefaultSpots_MatchJs()
        {
            var parts = new List<string>();
            foreach (int cols in new[] { 3, 4, 5, 6 })
                for (int i = 0; i < 60; i++) parts.Add(SpotText(MetaRules.DefaultSpot(i, cols)));
            Assert.AreEqual(Vectors()["spots"], string.Join(" ", parts));
        }

        [Test]
        public void Mins_MatchJs()
        {
            long[] secs = { 0, 1, 29, 59, 60, 89, 90, 91, 149, 150, 151, 3599, 3600, 5430, 86399 };
            Assert.AreEqual(Vectors()["mins"], Esc(string.Join("|", secs.Select(Office.Mins))));
        }

        private static int FirstDiff(string a, string b)
        {
            int i = 0;
            while (i < a.Length && i < b.Length && a[i] == b[i]) i++;
            return i;
        }

        private static string Around(string a, string b)
        {
            int i = Math.Max(0, FirstDiff(a, b) - 60);
            return a.Substring(i, Math.Min(160, a.Length - i));
        }

        /* ---------- the recipe (keep identical to Tools/meta_vectors.mjs) ---------- */

        private static readonly ModeInfo[] AllModes =
        {
            new ModeInfo("slots", "missing letters", ModeInfo.Spelling), new ModeInfo("vowels", "missing vowels", ModeInfo.Spelling),
            new ModeInfo("remix", "remix", ModeInfo.Spelling), new ModeInfo("fanmail", "fan mail", ModeInfo.Spelling),
            new ModeInfo("studio", "studio session", ModeInfo.Spelling), new ModeInfo("lyric", "lyric writer", ModeInfo.Spelling),
            new ModeInfo("prefix", "prefix mixer", ModeInfo.Spelling),
            new ModeInfo("merch", "merch table", ModeInfo.Math, "dec"), new ModeInfo("beat", "beat maker", ModeInfo.Math, "frac"),
            new ModeInfo("tickets", "ticket sales", ModeInfo.Math, "md"), new ModeInfo("stage", "stage designer", ModeInfo.Math, "xy"),
            new ModeInfo("truck", "pack the truck", ModeInfo.Math, "vol"), new ModeInfo("budget", "tour budget", ModeInfo.Math, "ops"),
            new ModeInfo("soundcheck", "soundcheck", ModeInfo.Math, "flu"),
        };
        private static readonly ModeInfo[] TestModes = { AllModes[2], AllModes[0], AllModes[7], AllModes[8] };
        private static readonly string[] Words = { "illegible", "irregular", "intercept", "arrival", "believe", "calendar" };
        private static readonly string[] Tags = { "no-borrow", "gave-total", "swapped", "too-short", "multiplied-exponent", "near", "made-up", "wrong-op" };
        private static readonly string[] VenueIds = { "records", "rec", "rooftop", "club", "theater", "festival", "arena" };
        private static readonly string[] ItemIds = Shop.All.Select(i => i.Id).Concat(new[] { "nope:x" }).ToArray();
        private static readonly string[] Keys = Milestones.All.Select(m => m.Id)
            .Concat(Shop.All.Where(i => i.Slot == Shop.StickerSlot).Select(i => i.Id)).Concat(new[] { "loose" }).ToArray();
        private static readonly DateTime Day0 = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

        private static string Day(int k) { return Day0.AddDays(k).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }

        /// <summary>Same as the script's <c>esc</c>: anything outside printable ASCII as a lowercase \uXXXX escape.</summary>
        internal static string Esc(string t)
        {
            var sb = new StringBuilder(t.Length);
            foreach (char ch in t)
            {
                if (ch >= 0x20 && ch <= 0x7e) sb.Append(ch);
                else sb.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        private static string Num(double v) { return ((long)v).ToString(CultureInfo.InvariantCulture); }
        private static string Q(double v) { return Num(Math.Floor(v * 1000 + 0.5)); }
        private static string SpotText(Spot p) { return Q(p.X) + "," + Q(p.Y) + "," + Num(p.R); }
        private static string Nul(int? v) { return v.HasValue ? v.Value.ToString(CultureInfo.InvariantCulture) : "null"; }
        private static string Nul(string v) { return v ?? "null"; }

        private static string BuyText(BuyResult r)
        {
            if (r.Ok) return "ok";
            switch (r.Reason)
            {
                case BuyReason.Short: return "short:" + (r.Need * CoinRules.FansPerGrantCoin).ToString(CultureInfo.InvariantCulture);   // 10B.1: coins, shown as the fans the web game counts
                case BuyReason.Owned: return "owned";
                default: return "unknown";
            }
        }

        /// <summary>The script's <c>trace(seed)</c>, step for step.</summary>
        internal static string Trace(int seed)
        {
            var r = new Rng(seed);
            SaveBlob s = MetaTests.Fresh();
            var o = new List<string>();
            IReadOnlyList<ModeInfo> modes = new[] { AllModes, TestModes, new ModeInfo[0] }[r.Int(0, 2)];
            s.Fans.Balance = r.Int(0, 80) * 1000;
            s.Fans.Lifetime = s.Fans.Balance + r.Int(0, 50) * 1000;
            s.Fans.Coins = s.Fans.Balance / CoinRules.FansPerGrantCoin;   // phase 10B.1: the wallet is coins now (every price is a multiple of 500 fans); Balance stands in for it below
            foreach (ShopItem it in Shop.All) if (r.Chance(.15) && !s.Inventory.Owned.Contains(it.Id)) s.Inventory.Owned.Add(it.Id);
            if (r.Chance(.1)) s.Inventory.Owned.Add("retired:item");
            foreach (string slot in new[] { "theme", "guitar", "dog" })
            {
                var opts = Shop.All.Where(i => i.Slot == slot).Select(i => i.Id.Substring(slot.Length + 1)).ToList();
                opts.Add("gone");
                s.Inventory.Equipped[slot] = r.Pick(opts);
            }
            s.Streak.Best = r.Int(0, 11);
            foreach (ModeInfo m in AllModes)
            {
                if (!r.Chance(.4)) continue;
                int level = r.Int(1, 3), a = r.Int(0, 30), c = r.Int(0, a), h = r.Int(0, a);
                int t3 = r.Int(0, 15), c3 = r.Int(0, 13);
                var k = new SkillRecord { Level = level, A = a, C = c, H = h };
                k.ByLevel[3] = new[] { t3, c3 };
                int ntags = r.Int(0, 3);
                for (int j = 0; j < ntags; j++)
                {
                    string tag = r.Pick(Tags);
                    int n;
                    k.Mistakes.TryGetValue(tag, out n);
                    k.Mistakes[tag] = n + r.Int(0, 4);
                }
                s.Skills[Milestones.SkillKey(m)] = k;
            }
            int nw = r.Int(0, 5);
            for (int j = 0; j < nw; j++)
            {
                string w = r.Pick(Words);
                int a = r.Int(0, 6);
                int c = r.Int(0, a), first = r.Int(0, a), miss = r.Int(0, 3);
                string last = r.Chance(.5) ? Day(r.Int(0, 30)) : null;
                s.Words[w] = new WordStat { A = a, C = c, First = first, H = 0, Miss = miss, Last = last };
            }
            int ns = r.Int(0, 14);
            for (int j = 0; j < ns; j++)
            {
                string d = Day(r.Int(0, 30));
                int n = r.Int(1, 10);
                int first = r.Int(0, n);
                string mode = r.Pick(AllModes).Id;
                bool sold = r.Chance(.1), encore = r.Chance(.2);
                s.Shows.Add(new ShowRecord { D = d, Mode = mode, N = n, First = first, Sold = sold, Encore = encore });
            }
            int nt = r.Int(0, 6);
            for (int j = 0; j < nt; j++) { string d = Day(r.Int(0, 30)); s.Time[d] = r.Int(0, 1500); }
            foreach (string v in VenueIds) if (r.Chance(.25)) s.Venues.Unlocked.Add(v);
            foreach (Milestone m in Milestones.All) if (r.Chance(.08)) s.Journal.Stickers[m.Id] = r.Chance(.25) ? "" : "pre";
            if (r.Chance(.1)) s.Journal.Stickers["old-thing"] = "pre";
            if (r.Chance(.3)) s.Journal.Layout["first-show"] = new Spot { X = .5, Y = .5, R = r.Int(-7, 7) };
            int nrw = r.Int(0, 3);
            for (int j = 0; j < nrw; j++) s.Review.Words.Add("w" + j);
            int nrm = r.Int(0, 2);
            for (int j = 0; j < nrm; j++) s.Review.Math.Add(new ReviewEntry { Skill = "dec", Level = 1, Seed = j, Prob = null });
            string today = Day(r.Int(20, 30));

            int nops = r.Int(5, 14);
            for (int i = 0; i < nops; i++)
            {
                int op = r.Int(0, 5);
                if (op == 0)
                {
                    string id = r.Pick(ItemIds);
                    BuyResult res = MetaRules.Buy(s, id);
                    s.Fans.Balance = s.Fans.Coins * CoinRules.FansPerGrantCoin;
                    o.Add("b " + id + " " + BuyText(res) + " " + s.Fans.Balance + " " + s.Fans.Lifetime);
                }
                else if (op == 1)
                {
                    string id = r.Pick(ItemIds);
                    o.Add("e " + id + " " + (MetaRules.Equip(s, id) ? 1 : 0));
                }
                else if (op == 2)
                {
                    o.Add("n " + string.Join(",", MetaRules.Earn(s, modes, today).Select(m => m.Id)));
                }
                else if (op == 3)
                {
                    string key = r.Pick(Keys);
                    double x = r.Int(-30, 130) / 100.0, y = r.Int(-30, 130) / 100.0;
                    int rot = r.Int(-12, 12);
                    MetaRules.Place(s, key, x, y, rot);
                    o.Add("p " + key + " " + SpotText(s.Journal.Layout[key]));
                }
                else if (op == 4)
                {
                    int n = r.Int(1, 10);
                    int first = r.Int(0, n);
                    string mode = r.Pick(AllModes).Id;
                    bool encore = r.Chance(.2);
                    s.Shows.Add(new ShowRecord { D = today, Mode = mode, N = n, First = first, Encore = encore });
                    s.Streak.Best += r.Int(0, 2);
                    o.Add("s " + mode + " " + n + " " + first + " " + (encore ? 1 : 0));
                }
                else
                {
                    int add = r.Int(1, 40) * 1000;
                    s.Fans.Balance += add; s.Fans.Lifetime += add; s.Fans.Coins += add / CoinRules.FansPerGrantCoin;
                    o.Add("f " + s.Fans.Balance + " " + s.Fans.Lifetime);
                }
            }

            Look look = MetaRules.GetLook(s);
            o.Add("look " + look.Theme + " " + look.Guitar + " " + look.Outfit);
            var eq = s.Inventory.Equipped;
            o.Add("own " + string.Join(",", s.Inventory.Owned) + " eq " + eq["theme"] + "," + eq["guitar"] + "," + eq["dog"]
                  + " fans " + s.Fans.Balance + " " + s.Fans.Lifetime);
            o.Add("st " + string.Join(",", s.Journal.Stickers.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => e.Key + "=" + e.Value)));
            List<PageItem> page = MetaRules.PageItems(s);
            o.Add("page " + string.Join(",", page.Select((it, i) => it.Key + "|" + it.Art + "|" + it.Name + "|" + it.Note + "|" + SpotText(MetaRules.SpotOf(s, it.Key, i)))));
            o.Add("left " + string.Join(",", MetaRules.ToEarn(s).Select(m => m.Id)));
            OfficeReport of = Office.Build(s, modes, today);
            o.Add("office " + of.Shows + " " + of.TotalSecs + " " + of.Lifetime + " " + of.Balance + " " + of.Best + " " + of.ReviewWords + " "
                  + of.ReviewMath + " " + of.Stickers + " " + of.StickersTotal + " " + Office.Mins(of.TotalSecs));
            o.Add("sk " + string.Join("|", of.Skills.Select(k => k.Id + "," + k.Name + "," + k.Kind + "," + k.Level + "," + k.A + "," + Nul(k.Acc) + "," + Nul(k.Hint) + "," + k.Shows)));
            o.Add("wd " + string.Join("|", of.Words.Select(w => w.W + "," + w.Miss + "," + w.A + "," + Nul(w.First) + "," + Nul(w.Last))));
            o.Add("mi " + string.Join("|", of.Mistakes.Select(m => m.Tag + "," + m.N + "," + m.Label + "," + m.Mode)));
            o.Add("dy " + string.Join("|", of.Daily.Select(d => d.D + "," + d.Secs + "," + d.Shows)));
            return Esc(string.Join(" ; ", o));
        }
    }
}
