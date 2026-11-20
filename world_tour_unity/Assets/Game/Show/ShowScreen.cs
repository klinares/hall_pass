using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WorldTour.Core.Data;
using WorldTour.Core.Maths;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Core.Shows;
using WorldTour.Game.Audio;
using WorldTour.Game.Data;
using WorldTour.Game.Modes;
using WorldTour.Game.Motion;
using WorldTour.Game.Save;
using WorldTour.Game.Ui;
using WorldTour.Game.Util;

namespace WorldTour.Game.Show
{
    /// <summary>A mode's drawing for one song (world_tour/modes/*: <c>render(ctx)</c>).</summary>
    public interface ISongMode
    {
        void Render(SongContext ctx);
    }

    /// <summary>What to play. Build one and call <see cref="ShowScreen.Open"/>.</summary>
    public sealed class ShowRequest
    {
        /// <summary>The mode id ("remix").</summary>
        public string ModeId = "remix";
        /// <summary>The venue the show is at (passed to the summary and the eyebrow).</summary>
        public string VenueId = Venues.First;
        /// <summary>The week whose words are played.</summary>
        public Week Week;
        /// <summary>Songs already chosen (the encore); null = pick from the week (show.js pickWords).</summary>
        public List<ShowItem> Items;
        public bool Encore;
        /// <summary>Seed for every random choice in the show; null = a new one from the clock. Tests pass one.</summary>
        public long? Seed;
        /// <summary>The save; null = Boot.Store, or an unsaved fresh save if there is none (Main opened alone).</summary>
        public SaveStore Store;
    }

    /// <summary>What the ticket screen (2.6) needs when a show ends.</summary>
    public sealed class ShowResult
    {
        public ShowRequest Request;
        public ShowSummary Summary;
        /// <summary>Venues unlocked during the show, in order (show.js G.unlocks).</summary>
        public List<Venue> Unlocks = new List<Venue>();
        /// <summary>Journal stickers earned by this show (show.js:222 awardStickers).</summary>
        public List<Milestone> Stickers = new List<Milestone>();
        /// <summary>The finished engine, for <see cref="ShowScreen.OpenEncore"/>.</summary>
        public ShowEngine Engine;
        public Week Week;
    }

    /// <summary>
    /// The show: world_tour/js/show.js mountShow on Unity. It picks the week's words, runs a ShowEngine over them, saves
    /// through ProgressRules.Attach (fans, words, history, stickers, as the JS does through progress.js), draws the top
    /// bar (quit, eyebrow, song number, fans, streak) and the song pips from engine state, and mounts the mode for each
    /// song. When the show ends it raises <see cref="Finished"/> with the summary, unlocks and stickers.
    /// The dog (2.3) follows <see cref="EngineReady"/>, <see cref="Said"/> and <see cref="NudgeDue"/>, and sits in
    /// <see cref="CoachArea"/>.
    /// Log lines for the browser tests: WT_SHOW_START, WT_SONG, WT_SONG_RESULT, WT_SHOW_DONE.
    /// </summary>
    public sealed class ShowScreen : GameScreen
    {
        /// <summary>Where <see cref="Open"/> looks for the prefab (made by the ShowScreenBuilder Editor script).</summary>
        public const string PrefabResource = "Screens/Screen_Show";
        /// <summary>show.js:118 the idle hint nudge.</summary>
        public const float NudgeSeconds = 15f;

        [SerializeField] StickerButton buttonPrefab;
        [Tooltip("show.js:75: the bolt sticker on the streak chip (stickers/bolt_sm).")]
        [SerializeField] Sprite streakIcon;
        [Tooltip("studio.js:20: the vinyl sticker on the record button (stickers/vinyl).")]
        [SerializeField] Sprite recordSticker;

        /// <summary>The record button's art for studio session, or null (a plain disc is drawn).</summary>
        internal Sprite RecordStickerSprite { get { return recordSticker; } }

        /// <summary>Raised for every show screen that starts a show, so the dog can follow without a scene reference.</summary>
        public static event Action<ShowScreen> Started;

        /// <summary>The engine is built and about to start (subscribe to its Raised here).</summary>
        public event Action<ShowEngine, Func<double>> EngineReady;
        /// <summary>A mode's line for the dog's bubble (ctx.say), with a pose id or null.</summary>
        public event Action<string, string> Said;
        /// <summary>15 s without a hint while one is available (show.js armNudge): the dog suggests the bulb.</summary>
        public event Action NudgeDue;
        /// <summary>The show is over and saved.</summary>
        public event Action<ShowResult> Finished;

        public ShowEngine Engine { get; private set; }
        public ShowRequest Request { get; private set; }
        public RectTransform CoachArea { get; private set; }
        public SaveBlob Save { get; private set; }

        private SaveStore _store;
        private readonly SaveThrottle _throttle = new SaveThrottle();
        private Func<double> _random;
        private ShowResult _result;
        private SongContext _song;
        private Func<int, bool> _hintApply;
        private ShowButton _hintButton;
        private bool _hintBlocked;
        private float _nudgeAt = -1;
        private RectTransform _play, _pips;
        private TMP_Text _songNo, _fans, _streak;
        private readonly List<Image> _pipImages = new List<Image>();

        // ---------- opening ----------

        /// <summary>
        /// Starts a show on a new screen: pushed on top (from the venue card, so quitting goes back to it, as the web
        /// quit button does), or replacing the top screen (from the ticket: play again, encore), so screens don't pile up.
        /// </summary>
        public static ShowScreen Open(ScreenStack stack, ShowRequest request, bool replaceTop = false)
        {
            if (stack == null) throw new ArgumentNullException(nameof(stack));
            ShowScreen prefab = Resources.Load<ShowScreen>(PrefabResource);
            GameObject template = null;
            if (prefab == null)
            {
                template = new GameObject("Screen_Show", typeof(RectTransform), typeof(CanvasGroup));
                template.SetActive(false);
                prefab = template.AddComponent<ShowScreen>();
            }
            ShowScreen s = replaceTop && stack.Count > 1 ? stack.Replace(prefab) : stack.Push(prefab);
            if (template != null) Destroy(template);
            s.Run(request);
            return s;
        }

        /// <summary>
        /// The encore from a finished show (show.js:226): its missed songs, shuffled, at half fans. From the ticket, pass
        /// replaceTop so the encore takes the ticket's place.
        /// </summary>
        public static ShowScreen OpenEncore(ScreenStack stack, ShowResult finished, bool replaceTop = false)
        {
            if (finished == null || finished.Engine == null || !finished.Summary.CanEncore) throw new InvalidOperationException("no encore for this show");
            var req = new ShowRequest
            {
                ModeId = finished.Request.ModeId, VenueId = finished.Request.VenueId, Week = finished.Week, Encore = true,
                Store = finished.Request.Store, Seed = finished.Request.Seed.HasValue ? finished.Request.Seed + 1 : null,
                Items = finished.Engine.EncoreItems(NewRandom(finished.Request.Seed.HasValue ? finished.Request.Seed + 2 : null))
            };
            return Open(stack, req, replaceTop);
        }

        /// <summary>show.js onAgain: a fresh show of the same mode, venue and week (new songs picked).</summary>
        public static ShowScreen OpenAgain(ScreenStack stack, ShowResult finished, bool replaceTop = false)
        {
            if (finished == null || finished.Request == null) throw new ArgumentNullException(nameof(finished));
            var req = new ShowRequest { ModeId = finished.Request.ModeId, VenueId = finished.Request.VenueId, Week = finished.Week, Store = finished.Request.Store };
            return Open(stack, req, replaceTop);
        }

        private static Func<double> NewRandom(long? seed)
        {
            long s = seed ?? (DateTime.UtcNow.Ticks ^ Environment.TickCount);
            return new Rng(s).Next;
        }

        // ---------- running ----------

        /// <summary>Starts the show (show.js mountShow).</summary>
        public void Run(ShowRequest request)
        {
            if (Engine != null) throw new InvalidOperationException("this screen already runs a show");
            Request = request ?? throw new ArgumentNullException(nameof(request));
            _random = NewRandom(request.Seed);
            _store = request.Store ?? Boot.Store;
            request.Store = _store;
            Save = _store != null ? _store.Blob : SaveBlob.Defaults(Today());
            MathConfig config = DataLoader.Math != null ? DataLoader.Math.Config : null;

            List<ShowItem> items = request.Items;
            if (items == null)
            {
                // index.js poolFor: lyric writer and prefix mixer play only their own words (lyric.js:70, prefix.js:156)
                var pool = ModeRules.Pool(request.ModeId, request.Week)
                    .Where(w => w != null && !string.IsNullOrEmpty(w.w))
                    .Select(w => new WordItem(w.w, w.s, w)).ToList();
                items = ShowPicker.PickWords(pool, new HashSet<string>(ProgressRules.ReviewWords(Save)),
                    w => ProgressRules.WordAttempts(Save, w), _random).Cast<ShowItem>().ToList();
            }

            Engine = new ShowEngine(new ShowSetup
            {
                IsMath = false, ModeId = request.ModeId, VenueId = request.VenueId, Items = items, Encore = request.Encore,
                SkillRecord = ProgressRules.Skill(Save, "spell:" + request.ModeId)
            });
            _result = new ShowResult { Request = request, Engine = Engine, Week = request.Week };
            // Attach first, so the save (and the stickers) are done before this screen reacts to ShowFinished.
            ProgressRules.Attach(Engine, Save, Today, v => _result.Unlocks.Add(v), ModeList.All(config), st => _result.Stickers = st);
            Engine.Raised += OnEvent;

            Build();
            Debug.Log("WT_SHOW_START mode=" + request.ModeId + " week=" + (request.Week != null ? request.Week.id : "none") + " n=" + items.Count);
            var ready = EngineReady; if (ready != null) ready(Engine, _random);
            var started = Started; if (started != null) started(this);
            Engine.Start();
        }

        private static string Today()
        {
            return LocalClock.TodayIso();
        }

        private void OnEvent(ShowEvent e)
        {
            switch (e.Kind)
            {
                case ShowEventKind.ItemStarted:
                    var w = e.Slot.Item as WordItem;
                    Debug.Log("WT_SONG i=" + (e.Index + 1) + " word=" + (w != null ? w.W : e.Slot.Item.Kid));
                    Mount(e);
                    break;
                case ShowEventKind.WrongTry:
                    Sfx.Play(SfxId.Thud);
                    break;
                case ShowEventKind.Answered:
                    Sfx.Play(SfxId.Strum);
                    Debug.Log("WT_SONG_RESULT i=" + (e.Index + 1) + " kind=" + (e.Win == WinKind.Clean ? "clean" : e.Win == WinKind.HintOnly ? "hinted" : "wrong"));
                    _nudgeAt = -1;
                    break;
                case ShowEventKind.Revealed:
                    Debug.Log("WT_SONG_RESULT i=" + (e.Index + 1) + " kind=revealed");
                    _nudgeAt = -1;
                    break;
                case ShowEventKind.FansAwarded:
                case ShowEventKind.ItemRecorded:
                case ShowEventKind.MistakeNoted:
                    _throttle.MarkDirty(Time.unscaledTime);
                    break;
                case ShowEventKind.ShowFinished:
                    OnFinished(e.Summary);
                    return;
            }
            Stats();
        }

        private void OnFinished(ShowSummary summary)
        {
            _song = null;
            _nudgeAt = -1;
            _throttle.MarkDirty(Time.unscaledTime);
            FlushSave();
            _result.Summary = summary;
            Debug.Log("WT_SHOW_DONE fans=" + summary.Fans + " first=" + summary.First + " sold=" + (summary.Sold ? "true" : "false"));
            Stats();
            var h = Finished;                                  // GameFlow opens the ticket (2.6), which plays the crowd
            if (h != null) h(_result);
            else Debug.LogWarning("[show] the show ended with nobody to open the ticket");
        }

        // ---------- songs ----------

        internal bool IsCurrent(SongContext c)
        {
            // Not isActiveAndEnabled: a timer that lands while the screen is briefly inactive (a stack transition, focus)
            // must still run, or the wait after a wrong try never ends (2.7 tester's freeze). A destroyed screen is out.
            return c != null && this != null && ReferenceEquals(c, _song);
        }

        private void Mount(ShowEvent e)
        {
            _hintApply = null;
            _hintButton = null;
            _hintBlocked = false;
            _nudgeAt = -1;
            for (int i = _play.childCount - 1; i >= 0; i--) Destroy(_play.GetChild(i).gameObject);
            RectTransform area = ShowUi.Rect("Song " + (e.Index + 1), _play);
            ShowUi.Stretch(area);
            _song = new SongContext(this, e.Index, Engine, e.Slot, area, _random, buttonPrefab);
            ISongMode mode = ModeFor(Request.ModeId, area.gameObject);
            mode.Render(_song);
        }

        private static ISongMode ModeFor(string id, GameObject host)
        {
            switch (id)
            {
                case "slots": return host.AddComponent<SlotsMode>();
                case "vowels": return host.AddComponent<VowelsMode>();
                case "remix": return host.AddComponent<RemixMode>();
                case "fanmail": return host.AddComponent<FanmailMode>();
                case "studio": return host.AddComponent<StudioMode>();
                case "lyric": return host.AddComponent<LyricMode>();
                case "prefix": return host.AddComponent<PrefixMode>();
                default: throw new NotSupportedException("mode " + id + " isn't built yet");
            }
        }

        internal void ScheduleNext(SongContext c, float seconds)
        {
            ShowTimers.After(seconds, () =>
            {
                if (IsCurrent(c) && Engine.Index == c.Index && !Engine.Finished) Engine.Next();
            });
        }

        internal void Later(SongContext c, Action action, float seconds)
        {
            ShowTimers.After(seconds, () => { if (IsCurrent(c)) action(); });
        }

        internal void RaiseSaid(string line, string pose)
        {
            var h = Said;
            if (h != null) h(line, pose);
        }

        // ---------- hints ----------

        internal ShowButton MakeHintButton(SongContext c, RectTransform parent)
        {
            if (!IsCurrent(c)) return null;
            _hintButton = ShowUi.Button(buttonPrefab, parent, "Hint", RemixLines.HintButton(Engine.HintsLeft), true, () => OnHint(c));
            PaintHint();
            return _hintButton;
        }

        internal void SetupHints(SongContext c, int max, Func<int, bool> apply)
        {
            if (_hintApply != null) return;                    // once per song (1.4 tester)
            _hintApply = apply;
            Engine.SetupHints(max);
            ArmNudge();
            PaintHint();
        }

        internal void SetHintBlocked(bool blocked)
        {
            _hintBlocked = blocked;
            PaintHint();
        }

        private void OnHint(SongContext c)
        {
            if (!IsCurrent(c) || _hintBlocked || !Engine.CanHint || _hintApply == null) return;
            if (Engine.Hint(_hintApply)) ArmNudge();
            PaintHint();
        }

        /// <summary>show.js paintHint: "hint (n)" while hints are left, off when none are or the mode holds it.</summary>
        internal void PaintHint()
        {
            if (_hintButton == null || Engine == null) return;
            _hintButton.SetText(RemixLines.HintButton(Engine.HintsLeft));
            _hintButton.Interactable = Engine.CanHint && !_hintBlocked;
        }

        private void ArmNudge()
        {
            _nudgeAt = Time.unscaledTime + NudgeSeconds;
        }

        private void Update()
        {
            if (_throttle.Due(Time.unscaledTime)) WriteSave();
            if (_nudgeAt > 0 && Time.unscaledTime >= _nudgeAt)
            {
                _nudgeAt = -1;
                if (_song != null && _song.Open && Engine.CanHint && !_hintBlocked)
                {
                    if (_hintButton != null) Feedback.Pop(_hintButton.Rect);
                    var h = NudgeDue; if (h != null) h();
                }
            }
        }

        // ---------- saving ----------

        private void FlushSave()
        {
            if (_throttle.Flush()) WriteSave();
        }

        private void WriteSave()
        {
            if (_store != null) _store.Save();
        }

        protected internal override void OnHidden() { FlushSave(); }
        protected internal override void OnRemoved() { FlushSave(); }

        private void OnApplicationPause(bool paused) { if (paused) FlushSave(); }
        private void OnApplicationFocus(bool focused) { if (!focused) FlushSave(); }

        // ---------- drawing ----------

        private void Build()
        {
            RectTransform root = Rect;
            ShowUi.Stretch(root);

            // the top bar (show.js .showbar)
            RectTransform bar = ShowUi.Rect("ShowBar", root);
            bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1); bar.pivot = new Vector2(.5f, 1);
            bar.offsetMin = new Vector2(Tokens.Gutter, -86); bar.offsetMax = new Vector2(-Tokens.Gutter, -12);

            RectTransform quitBox = ShowUi.Rect("Quit", bar);
            ShowUi.Place(quitBox, new Vector2(0, .5f), new Vector2(Tokens.Tap, Tokens.Tap), new Vector2(Tokens.Tap / 2, 0));
            ShowButton quit = ShowUi.Button(buttonPrefab, quitBox, "QuitButton", "x", true, Quit);
            ShowUi.Stretch(quit.Rect);

            RectTransform where = ShowUi.Rect("Where", bar);
            where.anchorMin = new Vector2(0, 0); where.anchorMax = new Vector2(1, 1);
            where.offsetMin = new Vector2(Tokens.Tap + 20, 0); where.offsetMax = new Vector2(-260, 0);
            Venue v = Venues.ById(Request.VenueId);
            ModeInfo m = ModeList.ById(Request.ModeId, null);
            string star = " " + (char)0x2736 + " ";
            RectTransform eyebrowBox = ShowUi.Rect("Eyebrow", where);
            eyebrowBox.anchorMin = new Vector2(0, .5f); eyebrowBox.anchorMax = new Vector2(1, 1); eyebrowBox.offsetMin = eyebrowBox.offsetMax = Vector2.zero;
            ShowUi.Label(eyebrowBox, "Text", (m != null ? m.Name : Request.ModeId) + (Request.Encore ? star + "encore" : "") + star + v.Short,
                Tokens.Fs0, FontRole.BodyBold, ColorRole.Ink, TextAlignmentOptions.Left);
            RectTransform songBox = ShowUi.Rect("SongNo", where);
            songBox.anchorMin = new Vector2(0, 0); songBox.anchorMax = new Vector2(1, .55f); songBox.offsetMin = songBox.offsetMax = Vector2.zero;
            _songNo = ShowUi.Label(songBox, "Text", "", Tokens.Fs3, FontRole.Display, ColorRole.Ink, TextAlignmentOptions.Left);

            _fans = Chip(bar, "Fans", new Vector2(-150, 0), "fans");
            _streak = Chip(bar, "Streak", new Vector2(-40, 0), null, streakIcon);

            // pips (show.js .pips)
            _pips = ShowUi.Rect("Pips", root);
            _pips.anchorMin = new Vector2(0, 1); _pips.anchorMax = new Vector2(1, 1); _pips.pivot = new Vector2(.5f, 1);
            _pips.offsetMin = new Vector2(Tokens.Gutter, -104); _pips.offsetMax = new Vector2(-Tokens.Gutter, -94);
            var hl = _pips.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 5; hl.childForceExpandWidth = true; hl.childForceExpandHeight = true; hl.childControlWidth = true; hl.childControlHeight = true;

            // the stage: play area, and the coach (dog) beside it
            _play = ShowUi.Rect("Play", root);
            _play.anchorMin = new Vector2(0, 0); _play.anchorMax = new Vector2(1, 1);
            _play.offsetMin = new Vector2(Tokens.Gutter, Tokens.Gutter); _play.offsetMax = new Vector2(-Tokens.Gutter - 300, -118);
            CoachArea = ShowUi.Rect("Coach", root);
            CoachArea.anchorMin = new Vector2(1, 0); CoachArea.anchorMax = new Vector2(1, 1); CoachArea.pivot = new Vector2(1, .5f);
            CoachArea.offsetMin = new Vector2(-Tokens.Gutter - 280, Tokens.Gutter); CoachArea.offsetMax = new Vector2(-Tokens.Gutter, -118);
        }

        private TMP_Text Chip(RectTransform bar, string name, Vector2 offset, string small, Sprite icon = null)
        {
            RectTransform chip = ShowUi.Rect(name, bar);
            ShowUi.Place(chip, new Vector2(1, .5f), new Vector2(small != null ? 130 : 80, 44), offset + new Vector2(small != null ? -65 : -40, 0));
            chip.anchoredPosition = offset;
            ShowUi.Panel(chip, ColorRole.Card);
            TMP_Text label = ShowUi.Label(chip, "Text", "0", Tokens.Fs2, FontRole.BodyBold, ColorRole.Ink);
            if (icon != null)
            {
                // components.css .chip: padding 0 12px 0 6px, a 28px sticker, then the number
                var img = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                var irt = img.rectTransform;
                irt.SetParent(chip, false);
                irt.anchorMin = irt.anchorMax = new Vector2(0, .5f);
                irt.pivot = new Vector2(0, .5f);
                irt.sizeDelta = new Vector2(30, 30);
                irt.anchoredPosition = new Vector2(6, 0);
                img.sprite = icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
                RectTransform lrt = label.rectTransform;
                lrt.offsetMin = new Vector2(lrt.offsetMin.x + 30f, lrt.offsetMin.y);
            }
            return label;
        }

        /// <summary>show.js stats(): fans, streak, "song n of m" (or the bonus line) and the pips.</summary>
        private void Stats()
        {
            if (Engine == null || _songNo == null) return;
            _fans.SetLower(Engine.Fans.ToString("#,##0", CultureInfo.InvariantCulture) + " <size=60%>fans</size>");
            _streak.SetLower(Engine.Streak.ToString(CultureInfo.InvariantCulture));
            if (Engine.IsBonus) _songNo.SetLower("bonus song <size=60%>" + (char)0x2736 + " one more try</size>");
            else _songNo.SetLower("song " + Engine.SongNumber + " <size=60%>of " + Engine.SongCount + "</size>");

            while (_pipImages.Count < Engine.Queue.Count)
            {
                RectTransform p = ShowUi.Rect("Pip", _pips);
                _pipImages.Add(ShowUi.Panel(p, ColorRole.Card, .5f));
            }
            int index = Engine.Finished ? Engine.Queue.Count : Engine.Index;
            for (int i = 0; i < _pipImages.Count; i++)
            {
                ShowSlot s = Engine.Queue[i];
                Image img = _pipImages[i];
                img.transform.SetSiblingIndex(i);
                Color c;
                if (i < index) c = Theme.Color(s.State == SlotState.Miss ? ColorRole.Muted : ColorRole.Ink);
                else if (i == index) c = Theme.Color(ColorRole.Accent);
                else { c = Theme.Color(ColorRole.Card); c.a = s.Bonus ? .25f : .5f; }
                img.color = c;
                img.rectTransform.localScale = new Vector3(1, i == index ? 1.35f : 1, 1);
            }
        }

        private void Quit()
        {
            FlushSave();
            if (Stack != null) Stack.Pop();
        }
    }
}
