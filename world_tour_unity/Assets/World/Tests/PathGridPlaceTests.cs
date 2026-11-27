using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WorldTour.World.Tests
{
    /// <summary>
    /// Phase 8.3: in each of the eight real places, the path finder gets her from the spawn to the stage spot, the exit and a talking spot at every crew member,
    /// round the obstacles and the crowd box exactly as the game sees them (the gameplay adds the crowd's keep-out box at run time; the test adds the same one).
    /// </summary>
    public class PathGridPlaceTests
    {
        const string Scenes = "Assets/World/Scenes";

        static Transform Find(string name) { var g = GameObject.Find(name); return g != null ? g.transform : null; }

        [Test]
        public void EveryPlace_SpawnReachesTheStageTheExitAndEveryCrewMember()
        {
            string[] files = Directory.GetFiles(Scenes, "Place_*.unity");
            Assert.GreaterOrEqual(files.Length, 8, "all eight places exist");
            var report = new List<string>();
            foreach (string file in files)
            {
                EditorSceneManager.OpenScene(file, OpenSceneMode.Single);
                string place = Path.GetFileNameWithoutExtension(file);
                var circles = new List<Vector3>();
                foreach (var o in Object.FindObjectsByType<Obstacle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    circles.Add(new Vector3(o.transform.position.x, o.transform.position.z, o.radius));
                var boxes = new List<Rect>();
                Transform crowd = Find("CrowdArea");
                if (crowd != null) { Vector3 c = crowd.position, s = crowd.lossyScale; boxes.Add(new Rect(c.x - Mathf.Abs(s.x) / 2f, c.z - Mathf.Abs(s.z) / 2f, Mathf.Abs(s.x), Mathf.Abs(s.z))); }
                foreach (var z in Object.FindObjectsByType<KeepOutZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) boxes.Add(z.Area);
                System.Func<Vector3, bool> blocked = p =>
                {
                    foreach (Vector3 c in circles) { float dx = p.x - c.x, dz = p.z - c.y, m = c.z + PlayerController.PlanMargin; if (dx * dx + dz * dz < m * m) return true; }
                    foreach (Rect r in boxes) if (p.x > r.xMin - PlayerController.PlanMargin && p.x < r.xMax + PlayerController.PlanMargin && p.z > r.yMin - PlayerController.PlanMargin && p.z < r.yMax + PlayerController.PlanMargin) return true;
                    return false;
                };
                var player = Object.FindFirstObjectByType<PlayerController>();
                float bounds = player != null ? player.bounds : 13.5f;
                Transform spawn = Find("PlayerSpawn");
                Assert.IsNotNull(spawn, place + " has a PlayerSpawn");
                Vector3 from = spawn.position; from.y = 0;

                var goals = new List<(string, Vector3, float)>();
                if (Find("StageSpot") != null) goals.Add(("the stage", Find("StageSpot").position, WorldGameRadius.Stage));
                if (Find("ExitSpot") != null) goals.Add(("the exit", Find("ExitSpot").position, WorldGameRadius.Exit));
                for (int i = 1; i <= 3; i++)
                {
                    Transform c = Find("Crew_" + i);
                    if (c == null) continue;
                    Vector3 to = from - c.position; to.y = 0;
                    Vector3 stand = c.position + (to.sqrMagnitude > 0.01f ? to.normalized : Vector3.back) * 1.7f;
                    goals.Add(("crew " + i, stand, 2.4f));
                }
                foreach (var (label, goal, within) in goals)
                {
                    Vector3 g = goal; g.y = 0;
                    // the game pushes a clicked point out of blockers first (0.45 m); do the same, so a stand point inside a prop is moved out
                    g = PushOutOfBlockers(g, circles, boxes, PlayerController.PlanMargin + 0.05f);
                    var path = PathGrid.Find(from, g, bounds, blocked, out bool reached);
                    Vector3 end = path.Count > 0 ? path[path.Count - 1] : from;
                    float miss = Vector3.Distance(end, goal);
                    if (!reached && miss > within) report.Add(place + ": " + label + " ends " + miss.ToString("0.0") + " m short");
                    else
                    {
                        Vector3 at = from;
                        foreach (Vector3 w in path)
                        {
                            if (!PathGrid.LineClear(at, w, blocked)) { report.Add(place + ": " + label + " has a leg through something"); break; }
                            at = w;
                        }
                    }
                    // and back again from there to the spawn
                    var back = PathGrid.Find(end, from, bounds, blocked, out bool backReached);
                    if (!backReached && !blocked(end)) report.Add(place + ": no way back from " + label);
                }
            }
            Assert.IsEmpty(report, string.Join("\n", report));
        }

        /// <summary>
        /// 10B.1 retest stall: in the club she was wedged between two chairs (-2.12, 2.86): the walk's push-out goes one blocker at a time, so between two overlapping chairs
        /// it left her inside one, a notch the planner sees as solid on every side (a click then had no path and did nothing for ever). Walking the keys' way (down and left
        /// from the north side of the chair row) must never end inside a chair now, and must keep her out of the notch.
        /// </summary>
        [Test]
        public void Club_WalkingIntoTheChairRowNeverWedgesHerBetweenTwoChairs()
        {
            EditorSceneManager.OpenScene(Scenes + "/Place_Club.unity", OpenSceneMode.Single);
            var circles = new List<Vector3>();
            foreach (var o in Object.FindObjectsByType<Obstacle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                circles.Add(new Vector3(o.transform.position.x, o.transform.position.z, o.radius));
            System.Func<Vector3, float, bool> blockedAt = (p, margin) =>
            {
                foreach (Vector3 c in circles) { float dx = p.x - c.x, dz = p.z - c.y, m = c.z + margin; if (dx * dx + dz * dz < m * m) return true; }
                return false;
            };
            System.Func<Vector3, Vector3> pushOut = p =>
            {
                foreach (Vector3 c in circles)
                {
                    float dx = p.x - c.x, dz = p.z - c.y, m = c.z + PlayerController.WalkMargin, d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d < m) { if (d < 1e-4f) { dx = 1f; d = 1f; } p.x = c.x + dx / d * m; p.z = c.y + dz / d * m; }
                }
                return p;
            };
            System.Func<Vector3, bool> blocked = p => blockedAt(p, PlayerController.StepMargin);
            int wedged = 0, plainWedged = 0;
            // walk from many starts north-east of the table and chair rows (where she spawns and walks from), heading down-left along the keys' diagonal and then straight left
            for (float sx = 0.5f; sx <= 3.5f; sx += 0.37f)
                for (float sz = 5.0f; sz <= 7.0f; sz += 0.23f)
                {
                    Vector3 a = new Vector3(sx, 0f, sz), b = a;
                    if (blockedAt(a, 0.35f)) continue;
                    for (int i = 0; i < 400; i++)
                    {
                        Vector3 dir = i < 200 ? new Vector3(-0.82f, 0f, -0.57f) : new Vector3(-1f, 0f, 0f);
                        Vector3 d = dir * (5f / 60f);
                        a = Obstacle.SlideStep(a, d, pushOut, blocked);
                        b = pushOut(b + d);
                        Assert.IsFalse(blocked(a), "never ends a step inside a chair, start " + sx + "," + sz + " step " + i + " at " + a);
                    }
                    bool Stuck(Vector3 at) { return blockedAt(at, PlayerController.PlanMargin) && PathGrid.FindEscaping(at, new Vector3(-5.6f, 0f, -0.6f), 13.5f, blockedAt, PlayerController.PlanMargin, PlayerController.TightMargin, out bool r).Count == 0; }
                    if (Stuck(a)) { wedged++; }
                    if (Stuck(b)) plainWedged++;
                }
            Assert.AreEqual(0, wedged, "with the slide step she is never wedged (the old plain push-out wedged " + plainWedged + " of these walks)");
        }

        [Test]
        public void SlideStep_SlidesAlongABlockerAndFreesAWedgedWalker()
        {
            // one wall-like blocker: a circle at the origin, margin 0.35 push-out, 0.34 test
            System.Func<Vector3, Vector3> push = p => { var d = new Vector2(p.x, p.z); float m = 1f + 0.35f; if (d.magnitude < m) { d = d.normalized * m; p.x = d.x; p.z = d.y; } return p; };
            System.Func<Vector3, bool> blocked = p => new Vector2(p.x, p.z).magnitude < 1f + 0.345f;
            Vector3 at = new Vector3(0f, 0f, 1.4f);
            Vector3 next = Obstacle.SlideStep(at, new Vector3(0f, 0f, -0.1f), push, blocked);
            Assert.IsFalse(blocked(next), "pushed out, never inside");
            Vector3 wedged = new Vector3(0f, 0f, 0.5f);   // already inside: the plain push-out is used so a step can take her out
            Assert.IsTrue(blocked(wedged));
            Assert.AreEqual(1.35f, new Vector2(Obstacle.SlideStep(wedged, new Vector3(0f, 0f, 0.1f), push, blocked).x, Obstacle.SlideStep(wedged, new Vector3(0f, 0f, 0.1f), push, blocked).z).magnitude, 1e-3f);
        }

        /// <summary>
        /// Round 8.4: not only from the spawn. Every point a player stands at (spawn, stage spot, exit, a talking spot at each crew member) must reach every other,
        /// the way a click-walk from the stage to the exit does.
        /// </summary>
        [Test]
        public void EveryPlace_EveryKeyPointReachesEveryOther()
        {
            var report = new List<string>();
            foreach (string file in Directory.GetFiles(Scenes, "Place_*.unity"))
            {
                EditorSceneManager.OpenScene(file, OpenSceneMode.Single);
                string place = Path.GetFileNameWithoutExtension(file);
                var circles = new List<Vector3>();
                foreach (var o in Object.FindObjectsByType<Obstacle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    circles.Add(new Vector3(o.transform.position.x, o.transform.position.z, o.radius));
                var boxes = new List<Rect>();
                Transform crowd = Find("CrowdArea");
                if (crowd != null) { Vector3 c = crowd.position, s = crowd.lossyScale; boxes.Add(new Rect(c.x - Mathf.Abs(s.x) / 2f, c.z - Mathf.Abs(s.z) / 2f, Mathf.Abs(s.x), Mathf.Abs(s.z))); }
                foreach (var z in Object.FindObjectsByType<KeepOutZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) boxes.Add(z.Area);
                System.Func<Vector3, bool> blocked = p =>
                {
                    foreach (Vector3 c in circles) { float dx = p.x - c.x, dz = p.z - c.y, m = c.z + PlayerController.PlanMargin; if (dx * dx + dz * dz < m * m) return true; }
                    foreach (Rect r in boxes) if (p.x > r.xMin - PlayerController.PlanMargin && p.x < r.xMax + PlayerController.PlanMargin && p.z > r.yMin - PlayerController.PlanMargin && p.z < r.yMax + PlayerController.PlanMargin) return true;
                    return false;
                };
                var player = Object.FindFirstObjectByType<PlayerController>();
                float bounds = player != null ? player.bounds : 13.5f;
                Vector3 spawn = Find("PlayerSpawn").position; spawn.y = 0;
                var pts = new List<(string, Vector3)> { ("spawn", spawn) };
                if (Find("StageSpot") != null) pts.Add(("stage", Find("StageSpot").position));
                if (Find("ExitSpot") != null) pts.Add(("exit", Find("ExitSpot").position));
                for (int i = 1; i <= 3; i++)
                {
                    Transform c = Find("Crew_" + i);
                    if (c == null) continue;
                    Vector3 to = spawn - c.position; to.y = 0;
                    pts.Add(("crew " + i, c.position + (to.sqrMagnitude > 0.01f ? to.normalized : Vector3.back) * 1.7f));
                }
                for (int i = 0; i < pts.Count; i++)
                    pts[i] = (pts[i].Item1, PushOutOfBlockers(new Vector3(pts[i].Item2.x, 0, pts[i].Item2.z), circles, boxes, PlayerController.PlanMargin + 0.05f));
                foreach (var (la, a) in pts)
                    foreach (var (lb, b) in pts)
                    {
                        if (la == lb) continue;
                        var legs = PathGrid.Find(a, b, bounds, blocked, out bool reached);
                        if (la == "stage" && lb == "exit") Debug.Log("WT_ROUTE " + place + " " + string.Join(" ", legs.ConvertAll(w => w.x.ToString("0.0") + "," + w.z.ToString("0.0"))));
                        if (!reached) report.Add(place + ": " + la + " cannot reach " + lb);
                    }
            }
            Assert.IsEmpty(report, string.Join("\n", report));
        }

        static Vector3 PushOutOfBlockers(Vector3 p, List<Vector3> circles, List<Rect> boxes, float margin)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (Vector3 c in circles)
                {
                    Vector2 d = new Vector2(p.x - c.x, p.z - c.y); float min = c.z + margin, m = d.magnitude;
                    if (m < min) { if (m < 1e-4f) d = Vector2.right; else d /= m; p.x = c.x + d.x * min; p.z = c.y + d.y * min; }
                }
                foreach (Rect r in boxes) p = KeepOutZone.PushOutOf(r, p, margin);
            }
            return p;
        }

        /// <summary>The stage and exit trigger radii of WorldGame (1.4 and 1.5 m), repeated here because the World tests do not see the Gameplay assembly.</summary>
        static class WorldGameRadius { public const float Stage = 1.4f, Exit = 1.5f; }
    }
}
