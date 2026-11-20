using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WorldTour.Game.Prefs;
using WorldTour.Game.Save;

namespace WorldTour.Game.Ui
{
    /// <summary>
    /// What the web game keeps in its header and footer on every screen: the remembered sound toggle (main.js:33-44,
    /// components.css .iconbtn, icons i-sound / i-mute) and the "progress won't save on this device" notice
    /// (index.html footer .nosave, shown when storage is blocked or a write fails: store.js wt:nosave).
    /// Unity has no header bar, so both sit in the bottom-left corner, which is free on the map, venue card, show and
    /// ticket at 1366x768 and up (the show's top right holds its fan and streak chips). Built in code by MainRoot,
    /// above the screen stack. Logs "WT_SOUND_TOGGLE on|off".
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        public const string ToggleLog = "WT_SOUND_TOGGLE";
        /// <summary>index.html:31, word for word.</summary>
        public const string NoSaveText = "progress won't save on this device";

        RoundIconButton _sound;
        GameObject _noSave;
        SaveStore _store;

        public RectTransform Rect { get { return (RectTransform)transform; } }
        public RoundIconButton SoundButton { get { return _sound; } }

        public static Hud Create(Transform canvas, SaveStore store)
        {
            var go = new GameObject("Hud", typeof(RectTransform));
            go.layer = canvas.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling();
            var hud = go.AddComponent<Hud>();
            hud.Build(store);
            return hud;
        }

        void Build(SaveStore store)
        {
            _store = store;
            _sound = RoundIconButton.Create(transform, "SoundToggle");
            var srt = (RectTransform)_sound.transform;
            srt.anchorMin = srt.anchorMax = srt.pivot = Vector2.zero;
            srt.anchoredPosition = new Vector2(Tokens.GutterMin, Tokens.GutterMin);
            _sound.Clicked += Toggle;

            _noSave = BuildNoSave();
            if (_store != null) _store.NoSave += ShowNoSave;
            _noSave.SetActive(_store == null || !_store.Persistent);

            GameSettings.Changed += Paint;
            Paint();
        }

        void OnDestroy()
        {
            GameSettings.Changed -= Paint;
            if (_store != null) _store.NoSave -= ShowNoSave;
        }

        void ShowNoSave() { if (_noSave != null) _noSave.SetActive(true); }

        /// <summary>main.js sb.onclick: flip settings.sound, save, apply; turning it on plays a tap (GameSettings does).</summary>
        void Toggle()
        {
            bool on = !GameSettings.Sound;
            GameSettings.Sound = on;
            Debug.Log(ToggleLog + " " + (on ? "on" : "off"));
            Paint();
        }

        /// <summary>main.js paintSound: the icon follows the setting.</summary>
        void Paint()
        {
            if (_sound == null) return;
            ThemeAsset t = Theme.Asset;
            bool on = GameSettings.Sound;
            Sprite s = t != null ? (on ? t.iconSound : t.iconMute) : null;
            _sound.Icon = s;
            _sound.name = "SoundToggle";
        }

        GameObject BuildNoSave()
        {
            // components.css .nosave: card, 2px ink border, padding 2px 8px; footer font-size var(--fs-0). index.html puts it
            // in the footer, which centres its items at the bottom of the page: one line, centred along the bottom edge.
            var go = new GameObject("NoSave", typeof(RectTransform));
            go.layer = gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, Tokens.GutterMin);

            var border = go.AddComponent<Image>();
            border.raycastTarget = false;
            ThemeColor.Add(go, ColorRole.Ink);

            var faceGo = new GameObject("Face", typeof(RectTransform));
            faceGo.layer = gameObject.layer;
            var frt = (RectTransform)faceGo.transform;
            frt.SetParent(rt, false);
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2f, 2f); frt.offsetMax = new Vector2(-2f, -2f);
            var face = faceGo.AddComponent<Image>();
            face.raycastTarget = false;
            ThemeColor.Add(faceGo, ColorRole.Card);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.layer = gameObject.layer;
            var trt = (RectTransform)textGo.transform;
            trt.SetParent(frt, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8f, 2f); trt.offsetMax = new Vector2(-8f, -2f);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            Theme.ApplyFont(text, FontRole.Body);
            text.fontSize = Tokens.Fs0;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.SetLower(NoSaveText);
            ThemeColor.Add(textGo, ColorRole.Ink);

            Vector2 pref = text.GetPreferredValues(text.text);
            rt.sizeDelta = new Vector2(Mathf.Ceil(pref.x) + 2 * (8f + 2f), Mathf.Max(26f, Mathf.Ceil(pref.y) + 2 * (2f + 2f)));
            return go;
        }
    }
}
