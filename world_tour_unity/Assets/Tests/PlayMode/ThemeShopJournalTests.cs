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
using WorldTour.Core.Shows;
using WorldTour.Game.Map;
using WorldTour.Game.Meta;
using WorldTour.Game.Prefs;
using WorldTour.Game.Save;
using WorldTour.Game.Ui;
using WorldTour.Game.Util;

namespace WorldTour.Game.Tests
{
    /// <summary>
    /// Task 5.1 in the real scenes, through the UI objects' own handlers: fans are earned past the theme's price (through
    /// the same Core calls a show's end makes), backstage opens from the map's hub card, peach + navy is tried on, bought
    /// with two taps and put on; the map is re-skinned (palette colours and decor twins); the journal opens and the first
    /// sticker peels on, is marked seen, and is moved; then the save is read back from the file and applied again, and
    /// everything is still there: the theme, the purchase, the sticker, the seen mark, the spot, lifetime fans.
    /// Boot opens the save once per play session (as in the game), so this test shares the session's save with the other
    /// play-mode tests: it sets the state it needs on the live save first, and puts the theme back to tour pink after.
    /// Any error or exception log fails it.
    /// </summary>
    public class ThemeShopJournalTests
    {
        const long StartBalance = 50000;
        const long ShowFans = 12000;

        string _savePath;
        string _savedOriginal;
        bool _hadOriginal;
        readonly List<string> _logs = new List<string>();
        readonly List<string> _errors = new List<string>();

        void OnLog(string message, string stack, LogType type)
        {
            _logs.Add(message);
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors.Add(type + ": " + message + "\n" + stack);
        }

        [SetUp]
        public void SetUp()
        {
            _savePath = Path.Combine(Application.persistentDataPath, SaveBlob.Key + ".json");
            _hadOriginal = File.Exists(_savePath);
            _savedOriginal = _hadOriginal ? File.ReadAllText(_savePath) : null;
            Application.logMessageReceived += OnLog;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            if (Boot.Store != null && Boot.Store.Blob != null)
            {
                Boot.Store.Blob.Inventory.Equipped["theme"] = "tour-pink";   // later tests run in the default theme
                GameSettings.Apply();
            }
            Theme.Use(Palettes.TourPinkId);
            if (_hadOriginal) File.WriteAllText(_savePath, _savedOriginal);
            else if (File.Exists(_savePath)) File.Delete(_savePath);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator EarnBuyEquipTheme_ReskinsTheMap_StickerPeels_AllSurvivesAReload()
        {
            ScreenStack stack = null;
            yield return ToMap(st => stack = st);

            // ---- the state this test needs, on the session's live save: peach + navy not owned, tour pink on,
            // opening night not yet in the book (it is earned again from the show history when the journal opens)
            SaveBlob s = Boot.Store.Blob;
            s.Inventory.Owned.RemoveAll(x => x == "theme:peach-navy");
            s.Inventory.Equipped["theme"] = "tour-pink";
            s.Journal.Stickers.Remove("first-show");
            s.Journal.Seen.RemoveAll(x => x == "first-show");
            s.Journal.Layout.Remove("first-show");
            s.Fans.Balance = StartBalance;
            if (s.Fans.Lifetime < StartBalance) ProgressRules.AddFans(s, StartBalance - s.Fans.Lifetime);
            s.Fans.Balance = StartBalance;
            GameSettings.Apply();
            Assert.AreSame(Palettes.TourPink, Theme.Palette);

            // ---- earn fans past the price, as a show's end does (ShowEngine FansAwarded -> AddFans, then FinishShow)
            ProgressRules.AddFans(s, ShowFans);
            ProgressRules.FinishShow(s, new ShowSummary { Mode = "remix", Venue = "coffee", N = 10, First = 7, Hinted = 1, Fans = (int)ShowFans, Secs = 300, Best = 5 }, LocalClock.TodayIso());
            Assert.IsTrue(Boot.Store.Save());
            long lifetime = s.Fans.Lifetime, balance = s.Fans.Balance;
            Assert.AreEqual(StartBalance + ShowFans, balance);

            // ---- backstage from the map's hub card
            Transform hub = FindDeep(stack.Top.transform, "Hub_backstage", activeOnly: true);
            Assert.IsNotNull(hub, "no backstage card on the map");
            Press(hub.gameObject);
            yield return Until(() => stack.Top is BackstageScreen, 5f, "backstage never opened");
            var bs = (BackstageScreen)stack.Top;
            Assert.IsTrue(_logs.Any(l => l.StartsWith(BackstageScreen.ReadyLog)), "no " + BackstageScreen.ReadyLog);
            yield return null;

            // ---- try it on: the whole game wears it while it is selected
            Press(FindDeep(bs.transform, "Item_theme:peach-navy", activeOnly: true).gameObject);
            yield return null; yield return null;
            Assert.AreSame(Palettes.PeachNavy, Theme.Palette, "picking the theme tries it on");
            Image background = stack.GetComponentInParent<Canvas>().transform.Find("Background").GetComponent<Image>();
            AssertColor(Palettes.PeachNavy[ColorRole.Paper], background.color, "the page behind every screen");
            Assert.AreEqual("buy for 60,000", Act(bs).Text);

            // ---- buy it: two taps (the button guards repeats for 300 ms)
            Press(Act(bs).gameObject);
            yield return null; yield return null;
            Assert.AreEqual("tap again to buy", Act(bs).Text);
            Assert.AreEqual(balance, s.Fans.Balance, "one tap never spends");
            yield return new WaitForSecondsRealtime(0.35f);
            Press(Act(bs).gameObject);
            yield return null; yield return null;
            Assert.AreEqual(balance - 60000, s.Fans.Balance);
            Assert.AreEqual(lifetime, s.Fans.Lifetime, "spending never lowers total fans");
            CollectionAssert.Contains(s.Inventory.Owned, "theme:peach-navy");
            Assert.AreEqual("peach-navy", MetaRules.GetLook(s).Theme);
            Assert.AreEqual("theme is on", Act(bs).Text);
            StringAssert.Contains("\"theme\":\"peach-navy\"", File.ReadAllText(_savePath), "saved at once");
            Assert.IsTrue(_logs.Any(l => l.StartsWith(BackstageScreen.PressLog + " id=theme:peach-navy outcome=bought")));

            // ---- back to the map: re-skinned from the tokens, decor included
            Press(FindDeep(bs.transform, "Back", activeOnly: true).gameObject);
            yield return Until(() => stack.Top is MapScreen, 5f, "back never reached the map");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreSame(Palettes.PeachNavy, Theme.Palette, "leaving keeps the theme she bought");
            var map = (MapScreen)stack.Top;
            TextMeshProUGUI heading = FindDeep(map.transform, "RouteHeading", activeOnly: true).GetComponent<TextMeshProUGUI>();
            AssertColor(Palettes.PeachNavy[ColorRole.Ink], heading.color, "map text in navy ink");
            Image mapPaper = FindDeep(FindDeep(map.transform, "Map", activeOnly: true), "Paper", activeOnly: true).GetComponent<Image>();
            Assert.IsTrue(Theme.Asset.peachDecor.Any(t => t.peachNavy == mapPaper.sprite), "the map paper is the peach + navy twin (" + (mapPaper.sprite != null ? mapPaper.sprite.name : "none") + ")");

            // ---- the journal: opening night is earned again from the show history, peels on and is marked seen
            Press(FindDeep(map.transform, "Hub_journal", activeOnly: true).gameObject);
            yield return Until(() => stack.Top is JournalScreen, 5f, "the journal never opened");
            var js = (JournalScreen)stack.Top;
            Assert.AreEqual(0, js.PageNumber, "the book opens on the page with the new sticker");
            Assert.IsTrue(js.Stickers.ContainsKey("first-show"), "opening night is on page 1");
            yield return Until(() => _logs.Any(l => l == JournalScreen.PeelLog + " key=first-show"), 3f, "the new sticker never peeled on");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(1f, js.Stickers["first-show"].Body.GetComponent<CanvasGroup>().alpha, 0.001f, "it ends fully shown");
            CollectionAssert.Contains(s.Journal.Seen, "first-show");
            StringAssert.Contains("\"first-show\"", File.ReadAllText(_savePath));

            // ---- move it: click it, then click the page
            JournalSticker stk = js.Stickers["first-show"];
            Press(stk.gameObject);
            Assert.IsTrue(stk.IsPicked);
            js.PageClicked(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            Assert.IsFalse(stk.IsPicked);
            Assert.IsTrue(s.Journal.Layout.ContainsKey("first-show"), "the move is saved");
            Spot spot = s.Journal.Layout["first-show"];
            Assert.IsEmpty(_errors, string.Join("\n", _errors));

            // ---- read the save back from the file and apply it again (the live theme is reset first)
            SaveStore reread = SaveStore.Open(LocalClock.TodayIso(), new FileBackend());
            SaveBlob back = reread.Blob;
            Assert.IsNull(reread.LoadError);
            Assert.AreEqual(2, back.V);
            Assert.AreEqual(lifetime, back.Fans.Lifetime);
            Assert.AreEqual(balance - 60000, back.Fans.Balance);
            CollectionAssert.Contains(back.Inventory.Owned, "theme:peach-navy");
            Assert.AreEqual("peach-navy", MetaRules.GetLook(back).Theme);
            Assert.IsTrue(MetaRules.HasSticker(back, "first-show"));
            CollectionAssert.Contains(back.Journal.Seen, "first-show");
            Assert.AreEqual(spot.X, back.Journal.Layout["first-show"].X);
            Assert.AreEqual(spot.Y, back.Journal.Layout["first-show"].Y);
            Theme.Use(Palettes.TourPinkId);
            GameSettings.Bind(reread);
            Assert.AreSame(Palettes.PeachNavy, Theme.Palette, "the theme comes back from the saved file");
            GameSettings.Bind(Boot.Store);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsEmpty(_errors, string.Join("\n", _errors));
        }

        // ------------------------------------------------------------------ helpers

        static IEnumerator ToMap(Action<ScreenStack> got)
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
                // Boot runs once per session (its statics), as in the game: go to Main the way the start card does
                SceneManager.LoadScene("Main");
                yield return null;
                yield return null;
            }
            ScreenStack stack = null;
            yield return Until(() => (stack = UnityEngine.Object.FindFirstObjectByType<ScreenStack>()) != null && stack.Top is MapScreen, 30f, "the map never showed");
            yield return null;
            got(stack);
        }

        static StickerButton Act(BackstageScreen bs)
        {
            Transform t = FindDeep(bs.transform, "Act", activeOnly: true);
            Assert.IsNotNull(t, "no mirror button");
            return t.GetComponent<StickerButton>();
        }

        static void AssertColor(Rgba32 want, Color got, string what)
        {
            Color32 g = got;
            Assert.IsTrue(Math.Abs(g.r - want.R) <= 1 && Math.Abs(g.g - want.G) <= 1 && Math.Abs(g.b - want.B) <= 1, what + ": " + want.ToHex() + " expected, got " + ColorUtility.ToHtmlStringRGB(got));
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

        static Transform FindDeep(Transform root, string name, bool activeOnly = false)
        {
            if (root == null) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(!activeOnly))
                if (t.name == name) return t;
            return null;
        }
    }
}
