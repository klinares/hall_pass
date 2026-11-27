using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using WorldTour.Core.Meta;
using WorldTour.Game.Meta;
using WorldTour.Game.Ui;

namespace WorldTour.World.Tests
{
    /// <summary>Phase 10B.1: the wardrobe editor screen built from its prefab: tabs, owned items only, colours, patterns, outfits, and the try-on event.</summary>
    public class AvatarEditorTests
    {
        GameObject _canvas;
        AvatarLook _seen;
        string _seenSlot;
        int _events;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            ((RectTransform)_canvas.transform).sizeDelta = new Vector2(758f, 855f);
            _events = 0; _seen = null; _seenSlot = null;
            AvatarEditorScreen.LookChanged += OnLook;
        }

        [TearDown]
        public void TearDown()
        {
            AvatarEditorScreen.LookChanged -= OnLook;
            if (_canvas != null) Object.DestroyImmediate(_canvas);
        }

        void OnLook(AvatarLook l, string slot) { _seen = l; _seenSlot = slot; _events++; }

        AvatarEditorScreen Open(string start = null)
        {
            var stack = _canvas.AddComponent<ScreenStack>();
            var rootGo = new GameObject("Root", typeof(RectTransform), typeof(CanvasGroup));
            rootGo.SetActive(false);
            var root = rootGo.AddComponent<GameScreen>();
            stack.SetRoot(root);
            Object.DestroyImmediate(rootGo);
            return AvatarEditorScreen.Open(stack, start);
        }

        static Transform Find(Transform t, string name)
        {
            foreach (Transform c in t)
            {
                if (c.name == name) return c;
                Transform d = Find(c, name);
                if (d != null) return d;
            }
            return null;
        }

        [Test]
        public void Prefab_Exists_AndTheEditorOpensOnTops_ShowingOnlyOwnedItems()
        {
            Assert.IsNotNull(Resources.Load<AvatarEditorScreen>(AvatarEditorScreen.PrefabResource), "run World Tour > Build avatar editor");
            AvatarEditorScreen s = Open();
            Assert.AreEqual("top", s.Tab);
            Assert.AreEqual(1, _events);
            Assert.AreEqual("top", _seenSlot);
            Assert.IsNotNull(Find(s.transform, "Item_top:tee"));
            Assert.IsNotNull(Find(s.transform, "Item_top:zipjacket"));
            Assert.IsNull(Find(s.transform, "Item_top:rockerjacket"), "not owned, not shown");
            Assert.IsNotNull(Find(s.transform, "Color_" + AvatarCatalogue.Peach));
            Assert.IsNotNull(Find(s.transform, "Pattern_1"));
            Assert.IsNotNull(Find(s.transform, "Tab_outfits"));
        }

        [Test]
        public void PickingAColour_APattern_AndAnItem_ChangesTheLook_AndFiresTheTryOn()
        {
            AvatarEditorScreen s = Open();
            int before = _events;
            Find(s.transform, "Color_" + AvatarCatalogue.Peach).GetComponent<Button>().onClick.Invoke();
            Assert.Greater(_events, before);
            Assert.AreEqual(AvatarCatalogue.Peach, _seen.Top.Color);
            Find(s.transform, "Pattern_2").GetComponent<StickerButton>().OnClick.Invoke();
            Assert.AreEqual(2, _seen.Top.Pattern);
            Find(s.transform, "Item_top:tee").GetComponent<StickerButton>().OnClick.Invoke();
            Assert.AreEqual("top:tee", _seen.Top.Id);
            Assert.AreEqual(AvatarCatalogue.Peach, _seen.Top.Color, "the colour stays when the item changes");
            Assert.AreEqual(2, _seen.Top.Pattern);
        }

        [Test]
        public void EveryTab_Builds_AndTheOutfitsTabSavesByWord()
        {
            AvatarEditorScreen s = Open();
            foreach (string tab in new[] { "bottom", "shoes", "hair", "haircolor", "skin", "hat", "glasses", "bag", "guitar", "dog", "lights", "outfits" })
            {
                Find(s.transform, "Tab_" + tab).GetComponent<StickerButton>().OnClick.Invoke();
                Assert.AreEqual(tab, s.Tab);
                Assert.IsNotNull(Find(s.transform, "Tab_top"), tab);
            }
            Find(s.transform, "WordNext").GetComponent<StickerButton>().OnClick.Invoke();
            Find(s.transform, "Save_2").GetComponent<StickerButton>().OnClick.Invoke();
            Assert.IsNotNull(Find(s.transform, "OutfitName_2"));
        }

        [Test]
        public void SkinAndHairColourTabs_ChangeTheLook()
        {
            AvatarEditorScreen s = Open();
            Find(s.transform, "Tab_skin").GetComponent<StickerButton>().OnClick.Invoke();
            Find(s.transform, "Skin_4").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(4, _seen.Skin);
            Find(s.transform, "Tab_haircolor").GetComponent<StickerButton>().OnClick.Invoke();
            Find(s.transform, "Color_" + AvatarCatalogue.Navy).GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(AvatarCatalogue.Navy, _seen.HairColor);
            Assert.AreEqual(AvatarCatalogue.Navy, _seen.Hair.Color);
        }

        [Test]
        public void Swatches_CanBeFocusedByKeyboard_AndEveryOneIsReachable()
        {
            AvatarEditorScreen s = Open();
            var buttons = s.GetComponentsInChildren<Button>(true).Where(b => b.name.StartsWith("Color_")).ToList();
            Assert.AreEqual(AvatarCatalogue.PaletteHex.Count, buttons.Count);
            // the editor moves focus itself (arrows inside a group, Tab between groups), so uGUI's own navigation is off; every swatch can still take focus
            foreach (Button b in buttons) { Assert.IsTrue(b.interactable); Assert.AreEqual(Navigation.Mode.None, b.navigation.mode); Assert.IsFalse(string.IsNullOrEmpty(b.GetComponent<SwatchName>().Text), "every swatch has a color name"); }
            Assert.IsNotNull(Find(s.transform, "ColorName"), "the worn color's name shows under the swatches");
        }
    }
}
