using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
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
    }

    /// <summary>
    /// The screen-space canvas of the world (phase 8.1): the small "talk" tag, the speech bubble with its offer buttons,
    /// centred cards (gig result, new stop, travel), a fade, and the panel that holds the 2D game's own screens (a gig, the
    /// show, the ticket) over the 3D view. It is built in code at 1366x768 like the Main scene, so the 2D screens lay out as they do there.
    /// </summary>
    public sealed class WorldUi : MonoBehaviour
    {
        const float BubbleWidth = 560f, Pad = 20f, ButtonH = 56f, ButtonGap = 12f;

        public Canvas Canvas { get; private set; }
        public ScreenStack Stack { get; private set; }
        public GameScreen RootScreen { get; private set; }
        public Hud Hud { get; private set; }
        public RectTransform PanelRoot { get; private set; }
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

            // dim behind the panel
            RectTransform dim = ShowUi.Rect("Dim", _canvasRt);
            ShowUi.Stretch(dim);
            _dim = dim.gameObject.AddComponent<Image>();
            _dim.color = new Color(0.07f, 0.06f, 0.12f, 0.42f);
            _dim.raycastTarget = true;
            dim.gameObject.SetActive(false);

            // the panel that holds the 2D screens
            PanelRoot = ShowUi.Rect("Panel", _canvasRt);
            ShowUi.Stretch(PanelRoot, 20f);
            var bg = PanelRoot.gameObject.AddComponent<Image>();
            bg.raycastTarget = true;
            bg.type = Image.Type.Sliced;
            Sprite paper = Theme.Sprite(SpriteRole.RoundedRect);
            if (paper != null) bg.sprite = paper;
            ThemeColor.Add(PanelRoot.gameObject, ColorRole.Paper);
            RectTransform screens = ShowUi.Rect("Screens", PanelRoot);
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
            ShowUi.Place(_tag, new Vector2(.5f, .5f), new Vector2(150, 36), Vector2.zero);
            Box(_tag, 3f);
            _tagText = ShowUi.Label(_tag, "Text", "", Tokens.Fs1, FontRole.BodyBold, ColorRole.Ink);
            _tag.gameObject.SetActive(false);
        }

        /// <summary>The small prompt above a crew member ("e  talk"). Pass the head's screen point in pixels.</summary>
        public void ShowTag(Vector2 screenPoint, string text)
        {
            _tagText.SetLower(text);
            _tag.gameObject.SetActive(true);
            _tag.anchoredPosition = ToLocal(screenPoint, new Vector2(0, 26f), _tag.sizeDelta);
        }

        public void HideTag() { if (_tag != null && _tag.gameObject.activeSelf) _tag.gameObject.SetActive(false); }

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
            whoBox.offsetMin = new Vector2(Pad, -Pad - 24); whoBox.offsetMax = new Vector2(-Pad, -Pad);
            _who = ShowUi.Label(whoBox, "Text", "", Tokens.Fs0, FontRole.BodyBold, ColorRole.Accent2, TextAlignmentOptions.Left);

            RectTransform lineBox = ShowUi.Rect("Line", _dialog);
            lineBox.anchorMin = new Vector2(0, 1); lineBox.anchorMax = new Vector2(1, 1); lineBox.pivot = new Vector2(.5f, 1);
            lineBox.offsetMin = new Vector2(Pad, -Pad - 24 - 68); lineBox.offsetMax = new Vector2(-Pad, -Pad - 24);
            _line = ShowUi.Label(lineBox, "Text", "", Tokens.Fs2, FontRole.Body, ColorRole.Ink, TextAlignmentOptions.TopLeft);
            _line.textWrappingMode = TextWrappingModes.Normal;

            RectTransform subBox = ShowUi.Rect("Sub", _dialog);
            subBox.anchorMin = new Vector2(0, 1); subBox.anchorMax = new Vector2(1, 1); subBox.pivot = new Vector2(.5f, 1);
            subBox.offsetMin = new Vector2(Pad, -Pad - 24 - 68 - 30); subBox.offsetMax = new Vector2(-Pad, -Pad - 24 - 68);
            _sub = ShowUi.Label(subBox, "Text", "", Tokens.Fs1, FontRole.BodyBold, ColorRole.Muted, TextAlignmentOptions.Left);

            _buttonRow = ShowUi.Rect("Buttons", _dialog);
            _buttonRow.anchorMin = new Vector2(0, 0); _buttonRow.anchorMax = new Vector2(1, 0); _buttonRow.pivot = new Vector2(.5f, 0);
            _buttonRow.offsetMin = new Vector2(Pad, Pad); _buttonRow.offsetMax = new Vector2(-Pad, Pad + ButtonH);
            _dialog.gameObject.SetActive(false);
        }

        /// <summary>
        /// A speech bubble with a line, an optional second line and buttons. With a screen point it hangs above that point
        /// (a crew member's head) with a tail; with none it is a card in the middle of the screen.
        /// </summary>
        public void OpenDialog(string who, string line, string sub, IList<DialogButton> buttons, Vector2? screenPoint)
        {
            _who.SetLower(who ?? "");
            _line.SetLower(line ?? "");
            _sub.SetLower(sub ?? "");
            bool hasSub = !string.IsNullOrEmpty(sub);
            _sub.gameObject.SetActive(hasSub);
            _buttons.Clear();
            for (int i = _buttonRow.childCount - 1; i >= 0; i--) Destroy(_buttonRow.GetChild(i).gameObject);
            if (buttons != null) _buttons.AddRange(buttons);

            float h = Pad + 24f + 68f + (hasSub ? 30f : 0f) + 12f + ButtonH + Pad;
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
                }
            }

            _dialogAnchor = screenPoint;
            _tail.gameObject.SetActive(screenPoint.HasValue);
            _dialog.gameObject.SetActive(true);
            _dialog.SetAsLastSibling();
            if (_fade != null) _fade.SetAsLastSibling();
            _openedFrame = Time.frameCount;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            PlaceDialog();
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
            else _dialog.anchoredPosition = new Vector2(0, 20f);
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
            if (_dialog != null) _dialog.gameObject.SetActive(false);
            _buttons.Clear();
        }

        void Press(DialogButton b)
        {
            if (b == null) return;
            if (b.Closes) CloseDialog();
            if (b.OnClick != null) b.OnClick();
        }

        void Update()
        {
            if (!DialogOpen || _buttons.Count == 0 || Time.frameCount <= _openedFrame) return;
            Keyboard k = Keyboard.current;
            if (k == null) return;
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame)
                Press(_buttons[0]);
            else if (k.escapeKey.wasPressedThisFrame && _buttons.Count > 1)
                Press(_buttons[_buttons.Count - 1]);
        }

        // ---------- the panel ----------

        /// <summary>Shows the dim and the panel. <paramref name="scale"/> shrinks it toward its top edge when below 1 (the stage view leaves the crowd visible below).</summary>
        public void OpenPanel(float scale, bool fromTop)
        {
            _dim.gameObject.SetActive(true);
            _dim.color = new Color(0.07f, 0.06f, 0.12f, fromTop ? 0.12f : 0.42f);
            PanelRoot.gameObject.SetActive(true);
            PanelRoot.pivot = new Vector2(.5f, fromTop ? 1f : .5f);
            PanelRoot.localScale = new Vector3(scale, scale, 1f);
            PanelRoot.SetAsLastSibling();
            if (Hud != null) Hud.Rect.SetAsLastSibling();
            _dialog.SetAsLastSibling();
            _fade.SetAsLastSibling();
        }

        public void ClosePanel()
        {
            _dim.gameObject.SetActive(false);
            PanelRoot.gameObject.SetActive(false);
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
}
