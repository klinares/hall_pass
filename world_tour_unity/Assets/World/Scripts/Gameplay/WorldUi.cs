using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using WorldTour.Game.Flow;
using WorldTour.Game.Prefs;
using WorldTour.World.Audio;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.World.Gameplay
{
    /// <summary>One button on a talk or offer card. The first button is the one Enter, Space and E press; Escape presses the last when there are two or more.</summary>
    public sealed class DialogButton
    {
        public string Label;
        public bool Ghost;
        public Action OnClick;
        /// <summary>False keeps the card open after the click (the stage's "change set").</summary>
        public bool Closes = true;
        /// <summary>Optional. When this is the last button, Escape closes the card and runs this instead of pressing the button (a talk card's Escape just ends the talk).</summary>
        public Action OnEscape;
    }

    /// <summary>
    /// The screen-space canvas of the world (phase 8.1): the small "talk" tag, the speech bubble with its offer buttons,
    /// centred cards (gig result, new stop, travel), a fade, and the panel that holds the 2D game's own screens (a gig, the
    /// show, the ticket) over the 3D view. It is built in code at 1366x768 like the Main scene, so the 2D screens lay out as they do there.
    /// </summary>
    public sealed class WorldUi : MonoBehaviour
    {
        const float BubbleWidth = 600f, Pad = 20f, ButtonH = 56f, ButtonGap = 12f;
        // type sizes on the cards: the typewriter face reads small, so these are bigger than the 2D game's 16 px body (fix round 1: nothing under 18)
        const float WhoSize = 20f, LineSize = 26f, SubSize = 22f, TagSize = 22f, PinSize = 22f, HintSize = 26f;
        const float WhoH = 30f, LineH = 94f, SubH = 36f;

        public Canvas Canvas { get; private set; }
        public ScreenStack Stack { get; private set; }
        public GameScreen RootScreen { get; private set; }
        public Hud Hud { get; private set; }
        public RectTransform PanelRoot { get; private set; }
        /// <summary>Holds riff's bubble while the panel is compact (see <see cref="DogHosts.BubbleHost"/>).</summary>
        public RectTransform BubbleHost { get; private set; }
        /// <summary>The panel is docked to the right (a gig or a show song), leaving the world visible beside it.</summary>
        public bool PanelCompact { get; private set; }
        /// <summary>The lens shift (fraction of the screen width) that centres the view in the open part beside the compact panel.</summary>
        public float OpenLensShift { get { Rect r = _canvasRt.rect; return (ShowLayout.WidthFor(r.width) + ShowLayout.PanelMargin) / (2f * Mathf.Max(1f, r.width)); } }
        public bool PanelOpen { get { return PanelRoot != null && PanelRoot.gameObject.activeSelf; } }
        public bool DialogOpen { get { return _dialog != null && _dialog.gameObject.activeSelf; } }

        WorldGameplayAssets _assets;
        RectTransform _canvasRt, _tag, _dialog, _tail, _fade;
        Image _dim, _fadeImage;
        TMP_Text _tagText, _who, _line, _sub;
        RectTransform _buttonRow;
        readonly List<DialogButton> _buttons = new List<DialogButton>();
        int _openedFrame = -1;
        Vector2? _dialogAnchor;
        Coroutine _fadeRoutine;
        readonly List<ShowButton> _buttonViews = new List<ShowButton>();
        int _focus, _nextFocus;
        bool _selectPending;
        sealed class Pin { public RectTransform Rt; public TMP_Text Text; public float Phase; }
        readonly Dictionary<string, Pin> _pins = new Dictionary<string, Pin>();
        RectTransform _hint;
        TMP_Text _hintText;
        CanvasGroup _hintGroup;

        public static WorldUi Create(WorldGameplayAssets assets)
        {
            var go = new GameObject("WorldCanvas", typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1366, 768);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            var ui = go.AddComponent<WorldUi>();
            ui.Build(canvas, assets);
            return ui;
        }

        void Build(Canvas canvas, WorldGameplayAssets assets)
        {
            Canvas = canvas;
            _assets = assets;
            _canvasRt = (RectTransform)canvas.transform;
            EnsureEventSystem();
            FocusNavigator.Suppress = () => DialogOpen;   // Tab on a card moves between its own buttons (see Update)

            // dim behind the panel
            RectTransform dim = ShowUi.Rect("Dim", _canvasRt);
            ShowUi.Stretch(dim);
            _dim = dim.gameObject.AddComponent<Image>();
            _dim.color = new Color(0.07f, 0.06f, 0.12f, 0.42f);
            _dim.raycastTarget = true;
            dim.gameObject.SetActive(false);

            // riff's speech bubble while a show or gig is on: drawn here, beside the 3D riff, not in the panel
            BubbleHost = ShowUi.Rect("DogBubbles", _canvasRt);
            ShowUi.Stretch(BubbleHost);
            DogHosts.BubbleHost = BubbleHost;

            // the panel that holds the 2D screens
            PanelRoot = ShowUi.Rect("Panel", _canvasRt);
            ShowUi.Stretch(PanelRoot, 20f);
            // the card behind the screens is its own child, so the docked panel can end where its content ends (PanelCardFit) while the 2D-style panel stays full size
            RectTransform card = ShowUi.Rect("Card", PanelRoot);
            ShowUi.Stretch(card);
            var bg = card.gameObject.AddComponent<Image>();
            bg.raycastTarget = true;
            bg.type = Image.Type.Sliced;
            Sprite paper = Theme.Sprite(SpriteRole.RoundedRect);
            if (paper != null) bg.sprite = paper;
            ThemeColor.Add(card.gameObject, ColorRole.Paper);
            RectTransform screens = ShowUi.Rect("Screens", PanelRoot);
            PanelRoot.gameObject.AddComponent<PanelCardFit>().Init(this, card, screens);
            ShowUi.Stretch(screens);
            Stack = screens.gameObject.AddComponent<ScreenStack>();
            var template = new GameObject("Root", typeof(RectTransform), typeof(CanvasGroup));
            template.SetActive(false);
            GameScreen proto = template.AddComponent<GameScreen>();
            RootScreen = Stack.SetRoot(proto);
            Destroy(template);
            PanelRoot.gameObject.SetActive(false);

            BuildTag();
            BuildDialog();

            // the fade sits on top of everything
            _fade = ShowUi.Rect("Fade", _canvasRt);
            ShowUi.Stretch(_fade);
            _fadeImage = _fade.gameObject.AddComponent<Image>();
            _fadeImage.color = new Color(0.07f, 0.06f, 0.12f, 0f);
            _fadeImage.raycastTarget = false;
            _fade.gameObject.SetActive(false);
        }

        /// <summary>The sound toggle and "won't save" notice, once the save is open.</summary>
        public void AddHud(WorldTour.Game.Save.SaveStore store)
        {
            if (Hud != null) return;
            Hud = Hud.Create(_canvasRt, store);
            // the fade stays above the HUD
            _fade.SetAsLastSibling();
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null || UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            DontDestroyOnLoad(es);
            es.AddComponent<EventSystem>();
            var module = es.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        // ---------- the talk tag ----------

        void BuildTag()
        {
            _tag = ShowUi.Rect("TalkTag", _canvasRt);
            ShowUi.Place(_tag, new Vector2(.5f, .5f), new Vector2(320, 46), Vector2.zero);
            Box(_tag, 3f);
            _tagText = ShowUi.Label(_tag, "Text", "", TagSize, FontRole.BodyBold, ColorRole.Ink);
            _tagText.textWrappingMode = TextWrappingModes.NoWrap;
            _tag.gameObject.SetActive(false);
        }

        /// <summary>
        /// The prompt under a crew member's marker (their name, and "e  talk" once she is close enough). Pass the screen point of the spot just under the marker, in
        /// pixels; the tag hangs below that point so it never covers the marker.
        /// </summary>
        public void ShowTag(Vector2 screenPoint, string text)
        {
            _tagText.SetLower(text);
            _tag.gameObject.SetActive(true);
            float w = Mathf.Clamp(_tagText.GetPreferredValues(_tagText.text, 1000, 0).x + 36f, 150f, 420f);
            _tag.sizeDelta = new Vector2(w, 46f);
            _tag.anchoredPosition = ToLocal(screenPoint, new Vector2(0, -_tag.sizeDelta.y * 0.5f - 2f), _tag.sizeDelta);
        }

        public void HideTag() { if (_tag != null && _tag.gameObject.activeSelf) _tag.gameObject.SetActive(false); }

        // ---------- pins: "the stage", "next stop" ----------

        /// <summary>
        /// A label pinned over a spot in the world (a blue pill with a pointer, bobbing a little), drawn above the point given in screen pixels.
        /// Distinct from the crew's white tag: these mark places, not people. <paramref name="visible"/> false hides it.
        /// </summary>
        public void SetPin(string key, string text, Vector2 screenPoint, bool visible)
        {
            Pin p;
            if (!_pins.TryGetValue(key, out p))
            {
                if (!visible) return;
                p = MakePin(key);
                _pins[key] = p;
            }
            if (!visible) { if (p.Rt.gameObject.activeSelf) p.Rt.gameObject.SetActive(false); return; }
            p.Text.SetLower(text);
            if (!p.Rt.gameObject.activeSelf) p.Rt.gameObject.SetActive(true);
            float w = Mathf.Clamp(p.Text.GetPreferredValues(p.Text.text, 1000, 0).x + 40f, 150f, 360f);
            p.Rt.sizeDelta = new Vector2(w, 46f);
            float bob = Mathf.Sin(Time.unscaledTime * 3f + p.Phase) * 5f;
            Vector2 pos = ToLocal(screenPoint, new Vector2(0, 46f * 0.5f + 16f + bob), p.Rt.sizeDelta);
            p.Rt.anchoredPosition = pos;
        }

        public void HidePins()
        {
            foreach (var kv in _pins) if (kv.Value.Rt.gameObject.activeSelf) kv.Value.Rt.gameObject.SetActive(false);
        }

        Pin MakePin(string key)
        {
            RectTransform rt = ShowUi.Rect("Pin_" + key, _canvasRt);
            ShowUi.Place(rt, new Vector2(.5f, .5f), new Vector2(200, 46), Vector2.zero);
            RectTransform tail = ShowUi.Rect("Tail", rt);
            ShowUi.Place(tail, new Vector2(.5f, 0f), new Vector2(22, 22), new Vector2(0, -4));
            tail.localRotation = Quaternion.Euler(0, 0, 45f);
            ShowUi.Panel(tail, ColorRole.Ink).raycastTarget = false;
            ShowUi.Panel(rt, ColorRole.Ink).raycastTarget = false;
            RectTransform face = ShowUi.Rect("Face", rt);
            ShowUi.Stretch(face, 3f);
            ShowUi.Panel(face, ColorRole.Accent2).raycastTarget = false;
            TMP_Text t = ShowUi.Label(face, "Text", "", PinSize, FontRole.BodyBold, ColorRole.OnInk);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            rt.SetSiblingIndex(Mathf.Max(0, _canvasRt.childCount - 3));   // under the dialog and the fade
            return new Pin { Rt = rt, Text = t, Phase = key.Length * 1.3f };
        }

        // ---------- the first-visit hint ----------

        /// <summary>A strip at the bottom of the view with one line of help ("walk with the arrow keys or click the ground").</summary>
        public void ShowHint(string text, float alpha = 1f)
        {
            if (_hint == null)
            {
                _hint = ShowUi.Rect("Hint", _canvasRt);
                _hint.anchorMin = _hint.anchorMax = new Vector2(.5f, 0f);
                _hint.pivot = new Vector2(.5f, 0f);
                _hint.sizeDelta = new Vector2(760, 60);
                _hint.anchoredPosition = new Vector2(0, 84f);
                Box(_hint, 3f);
                _hintText = ShowUi.Label(_hint, "Text", "", HintSize, FontRole.BodyBold, ColorRole.Ink);
                _hintText.textWrappingMode = TextWrappingModes.NoWrap;
                _hintGroup = _hint.gameObject.AddComponent<CanvasGroup>();
                _hintGroup.blocksRaycasts = false; _hintGroup.interactable = false;
                _hint.SetSiblingIndex(Mathf.Max(0, _canvasRt.childCount - 3));
            }
            _hintText.SetLower(text);
            _hintGroup.alpha = alpha;
            _hint.gameObject.SetActive(true);
        }

        public void HideHint() { if (_hint != null && _hint.gameObject.activeSelf) _hint.gameObject.SetActive(false); }

        public bool HintShown { get { return _hint != null && _hint.gameObject.activeSelf; } }

        // ---------- the bubble and cards ----------

        void BuildDialog()
        {
            _dialog = ShowUi.Rect("Dialog", _canvasRt);
            ShowUi.Place(_dialog, new Vector2(.5f, .5f), new Vector2(BubbleWidth, 200), Vector2.zero);
            _tail = ShowUi.Rect("Tail", _dialog);
            ShowUi.Place(_tail, new Vector2(.5f, 0f), new Vector2(26, 26), new Vector2(0, -2));
            _tail.localRotation = Quaternion.Euler(0, 0, 45f);
            Box(_tail, 3f);
            Box(_dialog, 3f);
            // the tail's inner half would show a line across the bubble's edge: draw the bubble body again over it
            RectTransform cover = ShowUi.Rect("Cover", _dialog);
            ShowUi.Stretch(cover, 3f);
            ShowUi.Panel(cover, ColorRole.Card).raycastTarget = false;

            RectTransform whoBox = ShowUi.Rect("Who", _dialog);
            whoBox.anchorMin = new Vector2(0, 1); whoBox.anchorMax = new Vector2(1, 1); whoBox.pivot = new Vector2(.5f, 1);
            whoBox.offsetMin = new Vector2(Pad, -Pad - WhoH); whoBox.offsetMax = new Vector2(-Pad, -Pad);
            _who = ShowUi.Label(whoBox, "Text", "", WhoSize, FontRole.BodyBold, ColorRole.Accent2, TextAlignmentOptions.Left);

            RectTransform lineBox = ShowUi.Rect("Line", _dialog);
            lineBox.anchorMin = new Vector2(0, 1); lineBox.anchorMax = new Vector2(1, 1); lineBox.pivot = new Vector2(.5f, 1);
            lineBox.offsetMin = new Vector2(Pad, -Pad - WhoH - LineH); lineBox.offsetMax = new Vector2(-Pad, -Pad - WhoH);
            _line = ShowUi.Label(lineBox, "Text", "", LineSize, FontRole.Body, ColorRole.Ink, TextAlignmentOptions.TopLeft);
            _line.textWrappingMode = TextWrappingModes.Normal;

            RectTransform subBox = ShowUi.Rect("Sub", _dialog);
            subBox.anchorMin = new Vector2(0, 1); subBox.anchorMax = new Vector2(1, 1); subBox.pivot = new Vector2(.5f, 1);
            subBox.offsetMin = new Vector2(Pad, -Pad - WhoH - LineH - SubH); subBox.offsetMax = new Vector2(-Pad, -Pad - WhoH - LineH);
            _sub = ShowUi.Label(subBox, "Text", "", SubSize, FontRole.BodyBold, ColorRole.Muted, TextAlignmentOptions.Left);

            _buttonRow = ShowUi.Rect("Buttons", _dialog);
            _buttonRow.anchorMin = new Vector2(0, 0); _buttonRow.anchorMax = new Vector2(1, 0); _buttonRow.pivot = new Vector2(.5f, 0);
            _buttonRow.offsetMin = new Vector2(Pad, Pad); _buttonRow.offsetMax = new Vector2(-Pad, Pad + ButtonH);
            _dialog.gameObject.SetActive(false);
        }

        /// <summary>
        /// A speech bubble with a line, an optional second line and buttons. With a screen point it hangs above that point
        /// (a crew member's head) with a tail; with none it is a card in the middle of the screen.
        /// </summary>
        public void OpenDialog(string who, string line, string sub, IList<DialogButton> buttons, Vector2? screenPoint, bool quiet = false)
        {
            if (!quiet && !DialogOpen) WorldAudio.Play(Cue.CardOpen);
            if (BubbleHost != null) BubbleHost.gameObject.SetActive(false);   // a card never sits on top of riff's bubble: his lines wait until it closes
            HidePins(); HideHint();
            _who.SetLower(who ?? "");
            _line.SetLower(line ?? "");
            _sub.SetLower(sub ?? "");
            bool hasSub = !string.IsNullOrEmpty(sub);
            _sub.gameObject.SetActive(hasSub);
            _buttons.Clear(); _buttonViews.Clear();
            for (int i = _buttonRow.childCount - 1; i >= 0; i--) Destroy(_buttonRow.GetChild(i).gameObject);
            if (buttons != null) _buttons.AddRange(buttons);

            // the line box is as tall as its text (1 to 3 lines), so a short card has no hole in it
            float lh = Mathf.Clamp(Mathf.Ceil(_line.GetPreferredValues(_line.text, BubbleWidth - 2f * Pad, 0).y) + 8f, 40f, LineH);
            var lineRt = (RectTransform)_line.transform.parent;
            lineRt.offsetMin = new Vector2(Pad, -Pad - WhoH - lh); lineRt.offsetMax = new Vector2(-Pad, -Pad - WhoH);
            var subBoxRt = (RectTransform)_sub.transform.parent;
            subBoxRt.offsetMin = new Vector2(Pad, -Pad - WhoH - lh - SubH); subBoxRt.offsetMax = new Vector2(-Pad, -Pad - WhoH - lh);
            float h = Pad + WhoH + lh + (hasSub ? SubH : 0f) + 12f + ButtonH + Pad;
            if (_buttons.Count == 0) h -= 12f + ButtonH;
            _dialog.sizeDelta = new Vector2(BubbleWidth, h);
            var subRt = (RectTransform)_sub.transform.parent;
            subRt.gameObject.SetActive(hasSub);

            // buttons: equal widths in a row
            int n = _buttons.Count;
            if (n > 0)
            {
                float w = (BubbleWidth - 2 * Pad - (n - 1) * ButtonGap) / n;
                for (int i = 0; i < n; i++)
                {
                    DialogButton b = _buttons[i];
                    RectTransform box = ShowUi.Rect("Button " + (i + 1), _buttonRow);
                    box.anchorMin = new Vector2(0, 0); box.anchorMax = new Vector2(0, 1); box.pivot = new Vector2(0, .5f);
                    box.offsetMin = new Vector2(i * (w + ButtonGap), 0); box.offsetMax = new Vector2(i * (w + ButtonGap) + w, 0);
                    DialogButton captured = b;
                    ShowButton sb = ShowUi.Button(_assets != null ? _assets.buttonPrefab : null, box, "Offer" + (i + 1), b.Label, b.Ghost, () => Press(captured));
                    ShowUi.Stretch(sb.Rect);
                    _buttonViews.Add(sb);
                }
            }

            _dialogAnchor = screenPoint;
            _tail.gameObject.SetActive(screenPoint.HasValue);
            _dialog.gameObject.SetActive(true);
            _dialog.SetAsLastSibling();
            if (_fade != null) _fade.SetAsLastSibling();
            _openedFrame = Time.frameCount;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            _focus = _nextFocus < _buttons.Count ? _nextFocus : 0;
            _nextFocus = 0;
            _selectPending = _buttons.Count > 0;   // selected one frame later, so the key that opened the card cannot also press its button
            PlaceDialog();
        }

        /// <summary>Test only: where each button of the open card is, "label:x:y|label:x:y" in page pixels, or null when no card is open (WorldGame logs it as WT_WORLD_BUTTONS).</summary>
        public string ButtonCentres()
        {
            if (!DialogOpen || _buttonViews.Count == 0) return null;
            Canvas.ForceUpdateCanvases();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _buttonViews.Count && i < _buttons.Count; i++)
            {
                if (i > 0) sb.Append('|');
                sb.Append(_buttons[i].Label.Replace(' ', '_')).Append(':').Append(WorldTour.Game.Modes.MathUi.Centre(_buttonViews[i].Rect));
            }
            return sb.ToString();
        }

        void PlaceDialog()
        {
            Vector2 size = _dialog.sizeDelta;
            if (_dialogAnchor.HasValue)
            {
                Vector2 p = ToLocal(_dialogAnchor.Value, new Vector2(0, size.y * 0.5f + 34f), size);
                _dialog.anchoredPosition = p;
                // keep the tail over the head even when the bubble is clamped to the screen
                Vector2 head = ToLocal(_dialogAnchor.Value, Vector2.zero, Vector2.zero);
                float tx = Mathf.Clamp(head.x - p.x, -size.x / 2f + 40f, size.x / 2f - 40f);
                _tail.anchoredPosition = new Vector2(tx, -2f);
            }
            else
            {
                // a card: the middle of the screen, or of the part beside the docked panel
                float cx = PanelCompact ? (_canvasRt.rect.xMin + OpenRightLocal()) / 2f : 0f;
                _dialog.anchoredPosition = new Vector2(cx, 20f);
            }
        }

        /// <summary>Moves the bubble with its crew member's head while it is open.</summary>
        public void MoveDialogAnchor(Vector2 screenPoint)
        {
            if (!DialogOpen || !_dialogAnchor.HasValue) return;
            _dialogAnchor = screenPoint;
            PlaceDialog();
        }

        public void CloseDialog()
        {
            if (_dialog != null && _dialog.gameObject.activeSelf)
            {
                var es = EventSystem.current;
                if (es != null && es.currentSelectedGameObject != null && es.currentSelectedGameObject.transform.IsChildOf(_dialog)) es.SetSelectedGameObject(null);
            }
            if (_dialog != null) _dialog.gameObject.SetActive(false);
            _buttons.Clear(); _buttonViews.Clear();
            if (BubbleHost != null) BubbleHost.gameObject.SetActive(true);
        }

        void Press(DialogButton b)
        {
            if (b == null) return;
            if (!b.Closes) _nextFocus = Mathf.Max(0, _buttons.IndexOf(b));   // "change set" and "another gig" keep the focus where it was
            if (b.Closes) CloseDialog();
            if (b.OnClick != null) b.OnClick();
        }

        void OnDestroy()
        {
            if (DogHosts.BubbleHost == BubbleHost) DogHosts.BubbleHost = null;
            FocusNavigator.Suppress = null;
        }

        /// <summary>Index of the dialog button that holds the EventSystem selection, or -1.</summary>
        int SelectedButton()
        {
            var es = EventSystem.current;
            if (es == null || es.currentSelectedGameObject == null) return -1;
            for (int i = 0; i < _buttonViews.Count; i++) if (_buttonViews[i].Go == es.currentSelectedGameObject) return i;
            return -1;
        }

        void SelectButton(int i)
        {
            if (EventSystem.current == null || i < 0 || i >= _buttonViews.Count) return;
            _focus = i;
            EventSystem.current.SetSelectedGameObject(_buttonViews[i].Go);
        }

        /// <summary>
        /// Keyboard on a card: Tab and Shift+Tab (or the arrow keys) move the focus between its buttons, wrapping round, with the usual ring on the focused one;
        /// Enter and Space press the focused button (the StickerButton does that itself while it is selected), E presses it too, Escape presses the last
        /// of two or more ("not now"). The first button has the focus when the card opens.
        /// </summary>
        void Update()
        {
            if (!DialogOpen || _buttons.Count == 0 || Time.frameCount <= _openedFrame) return;
            Keyboard k = Keyboard.current;
            bool justSelected = false;
            if (_selectPending) { _selectPending = false; SelectButton(_focus); justSelected = true; }
            // the focus is ours alone: the UI module also moves the selection on W, A, S, D and the arrows (held as she walked onto the spot, they repeat), which put it on
            // "stay here" or on the sound button in the corner, so Enter pressed the wrong thing. Put it back on the focused button (Tab and the arrows below change _focus).
            else if (SelectedButton() != _focus && Time.frameCount > _openedFrame + 1) { SelectButton(_focus); justSelected = true; }
            if (k == null) return;
            _focus = Mathf.Clamp(_focus, 0, _buttons.Count - 1);

            int n = _buttons.Count;
            if (k.tabKey.wasPressedThisFrame) SelectButton((_focus + (k.shiftKey.isPressed ? -1 : 1) + n) % n);
            else if (k.leftArrowKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame) SelectButton((_focus - 1 + n) % n);
            else if (k.rightArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame) SelectButton((_focus + 1) % n);
            else if (k.eKey.wasPressedThisFrame) Press(_buttons[_focus]);
            else if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)
            {
                // with the focused button selected the StickerButton presses itself; press here only when it could not (just selected, or the selection was lost)
                if (justSelected || SelectedButton() < 0) Press(_buttons[_focus]);
            }
            else if (k.escapeKey.wasPressedThisFrame)
            {
                // Escape always closes a card: it presses the last button ("not now", "stay here", or the only "ok")
                DialogButton last = _buttons[n - 1];
                WorldAudio.Play(Cue.CardClose);
                if (last.OnEscape != null) { CloseDialog(); last.OnEscape(); }
                else Press(last);
            }
        }

        // ---------- the panel ----------

        /// <summary>Shows the dim and the panel. <paramref name="scale"/> shrinks it toward its top edge when below 1 (the stage view leaves the crowd visible below).</summary>
        public void OpenPanel(float scale, bool fromTop)
        {
            PanelCompact = false;
            _dim.gameObject.SetActive(true);
            _dim.color = new Color(0.07f, 0.06f, 0.12f, fromTop ? 0.12f : 0.42f);
            PanelRoot.gameObject.SetActive(true);
            PanelRoot.anchorMin = Vector2.zero; PanelRoot.anchorMax = Vector2.one;
            PanelRoot.offsetMin = new Vector2(20f, 20f); PanelRoot.offsetMax = new Vector2(-20f, -20f);
            PanelRoot.pivot = new Vector2(.5f, fromTop ? 1f : .5f);
            PanelRoot.localScale = new Vector3(scale, scale, 1f);
            PanelRoot.SetAsLastSibling();
            if (Hud != null) Hud.Rect.SetAsLastSibling();
            _dialog.SetAsLastSibling();
            _fade.SetAsLastSibling();
        }

        /// <summary>
        /// The panel docked to the right edge (a gig or a show song): the screens inside are laid out <see cref="ShowLayout.LayoutWidth"/> wide and drawn at
        /// <see cref="ShowLayout.PanelScale"/>, so the world shows beside it and nothing dims it. Call with <see cref="ShowLayout.Compact"/> already set.
        /// </summary>
        public void OpenCompactPanel()
        {
            PanelCompact = true;
            _dim.gameObject.SetActive(false);
            PanelRoot.gameObject.SetActive(true);
            Rect c = _canvasRt.rect;
            PanelRoot.anchorMin = PanelRoot.anchorMax = new Vector2(1f, .5f);
            PanelRoot.pivot = new Vector2(1f, .5f);
            PanelRoot.sizeDelta = new Vector2(ShowLayout.LayoutWidth(c.width), ShowLayout.LayoutHeight(c.height));
            ShowLayout.PanelSize = PanelRoot.sizeDelta;   // the screens inside lay themselves out (and fit) to this
            PanelRoot.anchoredPosition = new Vector2(-ShowLayout.PanelMargin, 0f);
            PanelRoot.localScale = new Vector3(ShowLayout.PanelScale, ShowLayout.PanelScale, 1f);
            PanelRoot.SetAsLastSibling();
            if (Hud != null) Hud.Rect.SetAsLastSibling();
            _dialog.SetAsLastSibling();
            _fade.SetAsLastSibling();
        }

        public void ClosePanel()
        {
            PanelCompact = false;
            _dim.gameObject.SetActive(false);
            PanelRoot.gameObject.SetActive(false);
        }

        /// <summary>The world-side edge of the compact panel in screen pixels (the right edge of the open part), for placing the dog bubble.</summary>
        float OpenRightLocal()
        {
            Rect r = _canvasRt.rect;
            return PanelCompact ? r.xMax - ShowLayout.WidthFor(r.width) - ShowLayout.PanelMargin * 2f : r.xMax - 16f;
        }

        /// <summary>Hangs riff's bubble (if one is up) over a point on the screen, kept inside the open part beside the panel.</summary>
        public void PlaceDogBubble(Vector2 screenPoint, float lift = 0f)
        {
            PlaceDogBubble(screenPoint, null, null, lift);
        }

        /// <summary>
        /// Round 8.4: the bubble hangs beside riff, level with his head, on the side that keeps clear of her (feet and head screen points) so it never covers her body, and
        /// low enough that it stays off the venue sign above the stage. With no player points it hangs over riff as before.
        /// </summary>
        public void PlaceDogBubble(Vector2 screenPoint, Vector2? playerFeet, Vector2? playerHead, float lift = 0f)
        {
            if (BubbleHost == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, screenPoint, null, out Vector2 local);
            Rect r = _canvasRt.rect;
            Rect? her = null;
            if (playerFeet.HasValue && playerHead.HasValue)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, playerFeet.Value, null, out Vector2 f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, playerHead.Value, null, out Vector2 hd);
                her = Rect.MinMaxRect(Mathf.Min(f.x, hd.x) - 70f, Mathf.Min(f.y, hd.y) - 10f, Mathf.Max(f.x, hd.x) + 70f, Mathf.Max(f.y, hd.y) + 40f);
            }
            for (int i = 0; i < BubbleHost.childCount; i++)
            {
                var rt = BubbleHost.GetChild(i) as RectTransform;
                if (rt == null || !rt.gameObject.activeSelf) continue;
                float w = rt.rect.width, h = rt.rect.height;
                float minX = r.xMin + 16f + w / 2f, maxX = Mathf.Max(minX, OpenRightLocal() - w / 2f);
                float y = Mathf.Clamp(local.y + 34f, r.yMin + 80f, Mathf.Max(r.yMin + 80f, r.yMax - 16f - h));
                float x = Mathf.Clamp(local.x, minX, maxX);
                if (her.HasValue)
                {
                    // candidates: beside riff on the left, beside him on the right, then over him; the first that does not touch her wins
                    float gap = 50f;
                    float[] xs = { local.x - gap - w / 2f, local.x + gap + w / 2f, local.x, local.x };
                    float belowY = Mathf.Min(her.Value.yMin, local.y - 10f) - h - 8f;   // under her feet and riff: the ground in front of them
                    float bestScore = float.MaxValue;
                    for (int c = 0; c < xs.Length; c++)
                    {
                        float lo = c == 3 ? Mathf.Max(minX, r.xMin + 110f + w / 2f) : minX;   // the mute button sits in the bottom-left corner
                        float cx = Mathf.Clamp(xs[c], lo, Mathf.Max(lo, maxX));
                        float cy = c < 2 ? Mathf.Clamp(local.y + 34f, r.yMin + 80f, Mathf.Max(r.yMin + 80f, r.yMax - 16f - h))
                                 : c == 2 ? Mathf.Clamp(local.y + 34f + lift, r.yMin + 80f, Mathf.Max(r.yMin + 80f, r.yMax - 16f - h))
                                 : Mathf.Max(r.yMin + 16f, belowY);
                        Rect box = new Rect(cx - w / 2f, cy, w, h);
                        float ox = Mathf.Max(0f, Mathf.Min(box.xMax, her.Value.xMax) - Mathf.Max(box.xMin, her.Value.xMin));
                        float oy = Mathf.Max(0f, Mathf.Min(box.yMax, her.Value.yMax) - Mathf.Max(box.yMin, her.Value.yMin));
                        float score = ox * oy + c * 1f;   // overlap area, with the earlier candidate winning a tie
                        if (score < bestScore) { bestScore = score; x = cx; y = cy; }
                    }
                }
                else y = Mathf.Clamp(local.y + 34f + lift, r.yMin + 80f, Mathf.Max(r.yMin + 80f, r.yMax - 16f - h));
                rt.anchoredPosition = new Vector2(x, y);
            }
        }

        // ---------- the tour bus and the unlock confetti (phase 8.3) ----------

        RectTransform _travel, _bus, _wheelA, _wheelB;
        const float BusW = 360f, BusH = 130f, BusY = -210f;

        /// <summary>
        /// The travel moment: the card says where the bus is going, and a small tour bus drives across the screen under it (it parks in the middle under reduce motion).
        /// <see cref="TravelProgress"/> moves it; <see cref="HideTravel"/> clears it.
        /// </summary>
        public void ShowTravel(string who, string line, string sub)
        {
            if (_travel == null) BuildTravel();
            OpenDialog(who, line, sub, null, null, true);
            _travel.gameObject.SetActive(true);
            TravelProgress(0f);
            _travel.SetAsLastSibling();
            _dialog.SetAsLastSibling();   // above the fade, which dims the street behind
        }

        public void HideTravel()
        {
            if (_travel != null) _travel.gameObject.SetActive(false);
        }

        /// <summary>Puts the bus at 0 (off the left edge) to 1 (off the right edge); it keeps a little bounce on the road. Parked in the middle under reduce motion.</summary>
        public void TravelProgress(float t)
        {
            if (_bus == null) return;
            bool still = GameSettings.ReducedMotion;
            float half = _canvasRt.rect.width / 2f + BusW * 0.7f;
            float k = Mathf.Clamp01(t);
            float x = still ? 0f : Mathf.Lerp(-half, half, Mathf.SmoothStep(0f, 1f, k) * 0.5f + k * 0.5f);
            float bob = still ? 0f : Mathf.Sin(t * 60f) * 2.5f;
            _bus.anchoredPosition = new Vector2(x, BusY + bob);
            if (!still)
            {
                float spin = -t * 1600f;
                _wheelA.localRotation = Quaternion.Euler(0, 0, spin);
                _wheelB.localRotation = Quaternion.Euler(0, 0, spin);
            }
        }

        void BuildTravel()
        {
            _travel = ShowUi.Rect("Travel", _canvasRt);
            ShowUi.Stretch(_travel);
            // the road: an ink strip with white dashes
            RectTransform road = ShowUi.Rect("Road", _travel);
            ShowUi.Place(road, new Vector2(.5f, .5f), new Vector2(4000f, 22f), new Vector2(0, BusY - BusH / 2f - 8f));
            ShowUi.Panel(road, ColorRole.Ink).raycastTarget = false;
            for (int i = -20; i <= 20; i++)
            {
                RectTransform dash = ShowUi.Rect("Dash", road);
                ShowUi.Place(dash, new Vector2(.5f, .5f), new Vector2(46f, 5f), new Vector2(i * 120f, 0));
                ShowUi.Panel(dash, ColorRole.OnInk).raycastTarget = false;
            }
            // the bus: ink outline, pink body, four windows, a blue stripe with its name, two wheels
            _bus = ShowUi.Rect("Bus", _travel);
            ShowUi.Place(_bus, new Vector2(.5f, .5f), new Vector2(BusW, BusH), new Vector2(0, BusY));
            ShowUi.Panel(_bus, ColorRole.Ink).raycastTarget = false;
            RectTransform body = ShowUi.Rect("Body", _bus);
            ShowUi.Stretch(body, 4f);
            ShowUi.Panel(body, ColorRole.PaperDeep).raycastTarget = false;
            for (int i = 0; i < 4; i++)
            {
                RectTransform w = ShowUi.Rect("Window" + (i + 1), _bus);
                ShowUi.Place(w, new Vector2(0f, 1f), new Vector2(64f, 42f), new Vector2(18f + i * 80f + 32f, -14f - 21f));
                Box(w, 3f);
            }
            RectTransform stripe = ShowUi.Rect("Stripe", _bus);
            stripe.anchorMin = new Vector2(0f, 0f); stripe.anchorMax = new Vector2(1f, 0f); stripe.pivot = new Vector2(.5f, 0f);
            stripe.offsetMin = new Vector2(4f, 26f); stripe.offsetMax = new Vector2(-4f, 56f);
            ShowUi.Panel(stripe, ColorRole.Accent2).raycastTarget = false;
            TMP_Text name = ShowUi.Label(stripe, "Text", "world tour", 20f, FontRole.BodyBold, ColorRole.OnInk);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            _wheelA = Wheel(-BusW / 2f + 78f);
            _wheelB = Wheel(BusW / 2f - 78f);
            _travel.gameObject.SetActive(false);
        }

        /// <summary>One wheel (an ink tire with a card hub); returns its spoke, which is what turns.</summary>
        RectTransform Wheel(float x)
        {
            RectTransform tire = ShowUi.Rect("Wheel", _bus);
            ShowUi.Place(tire, new Vector2(.5f, 0f), new Vector2(46f, 46f), new Vector2(x, 2f));
            ShowUi.Panel(tire, ColorRole.Ink).raycastTarget = false;
            RectTransform hub = ShowUi.Rect("Hub", tire);
            ShowUi.Place(hub, new Vector2(.5f, .5f), new Vector2(20f, 20f), Vector2.zero);
            ShowUi.Panel(hub, ColorRole.Card).raycastTarget = false;
            RectTransform spoke = ShowUi.Rect("Spoke", hub);
            ShowUi.Place(spoke, new Vector2(.5f, .5f), new Vector2(14f, 4f), Vector2.zero);
            ShowUi.Panel(spoke, ColorRole.Ink).raycastTarget = false;
            return spoke;
        }

        /// <summary>The unlock card gets a pop (a quick overshoot) and a shower of confetti. Under reduce motion: neither, the card just shows.</summary>
        public void Celebrate()
        {
            if (GameSettings.ReducedMotion || !DialogOpen) return;
            StartCoroutine(PopDialog());
            StartCoroutine(Confetti());
        }

        IEnumerator PopDialog()
        {
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                float e = Mathf.Clamp01(t / 0.35f);
                float s = 0.82f + 0.18f * (1f + 2.2f * Mathf.Pow(e - 1f, 3f) + 1.2f * Mathf.Pow(e - 1f, 2f));
                _dialog.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            _dialog.localScale = Vector3.one;
        }

        IEnumerator Confetti()
        {
            ColorRole[] roles = { ColorRole.PaperDeep, ColorRole.Accent, ColorRole.Accent2, ColorRole.Card, ColorRole.Ink };
            RectTransform layer = ShowUi.Rect("Confetti", _canvasRt);
            ShowUi.Stretch(layer);
            var bits = new List<RectTransform>();
            var vel = new List<Vector3>();
            Rect r = _canvasRt.rect;
            var rng = new System.Random(77);
            for (int i = 0; i < 46; i++)
            {
                RectTransform b = ShowUi.Rect("Bit", layer);
                float w = 10f + (float)rng.NextDouble() * 10f;
                ShowUi.Place(b, new Vector2(.5f, .5f), new Vector2(w, w * 0.55f), new Vector2(Mathf.Lerp(r.xMin + 60f, r.xMax - 60f, (float)rng.NextDouble()), r.yMax + 20f + (float)rng.NextDouble() * 120f));
                ShowUi.Panel(b, roles[i % roles.Length]).raycastTarget = false;
                b.localRotation = Quaternion.Euler(0, 0, (float)rng.NextDouble() * 360f);
                bits.Add(b);
                vel.Add(new Vector3((float)(rng.NextDouble() - 0.5) * 120f, -(380f + (float)rng.NextDouble() * 320f), (float)(rng.NextDouble() - 0.5) * 540f));
            }
            layer.SetAsLastSibling();
            _dialog.SetAsLastSibling();
            _fade.SetAsLastSibling();
            float t = 0f;
            while (t < 1.9f)
            {
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < bits.Count; i++)
                {
                    Vector3 v = vel[i];
                    bits[i].anchoredPosition += new Vector2(v.x, v.y) * Time.unscaledDeltaTime;
                    bits[i].Rotate(0, 0, v.z * Time.unscaledDeltaTime);
                }
                yield return null;
            }
            Destroy(layer.gameObject);
        }

        // ---------- the fade ----------

        public void FadeTo(float alpha, float seconds)
        {
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeRoutine(alpha, seconds));
        }

        IEnumerator FadeRoutine(float alpha, float seconds)
        {
            _fade.gameObject.SetActive(true);
            _fade.SetAsLastSibling();
            _fadeImage.raycastTarget = alpha > 0.01f;
            float a0 = _fadeImage.color.a, t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                Color c = _fadeImage.color; c.a = Mathf.Lerp(a0, alpha, Mathf.Clamp01(t / Mathf.Max(0.0001f, seconds))); _fadeImage.color = c;
                yield return null;
            }
            Color e = _fadeImage.color; e.a = alpha; _fadeImage.color = e;
            if (alpha <= 0.01f) { _fade.gameObject.SetActive(false); _fadeImage.raycastTarget = false; }
            _fadeRoutine = null;
        }

        // ---------- helpers ----------

        /// <summary>An ink outline with a card face: an ink rounded rect with a card one inset by the border width.</summary>
        static void Box(RectTransform rt, float border)
        {
            ShowUi.Panel(rt, ColorRole.Ink).raycastTarget = false;
            RectTransform face = ShowUi.Rect("Face", rt);
            ShowUi.Stretch(face, border);
            ShowUi.Panel(face, ColorRole.Card).raycastTarget = false;
        }

        /// <summary>Canvas-local position for a screen point plus an offset, kept inside the canvas for a box of this size.</summary>
        Vector2 ToLocal(Vector2 screenPoint, Vector2 offset, Vector2 size)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, screenPoint, null, out Vector2 local);
            Vector2 p = local + offset;
            Rect r = _canvasRt.rect;
            float mx = Mathf.Max(0f, r.width / 2f - size.x / 2f - 16f), my = Mathf.Max(0f, r.height / 2f - size.y / 2f - 16f);
            p.x = Mathf.Clamp(p.x, -mx, mx);
            p.y = Mathf.Clamp(p.y, -my, my);
            return p;
        }

        /// <summary>True when the pointer is over a button or the open panel (a click there must not walk her).</summary>
        public bool PointerOverUi()
        {
            if (PanelOpen || DialogOpen) return true;
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }

    /// <summary>
    /// Phase 8.3 look fix: in the docked panel the card ends where its content ends (the old card ran to the bottom of the screen with an empty pink slab
    /// under the keypad), so more of the world shows. It measures the lowest visible graphic of the open screen and eases the card's height to it. Anywhere
    /// else (the full-size panel, the shop whose content fills the panel) the card is the whole panel, as before.
    /// </summary>
    sealed class PanelCardFit : MonoBehaviour
    {
        const float Pad = 22f, MinHeight = 340f, FullBleed = 0.9f, Rate = 12f;
        WorldUi _ui;
        RectTransform _card, _screens, _root;
        readonly List<Graphic> _graphics = new List<Graphic>();
        readonly Vector3[] _corners = new Vector3[4];
        float _height;
        int _frames;
        bool _full = true;

        public void Init(WorldUi ui, RectTransform card, RectTransform screens)
        {
            _ui = ui; _card = card; _screens = screens; _root = (RectTransform)transform;
        }

        void OnEnable() { _frames = 0; _height = -1f; }

        void LateUpdate()
        {
            if (_ui == null) return;
            if (!_ui.PanelCompact)
            {
                if (!_full) { ShowUi.Stretch(_card); _full = true; }
                return;
            }
            float full = _root.rect.height;
            float target = Measure(full);
            _frames++;
            if (_height < 0f || _frames < 3) _height = target;
            else _height = Mathf.Lerp(_height, target, 1f - Mathf.Exp(-Rate * Time.unscaledDeltaTime));
            _full = false;
            _card.anchorMin = new Vector2(0f, 1f); _card.anchorMax = new Vector2(1f, 1f);
            _card.pivot = new Vector2(.5f, 1f);
            _card.sizeDelta = new Vector2(0f, Mathf.Min(full, _height));
            _card.anchoredPosition = Vector2.zero;
        }

        /// <summary>The card height that holds everything visible in the screens, plus padding; the whole panel when something reaches its foot.</summary>
        float Measure(float full)
        {
            float low = float.MaxValue;
            _screens.GetComponentsInChildren(false, _graphics);
            for (int i = 0; i < _graphics.Count; i++)
            {
                Graphic g = _graphics[i];
                if (g == null || !g.isActiveAndEnabled || g.color.a < 0.02f || g.canvasRenderer.cull || g.canvasRenderer.GetInheritedAlpha() < 0.02f) continue;
                var tmp = g as TMP_Text;
                if (tmp != null && string.IsNullOrEmpty(tmp.text)) continue;
                g.rectTransform.GetWorldCorners(_corners);
                float y0 = float.MaxValue, y1 = float.MinValue;
                for (int c = 0; c < 4; c++)
                {
                    float y = _root.InverseTransformPoint(_corners[c]).y;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
                if (y1 - y0 > full * FullBleed) continue;   // a full-bleed backdrop is not content
                if (y0 < low) low = y0;
            }
            if (low == float.MaxValue) return full;
            float top = _root.rect.yMax;
            float h = top - low + Pad;
            return h > full - 30f ? full : Mathf.Max(MinHeight, h);
        }
    }
}
