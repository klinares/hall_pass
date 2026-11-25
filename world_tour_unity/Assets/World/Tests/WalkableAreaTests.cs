using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WorldTour.World.Tests
{
    /// <summary>
    /// Phase 8.4 P0d: the ground she can walk on is one piece. The path finder walks a 0.4 m grid with a 0.4 m margin round every obstacle, so a gap between two props narrower than
    /// that is a wall to it (a straight line may squeeze through, a click-walk from anywhere else may not). Flood fill from the spawn over that same grid: the stage, the exit and a talking
    /// spot at every crew member must be in it, and so must every open patch of ground of any size (a fence of posts that cuts a side of the plaza off shows up as a big unreached patch).
    /// </summary>
    public class WalkableAreaTests
    {
        const string Scenes = "Assets/World/Scenes";
        const int BigPatchCells = 40;   // about 6 m2

        static readonly Dictionary<string, Vector2> TrunkSpots = new Dictionary<string, Vector2> { { "Place_Theater", new Vector2(-5f, -12f) }, { "Place_Rec", new Vector2(-3.6f, -8.4f) } };

        static Transform Find(string name) { var g = GameObject.Find(name); return g != null ? g.transform : null; }

        [Test]
        public void EveryPlace_WalkableGroundIsOnePiece()
        {
            var report = new List<string>();
            foreach (string file in Directory.GetFiles(Scenes, "Place_*.unity")) Check(file, null, report);
            Assert.IsEmpty(report, string.Join("\n", report));
        }

        [Test, Explicit("scan: which trunk spots in the theater keep the ground in one piece")]
        public void Scan_TheaterTrunkSpots()
        {
            var ok = new List<string>();
            for (float x = -11f; x <= 11f; x += 1f)
                for (float z = -12f; z <= 11f; z += 1f)
                {
                    var report = new List<string>();
                    Check(Scenes + "/Place_Theater.unity", new Vector2(x, z), report);
                    if (report.Count == 0) ok.Add(x.ToString("0.0") + "," + z.ToString("0.0"));
                }
            Debug.Log("WT_TRUNK_OK " + string.Join(" ", ok));
        }

        static void Check(string file, Vector2? trunkOverride, List<string> report)
        {
            {
                if (trunkOverride == null || EditorSceneManager.GetActiveScene().path != file) EditorSceneManager.OpenScene(file, OpenSceneMode.Single);
                string place = Path.GetFileNameWithoutExtension(file);
                var circles = new List<Vector3>();
                foreach (var o in Object.FindObjectsByType<Obstacle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    circles.Add(new Vector3(o.transform.position.x, o.transform.position.z, o.radius));
                // the wardrobe trunk (WorldShop puts it at these hand-picked spots in rec and theater): the trunk and the two ends of its rail, as WardrobeTrunk builds them
                Vector2 trunk = default;
                if (trunkOverride.HasValue) trunk = trunkOverride.Value;
                if (trunkOverride.HasValue || TrunkSpots.TryGetValue(place, out trunk))
                {
                    const float k = 1.3f;
                    circles.Add(new Vector3(trunk.x, trunk.y, 0.75f * k)); circles.Add(new Vector3(trunk.x - 0.85f * k, trunk.y + 0.55f * k, 0.3f * k)); circles.Add(new Vector3(trunk.x + 0.85f * k, trunk.y + 0.55f * k, 0.3f * k));
                }
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
                float cell = PathGrid.Cell;
                int n = Mathf.CeilToInt(2f * bounds / cell) + 1;
                var free = new bool[n * n];
                for (int j = 0; j < n; j++) for (int i = 0; i < n; i++) free[i + n * j] = !blocked(new Vector3(-bounds + i * cell, 0f, -bounds + j * cell));
                System.Func<Vector3, int> index = p => Mathf.Clamp(Mathf.RoundToInt((p.x + bounds) / cell), 0, n - 1) + n * Mathf.Clamp(Mathf.RoundToInt((p.z + bounds) / cell), 0, n - 1);

                // label the pieces of free ground (8 neighbours, no corner cutting, like the path finder)
                var label = new int[n * n];
                var sizes = new List<int> { 0 };
                for (int start = 0; start < label.Length; start++)
                {
                    if (!free[start] || label[start] != 0) continue;
                    int id = sizes.Count; sizes.Add(0);
                    var stack = new Stack<int>(); stack.Push(start); label[start] = id;
                    while (stack.Count > 0)
                    {
                        int cur = stack.Pop(); sizes[id]++;
                        int ci = cur % n, cj = cur / n;
                        for (int dj = -1; dj <= 1; dj++) for (int di = -1; di <= 1; di++)
                        {
                            if (di == 0 && dj == 0) continue;
                            int ni = ci + di, nj = cj + dj;
                            if (ni < 0 || nj < 0 || ni >= n || nj >= n) continue;
                            int nc = ni + n * nj;
                            if (!free[nc] || label[nc] != 0) continue;
                            if (di != 0 && dj != 0 && (!free[ci + di + n * cj] || !free[ci + n * (cj + dj)])) continue;
                            label[nc] = id; stack.Push(nc);
                        }
                    }
                }
                Transform spawnT = Find("PlayerSpawn");
                Vector3 spawn = spawnT.position; spawn.y = 0;
                int home = label[index(spawn)];
                if (home == 0) { report.Add(place + ": the spawn is inside a blocked cell"); return; }

                var goals = new List<(string, Vector3)>();
                if (Find("StageSpot") != null) goals.Add(("the stage", Find("StageSpot").position));
                if (Find("ExitSpot") != null) goals.Add(("the exit", Find("ExitSpot").position));
                for (int i = 1; i <= 3; i++)
                {
                    Transform c = Find("Crew_" + i);
                    if (c == null) continue;
                    Vector3 to = spawn - c.position; to.y = 0;
                    goals.Add(("crew " + i + " (talking spot)", c.position + (to.sqrMagnitude > 0.01f ? to.normalized : Vector3.back) * 1.7f));
                }
                foreach (var (name, g) in goals)
                {
                    // the game pushes a clicked or stood-on point out of blockers, so the nearest free cell within a metre counts
                    int best = -1; float bd = 1.3f;
                    Vector3 gp = new Vector3(g.x, 0, g.z);
                    for (int j = 0; j < n; j++) for (int i = 0; i < n; i++)
                    {
                        if (!free[i + n * j]) continue;
                        float d = Vector3.Distance(new Vector3(-bounds + i * cell, 0, -bounds + j * cell), gp);
                        if (d < bd) { bd = d; best = i + n * j; }
                    }
                    if (best < 0) report.Add(place + ": " + name + " has no free ground within a metre");
                    else if (label[best] != home) report.Add(place + ": " + name + " is on a patch of ground (" + sizes[label[best]] + " cells) the spawn cannot walk to");
                }
                for (int id = 1; id < sizes.Count; id++)
                    if (id != home && sizes[id] >= BigPatchCells)
                    {
                        float sx = 0, sz = 0; int cnt = 0;
                        for (int c = 0; c < label.Length; c++) if (label[c] == id) { sx += -bounds + (c % n) * cell; sz += -bounds + (c / n) * cell; cnt++; }
                        report.Add(place + ": a patch of open ground of " + (sizes[id] * cell * cell).ToString("0") + " m2 around (" + (sx / cnt).ToString("0.0") + ", " + (sz / cnt).ToString("0.0") + ") is cut off from the spawn");
                    }
            }
        }
    }
}
