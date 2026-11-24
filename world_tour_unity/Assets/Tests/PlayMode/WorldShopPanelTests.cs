using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Game.Data;
using WorldTour.Game.Flow;
using WorldTour.Game.Map;
using WorldTour.Game.Meta;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;
using WorldTour.Game.Util;

namespace WorldTour.Game.Tests
{
    /// <summary>
    /// Phase 8.3: the backstage shop as the world's compact panel (<see cref="BackstageScreen.OpenCompact"/>). With the panel laid out as the world docks it (the 1366x768 case),
    /// every shelf's controls stay inside it, the try-on events fire (each selection, and null when it closes), the two-tap buy spends only the balance, puts the item on
    /// and saves it, an item she cannot afford says how many more fans are needed, and the 2D game's own backstage still builds without the world's shelves.
    /// Named to run after the other play-mode fixtures (it reuses their start-up and the session's save, which it puts back).
    /// </summary>
    public class WorldShopPanelTests
    {
        string _savePath, _savedOriginal;
        bool _hadOriginal;
        readonly List<string> _errors = new List<string>();

        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors.Add(type + ": " + message + "\n" + stack);
        }

        [SetUp]
        public void SetUp()
        {
            _errors.Clear();
            ShowLayout.Compact = false;
            _savePath = Path.Combine(Application.persistentDataPath, SaveBlob.Key + ".json");
            _hadOriginal = File.Exists(_savePath);
            _savedOriginal = _hadOriginal ? File.ReadAllText(_savePath) : null;
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(_savePath, SaveJson.ToText(SaveBlob.Defaults(LocalClock.TodayIso())));
            Application.logMessageReceived += OnLog;
        }

        [TearDown]
        public void TearDown()
        {
            ShowLayout.Compact = false;
            ShowLayout.PanelSize = Vector2.zero;
            Application.logMessageReceived -= OnLog;
            if (_hadOriginal) File.WriteAllText(_savePath, _savedOriginal);
            else if (File.Exists(_savePath)) File.Delete(_savePath);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Compact_EveryShelfFits_TryOnFires_TwoTapBuyPutsItOn_AndLeavingRevertsTheTryOn()
        {
            ScreenStack main = null;
            yield return ToMain(s => main = s);
            SaveBlob blob = Boot.Store.Blob;
            long balance0 = blob.Fans.Balance, lifetime0 = blob.Fans.Lifetime;
            var owned0 = new List<string>(blob.Inventory.Owned);
            var equipped0 = new Dictionary<string, string>(blob.Inventory.Equipped);
            blob.Fans.Balance = 20000; blob.Fans.Lifetime = 20000;
            var failures = new List<string>();
            var events = new List<string>();
            Action<string> onTry = id => events.Add(id ?? "null");
            BackstageScreen.TryOnChanged += onTry;

            // the panel as WorldUi docks it at 1366x768
            var canvasGo = new GameObject("ShopTestCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvasGo.layer = 5;
            canvas.sortingOrder = 50;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform panel = ShowUi.Rect("Panel", canvasGo.transform);
            panel.anchorMin = panel.anchorMax = new Vector2(1f, .5f);
            panel.pivot = new Vector2(1f, .5f);
            panel.anchoredPosition = new Vector2(-ShowLayout.PanelMargin, 0f);
            panel.sizeDelta = new Vector2(ShowLayout.LayoutWidth(1366f), ShowLayout.LayoutHeight(768f));
            panel.localScale = new Vector3(ShowLayout.PanelScale, ShowLayout.PanelScale, 1f);
            ShowUi.Panel(panel, ColorRole.Paper);
            ThemeColor.Add(panel.gameObject, ColorRole.Paper);
            var proto = new GameObject("Root", typeof(RectTransform), typeof(CanvasGroup));
            proto.SetActive(false);
            RectTransform screens = ShowUi.Rect("Screens", panel);
            ShowUi.Stretch(screens);
            ScreenStack stack = screens.gameObject.AddComponent<ScreenStack>();
            stack.SetRoot(proto.AddComponent<GameScreen>());
            UnityEngine.Object.Destroy(proto);
            ShowLayout.PanelSize = panel.sizeDelta;
            ShowLayout.Compact = true;

            BackstageScreen shop = BackstageScreen.OpenCompact(stack);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsTrue(shop.IsCompact);
            Assert.AreEqual("outfit:tour", events.Last(), "starts on what she wears");
            Check("outfits", panel, shop, failures);

            // every shelf: a tab press selects what she wears there, and everything stays inside the panel
            foreach (string slot in new[] { "hair", "guitar", "dog", "lights", "outfit" })
            {
                Transform tab = Find(shop.transform, "Tab_" + slot);
                Assert.IsNotNull(tab, "tab " + slot);
                Press(tab.gameObject);
                yield return new WaitForSecondsRealtime(0.15f);
                ShopItem worn = MetaRules.Pick(blob, slot);
                Assert.AreEqual(worn.Id, events.Last(), "the " + slot + " tab selects the worn item");
                Assert.IsNotNull(Find(shop.transform, "Item_" + worn.Id), slot + " shelf shows the worn item");
                int items = shop.transform.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Item_"));
                Assert.AreEqual(Shop.Every.Count(i => i.Slot == slot), items, slot + " shelf shows every item of the slot");
                Check(slot, panel, shop, failures);
            }
            Assert.IsNull(Find(shop.transform, "Item_theme:peach-navy"), "no theme shelf in the world's shop");
            Assert.IsNull(Find(shop.transform, "Item_sticker:pick"), "no sticker shelf either");

            // try on peach + navy: selected, not bought, nothing spent, then the two-tap buy
            Press(Find(shop.transform, "Item_outfit:peach-navy").gameObject);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.AreEqual("outfit:peach-navy", events.Last());
            Assert.AreEqual("outfit:tour", WardrobeLook.Of(blob).Outfit, "only tried on");
            StickerButton act = Find(shop.transform, "Act").GetComponent<StickerButton>();
            Assert.AreEqual("buy for 5,000", act.Text);
            Press(act.gameObject);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.AreEqual(20000, blob.Fans.Balance, "the first tap spends nothing");
            Assert.AreEqual("tap again to buy", Find(shop.transform, "Act").GetComponent<StickerButton>().Text);
            Press(Find(shop.transform, "Act").gameObject);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(15000, blob.Fans.Balance, "the second tap buys");
            Assert.AreEqual(20000, blob.Fans.Lifetime, "lifetime fans never go down");
            Assert.AreEqual("outfit:peach-navy", WardrobeLook.Of(blob).Outfit, "and puts it on");
            Assert.IsTrue(MetaRules.Owns(blob, "outfit:peach-navy"));
            Assert.AreEqual("wearing it", Find(shop.transform, "Act").GetComponent<StickerButton>().Text);
            string saved = File.ReadAllText(_savePath);
            StringAssert.Contains("peach-navy", saved, "saved at once");
            Check("after buying", panel, shop, failures);

            // too dear: says how many more fans, and the button does nothing
            Press(Find(shop.transform, "Item_outfit:starlight").gameObject);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.AreEqual("20,000 more fans needed", Find(shop.transform, "Act").GetComponent<StickerButton>().Text);
            Assert.IsFalse(Find(shop.transform, "Act").GetComponent<StickerButton>().interactable);
            Assert.AreEqual("outfit:starlight", events.Last(), "still tried on");

            // leaving without buying: the try-on is reverted (null event) and the save keeps peach + navy
            stack.Pop();
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual("null", events.Last(), "the host puts the saved look back");
            Assert.AreEqual("outfit:peach-navy", WardrobeLook.Of(blob).Outfit);
            BackstageScreen.TryOnChanged -= onTry;

            // the 2D backstage is unchanged: no world shelves, riff's outfit is where it starts
            ShowLayout.Compact = false;
            ShowLayout.PanelSize = Vector2.zero;
            BackstageScreen shop2d = BackstageScreen.Open(stack);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsFalse(shop2d.IsCompact);
            Assert.AreEqual("dog:none", shop2d.Model.Selected);
            Assert.IsNull(Find(shop2d.transform, "Item_outfit:peach-navy"), "the 2D shelves do not show world items");
            Assert.IsNotNull(Find(shop2d.transform, "Item_theme:peach-navy"));
            stack.Pop();
            yield return null;

            blob.Fans.Balance = balance0; blob.Fans.Lifetime = lifetime0;
            blob.Inventory.Owned.Clear(); blob.Inventory.Owned.AddRange(owned0);
            blob.Inventory.Equipped.Clear(); foreach (var kv in equipped0) blob.Inventory.Equipped[kv.Key] = kv.Value;
            UnityEngine.Object.Destroy(canvasGo);
            Assert.IsEmpty(failures, string.Join("\n", failures));
            Assert.IsEmpty(_errors, string.Join("\n", _errors));
        }

        /// <summary>Every visible graphic lies inside the panel, and no text is drawn under about 10 px.</summary>
        static void Check(string label, RectTransform panel, BackstageScreen shop, List<string> failures)
        {
            Canvas.ForceUpdateCanvases();
            Rect bounds = panel.rect;
            var seen = new HashSet<string>();
            foreach (Graphic g in shop.GetComponentsInChildren<Graphic>(false))
            {
                if (!g.gameObject.activeInHierarchy || g.color.a < 0.01f || g.name == "FocusRing" || g.name == "Selected") continue;
                var tmp = g as TMP_Text;
                if (tmp != null && string.IsNullOrEmpty(tmp.text)) continue;
                var c = new Vector3[4];
                g.rectTransform.GetWorldCorners(c);
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                foreach (Vector3 w in c)
                {
                    Vector3 p = panel.InverseTransformPoint(w);
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                }
                const float tol = 2f;
                string where = PathOf(g.transform, shop.transform);
                if (minX < bounds.xMin - tol || maxX > bounds.xMax + tol || minY < bounds.yMin - tol || maxY > bounds.yMax + tol)
                {
                    if (seen.Add(where)) failures.Add(label + ": outside the panel: " + where + " [" + Mathf.RoundToInt(minX) + ".." + Mathf.RoundToInt(maxX) + " x " + Mathf.RoundToInt(minY) + ".." + Mathf.RoundToInt(maxY) + "] panel " + bounds);
                }
                if (tmp != null && !tmp.enableAutoSizing && tmp.fontSize * ShowLayout.PanelScale < 10f && seen.Add(where + "#size")) failures.Add(label + ": text too small: " + where);
            }
        }

        static string PathOf(Transform t, Transform stop)
        {
            var parts = new List<string>();
            for (Transform x = t; x != null && x != stop; x = x.parent) parts.Add(x.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        static IEnumerator ToMain(Action<ScreenStack> got)
        {
            if (!Boot.Ready)
            {
                SceneManager.LoadScene("Boot");
                yield return Until(() => Boot.Ready, 30f, "Boot never became ready");
                StartCard card = UnityEngine.Object.FindFirstObjectByType<StartCard>();
                Assert.IsNotNull(card, "no start card");
                yield return null;
                Press(card.GetComponentsInChildren<StickerButton>(true).First().gameObject);
            }
            else
            {
                SceneManager.LoadScene("Main");
                yield return null;
                yield return null;
            }
            ScreenStack stack = null;
            yield return Until(() => (stack = UnityEngine.Object.FindFirstObjectByType<ScreenStack>()) != null && stack.Top is MapScreen, 30f, "the map never showed");
            got(stack);
        }

        static IEnumerator Until(Func<bool> done, float seconds, string failure)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail(failure);
                yield return null;
            }
        }

        static void Press(GameObject go)
        {
            var e = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(go, e, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(go, e, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(go, e, ExecuteEvents.pointerClickHandler);
        }

        static Transform Find(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
