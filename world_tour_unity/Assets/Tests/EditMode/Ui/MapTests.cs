using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WorldTour.Core.Data;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Progress;
using WorldTour.Game.Map;

namespace WorldTour.Game.EditTests
{
    /// <summary>
    /// The pure parts of the tour map (task 2.4): the route curves from the sprite manifest checked against a port of
    /// art/map.js curve()/routePoints(), path flattening and dashing, the screens.css sizes for the three window
    /// shapes, the copy and number formats of map.js / venue.js / copy.js / util.js, and the map model rules.
    /// </summary>
    public class MapTests
    {
        static JsonValue s_Manifest;

        /// <summary>Assets/Art/Sprites/manifest.json (task 2.1). B24_ASSETS overrides Assets/ outside Unity.</summary>
        static JsonValue Manifest()
        {
            if (s_Manifest != null) return s_Manifest;
            string assets = Environment.GetEnvironmentVariable("B24_ASSETS") ?? Application.dataPath;
            return s_Manifest = StrictJson.Parse(File.ReadAllText(Path.Combine(assets, "Art", "Sprites", "manifest.json")));
        }

        static MapLayout Layout() { return MapLayout.FromManifest(Manifest()["map"]); }

        // ---------- layout data and route curves ----------

        [Test]
        public void Manifest_LayoutIsTheWebLandscapeLayout()
        {
            MapLayout L = Layout();
            Assert.AreEqual(1000f, L.W); Assert.AreEqual(610f, L.H);
            Vector2[] pts = { new Vector2(120, 150), new Vector2(370, 128), new Vector2(628, 158), new Vector2(880, 136),
                              new Vector2(880, 446), new Vector2(628, 470), new Vector2(370, 446), new Vector2(120, 462) };
            CollectionAssert.AreEqual(pts, L.Pts);
            Assert.AreEqual(new Vector2(985, 291), L.Via[3]);
            Assert.AreEqual(7, L.Decor.Count);
            Assert.AreEqual("note", L.Decor[0].Id); Assert.AreEqual(12f, L.Decor[0].Rot);
            CollectionAssert.AreEqual(Venues.All.Select(v => v.Id).ToArray(), L.VenueOrder, "manifest venue order is the Core venue table");
            Assert.AreEqual(Venues.All.Count, L.CurveDone.Length);
        }

        [Test]
        public void ManifestCurves_MatchAPortOfMapJsCurve()
        {
            MapLayout L = Layout();
            Assert.AreEqual(L.CurveAll, SvgPath.Curve(L.RoutePoints()));
            Assert.AreEqual("", L.DoneCurve(0));
            for (int k = 1; k < L.CurveDone.Length; k++)
                Assert.AreEqual(L.CurveDone[k], SvgPath.Curve(L.RoutePoints(k)), "curveDone[" + k + "]");
        }

        [Test]
        public void ParsedRoute_PassesThroughEveryVenue()
        {
            MapLayout L = Layout();
            List<Vector2[]> segs = SvgPath.Parse(L.CurveAll);
            Assert.AreEqual(8, segs.Count, "7 hops plus the swing-wide waypoint");
            var ends = new List<Vector2> { segs[0][0] };
            ends.AddRange(segs.Select(s => s[3]));
            foreach (Vector2 p in L.Pts) Assert.IsTrue(ends.Contains(p), "route misses " + p);
            for (int k = 1; k < L.CurveDone.Length; k++)
            {
                List<Vector2[]> d = SvgPath.Parse(L.CurveDone[k]);
                Assert.AreEqual(L.Pts[k], d[d.Count - 1][3], "done route ends at venue " + k);
            }
        }

        [Test]
        public void Flatten_Cut_Length()
        {
            List<Vector2[]> segs = SvgPath.Parse("M0 0 C0 0 10 0 10 0 C10 0 10 10 10 10");
            List<Vector2> pts = SvgPath.Flatten(segs, 10);
            Assert.AreEqual(21, pts.Count);
            Assert.AreEqual(20f, SvgPath.Length(pts), 0.01f);
            List<Vector2> cut = SvgPath.Cut(pts, 15f);
            Assert.AreEqual(15f, SvgPath.Length(cut), 0.01f);
            Assert.AreEqual(10f, cut[cut.Count - 1].x, 0.01f); Assert.AreEqual(5f, cut[cut.Count - 1].y, 0.01f);
            Assert.AreEqual(20f, SvgPath.Length(SvgPath.Cut(pts, 99f)), 0.01f);
            Assert.Throws<FormatException>(() => SvgPath.Parse("C1 2 3 4 5 6"));
            Assert.Throws<FormatException>(() => SvgPath.Parse("M1 2 C3 4"));
        }

        [Test]
        public void Dashes_FollowTheDashArray()
        {
            var line = new List<Vector2> { new Vector2(0, 0), new Vector2(42, 0), new Vector2(42, 21) };   // 63 long
            List<List<Vector2>> d = SvgPath.Dashes(line, 10f, 11f);
            Assert.AreEqual(3, d.Count, "dashes at 0-10, 21-31, 42-52");
            foreach (List<Vector2> piece in d) Assert.AreEqual(10f, SvgPath.Length(piece), 0.01f);
            Assert.AreEqual(new Vector2(21, 0), d[1][0]);
            Assert.AreEqual(new Vector2(42, 0), d[2][0]);
            Assert.AreEqual(new Vector2(42, 10), d[2][d[2].Count - 1], "a dash turns the corner");
            Assert.AreEqual(1, SvgPath.Dashes(line, 0f, 0f).Count, "no dash array: one solid piece");
        }

        // ---------- sizes at the three window shapes ----------

        [Test]
        public void Sizes_At1366x768_1920x1080_And16by10()
        {
            // Canvas 1366x768 reference, match 0.5: 16:9 windows (1366x768, 1920x1080) are a 1366-wide canvas,
            // 16:10 (1440x900, 1920x1200) a 1296-wide one. Both give the 1180-wide page less two 32 gutters.
            Assert.AreEqual(1116f, MapLayout.MapWidth(1366f, 32f));
            Assert.AreEqual(1116f, MapLayout.MapWidth(1296f, 32f));
            Assert.AreEqual(936f, MapLayout.MapWidth(1000f, 32f));
            MapLayout L = Layout();
            Assert.AreEqual(680.76f, L.MapHeight(1116f), 0.01f);
            Assert.AreEqual(190f, MapLayout.VenueWidth(1116f), "19% capped at 190");
            Assert.AreEqual(1116f * 0.07f, MapLayout.DecorWidth(1116f), 0.001f);
            Assert.AreEqual(84f, MapLayout.DecorWidth(1300f));
            Assert.AreEqual(new Vector2(120f * 1.116f, -150f * 1.116f), L.ToLocal(new Vector2(120, 150), 1116f));
            Assert.AreEqual(2f, MapLayout.UnityTilt(-2f));
        }

        [Test]
        public void SpriteBox_FitsTheWebBox()
        {
            // venue_coffee: 596x500 sprite, web box at (-15,-12) 561x476
            var b = new SpriteBox { W = 596, H = 500, BoxX = -15, BoxY = -12, BoxW = 561, BoxH = 476 };
            b.Fit(190f, out Vector2 off, out Vector2 size);
            float k = 190f / 561f;
            Assert.AreEqual(15f * k, off.x, 1e-4f); Assert.AreEqual(12f * k, off.y, 1e-4f);
            Assert.AreEqual(596f * k, size.x, 1e-4f); Assert.AreEqual(500f * k, size.y, 1e-4f);
            Assert.AreEqual(190f * 476f / 561f, b.BoxHeightFor(190f), 1e-4f);
            Dictionary<string, SpriteBox> boxes = MapLayout.Boxes(Manifest());
            foreach (Venue v in Venues.All) Assert.IsTrue(boxes.ContainsKey("venue_" + v.Art), "venue_" + v.Art);
            foreach (MapDecor d in Layout().Decor) Assert.IsTrue(boxes.ContainsKey(d.Id + "_sm"), d.Id + "_sm");
            foreach (ModeEntry m in ModeCatalog.All) Assert.IsTrue(boxes.ContainsKey(m.Sticker + "_sm"), m.Sticker + "_sm");
        }

        // ---------- copy ----------

        [Test]
        public void Numbers_AsUtilJs()
        {
            Assert.AreEqual("33,300", MapText.Fmt(33300)); Assert.AreEqual("0", MapText.Fmt(0)); Assert.AreEqual("900,000", MapText.Fmt(900000));
            Assert.AreEqual("20k", MapText.Short(20000)); Assert.AreEqual("125k", MapText.Short(125000));
            Assert.AreEqual("1.5m", MapText.Short(1500000)); Assert.AreEqual("1m", MapText.Short(1000000));
            Assert.AreEqual("950", MapText.Short(950)); Assert.AreEqual("1000k", MapText.Short(999999));
        }

        [Test]
        public void MapCopy_AsMapJs()
        {
            Venue records = Venues.ById("records");
            Assert.AreEqual("20k fans unlocks record store", MapText.RouteLine(records));
            Assert.AreEqual("every venue unlocked. legend.", MapText.RouteLine(null));
            Assert.AreEqual("tonight: coffee shop " + MapText.Arrow, MapText.Tonight(Venues.ById("coffee")));
            Assert.AreEqual("day streak", MapText.DayStreak(2, 2));
            Assert.AreEqual("day streak " + MapText.Star + " best 5", MapText.DayStreak(2, 5));
            Assert.AreEqual("01", MapText.VenueNumber(0)); Assert.AreEqual("08", MapText.VenueNumber(7));
            Assert.AreEqual("record store basement, locked, needs 20,000 fans", MapText.VenueAria(records, false));
            Assert.AreEqual("natalia" + MapText.Apos + "s\nworld tour", MapText.Title);
        }

        [Test]
        public void DogHome_AsCopyJs()
        {
            Assert.AreEqual("oh hey. i napped for 4 days. ready when you are.", MapText.DogHome("rec center", "100", 5, 4, false));
            Assert.AreEqual("next stop: <b>rec center</b>. 26,700 fans to go.", MapText.DogHome("rec center", "26,700", 0, 1, false));
            Assert.AreEqual("we made it to the arena. every show from here is a victory lap.", MapText.DogHome(null, "0", 0, null, false));
            Assert.AreEqual("3 days in a row. don" + MapText.Apos + "t break the chain.", MapText.DogHome("x", "1", 3, 0, true));
            Assert.AreEqual("one show a day keeps the streak alive.", MapText.DogHome("x", "1", 1, 0, true));
        }

        [Test]
        public void VenueCopy_AsVenueJs()
        {
            Venue rec = Venues.ById("rec");
            Assert.AreEqual("stop 3 of 8 " + MapText.Star + " locked", MapText.StopOf(2, 8, false));
            Assert.AreEqual("stop 1 of 8 " + MapText.Star + " tonight", MapText.StopOf(0, 8, true));
            Assert.AreEqual("unlocks at <b>60,000</b> fans. 26,700 to go.", MapText.Need(rec, 33300));
            Assert.AreEqual("play at record store instead", MapText.PlayInstead(Venues.ById("records")));
        }

        [Test]
        public void ModeCatalog_IsTheWebRegistry_PlayableSoFar()
        {
            CollectionAssert.AreEqual(new[] { "slots", "vowels", "remix", "fanmail", "studio", "lyric", "prefix",
                "merch", "beat", "tickets", "stage", "truck", "budget", "soundcheck" }, ModeCatalog.All.Select(m => m.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "slots", "vowels", "remix", "fanmail", "studio", "lyric", "prefix" }, ModeCatalog.All.Where(m => m.Playable).Select(m => m.Id).ToArray());
            Assert.AreEqual("tap the letter stickers in order", ModeCatalog.ById("remix").Sub);
            Assert.AreEqual("cassette", ModeCatalog.ById("remix").Sticker);
            Assert.AreEqual(14, ModeCatalog.Infos.Count);
        }

        // ---------- model ----------

        const string Today = "2026-11-20";

        [Test]
        public void Model_FreshSave_CoffeeOnly()
        {
            SaveBlob s = SaveBlob.Defaults(Today);
            MapModel m = MapModel.Build(s, Today);
            Assert.AreEqual("coffee", m.Newest.Id); Assert.AreEqual("records", m.Next.Id);
            Assert.AreEqual(0, m.Reached); Assert.AreEqual(0, m.ReachedBefore);
            Assert.AreEqual(20000, m.ToGo);
            Assert.AreEqual(8, m.Venues.Count);
            Assert.IsTrue(m.Venues[0].Open && m.Venues[0].Here);
            Assert.IsFalse(m.Venues.Skip(1).Any(v => v.Open));
            CollectionAssert.IsEmpty(m.Fresh);
            Assert.IsFalse(m.Sleepy);
        }

        [Test]
        public void Model_AnUnlockSinceLastVisit_IsFresh_AndTheRouteGrowsFromTheVenueBefore()
        {
            SaveBlob s = SaveBlob.Defaults(Today);
            ProgressRules.AddFans(s, 65000);   // coffee + records + rec
            s.Venues.Seen.Add("records");      // records was shown on an earlier visit
            MapModel m = MapModel.Build(s, Today);
            Assert.AreEqual(2, m.Reached); Assert.AreEqual(1, m.ReachedBefore);
            CollectionAssert.AreEqual(new[] { "rec" }, m.Fresh);
            Assert.IsTrue(m.Venues[2].Fresh && m.Venues[2].Here && m.Venues[2].Open);
            Assert.AreEqual("rooftop", m.Next.Id); Assert.AreEqual(60000, m.ToGo);

            bool changed = MapModel.Commit(s, m.Fresh, Today, out var stickers);
            Assert.IsTrue(changed);
            CollectionAssert.Contains(s.Venues.Seen, "rec");
            CollectionAssert.IsSubsetOf(new[] { "venue-records", "venue-rec" }, stickers.Select(x => x.Id).ToList(), "map.js:17 awardStickers on mount");
            MapModel again = MapModel.Build(s, Today);
            CollectionAssert.IsEmpty(again.Fresh, "an unlock moment plays once");
            Assert.AreEqual(2, again.ReachedBefore);
            Assert.IsFalse(MapModel.Commit(s, again.Fresh, Today, out _), "nothing left to save");
        }

        [Test]
        public void Model_TwoUnlocksAtOnce_BothFresh_RouteFromCoffee()
        {
            SaveBlob s = SaveBlob.Defaults(Today);
            ProgressRules.AddFans(s, 61000);
            MapModel m = MapModel.Build(s, Today);
            CollectionAssert.AreEqual(new[] { "records", "rec" }, m.Fresh);
            Assert.AreEqual(0, m.ReachedBefore); Assert.AreEqual(2, m.Reached);
        }

        [Test]
        public void Model_AllVenues_NoNext_AndSleepyAfterThreeDaysAway()
        {
            SaveBlob s = SaveBlob.Defaults(Today);
            ProgressRules.AddFans(s, 900000);
            s.Streak.Last = "2026-11-16"; s.Streak.Days = 4; s.Streak.Best = 6;
            MapModel m = MapModel.Build(s, Today);
            Assert.IsNull(m.Next); Assert.AreEqual(0, m.ToGo);
            Assert.AreEqual(7, m.Reached);
            Assert.AreEqual(4, m.Away); Assert.IsTrue(m.Sleepy);
            Assert.AreEqual(0, m.Streak, "the streak lapsed (progress.js dayStreak)");
            Assert.AreEqual(6, m.Best);
        }
    }
}
