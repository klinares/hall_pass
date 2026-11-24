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
using WorldTour.Core.Data;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Core.Tour;
using WorldTour.Game.Data;
using WorldTour.Game.Flow;
using WorldTour.Game.Map;
using WorldTour.Game.Modes;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;
using WorldTour.Game.Util;

namespace WorldTour.Game.Tests
{
    /// <summary>
    /// Phase 8.1 stage show. The 2D game's screens must keep their old layout when nothing sets <see cref="ShowLayout.Compact"/>
    /// (crowd strip, dog column, two math columns, the tall show bar), and when the world sets it every mode must fit the docked
    /// panel: nothing outside it, no crowd strip, no big dog card, and text no smaller than the 2D game's smallest label. Named to run
    /// after the other play-mode fixtures (it reuses their start-up).
    /// </summary>
    public class WorldCompactLayoutTests
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
            Application.logMessageReceived -= OnLog;
            if (_hadOriginal) File.WriteAllText(_savePath, _savedOriginal);
            else if (File.Exists(_savePath)) File.Delete(_savePath);
        }

        // ------------------------------------------------------------------ the 2D game, not hosted

        [UnityTest, Timeout(240000)]
        public IEnumerator NotHosted_TheShowKeepsTheOldLayout()
        {
            Assert.IsFalse(ShowLayout.Compact, "nothing sets the compact layout by default");
            ScreenStack stack = null;
            yield return ToMain(s => stack = s);
            foreach (string mode in new[] { "tickets", "remix" })
            {
                ShowScreen show = DogHosts.OpenWithDog(stack, () => ShowScreen.Open(stack, Request(mode, 3)));
                yield return null; yield return null;
                Transform t = show.transform;
                Assert.IsNotNull(Find(t, "Crowd"), mode + ": the crowd strip is drawn");
                RectTransform play = (RectTransform)Find(t, "Play");
                Assert.AreEqual(new Vector2(Tokens.Gutter, ShowCrowd.StripHeight + 4f), play.offsetMin, mode + ": play area start");
                Assert.AreEqual(new Vector2(-Tokens.Gutter - 300f, -118f), play.offsetMax, mode + ": play area leaves the coach column and the tall bar");
                RectTransform bar = (RectTransform)Find(t, "ShowBar");
                Assert.AreEqual(new Vector2(Tokens.Gutter, -86f), bar.offsetMin, mode + ": the show bar");
                Assert.AreEqual(new Vector2(-Tokens.Gutter, -12f), bar.offsetMax, mode + ": the show bar");
                RectTransform coach = show.CoachArea;
                Assert.AreEqual(new Vector2(-Tokens.Gutter - 280f, ShowCrowd.StripHeight + 4f), coach.offsetMin, mode + ": the coach column");
                Assert.IsNotNull(Find(t, "DogBox"), mode + ": the dog card is there");
                if (mode == "tickets")
                {
                    Transform left = Find(t, "Left");
                    Assert.IsNotNull(left, "the math columns");
                    Assert.IsNotNull(left.parent.GetComponent<HorizontalLayoutGroup>(), "two math columns side by side");
                    Assert.IsNull(left.parent.GetComponent<VerticalLayoutGroup>(), "not stacked");
                }
                stack.Pop();
                yield return null; yield return null;
            }
            Assert.IsEmpty(_errors, string.Join("\n", _errors));
        }

        // ------------------------------------------------------------------ hosted by the world

        [UnityTest, Timeout(600000)]
        public IEnumerator Hosted_EveryModeFitsTheDockedPanel_AndDrawsNoCrowdStripOrDogCard()
        {
            ScreenStack main = null;
            yield return ToMain(s => main = s);
            var failures = new List<string>();
            // the panel as WorldUi docks it at 1366x768
            var canvasGo = new GameObject("CompactTestCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvasGo.layer = 5;
            canvas.sortingOrder = 50;
            // WT_SHOT_DIR (a developer's check, off by default): draw each song through a camera into a 1366x768 picture and save it
            string shotDir = Environment.GetEnvironmentVariable("WT_SHOT_DIR");
            Camera shotCam = null; RenderTexture shotRt = null;
            if (!string.IsNullOrEmpty(shotDir) && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Directory.CreateDirectory(shotDir);
                shotRt = new RenderTexture(1366, 768, 24, RenderTextureFormat.ARGB32);
                var camGo = new GameObject("ShotCam", typeof(Camera));
                shotCam = camGo.GetComponent<Camera>();
                shotCam.clearFlags = CameraClearFlags.SolidColor;
                shotCam.backgroundColor = new Color(0.18f, 0.32f, 0.42f);
                shotCam.orthographic = true; shotCam.targetTexture = shotRt; shotCam.cullingMask = 1 << 5; shotCam.enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = shotCam; canvas.planeDistance = 1f;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1366, 768); scaler.matchWidthOrHeight = 0.5f;
            }
            else canvas.renderMode = RenderMode.ScreenSpaceOverlay;
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
            RectTransform bubbles = ShowUi.Rect("Bubbles", canvasGo.transform);
            ShowUi.Stretch(bubbles);
            DogHosts.BubbleHost = bubbles;
            ShowLayout.PanelSize = panel.sizeDelta;
            s_ShotCam = shotCam; s_ShotDir = shotDir;
            ShowLayout.Compact = true;

            // every mode, five different songs each (longer prompts and bigger pictures turn up in some seeds)
            var runs = new List<KeyValuePair<ModeInfo, long>>();
            foreach (ModeInfo info in ModeList.All(DataLoader.Math != null ? DataLoader.Math.Config : null))
                for (long seed = 1; seed <= 5; seed++) runs.Add(new KeyValuePair<ModeInfo, long>(info, seed * 7919L));
            foreach (KeyValuePair<ModeInfo, long> run in runs)
            {
                ModeInfo info = run.Key; long songSeed = run.Value;
                s_ShotCam = (songSeed == 7919L || songSeed == 2 * 7919L) ? shotCam : null;   // pictures of the first two songs of each mode
                ShowScreen show = DogHosts.OpenWithDog(stack, () => ShowScreen.Open(stack, Request(info.Id, 3, songSeed)));
                yield return new WaitForSecondsRealtime(0.5f);   // the screen slides in for 0.28 s
                string label = info.Id + "#" + songSeed;
                if (Find(show.transform, "Crowd") != null) failures.Add(label + ": the 2D crowd strip is drawn");
                if (Find(show.transform, "DogBox") != null) failures.Add(label + ": the big dog card is drawn");
                Check(label + " song 1", panel, show, failures);
                // open every hint the mode has: the pictures they add must fit too
                for (int h = 1; h <= 3; h++)
                {
                    Transform hint = Find(show.transform, "Hint", activeOnly: true);
                    if (hint == null) break;
                    Press(hint.gameObject);
                    yield return new WaitForSecondsRealtime(0.3f);
                    Check(label + " hint " + h, panel, show, failures);
                }
                stack.Pop();
                yield return null; yield return null;
            }
            ShowLayout.Compact = false;
            DogHosts.BubbleHost = null;
            ShowLayout.PanelSize = Vector2.zero;
            s_ShotCam = null;
            if (shotCam != null) UnityEngine.Object.Destroy(shotCam.gameObject);
            if (shotRt != null) shotRt.Release();
            UnityEngine.Object.Destroy(canvasGo);
            Assert.IsEmpty(failures, string.Join("\n", failures));
            Assert.IsEmpty(_errors, string.Join("\n", _errors));
        }

        /// <summary>Every visible graphic lies inside the panel, and no text is drawn smaller than the 2D game's smallest label (about 10 px).</summary>
        static void Check(string label, RectTransform panel, ShowScreen show, List<string> failures)
        {
            Canvas.ForceUpdateCanvases();
            Rect bounds = panel.rect;
            var seen = new HashSet<string>();
            float lowest = float.MaxValue;
            foreach (Graphic g in show.GetComponentsInChildren<Graphic>(false))
            {
                if (!g.gameObject.activeInHierarchy || g.color.a < 0.01f || g.name == "FocusRing") continue;   // the focus ring is an outline that overhangs by design
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
                if (!(g.name == "Shadow" || g.name == "Ring")) lowest = Mathf.Min(lowest, minY);
                const float tol = 2f;
                string where = PathOf(g.transform, show.transform);
                if (minX < bounds.xMin - tol || maxX > bounds.xMax + tol || minY < bounds.yMin - tol || maxY > bounds.yMax + tol)
                {
                    if (seen.Add(where)) failures.Add(label + ": outside the panel: " + where + " [" + Mathf.RoundToInt(minX) + ".." + Mathf.RoundToInt(maxX) + " x " + Mathf.RoundToInt(minY) + ".." + Mathf.RoundToInt(maxY) + "] panel " + bounds);
                }
                if (tmp != null && !tmp.enableAutoSizing && tmp.fontSize * ShowLayout.PanelScale < 10f)
                {
                    if (seen.Add(where + "#size")) failures.Add(label + ": text too small (" + tmp.fontSize * ShowLayout.PanelScale + " px): " + where);
                }
            }
            if (label.StartsWith("tickets#7919 hint 2"))
                foreach (string n in new[] { "DeskWrap", "Desk", "Work", "Paper" })
                {
                    Transform tt = Find(show.transform, n);
                    if (tt != null) { var rr = (RectTransform)tt; Debug.Log("WT_DBG " + n + " rect=" + rr.rect + " pos=" + rr.anchoredPosition + " anchors=" + rr.anchorMin + rr.anchorMax + " pivot=" + rr.pivot + " scale=" + rr.localScale + " active=" + tt.gameObject.activeSelf); }
                }
            CompactFit fit = show.GetComponentInChildren<CompactFit>();
            if (s_ShotCam != null)
            {
                s_ShotCam.Render();
                RenderTexture.active = s_ShotCam.targetTexture;
                var tex = new Texture2D(1366, 768, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1366, 768), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(s_ShotDir, label.Replace(' ', '_') + ".png"), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                RenderTexture.active = null;
            }
            Debug.Log("WT_FIT " + label + " slack=" + Mathf.RoundToInt(lowest - bounds.yMin) + " scale=" + (fit != null ? fit.Scale.ToString("0.00") : "none") + " desk=" + string.Join(",", show.GetComponentsInChildren<DeskFit>().Select(d => d.Scale.ToString("0.00"))));
        }

        static Camera s_ShotCam;
        static string s_ShotDir;

        static string PathOf(Transform t, Transform stop)
        {
            var parts = new List<string>();
            for (Transform x = t; x != null && x != stop; x = x.parent) parts.Add(x.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        // ------------------------------------------------------------------ helpers

        static ShowRequest Request(string modeId, int length, long seed = 12345)
        {
            var weeks = DataLoader.Weeks != null && DataLoader.Weeks.File != null && DataLoader.Weeks.File.weeks != null ? DataLoader.Weeks.File.weeks : new Week[0];
            return new ShowRequest { ModeId = modeId, VenueId = Venues.First, Week = WeekRules.DefaultWeek(weeks, LocalClock.TodayIso()), Length = length, Gig = true, Seed = seed, Store = Boot.Store };
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

        static Transform Find(Transform root, string name, bool activeOnly = false)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(!activeOnly))
                if (t.name == name) return t;
            return null;
        }
    }
}
