using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WorldTour.Core.Progress;
using WorldTour.Core.Shows;
using WorldTour.Game.Map;
using WorldTour.Game.Office;
using WorldTour.Game.Prefs;
using WorldTour.Game.Save;
using WorldTour.Game.Ui;
using WorldTour.Game.Util;

namespace WorldTour.Game.Tests
{
    /// <summary>
    /// Task 5.2 in the real scenes: the map's footer link opens the manager's office, which shows the live save; the save is
    /// downloaded (the Editor writes the file) and copied as a code; a bad code is refused and changes nothing; a good code
    /// waits for its yes (focus on cancel), then replaces the save with the old one kept in worldtour.v1.backup; the reset's
    /// erase does nothing until "reset progress…" asks, cancel (Enter) closes the question, and the confirmed erase backs up,
    /// wipes and goes back to the map. Then the play-time timer runs on a fake clock. Any error or exception log fails it.
    /// Boot opens the save once per play session, so this works on the live save and is named to run after RemixShowTests.
    /// </summary>
    public class TourOfficeTests
    {
        readonly List<string> _logs = new List<string>();
        readonly List<string> _errors = new List<string>();
        readonly Dictionary<string, string> _files = new Dictionary<string, string>();
        string Dir { get { return Application.persistentDataPath; } }
        string PathOf(string key) { return Path.Combine(Dir, key + ".json"); }
        string DownloadPath { get { return Path.Combine(Dir, OfficeText.FileName(LocalClock.TodayIso())); } }

        void OnLog(string message, string stack, LogType type)
        {
            _logs.Add(message);
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors.Add(type + ": " + message + "\n" + stack);
        }

        [SetUp]
        public void SetUp()
        {
            _logs.Clear(); _errors.Clear(); _files.Clear();
            foreach (string p in new[] { PathOf(SaveBlob.Key), PathOf(SaveBackup.Key), PathOf(SaveBackup.Key2), DownloadPath })
                _files[p] = File.Exists(p) ? File.ReadAllText(p) : null;
            foreach (string p in new[] { PathOf(SaveBackup.Key), PathOf(SaveBackup.Key2) }) if (File.Exists(p)) File.Delete(p);
            Application.logMessageReceived += OnLog;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            foreach (var kv in _files)
            {
                if (kv.Value != null) File.WriteAllText(kv.Key, kv.Value);
                else if (File.Exists(kv.Key)) File.Delete(kv.Key);
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Office_ShowsTheSave_ExportImportRoundTrip_BadImportRefused_ConfirmedReset_PlayTime()
        {
            ScreenStack stack = null;
            yield return ToMap(st => stack = st);
            string today = LocalClock.TodayIso();

            // ---- the live save this test starts from
            SaveStore store = Boot.Store;
            Assert.IsNotNull(store, "no save from Boot");
            SaveBlob before = SaveBlob.Defaults(today);
            ProgressRules.AddFans(before, 4321);
            ProgressRules.FinishShow(before, new ShowSummary { Mode = "remix", Venue = "coffee", N = 10, First = 8, Fans = 4321, Secs = 200, Best = 6 }, today);
            ProgressRules.RecordWord(before, "illegible", false, false, true, false, today);
            store.Replace(before);
            GameSettings.Apply();
            yield return null;

            // ---- the footer link on the map opens the office
            var map = (MapScreen)stack.Top;
            Transform link = FindDeep(map.transform, "OfficeLink", activeOnly: true);
            Assert.IsNotNull(link, "no manager's office link on the map");
            Press(link.gameObject);
            yield return Until(() => stack.Top is OfficeScreen, 5f, "the office never opened");
            var office = (OfficeScreen)stack.Top;
            yield return null;
            Assert.IsTrue(_logs.Any(l => l.StartsWith(OfficeScreen.OpenLog + " shows=1 fans=4321 words=1")), "open log: " + string.Join("\n", _logs.Where(l => l.StartsWith("WT_OFFICE"))));
            Assert.AreEqual(4321, office.Report.Lifetime);
            Assert.AreEqual("illegible", office.Report.Words.Single().W);
            Assert.AreEqual(office.BackButton.gameObject, EventSystem.current.currentSelectedGameObject, "back has focus first");

            // ---- keep a copy: the file and the code
            yield return Tap(office.DownloadButton.gameObject);
            Assert.IsTrue(File.Exists(DownloadPath), "the Editor writes the download under persistentDataPath");
            ImportPreview fromFile = SaveTransfer.Check(File.ReadAllText(DownloadPath), null, today);
            Assert.IsTrue(fromFile.Ok, fromFile.Error);
            Assert.AreEqual(4321, fromFile.Incoming.Fans.Lifetime);
            yield return Tap(office.CopyButton.gameObject);
            string code = GUIUtility.systemCopyBuffer;
            StringAssert.StartsWith(ExportCode.Prefix, code);
            StringAssert.StartsWith("code copied (", office.KeepStatus.text);

            // ---- a bad code: refused, nothing changes
            string saved = File.ReadAllText(PathOf(SaveBlob.Key));
            ImportPreview bad = office.ImportText(code.Substring(0, code.Length / 2), "paste");
            Assert.IsFalse(bad.Ok);
            StringAssert.EndsWith("nothing was changed.", office.LoadStatus.text);
            Assert.IsFalse(office.ImportYesButton.gameObject.activeInHierarchy, "no yes for a bad code");
            Assert.AreEqual(4321, store.Blob.Fans.Lifetime);
            Assert.AreEqual(saved, File.ReadAllText(PathOf(SaveBlob.Key)), "the stored save is untouched");
            Assert.IsFalse(File.Exists(PathOf(SaveBackup.Key)));

            // ---- a good code from another device: waits for the yes, with focus on cancel
            SaveBlob other = SaveBlob.Defaults(today);
            ProgressRules.AddFans(other, 9999);
            other.Venues.Seen.Add("records");
            string otherCode = ExportCode.Export(other);
            ImportPreview good = office.ImportText(otherCode, "paste");
            Assert.IsTrue(good.Ok, good.Error);
            Assert.IsTrue(office.ImportYesButton.gameObject.activeInHierarchy);
            Assert.AreEqual(office.ImportCancelButton.gameObject, EventSystem.current.currentSelectedGameObject, "focus on cancel, not on yes");
            StringAssert.Contains("this copy: 9,999 total fans", office.LoadStatus.text);
            Assert.AreEqual(4321, store.Blob.Fans.Lifetime, "nothing changes before the yes");
            yield return Tap(office.ImportYesButton.gameObject);
            Assert.AreEqual(9999, store.Blob.Fans.Lifetime, "the copy is loaded");
            StringAssert.Contains("\"lifetime\":9999", File.ReadAllText(PathOf(SaveBlob.Key)));
            StringAssert.Contains("\"lifetime\":4321", File.ReadAllText(PathOf(SaveBackup.Key)), "the old save is kept as the backup");
            Assert.IsTrue(_logs.Any(l => l.StartsWith(OfficeScreen.ImportLog + " changed=true backup=Kept saved=true fans=9999")));
            Assert.AreEqual(9999, office.Report.Lifetime, "the page is redrawn from the loaded save");
            Assert.AreEqual(OfficeText.Loaded, office.LoadStatus.text);

            // ---- round trip: the code made before the import brings the first save back
            Assert.IsTrue(office.ImportText(code, "paste").Ok);
            yield return Tap(office.ImportYesButton.gameObject);
            Assert.AreEqual(4321, store.Blob.Fans.Lifetime, "export then import gives the save back");
            StringAssert.Contains("\"lifetime\":9999", File.ReadAllText(PathOf(SaveBackup.Key)));
            StringAssert.Contains("\"lifetime\":4321", File.ReadAllText(PathOf(SaveBackup.Key2)));

            // ---- the motion setting
            bool wasOn = office.MotionCheck.IsOn;
            yield return Tap(office.MotionCheck.gameObject);
            Assert.AreEqual(!wasOn, office.MotionCheck.IsOn);
            Assert.AreEqual(wasOn ? MotionMode.Auto : MotionMode.Reduce, GameSettings.Motion);
            yield return Tap(office.MotionCheck.gameObject);
            Assert.AreEqual(wasOn ? MotionMode.Reduce : MotionMode.Auto, GameSettings.Motion);

            // ---- reset: erase does nothing before the question; Enter on cancel closes it
            office.ConfirmReset();
            Assert.AreEqual(4321, store.Blob.Fans.Lifetime, "no erase without the question");
            yield return Tap(office.ResetButton.gameObject);
            Assert.IsTrue(office.EraseButton.gameObject.activeInHierarchy);
            Assert.IsFalse(office.ResetButton.gameObject.activeInHierarchy);
            Assert.AreEqual(office.ResetCancelButton.gameObject, EventSystem.current.currentSelectedGameObject, "office.js focuses cancel");
            yield return new WaitForSecondsRealtime(ClickGuard.DefaultWindow + .05f);
            ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            yield return null;
            Assert.IsFalse(office.EraseButton.gameObject.activeInHierarchy, "Enter on cancel closes the question");
            Assert.AreEqual(4321, store.Blob.Fans.Lifetime);
            yield return Tap(office.ResetButton.gameObject);
            yield return Tap(office.EraseButton.gameObject);
            yield return Until(() => stack.Top is MapScreen, 5f, "the reset never went back to the map");
            Assert.AreEqual(0, store.Blob.Fans.Lifetime, "erased");
            Assert.AreEqual(0, store.Blob.Shows.Count);
            StringAssert.Contains("\"lifetime\":4321", File.ReadAllText(PathOf(SaveBackup.Key)), "the erased save is kept as the backup");
            Assert.IsTrue(_logs.Any(l => l.StartsWith(OfficeScreen.ResetLog + " changed=true backup=Kept")));

            // ---- time played: 15 s steps while visible with input in the last minute (a fake clock)
            PlayTimer timer = UnityEngine.Object.FindFirstObjectByType<PlayTimer>();
            Assert.IsNotNull(timer, "MainRoot adds the play-time timer");
            float t = 5000f;
            bool visible = true;
            timer.Clock = () => t;
            timer.Visible = () => visible;
            timer.Restart();
            store.Blob.Time.Clear();
            t = 5014f; yield return null;
            Assert.IsFalse(store.Blob.Time.ContainsKey(today), "nothing before 15 s");
            t = 5015f; yield return null;
            Assert.AreEqual(15, store.Blob.Time[today]);
            t = 5200f; yield return null;
            Assert.AreEqual(60, store.Blob.Time[today], "the minute after the load counts, then nothing without input");
            visible = false; timer.NoteInput();
            t = 5260f; yield return null;
            Assert.AreEqual(60, store.Blob.Time[today], "a hidden page doesn't count");
            visible = true; timer.NoteInput();
            t = 5290f; yield return null;
            Assert.AreEqual(90, store.Blob.Time[today], "pressed at 5260: 5270 and 5285 count");
            StringAssert.Contains("\"" + today + "\":90", File.ReadAllText(PathOf(SaveBlob.Key)), "saved");
            timer.Clock = () => Time.realtimeSinceStartup;
            timer.Visible = () => Application.isFocused;
            timer.Restart();

            Assert.IsEmpty(_errors, string.Join("\n", _errors));
        }

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
                SceneManager.LoadScene("Main");
                yield return null;
                yield return null;
            }
            ScreenStack stack = null;
            yield return Until(() => (stack = UnityEngine.Object.FindFirstObjectByType<ScreenStack>()) != null && stack.Top is MapScreen, 30f, "the map never showed");
            yield return null;
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

        /// <summary>A press that waits out the office buttons' 300 ms guard first.</summary>
        static IEnumerator Tap(GameObject go)
        {
            yield return new WaitForSecondsRealtime(ClickGuard.DefaultWindow + .05f);
            Press(go);
            yield return null;
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
            foreach (Transform t in root.GetComponentsInChildren<Transform>(!activeOnly))
                if (t.name == name) return t;
            return null;
        }
    }
}
