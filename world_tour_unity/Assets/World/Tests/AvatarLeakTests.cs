using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WorldTour.Core.Meta;
using WorldTour.World.Wardrobe;

namespace WorldTour.World.Tests
{
    /// <summary>Phase 10B.1 fix: changing her look over and over must not leak Materials, Textures or Meshes (the browser's renderer ran out of memory in a long run).</summary>
    public class AvatarLeakTests
    {
        const string StarPath = "Assets/World/Prefabs/Star.prefab";
        GameObject _star;

        [TearDown]
        public void TearDown() { if (_star != null) Object.DestroyImmediate(_star); }

        static int Count<T>() where T : Object { return Resources.FindObjectsOfTypeAll<T>().Length; }

        static AvatarLook Random(System.Random r)
        {
            AvatarLook l = AvatarLook.Default();
            foreach (string slot in AvatarCatalogue.Slots)
            {
                List<AvatarItem> items = AvatarCatalogue.InSlot(slot);
                AvatarItem it = items[r.Next(items.Count)];
                l.Set(slot, new AvatarPick(it.Id, AvatarCatalogue.TakesColor(slot) ? r.Next(AvatarCatalogue.PaletteHex.Count) : 0,
                    AvatarCatalogue.TakesPattern(slot) ? r.Next(AvatarCatalogue.PatternNames.Count) : 0));
            }
            l.Skin = r.Next(AvatarCatalogue.SkinHex.Count); l.HairColor = r.Next(AvatarCatalogue.PaletteHex.Count); l.Hair.Color = l.HairColor;
            return l;
        }

        [Test]
        public void TwoHundredRandomLooks_DoNotGrowMaterialsTexturesOrMeshes()
        {
            _star = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StarPath));
            StarWardrobe w = StarWardrobe.On(_star);
            var rnd = new System.Random(7);
            // warm up: the caches (one material per colour and pattern, the shape meshes) fill once
            for (int i = 0; i < 300; i++) { w.Apply(Random(rnd)); w.ShowGuitar(i % 2 == 0); }
            int mat0 = Count<Material>(), tex0 = Count<Texture2D>(), mesh0 = Count<Mesh>();
            for (int i = 0; i < 200; i++) { w.Apply(Random(rnd)); w.ShowGuitar(i % 2 == 0); }
            int mat1 = Count<Material>(), tex1 = Count<Texture2D>(), mesh1 = Count<Mesh>();
            Debug.Log("WT_LEAK materials " + mat0 + " -> " + mat1 + ", textures " + tex0 + " -> " + tex1 + ", meshes " + mesh0 + " -> " + mesh1);
            Assert.LessOrEqual(mat1 - mat0, 40, "materials");
            Assert.LessOrEqual(tex1 - tex0, 40, "textures");
            Assert.LessOrEqual(mesh1 - mesh0, 20, "meshes");
        }
    }
}
