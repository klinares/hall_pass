using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldTour.Core.Data;
using WorldTour.World.Gameplay;

namespace WorldTour.World.Tests
{
    /// <summary>Phase 8.3: every place's scene leaves a free spot near the spawn for the wardrobe trunk (the same search WorldShop runs at run time, on the scene's own blockers).</summary>
    public class WardrobePlacesTests
    {
        /// <summary>The blockers of a loaded place: its Obstacle circles, its KeepOutZone boxes, and the crowd box WorldGame adds at run time.</summary>
        public static Func<Vector3, float, bool> BlockedFunc(Scene scene)
        {
            var circles = new List<KeyValuePair<Vector3, float>>();
            var boxes = new List<Rect>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Obstacle o in root.GetComponentsInChildren<Obstacle>(false)) circles.Add(new KeyValuePair<Vector3, float>(o.transform.position, o.radius));
                foreach (KeepOutZone k in root.GetComponentsInChildren<KeepOutZone>(false)) boxes.Add(k.Area);
                foreach (Transform t in root.GetComponentsInChildren<Transform>(false))
                    if (t.name == "CrowdArea") { Vector3 c = t.position, s = t.lossyScale; boxes.Add(new Rect(c.x - Mathf.Abs(s.x) / 2f, c.z - Mathf.Abs(s.z) / 2f, Mathf.Abs(s.x), Mathf.Abs(s.z))); }
            }
            return (p, margin) =>
            {
                foreach (var c in circles)
                {
                    float dx = p.x - c.Key.x, dz = p.z - c.Key.z, min = c.Value + margin;
                    if (dx * dx + dz * dz < min * min) return true;
                }
                foreach (Rect r in boxes) if (p.x > r.xMin - margin && p.x < r.xMax + margin && p.z > r.yMin - margin && p.z < r.yMax + margin) return true;
                return false;
            };
        }

        public static Vector3? Find(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t.position;
            return null;
        }

        [Test]
        public void EveryPlace_HasAFreeSpotForTheTrunk_ClearOfCrewStageAndExit()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);   // so a place scene is never the only one loaded when it is closed
            int found = 0;
            foreach (Venue v in Venues.All)
            {
                string path = "Assets/World/Scenes/" + WorldPlace.SceneName(v.Id) + ".unity";
                if (!System.IO.File.Exists(path)) continue;
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    Vector3 spawn = Find(scene, "PlayerSpawn").Value;
                    var crew = new List<Vector3>();
                    for (int i = 1; i <= 3; i++) { Vector3? c = Find(scene, "Crew_" + i); if (c.HasValue) crew.Add(c.Value); }
                    Vector3 spot;
                    bool ok = WorldShop.PickSpot(spawn, BlockedFunc(scene), crew, Find(scene, "StageSpot"), Find(scene, "ExitSpot"), out spot, v.Id);
                    Vector3 want;
                    if (WorldShop.TryChosenSpot(v.Id, out want)) Assert.Less(Vector3.Distance(spot, want), 0.01f, v.Id + ": the hand-picked trunk spot is free, so it is the one used");
                    if (!ok)
                    {
                        var near = new List<string>();
                        foreach (GameObject root in scene.GetRootGameObjects())
                        {
                            foreach (Obstacle o in root.GetComponentsInChildren<Obstacle>(false)) if (Vector3.Distance(o.transform.position, spawn) < 13f) near.Add("O" + o.transform.position.x.ToString("0.0") + "," + o.transform.position.z.ToString("0.0") + "r" + o.radius.ToString("0.0"));
                            foreach (KeepOutZone k in root.GetComponentsInChildren<KeepOutZone>(false)) near.Add("K" + k.Area);
                        }
                        Debug.Log("WT_TRUNK_NONE " + v.Id + " spawn=" + spawn + " crew=" + string.Join(";", crew) + " stage=" + Find(scene, "StageSpot") + " exit=" + Find(scene, "ExitSpot") + " near=" + string.Join(" ", near));
                    }
                    Assert.IsTrue(ok, v.Id + ": no free spot beside the spawn");
                    Assert.Less(Vector3.Distance(new Vector3(spot.x, 0, spot.z), new Vector3(spawn.x, 0, spawn.z)), 11f, v.Id + ": near the spawn");
                    foreach (Vector3 c in crew) Assert.Greater(Vector3.Distance(new Vector3(spot.x, 0, spot.z), new Vector3(c.x, 0, c.z)), 3.3f, v.Id + ": clear of the crew");
                    Debug.Log("WT_TRUNK_SPOT " + v.Id + " " + spot.x.ToString("0.0") + " " + spot.z.ToString("0.0"));
                    found++;
                }
                finally { EditorSceneManager.CloseScene(scene, true); }
            }
            Assert.GreaterOrEqual(found, 8, "all eight places are built");
        }
    }
}
