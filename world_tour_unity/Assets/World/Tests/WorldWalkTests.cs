using NUnit.Framework;
using UnityEngine;

namespace WorldTour.World.Tests
{
    /// <summary>Phase 8.1 fix round 1: the keep-out box round the crowd, the steering round it, and the "is something between the lens and her" test.</summary>
    public class WorldWalkTests
    {
        // the coffee shop's crowd: centre (9, 3.3), 8.2 x 4.3
        static readonly Rect Crowd = new Rect(9f - 4.1f, 3.3f - 2.15f, 8.2f, 4.3f);

        static Vector3 P(float x, float z) { return new Vector3(x, 0f, z); }

        [Test]
        public void PushOutOf_MovesAPointInsideOutThroughTheNearestEdge_AndLeavesOutsidePointsAlone()
        {
            Vector3 q = KeepOutZone.PushOutOf(Crowd, P(9f, 3.3f), 0.35f);
            Assert.IsTrue(Mathf.Abs(q.z - (Crowd.yMin - 0.35f)) < 1e-3f || Mathf.Abs(q.z - (Crowd.yMax + 0.35f)) < 1e-3f, "ends on a grown edge: " + q);
            Assert.AreEqual(q, KeepOutZone.PushOutOf(Crowd, q, 0.35f), "and stays put when pushed again");
            Vector3 near = KeepOutZone.PushOutOf(Crowd, P(5f, 3.3f), 0.35f);
            Assert.AreEqual(Crowd.xMin - 0.35f, near.x, 1e-4f, "nearest edge is the west one");
            Assert.AreEqual(P(2f, 3f), KeepOutZone.PushOutOf(Crowd, P(2f, 3f), 0.35f));
        }

        [Test]
        public void SegmentHits_SeesALineThroughTheBoxAndNotOneBesideIt()
        {
            Assert.IsTrue(KeepOutZone.SegmentHits(Crowd, P(9f, -5f), P(9f, 9f)), "straight through");
            Assert.IsFalse(KeepOutZone.SegmentHits(Crowd, P(2f, -5f), P(2f, 9f)), "west of it");
            Assert.IsFalse(KeepOutZone.SegmentHits(Crowd, P(9f, -5f), P(9f, 1f)), "stops short");
            Assert.IsFalse(KeepOutZone.SegmentHits(Crowd, P(4f, 6f), P(14f, 6f)), "along the strip in front of the ramp");
        }

        [Test]
        public void SteerAround_GoesToACornerWhenTheLineCrossesTheBox_AndStraightWhenItDoesNot()
        {
            Vector3 goal = P(9f, 9.2f);   // the stage spot, behind the crowd seen from the street
            Vector3 g = KeepOutZone.SteerAroundRect(Crowd, P(9f, -3f), goal, 0.6f);
            Assert.AreNotEqual(goal, g, "the crowd is in the way");
            Assert.IsTrue(g.x < Crowd.xMin || g.x > Crowd.xMax, "round the side");
            Assert.AreEqual(goal, KeepOutZone.SteerAroundRect(Crowd, P(1f, 7f), goal, 0.6f), "clear line from the west strip");
        }

        [Test]
        public void SteerAround_ArrivesAtTheStageByWalkingRoundTheCrowd()
        {
            Vector3 pos = P(9f, -3f), goal = P(9f, 9.2f);
            for (int i = 0; i < 400 && Vector3.Distance(pos, goal) > 0.15f; i++)
            {
                Vector3 aim = KeepOutZone.SteerAroundRect(Crowd, pos, goal, 0.6f) - pos;
                pos += aim.normalized * Mathf.Min(0.1f, aim.magnitude);
                pos = KeepOutZone.PushOutOf(Crowd, pos, 0.35f);
                Assert.IsFalse(pos.x > Crowd.xMin && pos.x < Crowd.xMax && pos.z > Crowd.yMin && pos.z < Crowd.yMax, "never inside the crowd at step " + i);
            }
            Assert.Less(Vector3.Distance(pos, goal), 0.2f, "got there");
        }

        [Test]
        public void Blocks_AThingOnTheLineHidesHer_AndAThingBesideItOrAtHerFeetDoesNot()
        {
            Vector3 cam = new Vector3(0f, 6.6f, -11.4f), chest = new Vector3(0f, 1f, 0f);
            // a tree trunk and crown halfway: x 0, z -6, 3 m wide, 5 m tall
            Assert.IsTrue(FollowCamera.Blocks(new Bounds(new Vector3(0f, 2.5f, -6f), new Vector3(3f, 5f, 3f)), cam, chest, 0.7f, 1.2f));
            // a tree off to the side
            Assert.IsFalse(FollowCamera.Blocks(new Bounds(new Vector3(6f, 2.5f, -6f), new Vector3(3f, 5f, 3f)), cam, chest, 0.7f, 1.2f));
            // a bench at her feet
            Assert.IsFalse(FollowCamera.Blocks(new Bounds(new Vector3(0f, 0.8f, 0.3f), new Vector3(2f, 1.6f, 1f)), cam, chest, 0.7f, 1.2f));
            // a wall the lens is inside of
            Assert.IsTrue(FollowCamera.Blocks(new Bounds(new Vector3(0f, 5f, -11f), new Vector3(10f, 10f, 2f)), cam, chest, 0.7f, 1.2f));
            // something behind her
            Assert.IsFalse(FollowCamera.Blocks(new Bounds(new Vector3(0f, 3f, 5f), new Vector3(6f, 6f, 1f)), cam, chest, 0.7f, 1.2f));
        }

        [Test]
        public void HiddenThingsStayHiddenAcrossTheCamerasRescan_AndComeBackWhenTheWayIsClear()
        {
            var cam = new GameObject("cam", typeof(Camera), typeof(FollowCamera));
            var who = new GameObject("who");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                who.transform.position = Vector3.zero;
                var f = cam.GetComponent<FollowCamera>();
                f.target = who.transform;
                wall.transform.position = new Vector3(0f, 2.5f, -6f);
                wall.transform.localScale = new Vector3(3f, 5f, 1f);
                var r = wall.GetComponent<MeshRenderer>();
                var before = r.shadowCastingMode;
                f.SnapForPreview();
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly, r.shadowCastingMode, "in the way: hidden");
                f.RefreshForTest();
                f.RefreshForTest();
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly, r.shadowCastingMode, "still hidden after rescans (it used to come back after 1.5 s)");
                wall.transform.position = new Vector3(9f, 2.5f, -6f);
                f.SnapForPreview();
                Assert.AreEqual(before, r.shadowCastingMode, "out of the way: back as it was");
            }
            finally { Object.DestroyImmediate(cam); Object.DestroyImmediate(who); Object.DestroyImmediate(wall); }
        }

        [Test]
        public void IsCandidate_SkipsFloorsAndWholeDistricts_KeepsTreesAwningsAndPosts()
        {
            Assert.IsFalse(FollowCamera.IsCandidate(new Bounds(new Vector3(0, 0.05f, 0), new Vector3(40, 0.1f, 40))), "ground");
            Assert.IsFalse(FollowCamera.IsCandidate(new Bounds(new Vector3(0, 0.3f, 0), new Vector3(2, 0.6f, 2))), "kerb");
            Assert.IsTrue(FollowCamera.IsCandidate(new Bounds(new Vector3(0, 2.5f, 0), new Vector3(3, 5, 3))), "tree");
            Assert.IsTrue(FollowCamera.IsCandidate(new Bounds(new Vector3(0, 2.8f, 0), new Vector3(17, 0.5f, 2))), "a thin awning high up");
            Assert.IsTrue(FollowCamera.IsCandidate(new Bounds(new Vector3(0, 0.55f, 0), new Vector3(2.4f, 1.1f, 1))), "a counter top");
            Assert.IsFalse(FollowCamera.IsCandidate(new Bounds(new Vector3(0, 2f, 0), new Vector3(100, 4, 4))), "the whole map");
            Assert.IsTrue(FollowCamera.IsCandidate(new Bounds(new Vector3(0, 2f, 0), new Vector3(0.3f, 4, 0.3f))), "lamp post");
        }
    }
}
