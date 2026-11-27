using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Data;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// The backstage screen logic against the real web screen: Tools/meta/backstage_vectors.mjs runs
    /// world_tour/screens/backstage.js on a fake DOM for 80 starting saves and scripted clicks (pick an item, press the
    /// mirror's button, let the "tap again" timer run out) and records each step's screen. <see cref="Backstage"/> must
    /// give the same wallet, tags, selection, mirror, page theme, sound and toast events, and saved state, step by step.
    /// Phase 10B.1 changed the money: backstage prices are now coins (the old fans price over 500, rounded up), paid from the coin wallet, and fans are never
    /// spent. Every old price is a multiple of 500, so the web screen's snapshots still apply with its fans turned into coins: wallet and prices over 500
    /// (<see cref="InCoins"/>), "more fans needed" as "more coins needed".
    /// </summary>
    public class BackstageVectorTests
    {
        private static JsonValue _v;

        private static JsonValue Vectors()
        {
            if (_v == null) _v = StrictJson.Parse(File.ReadAllText(SaveFixtures.TestFile("backstage_vectors.json")));
            return _v;
        }

        public static IEnumerable<int> Seeds()
        {
            return Vectors()["cases"].Items.Select(c => c["seed"].Int);
        }

        [Test]
        public void Vectors_CoverEveryCase()
        {
            string all = string.Join("\n", Vectors()["cases"].Items.SelectMany(c => c["steps"].Items).Select(s => s["snap"].String));
            foreach (string want in new[] { "tap again to buy", "theme=peach-navy", "toast:yours!", "toast:stuck it in!", "more fans needed",
                "see it in your journal", "switch to this theme", "theme is on", "wear it", "mirror=trying on/" })
                StringAssert.Contains(want, all);
            Assert.AreEqual(80, Vectors()["cases"].Count);
        }

        [Test, TestCaseSource(nameof(Seeds))]
        public void Replay_MatchesTheWebScreen(int seed)
        {
            JsonValue c = Vectors()["cases"].Items.Single(x => x["seed"].Int == seed);
            JsonValue init = c["init"];
            SaveBlob s = SaveBlob.Defaults("2026-11-20");
            s.Fans.Balance = init["balance"].Long;
            s.Fans.Coins = init["balance"].Long / CoinRules.FansPerGrantCoin;
            s.Fans.Lifetime = init["lifetime"].Long;
            s.Inventory.Owned = init["owned"].Items.Select(x => x.String).ToList();
            foreach (string k in init["equipped"].Keys) s.Inventory.Equipped[k] = init["equipped"][k].String;

            Backstage b = null;
            string lastTags = null;
            int i = 0;
            foreach (JsonValue step in c["steps"].Items)
            {
                string op = step["op"].String, ev = "";
                switch (op)
                {
                    case "mount": b = new Backstage(s); break;
                    case "select": b.Select(step["arg"].String); ev = "tap"; break;
                    case "wait": b.Disarm(); break;
                    case "press":
                        ShopItem it = b.Item;
                        switch (b.Press())
                        {
                            case PressOutcome.Armed: ev = "tap"; break;
                            case PressOutcome.Bought: ev = "strum;toast:" + Backstage.BoughtToast(it); break;
                            case PressOutcome.Refused: ev = "wobble;thud"; break;
                            case PressOutcome.Equipped: ev = "pop"; break;
                        }
                        break;
                    default: Assert.Fail("unknown op " + op); break;
                }
                string got = Snap(s, b, ev, ref lastTags);
                Assert.AreEqual(InCoins(step["snap"].String), got, "seed " + seed + " step " + i + " (" + op + " " + step["arg"].String + ")");
                i++;
            }
        }

        private static long Ceil500(long n) { return (n + 499) / 500; }
        private static string Commas(long n) { return Backstage.Fmt(n); }
        private static long Plain(string s) { return long.Parse(s.Replace(",", "")); }

        /// <summary>The web screen's snapshot with its fans turned into coins (see the class summary).</summary>
        internal static string InCoins(string snap)
        {
            snap = System.Text.RegularExpressions.Regex.Replace(snap, @"^bal=([0-9,]+)", m => "bal=" + Commas(Plain(m.Groups[1].Value) / 500));
            snap = System.Text.RegularExpressions.Regex.Replace(snap, @":([0-9][0-9,]*):([01]{3})", m => ":" + Commas(Ceil500(Plain(m.Groups[1].Value))) + ":" + m.Groups[2].Value);
            snap = System.Text.RegularExpressions.Regex.Replace(snap, @"/([0-9,]+) more fans needed/", m => "/" + Commas(Ceil500(Plain(m.Groups[1].Value))) + " more coins needed/");
            snap = System.Text.RegularExpressions.Regex.Replace(snap, @"/buy for ([0-9,]+)/", m => "/buy for " + Commas(Ceil500(Plain(m.Groups[1].Value))) + " coins/");
            snap = System.Text.RegularExpressions.Regex.Replace(snap, @"\|state=([0-9]+)/", m => "|state=" + (long.Parse(m.Groups[1].Value) / 500) + "/");
            return snap;
        }

        private static string Snap(SaveBlob s, Backstage b, string ev, ref string lastTags)
        {
            string tags = string.Join(",", Shop.All.Select(it =>
            {
                ItemTag t = b.Tag(it);
                return it.Id + ":" + t.Tag + ":" + (t.Own ? 1 : 0) + (t.Wearing ? 1 : 0) + (t.Short ? 1 : 0);
            }));
            string tagsOut = tags == lastTags ? "~" : tags;
            lastTags = tags;
            Mirror m = b.Mirror();
            string does = m.Does == MirrorAction.Equip ? "equip" : m.Does == MirrorAction.Buy ? "buy" : "";
            var eq = s.Inventory.Equipped;
            return string.Join("|", new[]
            {
                "bal=" + Backstage.Fmt(s.Fans.Coins),
                "theme=" + m.Theme,
                "sel=" + b.Selected,
                "tags=" + tagsOut,
                "mirror=" + m.Eyebrow + "/" + m.Name + "/" + m.Action + "/" + (m.Does == MirrorAction.None ? 0 : 1) + "/" + does,
                "blurb=" + m.Blurb,
                "ev=" + ev,
                "state=" + s.Fans.Coins + "/" + s.Fans.Lifetime + "/" + string.Join(",", s.Inventory.Owned) + "/" + eq["theme"] + "," + eq["guitar"] + "," + eq["dog"],
            });
        }
    }
}
