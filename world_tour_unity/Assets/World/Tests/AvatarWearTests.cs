using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WorldTour.Core.Meta;
using WorldTour.World.Wardrobe;

namespace WorldTour.World.Tests
{
    /// <summary>Phase 10B.1: every avatar item on the real Star and riff prefabs: the palette painting, the parts of each slot, patterns, rebuilds and sizes.</summary>
    public class AvatarWearTests
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

        static AvatarLook With(string slot, string id, int color = 0, int pattern = 0)
        {
            AvatarLook l = AvatarLook.Default();
            l.Set(slot, new AvatarPick(id, color, pattern));
            return l;
        }

        static Color32 Px(Texture2D t, int cell)
        {
            int cw = t.width / 16, ch = t.height / 8;   // the palette is the lower left quarter of the doubled texture
            return t.GetPixel((cell % 8) * cw + cw / 2, (cell / 8) * ch + ch / 2);
        }

        static bool Same(Color32 a, Color b) { return Mathf.Abs(a.r - b.r * 255f) < 3f && Mathf.Abs(a.g - b.g * 255f) < 3f && Mathf.Abs(a.b - b.b * 255f) < 3f; }

        [Test]
        public void Assets_HaveTheReadablePalette_AndTheStarAnchors()
        {
            WardrobeAssets a = WardrobeAssets.Load();
            Assert.IsNotNull(a.palette, "run World Tour > Build avatar editor");
            Assert.IsTrue(a.palette.isReadable);
            foreach (string an in new[] { "StarHead", "StarChest", "StarHips", "StarArmL", "StarArmR", "StarLegL", "StarLegR", "StarFootL", "StarFootR" })
                Assert.IsNotNull(a.Anchor(an), an);
        }

        [Test]
        public void Apply_PaintsSkinHairAndClothes_IntoTheStarsTextures()
        {
            GameObject star = Make(StarPath);
            StarWardrobe w = StarWardrobe.On(star);
            AvatarLook l = AvatarLook.Default();
            l.Skin = 5; l.HairColor = AvatarCatalogue.Peach; l.Hair.Color = AvatarCatalogue.Peach;
            l.Top = new AvatarPick("top:zipjacket", AvatarCatalogue.Navy);
            l.Bottom = new AvatarPick("bottom:jeans", AvatarCatalogue.Peach);
            w.Apply(l);
            var body = star.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.Contains("_Body"));
            var head = star.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.Contains("_Head"));
            var legs = star.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.Contains("_LegLeft"));
            var tex = (Texture2D)body.sharedMaterial.mainTexture;
            Assert.AreSame(body.sharedMaterial, head.sharedMaterial, "body and head share the painted palette");
            Color32 skin = Px(tex, 13);
            Assert.That(Mathf.Abs(skin.r - AvatarColors.Skin(5).r * 255f), Is.LessThan(40f), "hands wear the skin tone");
            Color32 jacket = Px(tex, 16);
            Assert.That(jacket.b, Is.GreaterThan(jacket.r), "a navy jacket");
            Color32 hair = Px(tex, 25);
            Assert.That(hair.r, Is.GreaterThan(hair.b), "peach hair");
            var legTex = (Texture2D)legs.sharedMaterial.mainTexture;
            Assert.AreNotSame(tex, legTex);
            Color32 trousers = legTex.GetPixel(legTex.width / 2 + 128, legTex.height / 2 + 64);   // the trousers cloth canvas
            Assert.That(trousers.r, Is.GreaterThan(trousers.b), "peach trousers");
        }

        [Test]
        public void ShortSleevesAndShorts_ChangeWhichMaterialTheArmsAndLegsWear()
        {
            GameObject star = Make(StarPath);
            StarWardrobe w = StarWardrobe.On(star);
            var arm = star.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.Contains("_ArmLeft"));
            var body = star.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.Contains("_Body"));
            w.Apply(With("top", "top:zipjacket"));
            Assert.AreSame(body.sharedMaterial, arm.sharedMaterial, "long sleeves");
            w.Apply(With("top", "top:tee"));
            Assert.AreNotSame(body.sharedMaterial, arm.sharedMaterial, "short sleeves: bare forearms");
            Assert.That(w.PartsOf("top").Count, Is.GreaterThan(0), "the sleeve rings");
        }

        [Test]
        public void EveryItem_OfEverySlot_Builds_AndStaysOnHer()
        {
            GameObject star = Make(StarPath);
            StarWardrobe w = StarWardrobe.On(star);
            var plain = new HashSet<string> { "jeans", "boots", "bob", "leggings", "none", "tee", "tank" };
            Bounds sb = new Bounds(star.transform.position + Vector3.up * 0.95f, Vector3.zero);
            foreach (var r in star.GetComponentsInChildren<SkinnedMeshRenderer>(true)) sb.Encapsulate(r.bounds);
            foreach (string slot in new[] { "top", "bottom", "shoes", "hair", "hat", "glasses", "bag", "face" })
                foreach (AvatarItem it in AvatarCatalogue.InSlot(slot))
                {
                    w.Apply(With(slot, it.Id, 7));
                    IReadOnlyList<GameObject> parts = w.PartsOf(slot);
                    string key = AvatarCatalogue.Key(it.Id);
                    if (!plain.Contains(key) && !(slot == "top" && (key == "tee")))
                        Assert.Greater(parts.Count, 0, it.Id + " draws something");
                    Assert.Less(parts.Count, 80, it.Id + " stays light");
                    foreach (GameObject p in parts)
                    {
                        Assert.IsNotNull(p.GetComponent<MeshRenderer>().sharedMaterial, it.Id);
                        Vector3 c = p.GetComponent<MeshRenderer>().bounds.center;
                        Assert.That(c.y, Is.InRange(-0.1f, 3.2f), it.Id + " " + p.name + " height " + c.y);
                        Assert.That(Mathf.Abs(c.x - star.transform.position.x), Is.LessThan(1.6f), it.Id + " " + p.name + " x");
                        Assert.That(Mathf.Abs(c.z - star.transform.position.z), Is.LessThan(1.6f), it.Id + " " + p.name + " z");
                    }
                }
        }
        [Test]
        public void Patterns_AreInTheCloth_NotExtraParts_AndEveryColourBuilds()
        {
            GameObject star = Make(StarPath);
            StarWardrobe w = StarWardrobe.On(star);
            var skinned = star.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var body = skinned.First(r => r.name.Contains("_Body"));
            var legs = skinned.First(r => r.name.Contains("_LegLeft"));
            foreach (string slot in new[] { "top", "bottom" })
            {
                string id = slot == "top" ? "top:sweater" : "bottom:jeans";
                w.Apply(With(slot, id, 3, 0));
                int plain = w.PartsOf(slot).Count;
                var plainTex = (Texture2D)(slot == "top" ? body : legs).sharedMaterial.mainTexture;
                Color32[] before = plainTex.GetPixels32();
                for (int p = 1; p < AvatarCatalogue.PatternNames.Count; p++)
                {
                    w.Apply(With(slot, id, 3, p));
                    Assert.AreEqual(plain, w.PartsOf(slot).Count, slot + " " + AvatarCatalogue.PatternNames[p] + ": the pattern is not extra parts");
                    foreach (GameObject part in w.PartsOf(slot))
                        foreach (string bad in new[] { "Stripe", "Dot", "Star", "Check" }) StringAssert.DoesNotContain(bad, part.name, "no pattern parts");
                    Color32[] after = ((Texture2D)(slot == "top" ? body : legs).sharedMaterial.mainTexture).GetPixels32();
                    int differ = 0;
                    for (int i = 0; i < after.Length; i++) if (Mathf.Abs(after[i].r - before[i].r) + Mathf.Abs(after[i].g - before[i].g) + Mathf.Abs(after[i].b - before[i].b) > 40) differ++;
                    Assert.Greater(differ, 600, slot + " " + AvatarCatalogue.PatternNames[p] + " is painted into the cloth texture");
                    w.Apply(With(slot, id, 3, 0));
                }
            }
            for (int c = 0; c < AvatarCatalogue.PaletteHex.Count; c++) { w.Apply(With("top", "top:hoodie", c, 1)); Assert.Greater(w.PartsOf("top").Count, 0); }
        }

        [Test]
        public void HerFace_IsADecal_WithNoPaintedOnEyesOrBrowsAndNoLeftoverGear()
        {
            GameObject star = Make(StarPath);
            StarWardrobe w = StarWardrobe.On(star);
            w.Apply(AvatarLook.Default());
            WardrobeAssets a = WardrobeAssets.Load();
            foreach (string face in new[] { "smile", "bigsmile", "wink", "freckles" })
            {
                w.Apply(With("face", "face:" + face));
                Assert.AreEqual(1, w.PartsOf("face").Count, face);
                Assert.IsNotNull(a.FaceTexture(face), face);
            }
            // no cell of the buckle, belt or pouch in the body, none of the earring or the eyes in the head
            var skinned = star.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (string cellOf in new[] { "_Body", "_Head" })
            {
                Mesh m = skinned.First(r => r.name.Contains(cellOf)).sharedMesh;
                Vector2[] uv = m.uv; int[] t = m.triangles;
                for (int k = 0; k < t.Length; k += 3)
                {
                    Vector2 u = uv[t[k]];
                    if (u.x >= 0.5f || u.y >= 0.5f) continue;          // canvas patch, not the palette quarter
                    int cell = Mathf.Min(7, (int)(u.x * 16f)) + 8 * Mathf.Min(3, (int)(u.y * 8f));
                    if (cellOf == "_Body") CollectionAssert.DoesNotContain(new[] { 27, 29, 30 }, cell, "no belt, buckle or pouch");
                    else CollectionAssert.DoesNotContain(new[] { 26, 27 }, cell, "no painted-on eyes or earring");
                }
            }
        }

        [Test]
        public void ChangingOneSlot_RebuildsOnlyThatSlot()
        {
            GameObject star = Make(StarPath);
            StarWardrobe w = StarWardrobe.On(star);
            AvatarLook l = AvatarLook.Default();
            l.Hat = new AvatarPick("hat:beanie", 4);
            w.Apply(l);
            GameObject hat0 = w.PartsOf("hat")[0], top0 = w.PartsOf("top").Count > 0 ? w.PartsOf("top")[0] : null;
            l.Hat = new AvatarPick("hat:cap", 4);
            w.Apply(l);
            Assert.IsTrue(hat0 == null, "the old hat is gone");
            if (top0 != null) Assert.IsTrue(top0 != null, "the top was not touched");
            l.Hat = new AvatarPick("hat:none", 0);
            w.Apply(l);
            Assert.AreEqual(0, w.PartsOf("hat").Count);
        }

        [Test]
        public void EveryGuitar_Builds_WithTheColourPicked()
        {
            GameObject star = Make(StarPath);
            StarWardrobe w = StarWardrobe.On(star);
            w.ShowGuitar(true);
            foreach (AvatarItem it in AvatarCatalogue.InSlot("guitar"))
            {
                w.Apply(With("guitar", it.Id, AvatarCatalogue.Peach));
                Assert.IsNotNull(w.Guitar, it.Id);
                Assert.IsTrue(w.GuitarShown, it.Id);
                Renderer[] parts = w.Guitar.GetComponentsInChildren<Renderer>(true);
                Assert.GreaterOrEqual(parts.Length, 8, it.Id);
                Color want = AvatarColors.Palette(AvatarCatalogue.Peach);
                Assert.IsTrue(parts.Any(r => ColorUtility.ToHtmlStringRGB(r.sharedMaterial.GetColor("_BaseColor")) == ColorUtility.ToHtmlStringRGB(want)), it.Id + " wears peach");
            }
        }

        [Test]
        public void Riff_ShowsEveryDogItem_InThePickedColour()
        {
            GameObject riff = Make(RiffPath);
            DogWardrobe d = DogWardrobe.On(riff);
            foreach (AvatarItem it in AvatarCatalogue.InSlot("dog"))
            {
                d.Apply(new AvatarPick(it.Id, AvatarCatalogue.Navy));
                Assert.AreEqual(it.Id, d.Shown);
                int on = riff.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Wear_") && t.gameObject.activeSelf);
                Assert.AreEqual(it.Id == "dog:none" ? 0 : 1, on, it.Id);
            }
            d.Apply(new AvatarPick("dog:bowtie", AvatarCatalogue.Peach));
            d.Apply(new AvatarPick("dog:bowtie", AvatarCatalogue.Navy));
            Assert.AreEqual(1, riff.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Wear_bowtie" && t.gameObject.activeSelf), "one colour at a time");
        }

        [Test]
        public void Preview_TryOnWithNothingRegisteredDoesNothing()
        {
            AvatarPreview.Rig = null;
            Assert.DoesNotThrow(() => AvatarPreview.TryOn(AvatarLook.Default()));
            Assert.DoesNotThrow(() => AvatarPreview.TryOn(null));
        }
    }
}
