using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WorldTour.Core.Meta;
using WorldTour.World.Wardrobe;

namespace WorldTour.World.Tests
{
    /// <summary>
    /// Phase 10B.1 fix: putting a look on the Star must not make megabytes of managed garbage (in the browser the wasm heap never shrinks, and a dozen try-ons ran it out of memory).
    /// The Mono editor has no GC.GetAllocatedBytesForCurrentThread and cannot switch the collector off, so this measures growth of the managed heap in use around each apply
    /// (<see cref="UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong"/>) and asserts on the median apply, which ignores the one-time cache fills (a fabric tile per color and pattern).
    /// </summary>
    public class AvatarAllocTests
    {
        const string StarPath = "Assets/World/Prefabs/Star.prefab";
        GameObject _star;

        [TearDown]
        public void TearDown() { if (_star != null) UnityEngine.Object.DestroyImmediate(_star); }

        static long Used() { return UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong(); }

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

        static long Measure(StarWardrobe w, AvatarLook look, List<long> into)
        {
            long before = Used();
            w.Apply(look);
            long d = Math.Max(0L, Used() - before);
            into.Add(d);
            return d;
        }

        static long Median(List<long> l) { var c = new List<long>(l); c.Sort(); return c[c.Count / 2]; }
        static long Mean(List<long> l) { long s = 0; foreach (long x in l) s += x; return s / l.Count; }

        [Test]
        public void FiftyApplies_MedianUnderHalfAMegabyteOfGarbage_ForEveryKindOfChange()
        {
            _star = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StarPath));
            StarWardrobe w = StarWardrobe.On(_star);
            var rnd = new System.Random(11);
            for (int i = 0; i < 400; i++) w.Apply(Random(rnd));            // warm up: caches, textures, shared meshes, fabric tiles
            var random = new List<long>(); var tops = new List<long>(); var paint = new List<long>(); var hats = new List<long>(); var hair = new List<long>();
            for (int i = 0; i < 50; i++) Measure(w, Random(rnd), random);
            AvatarLook look = Random(rnd);
            w.Apply(look);
            string[] hatIds = { "hat:beanie", "hat:cap", "hat:bucket", "hat:beret" };
            for (int i = 0; i < 50; i++)
            {
                look.Top = new AvatarPick("top:hoodie", i % AvatarCatalogue.PaletteHex.Count, i % AvatarCatalogue.PatternNames.Count);   // a try-on: one thing changes
                Measure(w, look, tops);
                look.Skin = i % AvatarCatalogue.SkinHex.Count; Measure(w, look, paint);                                                  // paint only
                look.Hat = new AvatarPick(hatIds[i % hatIds.Length], i % AvatarCatalogue.PaletteHex.Count); Measure(w, look, hats);
                look.Hair = new AvatarPick(i % 2 == 0 ? "hair:curls" : "hair:long", 0); look.HairColor = i % AvatarCatalogue.PaletteHex.Count; look.Hair.Color = look.HairColor; Measure(w, look, hair);
            }
            Debug.Log("WT_ALLOC bytes per apply (median / mean): random looks " + Median(random) + " / " + Mean(random) + ", top try-on " + Median(tops) + " / " + Mean(tops) +
                ", paint only " + Median(paint) + " / " + Mean(paint) + ", hat " + Median(hats) + " / " + Mean(hats) + ", hair " + Median(hair) + " / " + Mean(hair));
            Assert.Less(Median(random), 512 * 1024, "random looks");
            Assert.Less(Median(tops), 512 * 1024, "top try-ons");
            Assert.Less(Median(paint), 512 * 1024, "paint only");
            Assert.Less(Median(hats), 512 * 1024, "hats");
            Assert.Less(Median(hair), 512 * 1024, "hair");
        }
    }
}
