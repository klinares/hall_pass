using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WorldTour.World.Wardrobe;

namespace WorldTour.World.Tests
{
    /// <summary>Phase 8.3 look fix: riff trots nose first. The Riff prefab's nose is at the root's -z, so the follower turns the root's +z away from the star.</summary>
    public class DogFacingTests
    {
        const string RiffPath = "Assets/World/Prefabs/Riff.prefab";
        GameObject _riff;

        [TearDown]
        public void TearDown() { if (_riff != null) Object.DestroyImmediate(_riff); }

        [Test]
        public void Facing_PointsTheNoseTheWayHeTravels_ForEveryHeading()
        {
            _riff = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RiffPath));
            var follower = _riff.GetComponent<DogFollower>();
            Assert.IsNotNull(follower);
            Assert.AreEqual(DogWardrobe.On(_riff).NoseYaw, follower.noseYaw, 0.1f, "the follower's half turn matches where the prefab's nose is");
            foreach (float yaw in new[] { 0f, 45f, 90f, 135f, 180f, -90f, -170f })
            {
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                _riff.transform.rotation = DogFollower.Facing(dir, follower.noseYaw);
                Vector3 nose = DogWardrobe.On(_riff).NoseDirection; nose.y = 0f;
                Assert.Greater(Vector3.Dot(nose.normalized, dir), 0.98f, "nose along heading " + yaw + ": " + nose);
            }
        }
    }
}
