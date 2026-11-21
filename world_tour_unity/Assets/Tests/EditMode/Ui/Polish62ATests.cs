using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using WorldTour.Game.Mascot;
using WorldTour.Game.Meta;
using WorldTour.Game.Motion;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.EditTests
{
    /// <summary>Polish 6.2A: the win feel (confetti, crowd), the peach art twins, readable ticket ink, and colour maths for linear blending.</summary>
    public class Polish62ATests
    {
        GameObject _root;

        [SetUp]
        public void SetUp() { MotionPrefs.Reset(); }

        [TearDown]
        public void TearDown()
        {
            MotionPrefs.Reset();
            Theme.Use(Palettes.TourPinkId);
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null;
        }

        RectTransform Host()
        {
            _root = new GameObject("Host", typeof(RectTransform));
            var rt = (RectTransform)_root.transform;
            rt.sizeDelta = new Vector2(1366, 768);
            return rt;
        }

        // ---- confetti ----

        [Test]
        public void Confetti_ThrowsBits_AndNoneWhenReduceMotionIsOn()
        {
            RectTransform host = Host();
            ConfettiLayer on = ConfettiLayer.Burst(host, 34, new Vector2(.5f, .6f), ConfettiLayer.WinSeconds, new System.Random(1));
            Assert.IsNotNull(on);
            Assert.AreEqual(34, on.Count);
            Assert.LessOrEqual(ConfettiLayer.WinSeconds, 0.4f, "a win's burst stays under the 400 ms effect rule");

            Object.DestroyImmediate(_root);
            host = Host();
            MotionPrefs.Reduced = true;
            Assert.IsNull(ConfettiLayer.Burst(host, 34, new Vector2(.5f, .6f), ConfettiLayer.WinSeconds), "reduce motion: no burst");
            Assert.AreEqual(0, host.childCount, "reduce motion: not even an empty layer");
        }

        [Test]
        public void WinToast_IsSkippedEntirelyWhenReduceMotionIsOn()
        {
            // polish 6.2E item 3: advisor rule, feel effects are "off when reduce motion is on". The win itself still shows in the
            // song result (the dog's line, the fans chip, the streak) and the confetti is already off.
            RectTransform host = Host();
            MotionPrefs.Reduced = true;
            var shown = MetaUi.FeelToast(host.gameObject.AddComponent<DummyHost>(), host, "nice", null);
            Assert.IsFalse(shown, "reduce motion: no win toast");
            Assert.AreEqual(0, host.childCount, "reduce motion: not even a hidden toast");
        }

        sealed class DummyHost : MonoBehaviour { }

        [Test]
        public void Confetti_NeverTakesAClick()
        {
            ConfettiLayer layer = ConfettiLayer.Burst(Host(), 5, new Vector2(.5f, .5f), 0.4f);
            Assert.IsFalse(layer.raycastTarget);
        }

        [Test]
        public void Triangulate_CoversTheBoltExactly()
        {
            var bolt = new[]
            {
                new Vector2(.2f, 1f), new Vector2(-.6f, -.15f), new Vector2(-.1f, -.15f),
                new Vector2(-.25f, -1f), new Vector2(.65f, .2f), new Vector2(.15f, .2f)
            };
            int[] t = ConfettiLayer.Triangulate(bolt);
            Assert.AreEqual((bolt.Length - 2) * 3, t.Length);
            float poly = 0f, tris = 0f;
            for (int i = 0; i < bolt.Length; i++) { Vector2 a = bolt[i], b = bolt[(i + 1) % bolt.Length]; poly += a.x * b.y - b.x * a.y; }
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector2 a = bolt[t[i]], b = bolt[t[i + 1]], c = bolt[t[i + 2]];
                tris += Mathf.Abs((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x));
            }
            Assert.AreEqual(Mathf.Abs(poly), tris, 1e-4f, "the triangles add up to the polygon's area");
        }

        // ---- crowd ----

        [Test]
        public void CrowdModel_GrowsWithFans_AndIsCapped()
        {
            Assert.AreEqual(CrowdModel.Min, CrowdModel.PeopleFor(0));
            int last = 0;
            for (int fans = 0; fans <= 60000; fans += 500)
            {
                int n = CrowdModel.PeopleFor(fans);
                Assert.GreaterOrEqual(n, last, "never shrinks");
                last = n;
            }
            Assert.Greater(CrowdModel.PeopleFor(10000), CrowdModel.PeopleFor(1000));
            Assert.AreEqual(CrowdModel.Max, CrowdModel.PeopleFor(10000000));
        }

        [Test]
        public void Crowd_GrowsOnScreen_AndCheersEvenWhenReduceMotionIsOn()
        {
            ShowCrowd crowd = ShowCrowd.Create(Host());
            Assert.AreEqual(CrowdModel.Min, crowd.People);
            crowd.SetFans(9000);
            int mid = crowd.People;
            Assert.Greater(mid, CrowdModel.Min);
            crowd.SetFans(2000);
            Assert.AreEqual(mid, crowd.People, "the crowd never shrinks within a show");
            crowd.SetFans(100000);
            Assert.AreEqual(CrowdModel.Max, crowd.People);

            crowd.Tick();
            Assert.IsFalse(crowd.ArmsRaised);
            crowd.Cheer();
            crowd.Tick();
            Assert.IsTrue(crowd.ArmsRaised, "a right answer raises arms");

            MotionPrefs.Reduced = true;
            crowd.Sag();
            crowd.Cheer();
            crowd.Tick();
            Assert.IsTrue(crowd.ArmsRaised, "reduce motion: the cheer is a static pose");
            Assert.AreEqual(0f, crowd.Motion, "reduce motion: nothing moves");
        }

        // ---- colour ----

        [Test]
        public void MetaColor_SolidAndAlpha_MatchTheBrowser()
        {
            // 8% white over black is 8% grey in the browser; the linear blend alone would show about 31%
            Assert.AreEqual(0.08f, MetaColor.Solid(Color.white, 0.08f, Color.black).r, 1e-4f);
            float a = MetaColor.Alpha(0.08f, 1f, 0f);
            float linearResult = a;                                   // white over black, blended in linear space
            float shown = Mathf.LinearToGammaSpace(linearResult);
            Assert.AreEqual(0.08f, shown, 0.005f);
            Assert.AreEqual(0.31f, Mathf.LinearToGammaSpace(0.08f), 0.01f, "the uncorrected value is the 31% the lead measured");
            Assert.AreEqual(0.6f, MetaColor.Alpha(0.6f, 0.5f, 0.5f), 1e-4f, "same colour under and over: any alpha is the same");
        }

        [Test]
        public void TicketInk_ReadsAtAA_InBothThemes([Values(Palettes.TourPinkId, Palettes.PeachNavyId)] string id)
        {
            Palette p = Palettes.Get(id);
            Color deep = Theme.ToColor(p[ColorRole.PaperDeep]), card = Theme.ToColor(p[ColorRole.Card]), ink = Theme.ToColor(p[ColorRole.Ink]);
            Assert.Less(MetaColor.Contrast(deep, card), 4.5, "the problem: raw paper-deep is too pale on the card");
            Color fixedInk = MetaColor.Readable(deep, card, ink);
            Assert.GreaterOrEqual(MetaColor.Contrast(fixedInk, card), 4.5, id);
        }

        // ---- peach art ----

        [Test]
        public void ThemeAsset_ListsPeachTwinsForDecorAndRiff()
        {
            ThemeAsset a = Resources.Load<ThemeAsset>(ThemeAsset.ResourcePath);
            Assert.IsNotNull(a);
            Assert.GreaterOrEqual(a.peachDecor.Count, 10 + 60, "10 decor pieces and riff's layers that take theme colours");
            foreach (ThemeAsset.DecorTwin t in a.peachDecor)
            {
                Assert.IsNotNull(t.tourPink);
                Assert.IsNotNull(t.peachNavy);
                Assert.AreEqual(t.tourPink.name, t.peachNavy.name);
                Assert.AreEqual(t.tourPink.rect.size, t.peachNavy.rect.size);
            }
            Assert.IsTrue(a.peachDecor.Any(t => t.tourPink.name == "dog_head_idle"), "riff's head has a peach twin");
        }

        [Test]
        public void DogRig_ShowsPeachLayers_InThePeachTheme_FromTheFirstFrame()
        {
            ThemeAsset a = Resources.Load<ThemeAsset>(ThemeAsset.ResourcePath);
            Assert.IsNotNull(a.dogArt);
            _root = new GameObject("Skin", typeof(RectTransform));
            ThemeSkin skin = _root.AddComponent<ThemeSkin>();
            typeof(ThemeSkin).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(skin, null);
            Theme.Use(Palettes.PeachNavyId);

            var dogGo = new GameObject("Dog", typeof(RectTransform));
            dogGo.transform.SetParent(_root.transform, false);
            ((RectTransform)dogGo.transform).sizeDelta = new Vector2(190, 190);
            DogRig rig = dogGo.AddComponent<DogRig>();
            rig.Init(a.dogArt, null, null);
            foreach (DogPose pose in new[] { DogPose.Idle, DogPose.Cheer, DogPose.Oops, DogPose.Think, DogPose.Idle })
            {
                rig.SetPose(pose);
                foreach (Image img in dogGo.GetComponentsInChildren<Image>(false))
                {
                    if (img.sprite == null) continue;
                    ThemeAsset.DecorTwin pair = a.peachDecor.FirstOrDefault(t => t.tourPink == img.sprite || t.peachNavy == img.sprite);
                    if (pair.tourPink == null) continue;                       // a layer with no twin is the same in both themes
                    Assert.AreSame(pair.peachNavy, img.sprite, pose + ": " + img.name + " is the peach layer straight away");
                }
            }
            // and back to tour pink
            Theme.Use(Palettes.TourPinkId);
            rig.SetPose(DogPose.Cheer);
            foreach (Image img in dogGo.GetComponentsInChildren<Image>(false))
            {
                ThemeAsset.DecorTwin pair = a.peachDecor.FirstOrDefault(t => t.peachNavy == img.sprite);
                Assert.IsNull(pair.peachNavy, "no peach layer left in tour pink: " + img.name);
            }
        }
    }
}
