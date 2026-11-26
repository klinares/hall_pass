using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WorldTour.Game.Audio;
using WorldTour.Game.Prefs;

namespace WorldTour.Game.Ui
{
    /// <summary>
    /// Phase 10A: what the sound button opens. A small card with two sliders, "music + ambience" (the birds and the guitar) and "effects" (footsteps, taps, applause), each from off
    /// to full in ten steps. They are saved with her progress (<see cref="GameSettings.Ambience"/>, <see cref="GameSettings.Effects"/>); music + ambience starts at 40 percent.
    /// The button toggles the card; a press anywhere else, or Escape, closes it. Built entirely in code by <see cref="Create"/>. Logs "WT_SOUND_SLIDER ambience=40 effects=100".
    /// </summary>
    public sealed class SoundPanel : MonoBehaviour
    {
        public const string SliderLog = "WT_SOUND_SLIDER";
        public const string AmbienceLabel = "music + ambience", EffectsLabel = "effects";
        const float Width = 280f, Pad = 14f, RowH = 64f, Gap = 4f, TrackH = 10f, Knob = 26f;

        RectTransform _rt, _button;
        Slider _ambience, _effects;
        TMP_Text _ambienceValue, _effectsValue;
        bool _painting;

        public bool IsOpen { get { return gameObject.activeSelf; } }
        public Slider AmbienceSlider { get { return _ambience; } }
        public Slider EffectsSlider { get { return _effects; } }

        public static SoundPanel Create(Transform parent, RectTransform button)
        {
            var go = new GameObject("SoundPanel", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var p = go.AddComponent<SoundPanel>();
            p._rt = rt;
            p._button = button;
            p.Build();
            go.SetActive(false);
            return p;
        }

        /// <summary>Under the button when it is in the top-right corner, above it when it is bottom-left (the show).</summary>
        public void Place(bool buttonAtBottomLeft)
        {
            if (_rt == null) return;
            float h = 2 * Pad + 2 * RowH + Gap;
            _rt.sizeDelta = new Vector2(Width, h);
            if (buttonAtBottomLeft)
            {
                _rt.anchorMin = _rt.anchorMax = _rt.pivot = Vector2.zero;
                _rt.anchoredPosition = new Vector2(Tokens.GutterMin, Tokens.GutterMin + Tokens.Tap + 10f);
            }
            else
            {
                _rt.anchorMin = _rt.anchorMax = _rt.pivot = Vector2.one;
                _rt.anchoredPosition = new Vector2(-Tokens.GutterMin, -(Tokens.GutterMin + Tokens.Tap + 10f));
            }
        }

        public void Toggle()
        {
            gameObject.SetActive(!gameObject.activeSelf);
            if (gameObject.activeSelf) { transform.SetAsLastSibling(); Refresh(); }
            Debug.Log(Hud.ToggleLog + " " + (gameObject.activeSelf ? "open" : "closed"));
        }

        public void Close() { if (gameObject.activeSelf) gameObject.SetActive(false); }

        void Build()
        {
            var shadow = Child(_rt, "Shadow");
            Stretch(shadow, 0f);
            shadow.offsetMin = new Vector2(3f, -3f); shadow.offsetMax = new Vector2(3f, -3f);
            Tint(shadow.gameObject, ColorRole.Ink, false);
            var border = Child(_rt, "Border");
            Stretch(border, 0f);
            Tint(border.gameObject, ColorRole.Ink, true);        // the card behind the sliders also takes the clicks, so none reach the world
            var face = Child(_rt, "Face");
            Stretch(face, 2f);
            Tint(face.gameObject, ColorRole.Card, false);

            _ambience = Row("Ambience", AmbienceLabel, 0, out _ambienceValue);
            _effects = Row("Effects", EffectsLabel, 1, out _effectsValue);
            _ambience.onValueChanged.AddListener(v => Changed(true, v));
            _effects.onValueChanged.AddListener(v => Changed(false, v));
            Place(false);
        }

        Slider Row(string name, string label, int index, out TMP_Text value)
        {
            RectTransform row = Child(_rt, name);
            row.anchorMin = new Vector2(0f, 1f); row.anchorMax = new Vector2(1f, 1f); row.pivot = new Vector2(.5f, 1f);
            row.offsetMin = new Vector2(Pad, 0f); row.offsetMax = new Vector2(-Pad, 0f);
            row.sizeDelta = new Vector2(-2 * Pad, RowH);
            row.anchoredPosition = new Vector2(0f, -(Pad + index * (RowH + Gap)));

            TMP_Text text = Text(row, "Label", label, 16f, TextAlignmentOptions.TopLeft);
            RectTransform lt = text.rectTransform;
            lt.anchorMin = new Vector2(0f, 1f); lt.anchorMax = new Vector2(1f, 1f); lt.pivot = new Vector2(0f, 1f);
            lt.sizeDelta = new Vector2(0f, 22f); lt.anchoredPosition = Vector2.zero;
            value = Text(row, "Value", "", 16f, TextAlignmentOptions.TopRight);
            RectTransform vt = value.rectTransform;
            vt.anchorMin = new Vector2(0f, 1f); vt.anchorMax = new Vector2(1f, 1f); vt.pivot = new Vector2(1f, 1f);
            vt.sizeDelta = new Vector2(0f, 22f); vt.anchoredPosition = Vector2.zero;

            RectTransform s = Child(row, "Slider");
            s.anchorMin = new Vector2(0f, 0f); s.anchorMax = new Vector2(1f, 0f); s.pivot = new Vector2(.5f, 0f);
            s.sizeDelta = new Vector2(0f, 30f); s.anchoredPosition = Vector2.zero;
            var hit = s.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);               // a clear background so the whole bar takes a click

            RectTransform outline = Child(s, "Outline");
            outline.anchorMin = new Vector2(0f, .5f); outline.anchorMax = new Vector2(1f, .5f); outline.pivot = new Vector2(.5f, .5f);
            outline.sizeDelta = new Vector2(-Knob + 6f, TrackH + 6f); outline.anchoredPosition = Vector2.zero;
            Tint(outline.gameObject, ColorRole.Ink, false);

            RectTransform track = Child(s, "Track");
            track.anchorMin = new Vector2(0f, .5f); track.anchorMax = new Vector2(1f, .5f); track.pivot = new Vector2(.5f, .5f);
            track.sizeDelta = new Vector2(-Knob, TrackH); track.anchoredPosition = Vector2.zero;
            Tint(track.gameObject, ColorRole.PaperDeep, false);

            RectTransform fillArea = Child(s, "Fill Area");
            fillArea.anchorMin = new Vector2(0f, .5f); fillArea.anchorMax = new Vector2(1f, .5f); fillArea.pivot = new Vector2(.5f, .5f);
            fillArea.sizeDelta = new Vector2(-Knob, TrackH); fillArea.anchoredPosition = Vector2.zero;
            RectTransform fill = Child(fillArea, "Fill");
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.pivot = new Vector2(0f, .5f);
            fill.sizeDelta = new Vector2(0f, 0f); fill.anchoredPosition = Vector2.zero;
            Tint(fill.gameObject, ColorRole.Accent2, false);

            RectTransform slide = Child(s, "Handle Slide Area");
            Stretch(slide, 0f);
            slide.offsetMin = new Vector2(Knob / 2f, 0f); slide.offsetMax = new Vector2(-Knob / 2f, 0f);
            RectTransform handle = Child(slide, "Handle");
            handle.anchorMin = new Vector2(0f, .5f); handle.anchorMax = new Vector2(0f, .5f); handle.pivot = new Vector2(.5f, .5f);
            handle.sizeDelta = new Vector2(Knob, Knob); handle.anchoredPosition = Vector2.zero;
            var ring = handle.gameObject.AddComponent<Image>();
            ring.sprite = UiShapes.Circle; ring.raycastTarget = true;
            ThemeColor.Add(handle.gameObject, ColorRole.Ink);
            RectTransform knobFace = Child(handle, "Face");
            Stretch(knobFace, 3f);
            var kf = knobFace.gameObject.AddComponent<Image>();
            kf.sprite = UiShapes.Circle; kf.raycastTarget = false;
            ThemeColor.Add(knobFace.gameObject, ColorRole.Card);

            var slider = s.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = ring;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 10f; slider.wholeNumbers = true;
            slider.transition = Selectable.Transition.None;
            return slider;
        }

        TMP_Text Text(RectTransform parent, string name, string s, float size, TextAlignmentOptions align)
        {
            RectTransform rt = Child(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            Theme.ApplyFont(t, FontRole.BodyBold);
            t.fontSize = size;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            t.SetLower(s);
            ThemeColor.Add(rt.gameObject, ColorRole.Ink);
            return t;
        }

        RectTransform Child(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        }

        static void Tint(GameObject go, ColorRole role, bool raycast)
        {
            var img = go.AddComponent<Image>();
            img.raycastTarget = raycast;
            ThemeColor.Add(go, role);
        }

        /// <summary>Shows what is saved: the sliders and their words.</summary>
        public void Refresh()
        {
            if (_ambience == null) return;
            _painting = true;
            _ambience.SetValueWithoutNotify(Mathf.Round(GameSettings.Ambience * 10f));
            _effects.SetValueWithoutNotify(Mathf.Round(GameSettings.Effects * 10f));
            _painting = false;
            _ambienceValue.SetLower(Words(_ambience.value));
            _effectsValue.SetLower(Words(_effects.value));
        }

        static string Words(float step) { return step <= 0f ? "off" : (step * 10f).ToString("0") + "%"; }

        void Changed(bool ambience, float step)
        {
            if (_painting) return;
            if (ambience) GameSettings.Ambience = step / 10f; else GameSettings.Effects = step / 10f;
            Debug.Log(SliderLog + " ambience=" + Mathf.RoundToInt(GameSettings.Ambience * 100f) + " effects=" + Mathf.RoundToInt(GameSettings.Effects * 100f));
            if (!ambience) Sfx.Play(SfxId.Tap);      // she hears the new effects level
        }

        void Update()
        {
            Keyboard k = Keyboard.current;
            if (k != null && k.escapeKey.wasPressedThisFrame) { Close(); return; }
            Mouse m = Mouse.current;
            if (m == null || !m.leftButton.wasPressedThisFrame) return;
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 p = m.position.ReadValue();
            if (RectTransformUtility.RectangleContainsScreenPoint(_rt, p, cam) || RectTransformUtility.RectangleContainsScreenPoint(_button, p, cam)) return;
            Close();
        }
    }
}
