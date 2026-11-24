using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WorldTour.World.Audio;

namespace WorldTour.World.Tests
{
    /// <summary>Phase 8.3: path finding on the flat ground (grid A* plus line-of-sight pulling), and what each place sounds like.</summary>
    public class PathGridTests
    {
        const float Bounds = 13.5f, Margin = 0.4f;

        static Vector3 P(float x, float z) { return new Vector3(x, 0f, z); }

        /// <summary>A blocked test over circles (x, z, radius in a Vector3) and boxes (Rect in x and z), like Obstacle.IsBlocked.</summary>
        static Func<Vector3, bool> Ground(IList<Vector3> circles, IList<Rect> boxes)
        {
            return p =>
            {
                foreach (Vector3 c in circles) { float dx = p.x - c.x, dz = p.z - c.y; if (dx * dx + dz * dz < (c.z + Margin) * (c.z + Margin)) return true; }
                foreach (Rect r in boxes) if (p.x > r.xMin - Margin && p.x < r.xMax + Margin && p.z > r.yMin - Margin && p.z < r.yMax + Margin) return true;
                return false;
            };
        }

        static float Length(Vector3 from, List<Vector3> path)
        {
            float len = 0f; Vector3 at = from;
            foreach (Vector3 w in path) { len += Vector3.Distance(at, w); at = w; }
            return len;
        }

        static void AssertPathClear(Vector3 from, List<Vector3> path, Func<Vector3, bool> blocked)
        {
            Vector3 at = from;
            foreach (Vector3 w in path)
            {
                Assert.IsTrue(PathGrid.LineClear(at, w, blocked), "the leg " + at + " to " + w + " crosses something");
                at = w;
            }
        }

        [Test]
        public void ClearGround_IsOneStraightLeg()
        {
            var blocked = Ground(new List<Vector3> { new Vector3(8f, 8f, 1f) }, new List<Rect>());
            bool reached;
            var path = PathGrid.Find(P(0f, 0f), P(5f, -4f), Bounds, blocked, out reached);
            Assert.IsTrue(reached);
            Assert.AreEqual(1, path.Count);
            Assert.AreEqual(P(5f, -4f), path[0]);
        }

        [Test]
        public void ARoundObstacleInTheWay_IsWalkedAroundNotThrough()
        {
            var blocked = Ground(new List<Vector3> { new Vector3(0f, 0f, 1.5f) }, new List<Rect>());
            bool reached;
            var path = PathGrid.Find(P(-5f, 0f), P(5f, 0f), Bounds, blocked, out reached);
            Assert.IsTrue(reached);
            Assert.GreaterOrEqual(path.Count, 2, "bends round it");
            AssertPathClear(P(-5f, 0f), path, blocked);
            Assert.Less(Length(P(-5f, 0f), path), 13f, "and not by a long way (straight is 10)");
        }

        [Test]
        public void ADeadEnd_IsEscapedByGoingBackOut()
        {
            // a U of boxes open to the west: she starts inside it and the goal is east of it. A side-step cannot do this; a search can.
            var boxes = new List<Rect>
            {
                new Rect(-1f, 2f, 6f, 0.6f),       // top arm
                new Rect(-1f, -2.6f, 6f, 0.6f),    // bottom arm
                new Rect(4.4f, -2.6f, 0.6f, 5.2f), // the back, east
            };
            var blocked = Ground(new List<Vector3>(), boxes);
            bool reached;
            var start = P(2f, 0f);
            var path = PathGrid.Find(start, P(9f, 0f), Bounds, blocked, out reached);
            Assert.IsTrue(reached, "round the whole U");
            AssertPathClear(start, path, blocked);
            Assert.Greater(Length(start, path), 14f, "it had to go out of the open side and round");
        }

        [Test]
        public void AClosedOffGoal_EndsAtTheNearestReachablePoint()
        {
            // a ring of boxes with the goal inside it
            var boxes = new List<Rect>
            {
                new Rect(-3f, 3f, 6f, 0.5f), new Rect(-3f, -3.5f, 6f, 0.5f), new Rect(-3.5f, -3.5f, 0.5f, 7f), new Rect(3f, -3.5f, 0.5f, 7f),
            };
            var blocked = Ground(new List<Vector3>(), boxes);
            bool reached;
            var path = PathGrid.Find(P(8f, 0f), P(0f, 0f), Bounds, blocked, out reached);
            Assert.IsFalse(reached);
            Assert.GreaterOrEqual(path.Count, 1);
            Vector3 end = path[path.Count - 1];
            Assert.IsFalse(blocked(end), "ends on free ground");
            Assert.Less(Vector3.Distance(end, P(0f, 0f)), Vector3.Distance(P(8f, 0f), P(0f, 0f)), "and nearer than where she began");
            Assert.Less(Vector3.Distance(end, P(0f, 0f)), 6.5f);
        }

        [Test]
        public void AGoalInsideAnObstacle_StopsBesideIt()
        {
            var blocked = Ground(new List<Vector3> { new Vector3(4f, 0f, 1f) }, new List<Rect>());
            bool reached;
            var path = PathGrid.Find(P(-4f, 0f), P(4f, 0f), Bounds, blocked, out reached);
            Assert.IsFalse(reached);
            Vector3 end = path[path.Count - 1];
            Assert.IsFalse(blocked(end));
            Assert.Less(Vector3.Distance(end, P(4f, 0f)), 3f, "close to the goal");
        }

        [Test]
        public void TheCoffeeShopCrowdAndStageDeck_AreCrossedBothWays()
        {
            // the coffee street: the crowd box (9, 3.3, 8.2 x 4.3) between the street and the stage at (9, 9.2), a stage deck whose sides are blockers
            var circles = new List<Vector3>();
            for (float z = 6.4f; z <= 11f; z += 1f) { circles.Add(new Vector3(5.2f, z, 0.5f)); circles.Add(new Vector3(12.8f, z, 0.5f)); }
            var boxes = new List<Rect> { new Rect(9f - 4.1f, 3.3f - 2.15f, 8.2f, 4.3f) };
            var blocked = Ground(circles, boxes);
            bool reached;
            var there = PathGrid.Find(P(9f, -3f), P(9f, 9.2f), Bounds, blocked, out reached);
            Assert.IsTrue(reached, "street to the stage");
            AssertPathClear(P(9f, -3f), there, blocked);
            var back = PathGrid.Find(P(9f, 9.2f), P(2f, -2f), Bounds, blocked, out reached);
            Assert.IsTrue(reached, "stage to the street");
            AssertPathClear(P(9f, 9.2f), back, blocked);
        }

        [Test]
        public void EveryReachablePointOnAnOpenFloorIsReached()
        {
            var circles = new List<Vector3> { new Vector3(-4f, -4f, 1f), new Vector3(3f, 2f, 1.2f), new Vector3(-2f, 6f, 0.8f), new Vector3(7f, -6f, 1f) };
            var boxes = new List<Rect> { new Rect(-1f, -1f, 3f, 1.5f), new Rect(5f, 3f, 1f, 6f) };
            var blocked = Ground(circles, boxes);
            var rng = new System.Random(5);
            int tried = 0;
            for (int i = 0; i < 60; i++)
            {
                Vector3 a = P((float)(rng.NextDouble() * 26 - 13), (float)(rng.NextDouble() * 26 - 13)), b = P((float)(rng.NextDouble() * 26 - 13), (float)(rng.NextDouble() * 26 - 13));
                if (blocked(a) || blocked(b)) continue;
                tried++;
                bool reached;
                var path = PathGrid.Find(a, b, Bounds, blocked, out reached);
                Assert.IsTrue(reached, a + " to " + b);
                AssertPathClear(a, path, blocked);
                Assert.AreEqual(b, path[path.Count - 1]);
            }
            Assert.Greater(tried, 25);
        }

        // ---------- the sound plan ----------

        [Test]
        public void EveryPlace_HasAGroundAndABed()
        {
            foreach (var v in WorldTour.Core.Data.Venues.All)
            {
                Surface s = AudioPlan.SurfaceFor(v.Id);
                Assert.Greater(AudioPlan.StepPitch(s), 0.5f);
                Assert.Greater(AudioPlan.StepVolume(s), 0.1f);
                Assert.Less(AudioPlan.StepVolume(s), 0.7f, "gentle");
                Assert.Less(AudioPlan.BedVolume(AudioPlan.BedFor(v.Id)), 0.35f, "the bed stays quiet");
            }
            Assert.AreEqual(Bed.Birds, AudioPlan.BedFor("coffee"));
            Assert.AreEqual(Bed.Birds, AudioPlan.BedFor("festival"));
            foreach (string id in new[] { "records", "club", "rooftop" }) Assert.AreEqual(Bed.City, AudioPlan.BedFor(id), id);
            foreach (string id in new[] { "rec", "theater", "arena" }) Assert.AreEqual(Bed.Hall, AudioPlan.BedFor(id), id);
            Assert.AreEqual(Surface.Grass, AudioPlan.SurfaceFor("festival"));
        }

        [Test]
        public void EveryClipTheWorldSoundLoads_IsInTheProject_AndShort()
        {
            string[] names = { "step_1", "step_2", "step_3", "step_4", "amb_birds", "amb_murmur", "cheer_applause", "ui_open", "ui_close", "ui_whoosh", "jingle_done", "jingle_unlock" };
            foreach (string n in names)
            {
                var clip = Resources.Load<AudioClip>("WorldAudio/" + n);
                Assert.IsNotNull(clip, n);
                Assert.AreEqual(1, clip.channels, n + " is mono");
                Assert.Less(clip.length, 25f, n);
                if (n.StartsWith("step_") || n.StartsWith("ui_")) Assert.Less(clip.length, 0.5f, n + " is a short click");
                if (n.StartsWith("jingle_")) Assert.Less(clip.length, 2f, n + " is a short jingle");
            }
        }

        [Test]
        public void CrowdMurmur_IsLouderWhenFullerAndCloser_AndSteadyInAShow()
        {
            Assert.AreEqual(0f, AudioPlan.CrowdVolume(0f, 2f, false), 1e-4f);
            Assert.Greater(AudioPlan.CrowdVolume(1f, 2f, false), AudioPlan.CrowdVolume(0.3f, 2f, false));
            Assert.Greater(AudioPlan.CrowdVolume(0.8f, 3f, false), AudioPlan.CrowdVolume(0.8f, 9f, false));
            Assert.AreEqual(0f, AudioPlan.CrowdVolume(1f, 20f, false), 1e-4f, "gone far away");
            Assert.Greater(AudioPlan.CrowdVolume(0.8f, 30f, true), 0.1f, "a show keeps it up wherever the camera is");
            Assert.LessOrEqual(AudioPlan.CrowdVolume(1f, 0f, true), 0.4f);
        }
    }
}
