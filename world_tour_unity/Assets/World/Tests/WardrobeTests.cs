using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WorldTour.Core.Meta;
using WorldTour.World.Wardrobe;

namespace WorldTour.World.Tests
{
    /// <summary>Phase 8.3: the wardrobe on the Star and riff prefabs, the stage tint and the trunk, built from the real prefabs and assets.</summary>
    public class WardrobeTests
    {
        const string StarPath = "Assets/World/Prefabs/Star.prefab", RiffPath = "Assets/World/Prefabs/Riff.prefab";
        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject g in _made) if (g != null) Object.DestroyImmediate(g);
            _made.Clear();
        }

        GameObject Make(string path)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            _made.Add(g);
            return g;
        }

        static WardrobeLook Look(params string[] ids)
        {
            var l = new WardrobeLook { Outfit = "outfit:tour", Hair = "hair:chestnut", Guitar = "guitar:classic", Dog = "dog:none", Lights = "lights:tour" };
            foreach (string id in ids) l.Set(id);
            return l;
        }

        static Transform Named(GameObject root, string name) { return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name); }

        [Test]
        public void Assets_HaveAMaterialSetForEveryOutfitAndHairColour_AndTheMeshesTheAccessoriesNeed()
        {
            WardrobeAssets a = WardrobeAssets.Load();
            Assert.IsNotNull(a, "Resources/WardrobeAssets (run World Tour > Build wardrobe)");
            foreach (ShopItem it in Shop.World.Where(i => i.Slot == Shop.Outfit))
            {
                OutfitSet o = a.outfits.FirstOrDefault(x => x.id == it.Id);
                Assert.IsNotNull(o, it.Id); Assert.IsNotNull(o.top, it.Id + " top"); Assert.IsNotNull(o.legs, it.Id + " legs");
            }
            foreach (ShopItem it in Shop.World.Where(i => i.Slot == Shop.Hair))
            {
                HairSet h = a.hair.FirstOrDefault(x => x.id == it.Id);
                Assert.IsNotNull(h, it.Id); Assert.IsNotNull(h.head, it.Id + " head");
            }
            Assert.IsNotNull(a.baseLit); Assert.IsNotNull(a.cube); Assert.IsNotNull(a.sphere); Assert.IsNotNull(a.cylinder);
            var tops = a.outfits.Select(o => o.top).Distinct().Count();
            Assert.AreEqual(a.outfits.Length, tops, "every outfit has its own jacket material");
        }

        [Test]
        public void Anchors_AreMadeOnTheBones_AlignedAndInMetres_WithoutChangingThePrefabs()
        {
            GameObject star = Make(StarPath), riff = Make(RiffPath);
            Assert.IsNull(Named(star, StarWardrobe.FrontAnchor), "the prefab itself has none");
            foreach (var pair in new[] { (star, StarWardrobe.FrontAnchor), (riff, DogWardrobe.HeadAnchor), (riff, DogWardrobe.NeckAnchor) })
            {
                Transform t = WardrobeAnchors.Ensure(pair.Item1, pair.Item2);
                Assert.AreSame(t, WardrobeAnchors.Ensure(pair.Item1, pair.Item2), "asking twice finds the same one");
                Assert.IsNotNull(t, pair.Item2);
                Assert.AreEqual(1f, t.lossyScale.x, 0.01f, pair.Item2 + " children are laid out in metres");
                Assert.AreEqual(1f, t.lossyScale.y, 0.01f); Assert.AreEqual(1f, t.lossyScale.z, 0.01f);
            }
            Assert.AreEqual(0f, Quaternion.Angle(riff.transform.rotation * Quaternion.Euler(0f, 180f, 0f), Named(riff, DogWardrobe.HeadAnchor).rotation), 1f, "the head anchor faces his nose (the root's -z)");
            Assert.AreEqual(180f, DogWardrobe.On(riff).NoseYaw, 0.1f, "so a pose that points the root's +z needs a half turn");
            float tilt = Vector3.Angle(Vector3.up, Named(riff, DogWardrobe.NeckAnchor).up);
            Assert.That(tilt, Is.InRange(10f, 60f), "the neck ring is tilted to the neck: " + tilt);
            Assert.AreEqual(0f, Quaternion.Angle(star.transform.rotation, Named(star, StarWardrobe.FrontAnchor).rotation), 1f, "aligned with the Star");
            Assert.AreEqual(0.62f, Named(star, StarWardrobe.FrontAnchor).position.y, 0.01f);
            Assert.Less(Named(riff, DogWardrobe.HeadAnchor).position.y, 1.2f);
            Assert.Greater(Named(riff, DogWardrobe.HeadAnchor).position.y, Named(riff, DogWardrobe.NeckAnchor).position.y - 0.05f, "the head is above the neck");
        }

        [Test]
        public void Star_OutfitAndHairChangeTheMaterials_AndGoingBackRestoresThem()
        {
            GameObject star = Make(StarPath);
            StarWardrobe w = StarWardrobe.On(star);
            var body = star.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.Contains("_Body"));
            var legs = star.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.Contains("_LegLeft"));
            var head = star.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.Contains("_Head"));
            w.Apply(Look());
            Material top0 = body.sharedMaterial, legs0 = legs.sharedMaterial, head0 = head.sharedMaterial;
            w.Apply(Look("outfit:peach-navy", "hair:blonde"));
            Assert.AreNotSame(top0, body.sharedMaterial); Assert.AreNotSame(legs0, legs.sharedMaterial); Assert.AreNotSame(head0, head.sharedMaterial);
            Assert.AreEqual("wd_top_peach-navy", body.sharedMaterial.name);
            foreach (var arm in star.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.Contains("_Arm")))
                Assert.AreSame(body.sharedMaterial, arm.sharedMaterial, "the arms wear the jacket");
            w.Apply(Look());
            Assert.AreSame(top0, body.sharedMaterial); Assert.AreSame(legs0, legs.sharedMaterial); Assert.AreSame(head0, head.sharedMaterial);
            // every outfit and hair colour applies without leaving a renderer empty
            foreach (ShopItem it in Shop.World.Where(i => i.Slot == Shop.Outfit || i.Slot == Shop.Hair))
            {
                w.Apply(Look(it.Id));
                Assert.IsNotNull(body.sharedMaterial.mainTexture, it.Id); Assert.IsNotNull(head.sharedMaterial.mainTexture, it.Id);
            }
        }

        [Test]
        public void Star_Guitar_IsBuiltOnTheChestAnchor_WearsTheSkinColour_AndIsHiddenOffStage()
        {
            GameObject star = Make(StarPath);
            StarWardrobe w = StarWardrobe.On(star);
            w.Apply(Look("guitar:mint"));
            Assert.IsFalse(w.GuitarShown, "not until she is on stage");
            w.ShowGuitar(true);
            Assert.IsTrue(w.GuitarShown);
            Assert.AreSame(Named(star, StarWardrobe.FrontAnchor), w.Guitar.transform.parent);
            Renderer[] parts = w.Guitar.GetComponentsInChildren<Renderer>(true);
            Assert.GreaterOrEqual(parts.Length, 8);
            Color mint = StarWardrobe.GuitarColor("guitar:mint");
            Assert.AreEqual(ColorUtility.ToHtmlStringRGB(new Color(0x8f / 255f, 0xe3 / 255f, 0xc4 / 255f)), ColorUtility.ToHtmlStringRGB(mint));
            Renderer body = parts.First(r => r.name == "LowerBout");
            Assert.AreEqual(ColorUtility.ToHtmlStringRGB(mint), ColorUtility.ToHtmlStringRGB(body.sharedMaterial.GetColor("_BaseColor")));
            w.Apply(Look("guitar:cherry"));
            Assert.AreEqual(ColorUtility.ToHtmlStringRGB(StarWardrobe.GuitarColor("guitar:cherry")), ColorUtility.ToHtmlStringRGB(body.sharedMaterial.GetColor("_BaseColor")), "a skin change recolours the guitar she holds");
            w.ShowGuitar(false);
            Assert.IsFalse(w.GuitarShown);
            Bounds b = new Bounds(parts[0].bounds.center, Vector3.zero);
            foreach (Renderer r in parts) b.Encapsulate(r.bounds);
            Assert.That(b.size.magnitude, Is.InRange(0.6f, 2.4f), "a guitar-sized thing: " + b.size);
        }

        [Test]
        public void Riff_EachOutfitIsASmallMeshOnTheHeadOrNeck_OneAtATime()
        {
            GameObject riff = Make(RiffPath);
            DogWardrobe d = DogWardrobe.On(riff);
            d.Apply("dog:none");
            Assert.AreEqual(0, riff.GetComponentsInChildren<MeshRenderer>(true).Length, "just riff");
            foreach (string id in new[] { "dog:bandana", "dog:beanie", "dog:shades", "dog:phones", "dog:collar" })
            {
                d.Apply(id);
                string n = "Wear_" + id.Substring(4);
                Transform on = Named(riff, n);
                Assert.IsNotNull(on, id); Assert.IsTrue(on.gameObject.activeSelf, id);
                bool neck = id == "dog:bandana" || id == "dog:collar";
                Assert.AreSame(Named(riff, neck ? DogWardrobe.NeckAnchor : DogWardrobe.HeadAnchor), on.parent, id);
                Bounds b = new Bounds(on.position, Vector3.zero);
                foreach (Renderer r in on.GetComponentsInChildren<Renderer>(true)) b.Encapsulate(r.bounds);
                Assert.That(b.size.magnitude, Is.InRange(0.05f, 0.6f), id + " is small: " + b.size);
                foreach (Transform other in riff.GetComponentsInChildren<Transform>(true))
                    if (other.name.StartsWith("Wear_") && other.name != n) Assert.IsFalse(other.gameObject.activeSelf, other.name + " is off while " + id + " is on");
            }
            d.Apply("dog:none");
            foreach (Transform t in riff.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Wear_")) Assert.IsFalse(t.gameObject.activeSelf, t.name);
        }

        [Test]
        public void StageTint_RecoloursEverySpotBeamAndLens_AndTheFreeItemPutsThemBack()
        {
            var root = new GameObject("TestStage"); _made.Add(root);
            Mesh own = MeshKit.BeamCone(6.5f, 1.25f, new Color(3f, 0.7f, 1.6f), "orig");
            var beams = new List<MeshFilter>(); var lenses = new List<MeshRenderer>();
            WardrobeAssets a = WardrobeAssets.Load();
            for (int i = 0; i < 3; i++)
            {
                var spot = new GameObject((i == 2 ? "Sweep" : "Spot") + i); spot.transform.SetParent(root.transform);
                var beam = new GameObject("Beam"); beam.transform.SetParent(spot.transform);
                var mf = beam.AddComponent<MeshFilter>(); mf.sharedMesh = own; beam.AddComponent<MeshRenderer>().sharedMaterial = a.baseLit; beams.Add(mf);
                var lens = new GameObject("Lens"); lens.transform.SetParent(spot.transform);
                lens.AddComponent<MeshFilter>().sharedMesh = a.sphere; var lr = lens.AddComponent<MeshRenderer>(); lr.sharedMaterial = a.baseLit; lenses.Add(lr);
            }
            var decoy = new GameObject("Beam"); decoy.transform.SetParent(root.transform);   // not under a Spot or Sweep: left alone
            decoy.AddComponent<MeshFilter>().sharedMesh = own; decoy.AddComponent<MeshRenderer>().sharedMaterial = a.baseLit;
            var tint = root.AddComponent<StageTint>();
            tint.Find(root.transform);
            Assert.AreEqual(3, tint.Count);
            tint.Apply("lights:pink");
            Color pink = StageTint.ColorOf("lights:pink");
            foreach (MeshFilter mf in beams)
            {
                Assert.AreNotSame(own, mf.sharedMesh);
                Color c = mf.sharedMesh.colors[0];
                Assert.Greater(c.r, c.g, "pink: red above green"); Assert.Greater(c.b, c.g);
                Assert.AreEqual(own.bounds.max.z, mf.sharedMesh.bounds.max.z, 0.01f, "same length");
                Assert.AreEqual(own.bounds.extents.x, mf.sharedMesh.bounds.extents.x, 0.02f, "same width");
            }
            var block = new MaterialPropertyBlock();
            lenses[0].GetPropertyBlock(block);
            Color em = block.GetColor("_EmissionColor");
            Assert.Greater(em.r, 1f, "the lens glows"); Assert.Greater(em.r, em.g);
            tint.Apply("lights:cyan");
            Assert.Greater(beams[0].sharedMesh.colors[0].b, beams[0].sharedMesh.colors[0].r, "cyan: blue above red");
            Assert.AreSame(beams[0].sharedMesh, beams[1].sharedMesh, "one mesh per colour and size");
            tint.Apply("lights:tour");
            foreach (MeshFilter mf in beams) Assert.AreSame(own, mf.sharedMesh, "the place's own beam is back");
            lenses[0].GetPropertyBlock(block);
            Assert.IsTrue(block.isEmpty, "and its own lens");
            Assert.AreEqual(Color.white, StageTint.ColorOf("lights:tour"));
            Assert.AreEqual(Color.white, StageTint.ColorOf("nope"));
            Assert.AreNotEqual(pink, Color.white);
        }

        [Test]
        public void Trunk_HasABodyARailAMarkerAndBlockers_AndNothingFallsOutsideHalfAMetreOfItsFootprint()
        {
            GameObject t = WardrobeTrunk.Build(new Vector3(3f, 0f, 2f), WardrobeAssets.Load());
            _made.Add(t);
            Assert.IsNotNull(t);
            Assert.IsNotNull(Named(t, "Body")); Assert.IsNotNull(Named(t, "Rail")); Assert.IsNotNull(Named(t, "Ring"));
            Assert.IsNotNull(Named(t, "Marker").GetComponent<MarkerBob>());
            Assert.AreEqual(3, t.GetComponentsInChildren<Obstacle>(true).Length, "the trunk and the two rail ends block walking");
            Bounds b = new Bounds(t.transform.position, Vector3.zero);
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true)) b.Encapsulate(r.bounds);
            Assert.Less(b.size.x, 4.6f); Assert.Less(b.size.z, 3.9f);   // the gold ring is the widest piece
            Assert.Less(b.max.y, 4.2f);
        }
    }
}
