using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WorldTour.Core.Data;
using WorldTour.Core.Maths;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Core.Shows;
using WorldTour.Game.Data;
using WorldTour.Game.Map;
using WorldTour.Game.Prefs;
using WorldTour.Game.Save;
using WorldTour.Game.Ui;
using OfficeRules = WorldTour.Core.Meta.Office;

namespace WorldTour.Game.Office
{
    /// <summary>
    /// The manager's office (world_tour/screens/office.js): a plain parent view of the save, the reduce-motion setting,
    /// keeping and loading a copy of the save (Unity only: file download, copyable code, file pick, pasted code), and a
    /// reset that needs a second, explicit press. Built in code on <see cref="OnPushed"/>; the prefab
    /// (Resources/Screens/Screen_Office, made by Editor/OfficeScreenBuilder) only carries this component.
    /// Nothing here changes the save except a confirmed import or a confirmed reset, and both keep a backup first
    /// (SaveStore.ReplaceKeepingBackup / ResetKeepingBackup).
    /// Logs: "WT_OFFICE_OPEN ...", "WT_OFFICE_EXPORT kind=file|code ok=...", "WT_OFFICE_CHECK ok=... error=...",
    /// "WT_OFFICE_IMPORT changed=... backup=... saved=... fans=...", "WT_OFFICE_RESET changed=... backup=...".
    /// </summary>
    public sealed class OfficeScreen : GameScreen
    {
        public const string PrefabResource = "Screens/Screen_Office";
        public const string OpenLog = "WT_OFFICE_OPEN";
        public const string ExportLog = "WT_OFFICE_EXPORT";
        public const string CheckLog = "WT_OFFICE_CHECK";
        public const string ImportLog = "WT_OFFICE_IMPORT";
        public const string ResetLog = "WT_OFFICE_RESET";
        public const string GentleLog = "WT_OFFICE_GENTLE";

        /// <summary>Where the office finds the save. Defaults to Boot's; a session-only save when Main was opened without Boot.</summary>
        public static Func<SaveStore> StoreSource = () => Boot.Store;

        readonly ResetConfirm _reset = new ResetConfirm();
        readonly ImportConfirm _import = new ImportConfirm();
        SaveStore _store;
        RectTransform _viewport, _content;
        ScrollRect _scroll;
        GameObject _lastSelected;
        float _builtFor = -1f;
        bool _picking;

        // the parts tests and the keyboard reach
        public OfficeButton BackButton { get; private set; }
        public OfficeCheck MotionCheck { get; private set; }
        public OfficeCheck GentleSpellingCheck { get; private set; }
        public OfficeCheck GentleMathCheck { get; private set; }
        public TMP_Text RuleLine { get; private set; }
        public OfficeButton DownloadButton { get; private set; }
        public OfficeButton CopyButton { get; private set; }
        public OfficeButton PickButton { get; private set; }
        public PasteBox Paste { get; private set; }
        public OfficeButton ImportCancelButton { get; private set; }
        public OfficeButton ImportYesButton { get; private set; }
        public OfficeButton ResetButton { get; private set; }
        public OfficeButton ResetCancelButton { get; private set; }
        public OfficeButton EraseButton { get; private set; }
        public TMP_Text KeepStatus { get; private set; }
        public TMP_Text LoadStatus { get; private set; }
        public TMP_Text ResetStatus { get; private set; }
        public OfficeReport Report { get; private set; }

        GameObject _importConfirm, _resetConfirm;
        string _keepMessage = "", _loadMessage = "";

        // ---------- opening ----------

        /// <summary>Puts the office on top of the stack (the map stays below it, for "back to the game").</summary>
        public static OfficeScreen Open(ScreenStack stack)
        {
            if (stack == null) throw new ArgumentNullException(nameof(stack));
            OfficeScreen prefab = Resources.Load<OfficeScreen>(PrefabResource);
            GameObject template = null;
            if (prefab == null)
            {
                template = new GameObject("Screen_Office", typeof(RectTransform), typeof(CanvasGroup));
                template.SetActive(false);
                prefab = template.AddComponent<OfficeScreen>();
            }
            OfficeScreen s = stack.Count > 0 ? stack.Push(prefab) : stack.SetRoot(prefab);
            if (template != null) Destroy(template);
            return s;
        }

        protected internal override void OnPushed()
        {
            _store = StoreSource != null ? StoreSource() : null;
            if (_store == null)
            {
                Debug.LogWarning("[office] no save from Boot; this office shows a session-only save");
                _store = SaveStore.Open(SaveStore.TodayIso(), new MemoryBackend());
            }
            BuildFrame();
            // office.js mount: P.awardStickers() before reading the save
            List<Milestone> fresh = ProgressRules.AwardStickers(_store.Blob, ModeCatalog.Infos, Today());
            if (fresh.Count > 0) _store.Save();
            Rebuild();
            Debug.Log(OpenLog + " shows=" + Report.Shows + " fans=" + Report.Lifetime + " words=" + Report.Words.Count
                + " mistakes=" + Report.Mistakes.Count + " persistent=" + (_store.Persistent ? "true" : "false"));
        }

        protected internal override void OnShown() { LogHitsSoon(); }

        protected internal override void OnHidden()
        {
            SaveFiles.ArmPaste(false);
        }

        protected internal override void OnRemoved()
        {
            SaveFiles.ArmPaste(false);
            _import.Cancel();
            _reset.Cancel();
        }

        static string Today() { return SaveStore.TodayIso(); }

        // ---------- actions ----------

        void Back()
        {
            if (Stack != null && Stack.Count > 1) Stack.Pop();
        }

        void ToggleMotion()
        {
            // office.js: P.setSetting('motion', checked ? 'reduced' : 'auto')
            GameSettings.Motion = MotionCheck.IsOn ? MotionMode.Reduce : MotionMode.Auto;
        }

        /// <summary>The gentle switch for one subject: kept in the save at once, and the line under the switches follows.</summary>
        void SetGentle(bool math, bool on)
        {
            SettingsData st = _store.Blob.Settings;
            if (math) st.GentleMath = on; else st.GentleSpelling = on;
            bool saved = _store.Save();
            RuleLine.SetLower(RuleText());
            Debug.Log(GentleLog + " subject=" + (math ? "math" : "spelling") + " gentle=" + (on ? "true" : "false") + " saved=" + (saved ? "true" : "false")
                + " mathUp=" + LevelPolicy.ForMath(Config(), "md", st.GentleMath).UpAfter + " spellUp=" + LevelPolicy.ForSpelling(st.GentleSpelling).UpAfter);
        }

        static MathConfig Config() { return DataLoader.Math != null ? DataLoader.Math.Config : null; }

        string RuleText()
        {
            SettingsData st = _store.Blob.Settings;
            LevelPolicy m = LevelPolicy.ForMath(Config(), "md", st.GentleMath), sp = LevelPolicy.ForSpelling(st.GentleSpelling);
            return OfficeText.RuleFoot(m.UpAfter, m.DownAfter, sp.UpAfter, sp.DownAfter);
        }

        void Download()
        {
            string name = OfficeText.FileName(Today());
            string where;
            bool ok = SaveFiles.Download(name, ExportCode.ExportFile(_store.Blob), out where);
            Debug.Log(ExportLog + " kind=file ok=" + (ok ? "true" : "false") + " name=" + name);
            SetKeep(!ok ? OfficeText.DownloadFailed : where != null ? OfficeText.DownloadedEditor(where) : OfficeText.Downloaded(name));
        }

        void CopyCode()
        {
            string code = ExportCode.Export(_store.Blob);
            bool ok = SaveFiles.Copy(code);
            Debug.Log(ExportLog + " kind=code ok=" + (ok ? "true" : "false") + " chars=" + code.Length);
            SetKeep(ok ? OfficeText.Copied(code.Length) : OfficeText.CopyFailed);
        }

        void Pick()
        {
            _import.Cancel();
            ShowImportConfirm(false);
            if (SaveFiles.OpenPicker()) { _picking = true; SetLoad(OfficeText.PickWaiting); }
            else SetLoad(Application.platform == RuntimePlatform.WebGLPlayer ? OfficeText.PickFailed : OfficeText.PickEditor);
        }

        /// <summary>Checks a pasted code or a file's text. Changes nothing; a good one waits for "yes, load this copy".</summary>
        public ImportPreview ImportText(string text, string from = "paste")
        {
            ImportPreview p = _import.Check(text, _store.Blob, Today());
            Debug.Log(CheckLog + " from=" + from + " ok=" + (p.Ok ? "true" : "false") + " chars=" + (text != null ? text.Length : 0)
                + (p.Ok ? " loses=" + (p.LosesProgress ? "true" : "false") : " error=" + p.Error));
            if (!p.Ok)
            {
                ShowImportConfirm(false);
                SetLoad(OfficeText.Rejected(p.Error));
                return p;
            }
            var lines = new List<string>();
            if (from == "paste" && text != null) lines.Add(OfficeText.Pasted(text.Length));
            lines.AddRange(OfficeText.PreviewLines(p, Venues.All.Count));
            lines.Add(OfficeText.LoadQuestion);
            SetLoad(string.Join("\n", lines));
            ShowImportConfirm(true);
            Select(ImportCancelButton);
            return p;
        }

        /// <summary>"yes, load this copy": backs up the save here, then puts the checked one in its place.</summary>
        public void ConfirmImport()
        {
            SaveBlob incoming = _import.TakeConfirmed();
            ShowImportConfirm(false);
            if (incoming == null) return;
            SwapResult r = _store.ReplaceKeepingBackup(incoming);
            Debug.Log(ImportLog + " changed=" + (r.Changed ? "true" : "false") + " backup=" + r.Backup + " saved=" + (r.Saved ? "true" : "false")
                + " fans=" + _store.Blob.Fans.Lifetime);
            string message = OfficeText.AfterImport(r.Changed, r.Backup, r.Saved);
            if (r.Changed)
            {
                GameSettings.Apply();
                ThemeSkin.Dirty();   // theme, guitar skin and outfit live in the save
                _loadMessage = message;
                Rebuild();
                Select(PickButton);
            }
            else SetLoad(message);
        }

        public void CancelImport()
        {
            _import.Cancel();
            ShowImportConfirm(false);
            SetLoad("");
            Select(PickButton);
        }

        /// <summary>"reset progress…": opens the question with cancel focused (office.js: $('cancel').focus()).</summary>
        public void AskReset()
        {
            _reset.Ask();
            ShowResetConfirm(true);
            Select(ResetCancelButton);
        }

        public void CancelReset()
        {
            _reset.Cancel();
            ShowResetConfirm(false);
            Select(ResetButton);
        }

        /// <summary>"yes, erase everything" (only while the question is open): back up, wipe, back to the map.</summary>
        public void ConfirmReset()
        {
            if (!_reset.TryErase()) return;
            SwapResult r = _store.ResetKeepingBackup(Today());
            Debug.Log(ResetLog + " changed=" + (r.Changed ? "true" : "false") + " backup=" + r.Backup + " saved=" + (r.Saved ? "true" : "false"));
            if (!r.Changed)
            {
                ShowResetConfirm(false);
                ResetStatus.SetLower(OfficeText.BackupFailed);
                ResetStatus.gameObject.SetActive(true);
                Select(ResetButton);
                return;
            }
            // office.js: store.reset(); applyLook(); location.hash = '#/map'
            GameSettings.Apply();
            ThemeSkin.Dirty();   // theme, guitar skin and outfit live in the save
            if (Stack != null && Stack.Count > 1) Stack.PopToRoot();
            else Rebuild();
        }

        // ---------- per frame ----------

        void Update()
        {
            if (Stack == null || Stack.Top != this) return;
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                // Esc closes an open question first, then leaves (as backstage and the journal do)
                if (_reset.Asking) CancelReset();
                else if (_import.Pending != null) CancelImport();
                else Back();
                return;
            }
            if (_viewport != null && Mathf.Abs(_viewport.rect.width - _builtFor) > 1f) Resize();
            WatchScroll();
            PollPick();
            PollPaste();
            FollowFocus();
        }

        // ---------- where the controls are (WT_HITS, for driving the web build by mouse) ----------

        Coroutine _hits;
        float _scrollY = float.NaN, _scrollStill;

        void LogHitsSoon()
        {
            if (!isActiveAndEnabled) return;
            if (_hits != null) StopCoroutine(_hits);
            _hits = StartCoroutine(LogHitsLater());
        }

        System.Collections.IEnumerator LogHitsLater()
        {
            yield return new WaitForSecondsRealtime(0.4f);   // after the slide-in or the layout
            _hits = null;
            var list = new List<KeyValuePair<string, RectTransform>>();
            void Add(string name, Component c) { if (c != null) list.Add(new KeyValuePair<string, RectTransform>(name, (RectTransform)c.transform)); }
            Add("back", BackButton); Add("motion", MotionCheck); Add("download", DownloadButton); Add("copy", CopyButton);
            Add("pick", PickButton); Add("paste", Paste); Add("importCancel", ImportCancelButton); Add("importYes", ImportYesButton);
            Add("reset", ResetButton); Add("resetCancel", ResetCancelButton); Add("erase", EraseButton);
            WorldTour.Game.Meta.MetaUi.LogHits("office", list);
        }

        /// <summary>Logs the hits again once the page has stopped scrolling.</summary>
        void WatchScroll()
        {
            if (_content == null) return;
            float y = _content.anchoredPosition.y;
            if (!Mathf.Approximately(y, _scrollY)) { _scrollY = y; _scrollStill = 0f; return; }
            if (_scrollStill < 0f) return;
            _scrollStill += Time.unscaledDeltaTime;
            if (_scrollStill > 0.25f) { _scrollStill = -1f; LogHitsSoon(); }
        }

        void PollPick()
        {
            if (!_picking) return;
            switch (SaveFiles.PickStatus)
            {
                case PickState.Ready:
                    _picking = false;
                    string text = SaveFiles.TakePicked();
                    if (text == null) SetLoad(OfficeText.PickUnreadable);
                    else ImportText(text, "file");
                    break;
                case PickState.Cancelled: _picking = false; SetLoad(OfficeText.PickCancelled); break;
                case PickState.TooLarge: _picking = false; SetLoad(OfficeText.PickTooBig); break;
                case PickState.Failed: _picking = false; SetLoad(OfficeText.PickUnreadable); break;
            }
        }

        void PollPaste()
        {
            if (Paste == null || !Paste.Focused) return;
            Keyboard k = Keyboard.current;
            bool ctrlV = k != null && k.vKey.wasPressedThisFrame && (k.ctrlKey.isPressed || k.leftMetaKey.isPressed || k.rightMetaKey.isPressed);
            bool tooLarge;
            string text = SaveFiles.TakePaste(ctrlV, out tooLarge);
            if (tooLarge) { ShowImportConfirm(false); SetLoad(OfficeText.Rejected(SaveTransfer.TooBig)); return; }
            if (text != null) ImportText(text, "paste");
        }

        void OnPasteFocus(bool on)
        {
            SaveFiles.ArmPaste(on);
        }

        void FollowFocus()
        {
            // keep the focused control in view when focus moves by Enter (confirm rows) or arrows
            EventSystem es = EventSystem.current;
            GameObject sel = es != null ? es.currentSelectedGameObject : null;
            if (sel == _lastSelected || _scroll == null) return;
            _lastSelected = sel;
            if (sel == null || !sel.transform.IsChildOf(_content)) return;
            ScrollIntoView((RectTransform)sel.transform);
        }

        void ScrollIntoView(RectTransform target)
        {
            Canvas.ForceUpdateCanvases();
            float viewH = _viewport.rect.height, contentH = _content.rect.height;
            if (contentH <= viewH) return;
            var c = new Vector3[4];
            target.GetWorldCorners(c);
            float top = -_content.InverseTransformPoint(c[1]).y, bottom = -_content.InverseTransformPoint(c[0]).y;
            float y = _content.anchoredPosition.y;
            if (bottom > y + viewH - 24f) y = bottom - viewH + 24f;
            if (top < y + 16f) y = top - 16f;
            y = Mathf.Clamp(y, 0f, contentH - viewH);
            _scroll.StopMovement();
            _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, y);
        }

        void Select(Selectable s)
        {
            if (s == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(s.gameObject);
        }

        void SetKeep(string m)
        {
            _keepMessage = m ?? "";
            if (KeepStatus == null) return;
            KeepStatus.SetLower(_keepMessage);
            KeepStatus.gameObject.SetActive(_keepMessage.Length > 0);
        }

        void SetLoad(string m)
        {
            _loadMessage = m ?? "";
            if (LoadStatus == null) return;
            LoadStatus.SetLower(_loadMessage);
            LoadStatus.gameObject.SetActive(_loadMessage.Length > 0);
        }

        void ShowImportConfirm(bool on) { if (_importConfirm != null) _importConfirm.SetActive(on); LogHitsSoon(); }

        void ShowResetConfirm(bool on)
        {
            if (_resetConfirm != null) _resetConfirm.SetActive(on);
            if (ResetButton != null) ResetButton.gameObject.SetActive(!on);
            if (on && ResetStatus != null) ResetStatus.gameObject.SetActive(false);
            LogHitsSoon();
        }

        // ---------- drawing ----------

        void BuildFrame()
        {
            RectTransform root = Rect;
            OfficeUi.Stretch(root);
            var bg = root.gameObject.AddComponent<Image>();
            bg.raycastTarget = true;
            ThemeColor.Add(root.gameObject, ColorRole.Paper);

            _viewport = OfficeUi.Stretch(OfficeUi.Rect("Viewport", root));
            _viewport.gameObject.AddComponent<RectMask2D>();
            var hit = _viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            _content = OfficeUi.Rect("Content", _viewport);
            _content.anchorMin = _content.anchorMax = new Vector2(.5f, 1f);
            _content.pivot = new Vector2(.5f, 1f);
            var v = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(0, 0, 18, 96);   // the bottom clears the HUD's corner buttons
            v.spacing = OfficeUi.CardGap;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            var fit = _content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll = root.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = _viewport;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40f;
            _scroll.inertia = true;
            Resize();
        }

        void Resize()
        {
            float w = _viewport.rect.width;
            _builtFor = w;
            _content.sizeDelta = new Vector2(Mathf.Min(OfficeUi.MaxWidth, Mathf.Max(320f, w - 2 * Tokens.Gutter)), _content.sizeDelta.y);
        }

        /// <summary>Draws the whole page from the save as it is now (on open, and after an import).</summary>
        void Rebuild()
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                GameObject child = _content.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            SaveBlob s = _store.Blob;
            OfficeReport o = OfficeRules.Build(s, ModeCatalog.Infos, Today());
            Report = o;

            // back link, title, sub
            RectTransform top = Row(_content, "Top");
            BackButton = OfficeButton.Create(top, "Back", OfficeText.BackArrow + " " + OfficeText.Back);
            BackButton.Clicked += Back;
            FirstSelected = BackButton;
            Text(_content, "Title", OfficeText.Title, OfficeUi.FsH1, FontRole.BodyBold);
            Text(_content, "Sub", OfficeText.Sub, OfficeUi.FsBody, FontRole.Body);

            // at a glance
            RectTransform card = Card("Glance", OfficeText.GlanceHead);
            List<GlanceItem> g = OfficeText.Glance(o);
            for (int r = 0; r < 2; r++)
            {
                RectTransform row = Row(card, "GlanceRow" + r, 10f);
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
                for (int i = r * 3; i < r * 3 + 3 && i < g.Count; i++) Glance(row, g[i]);
            }

            // time played
            card = Card("Time", OfficeText.TimeHead);
            int peak = OfficeText.Peak(o.Daily);
            foreach (OfficeDay d in o.Daily) DayRow(card, OfficeText.DayOf(d, peak));
            Foot(card, OfficeText.TimeFoot);

            // accuracy
            card = Card("Accuracy", OfficeText.AccuracyHead);
            LevelPolicy fsp = LevelPolicy.ForSpelling(s.Settings.GentleSpelling), fm = LevelPolicy.ForMath(Config(), "md", s.Settings.GentleMath);
            Text(card, "Foot", OfficeText.AccuracyNote(fsp.UpAfter, fsp.DownAfter, fm.UpAfter, fm.DownAfter), OfficeUi.FsSmall, FontRole.BodyBold);
            float[] skillCols = { -1f, 150f, 52f, 56f, 56f, 52f };
            foreach (string kind in new[] { ModeInfo.Spelling, ModeInfo.Math })
            {
                Text(card, "H3", kind == ModeInfo.Spelling ? OfficeText.Spelling : OfficeText.Math, OfficeUi.FsH3, FontRole.BodyBold);
                TableRow(card, OfficeText.SkillHeads, skillCols, head: true);
                foreach (OfficeSkill k in o.Skills.Where(x => x.Kind == kind))
                {
                    ModeEntry me = ModeCatalog.ById(k.Id);
                    if (kind == ModeInfo.Math && (me == null || !me.Playable || (Config() != null && !Config().IsEnabled(me.Info.Skill)))) continue;
                    string[] cells = OfficeText.SkillRow(k);
                    string key = Milestones.SkillKey(me.Info);
                    LevelPolicy pol = ProgressRules.PolicyFor(s, key, Config());
                    cells[1] = OfficeText.LevelCell(ProgressRules.PlayedLevel(s, key, pol), pol.Cap, MathConfig.HighestLevel);
                    TableRow(card, cells, skillCols, head: false);
                }
            }

            // words missed most
            card = Card("Words", OfficeText.WordsHead);
            float[] wordCols = { -1f, 60f, 60f, 60f, 100f };
            if (o.Words.Count == 0) Text(card, "Empty", OfficeText.WordsEmpty, OfficeUi.FsBody, FontRole.Body);
            else
            {
                TableRow(card, OfficeText.WordHeads, wordCols, head: true);
                foreach (OfficeWord w in o.Words) TableRow(card, OfficeText.WordRow(w), wordCols, head: false);
            }

            // math mistakes seen most
            card = Card("Mistakes", OfficeText.MistakesHead);
            float[] mistakeCols = { -1f, 130f, 56f };
            if (o.Mistakes.Count == 0) Text(card, "Empty", OfficeText.MistakesEmpty, OfficeUi.FsBody, FontRole.Body);
            else
            {
                TableRow(card, OfficeText.MistakeHeads, mistakeCols, head: true);
                foreach (OfficeMistake m in o.Mistakes) TableRow(card, OfficeText.MistakeRow(m), mistakeCols, head: false);
                Foot(card, OfficeText.MistakesFoot);
            }

            // settings
            card = Card("Settings", OfficeText.SettingsHead);
            MotionCheck = OfficeCheck.Create(card, "ReduceMotion", OfficeText.ReduceMotion, s.Settings.Motion == MotionModes.ReduceText);
            MotionCheck.Clicked += ToggleMotion;

            // how hard it plays (phase 7.1): gentle or standard, per subject
            card = Card("Difficulty", OfficeText.DifficultyHead);
            Text(card, "Body", OfficeText.DifficultyBody, OfficeUi.FsBody, FontRole.Body);
            GentleSpellingCheck = OfficeCheck.Create(card, "GentleSpelling", OfficeText.GentleSpelling, s.Settings.GentleSpelling);
            GentleSpellingCheck.Clicked += () => SetGentle(false, GentleSpellingCheck.IsOn);
            GentleMathCheck = OfficeCheck.Create(card, "GentleMath", OfficeText.GentleMath, s.Settings.GentleMath);
            GentleMathCheck.Clicked += () => SetGentle(true, GentleMathCheck.IsOn);
            RuleLine = Text(card, "Rule", RuleText(), OfficeUi.FsSmall, FontRole.Body);

            // keep a copy (Unity only)
            card = Card("Keep", OfficeText.KeepHead);
            Text(card, "Body", OfficeText.KeepBody, OfficeUi.FsBody, FontRole.Body);
            RectTransform keepRow = Row(card, "KeepButtons");
            DownloadButton = OfficeButton.Create(keepRow, "Download", OfficeText.DownloadButton);
            DownloadButton.Clicked += Download;
            CopyButton = OfficeButton.Create(keepRow, "CopyCode", OfficeText.CopyButton);
            CopyButton.Clicked += CopyCode;
            KeepStatus = Text(card, "KeepStatus", "", OfficeUi.FsBody, FontRole.BodyBold);
            SetKeep(_keepMessage);

            // load a copy (Unity only)
            card = Card("Load", OfficeText.LoadHead);
            Text(card, "Body", OfficeText.LoadBody, OfficeUi.FsBody, FontRole.Body);
            RectTransform loadRow = Row(card, "LoadButtons");
            PickButton = OfficeButton.Create(loadRow, "PickFile", OfficeText.PickButton);
            PickButton.Clicked += Pick;
            Paste = PasteBox.Create(card, "PasteCode", OfficeText.PastePrompt, OfficeText.PasteFocused);
            Paste.FocusChanged += OnPasteFocus;
            LoadStatus = Text(card, "LoadStatus", "", OfficeUi.FsBody, FontRole.BodyBold);
            RectTransform ic = Row(card, "ImportConfirm");
            _importConfirm = ic.gameObject;
            ImportCancelButton = OfficeButton.Create(ic, "ImportCancel", OfficeText.Cancel);
            ImportCancelButton.Clicked += CancelImport;
            ImportYesButton = OfficeButton.Create(ic, "ImportYes", OfficeText.LoadYes);
            ImportYesButton.Clicked += ConfirmImport;
            SetLoad(_loadMessage);
            ShowImportConfirm(_import.Pending != null);

            // start over
            card = Card("Danger", OfficeText.ResetHead, danger: true);
            Text(card, "Body", OfficeText.ResetBody, OfficeUi.FsBody, FontRole.Body);
            RectTransform resetRow = Row(card, "ResetRow");
            ResetButton = OfficeButton.Create(resetRow, "Reset", OfficeText.ResetButton);
            ResetButton.Clicked += AskReset;
            var confirm = OfficeUi.Rect("ResetConfirm", card);
            _resetConfirm = confirm.gameObject;
            var cv = confirm.gameObject.AddComponent<VerticalLayoutGroup>();
            cv.spacing = 10f;
            cv.childControlWidth = cv.childControlHeight = true;
            cv.childForceExpandWidth = true;
            cv.childForceExpandHeight = false;
            TMP_Text sure = Text(confirm, "Sure", "<b>" + OfficeText.Sure + "</b>" + OfficeText.SureRest, OfficeUi.FsBody, FontRole.Body);
            sure.richText = true;
            RectTransform orow = Row(confirm, "ConfirmButtons");
            ResetCancelButton = OfficeButton.Create(orow, "ResetCancel", OfficeText.Cancel);
            ResetCancelButton.Clicked += CancelReset;
            EraseButton = OfficeButton.Create(orow, "Erase", OfficeText.Erase, red: true);
            EraseButton.Clicked += ConfirmReset;
            ResetStatus = Text(card, "ResetStatus", "", OfficeUi.FsBody, FontRole.BodyBold);
            ResetStatus.gameObject.SetActive(false);
            ShowResetConfirm(_reset.Asking);

            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            if (Stack != null && Stack.Top == this && EventSystem.current != null
                && (EventSystem.current.currentSelectedGameObject == null || !EventSystem.current.currentSelectedGameObject.activeInHierarchy))
                Select(BackButton);
        }

        // ---------- pieces ----------

        TMP_Text Text(Transform parent, string name, string text, float size, FontRole font)
        {
            return OfficeUi.Text(parent, name, text, font, size, ColorRole.Ink);
        }

        void Foot(Transform parent, string text) { Text(parent, "Foot", text, OfficeUi.FsSmall, FontRole.Body); }

        /// <summary>.ocard: a card face with a 2px ink border (red for .danger), padding 14 16, and an h2.</summary>
        RectTransform Card(string name, string head, bool danger = false)
        {
            RectTransform rt = OfficeUi.Rect(name, _content);
            OfficeUi.Stretch(OfficeUi.Box(rt, "Ring", danger ? (ColorRole?)null : ColorRole.Ink, OfficeControl.Red).rectTransform);
            OfficeUi.Stretch(OfficeUi.Box(rt, "Face", ColorRole.Card, OfficeControl.Red).rectTransform, 2f);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset((int)OfficeUi.CardPadX, (int)OfficeUi.CardPadX, (int)OfficeUi.CardPadY, (int)OfficeUi.CardPadY);
            v.spacing = 8f;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            Text(rt, "H2", head, OfficeUi.FsH2, FontRole.BodyBold);
            return rt;
        }

        static RectTransform Row(Transform parent, string name, float spacing = Tokens.RowGap - 2f)
        {
            RectTransform rt = OfficeUi.Rect(name, parent);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return rt;
        }

        void Glance(RectTransform row, GlanceItem item)
        {
            RectTransform cell = OfficeUi.Rect("Item", row);
            var le = cell.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minWidth = 0f;
            le.preferredWidth = 1f;   // equal columns in both rows, whatever the text
            var v = cell.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(11, 0, 0, 0);
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            // .glance div: border-left 3px accent
            RectTransform bar = OfficeUi.Rect("Accent", cell);
            bar.anchorMin = new Vector2(0f, 0f); bar.anchorMax = new Vector2(0f, 1f); bar.pivot = new Vector2(0f, .5f);
            bar.sizeDelta = new Vector2(3f, 0f);
            bar.gameObject.AddComponent<Image>().raycastTarget = false;
            ThemeColor.Add(bar.gameObject, ColorRole.Accent);
            bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Text(cell, "Dt", item.Label, OfficeUi.FsGlanceLabel, FontRole.Body);
            Text(cell, "Dd", item.Value, OfficeUi.FsGlanceValue, FontRole.BodyBold);
        }

        void DayRow(RectTransform card, DayLine d)
        {
            RectTransform row = Row(card, "Day", 8f);
            TMP_Text label = Text(row, "Dd", d.Label, OfficeUi.FsDay, FontRole.Body);
            Fixed(label, 104f);
            RectTransform bar = OfficeUi.Rect("Bar", row);
            var ble = bar.gameObject.AddComponent<LayoutElement>();
            ble.flexibleWidth = 1f; ble.minHeight = ble.preferredHeight = 14f; ble.minWidth = 40f;
            OfficeUi.Stretch(OfficeUi.Box(bar, "Track", ColorRole.Ink, OfficeControl.Red, .08f).rectTransform);
            Image fill = OfficeUi.Box(bar, "Fill", ColorRole.Accent2, OfficeControl.Red);
            RectTransform frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2((float)(d.Width / 100.0), 1f);
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            fill.enabled = d.Width > 0;
            TMP_Text value = Text(row, "Dv", d.Value, OfficeUi.FsDay, FontRole.Body);
            Fixed(value, 150f);
        }

        static void Fixed(TMP_Text t, float width)
        {
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = width;
            le.flexibleWidth = 0f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
        }

        /// <summary>One .otab row: the first column takes what is left (left aligned), the rest are fixed and right aligned.</summary>
        void TableRow(RectTransform card, IReadOnlyList<string> cells, float[] widths, bool head, bool faded = false)
        {
            RectTransform row = Row(card, head ? "Head" : "Row", 8f);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(0, 0, head ? 2 : 5, head ? 4 : 5);
            if (faded) row.gameObject.AddComponent<CanvasGroup>().alpha = .75f;
            // border-bottom: 1px ink at 18% (2px ink under the head)
            RectTransform line = OfficeUi.Rect("Rule", row);
            line.anchorMin = new Vector2(0f, 0f); line.anchorMax = new Vector2(1f, 0f); line.pivot = new Vector2(.5f, 0f);
            line.sizeDelta = new Vector2(0f, head ? 2f : 1f);
            line.gameObject.AddComponent<Image>().raycastTarget = false;
            ThemeColor.Add(line.gameObject, ColorRole.Ink, head ? 1f : .18f);
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            for (int i = 0; i < cells.Count && i < widths.Length; i++)
            {
                bool first = i == 0;
                TMP_Text t = OfficeUi.Text(row, "C" + i, cells[i], FontRole.BodyBold, head ? OfficeUi.FsHead : OfficeUi.FsBody, ColorRole.Ink,
                    first ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight, wrap: first);
                var le = t.gameObject.AddComponent<LayoutElement>();
                if (first) { le.flexibleWidth = 1f; le.minWidth = 60f; le.preferredWidth = 60f; }
                else { le.minWidth = le.preferredWidth = widths[i]; le.flexibleWidth = 0f; }
            }
        }
    }
}
