using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldTour.World.Gameplay;

namespace WorldTour.World.Tests
{
    /// <summary>Phase 8.4 fix round 2: crew stand well apart (a press of E must pick the one beside her), and the theater's ropes leave a straight way from the spawn to the exit.</summary>
    public class PlaceLayoutTests
    {
        const string Scenes = "Assets/World/Scenes";
        public const float MinCrewGap = 2.5f;

        [Test]
        public void EveryPlace_CrewStandAtLeastTwoAndAHalfMetresApart()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            int places = 0;
            foreach (string file in Directory.GetFiles(Scenes, "Place_*.unity"))
            {
                Scene scene = EditorSceneManager.OpenScene(file, OpenSceneMode.Additive);
                try
                {
                    var crew = new List<Vector3>();
                    for (int i = 1; i <= 3; i++) { Vector3? c = WardrobePlacesTests.Find(scene, "Crew_" + i); if (c.HasValue) crew.Add(c.Value); }
                    Assert.AreEqual(3, crew.Count, file + ": three crew");
                    for (int a = 0; a < crew.Count; a++)
                        for (int b = a + 1; b < crew.Count; b++)
                        {
                            Vector3 d = crew[a] - crew[b]; d.y = 0;
                            Assert.GreaterOrEqual(d.magnitude, MinCrewGap, file + ": crew " + (a + 1) + " and " + (b + 1) + " are " + d.magnitude.ToString("0.0") + " m apart");
                        }
                    places++;
                }
                finally { EditorSceneManager.CloseScene(scene, true); }
            }
            Assert.GreaterOrEqual(places, 8);
        }

        [Test]
        public void Theater_TheWayFromTheSpawnToTheExitIsNearlyStraight()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Scene scene = EditorSceneManager.OpenScene(Scenes + "/Place_Theater.unity", OpenSceneMode.Additive);
            try
            {
                var blocked = WardrobePlacesTests.BlockedFunc(scene);
                System.Func<Vector3, bool> isBlocked = p => blocked(p, PlayerController.PlanMargin);
                Vector3 spawn = WardrobePlacesTests.Find(scene, "PlayerSpawn").Value, exit = WardrobePlacesTests.Find(scene, "ExitSpot").Value;
                // the exit ring is entered from its edge: aim for a point 1.2 m short of the middle
                Vector3 to = spawn - exit; to.y = 0;
                Vector3 goal = exit + to.normalized * 1.2f;
                bool reached;
                var path = PathGrid.Find(spawn, goal, 13.5f, isBlocked, out reached);
                Assert.IsTrue(reached, "the exit can be reached");
                float len = 0f; Vector3 prev = spawn;
                foreach (Vector3 w in path) { Vector3 d = w - prev; d.y = 0; len += d.magnitude; prev = w; }
                Vector3 direct = goal - spawn; direct.y = 0;
                Assert.LessOrEqual(len, direct.magnitude * 1.15f, "the path to the exit is " + len.ToString("0.0") + " m for " + direct.magnitude.ToString("0.0") + " m as the crow flies, so something is in the way (ropes, props)");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
