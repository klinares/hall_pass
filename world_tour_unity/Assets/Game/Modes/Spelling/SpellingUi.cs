using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WorldTour.Game.Motion;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// The pieces the spelling modes share (world_tour/modes/spelling/parts.js clue, desk, slotRow), drawn the way remix
    /// draws them (2.5 and its polish round): a column of clue card, ink desk and controls, built in code from the theme.
    /// </summary>
    public static class SpellingUi
    {
        /// <summary>screens.css:200 empty slot fill; see RemixMode.EmptySlotAlpha (linear blending).</summary>
        public const float EmptySlotAlpha = .01f;

        /// <summary>The song's column (remix's layout: centred, 18 apart).</summary>
        public static void Column(RectTransform area)
        {
            var col = area.gameObject.AddComponent<VerticalLayoutGroup>();
            col.childAlignment = TextAnchor.UpperCenter;
            col.spacing = 18;
            col.childControlWidth = true; col.childControlHeight = true;
            col.childForceExpandWidth = false; col.childForceExpandHeight = false;
        }

        /// <summary>parts.js:8-15 the torn clue card. The clue keeps its own case (.clue p, text-transform:none).</summary>
        public static RectTransform Clue(RectTransform area, string clue)
        {
            RectTransform card = Box("Clue", area, 640, -1);
            ShowUi.Panel(card, ColorRole.Card, 1f, SpriteRole.PaperPanel);
            var c = card.gameObject.AddComponent<VerticalLayoutGroup>();
            c.padding = new RectOffset(20, 20, 16, 18);
            c.spacing = 4;
            c.childControlWidth = true; c.childControlHeight = true; c.childForceExpandHeight = false;
            Text(card, "Label", RemixLines.ClueLabel, Tokens.Fs0, FontRole.BodyBold, ColorRole.Accent2, true);
            TMP_Text t = Text(card, "Text", "", 17.6f, FontRole.Body, ColorRole.Ink, false);
            t.text = "<noparse>" + (clue ?? "") + "</noparse>";
            return card;
        }

        /// <summary>parts.js:17-23 the stage desk with its tag; children stack inside it.</summary>
        public static RectTransform Desk(RectTransform area, string tag)
        {
            RectTransform desk = Box("Desk", area, 680, -1);
            ShowUi.Panel(desk, ColorRole.Ink);
            var d = desk.gameObject.AddComponent<VerticalLayoutGroup>();
            d.padding = new RectOffset(14, 14, 16, 22);
            d.spacing = 14;
            d.childAlignment = TextAnchor.UpperCenter;
            d.childControlWidth = true; d.childControlHeight = true; d.childForceExpandWidth = false; d.childForceExpandHeight = false;
            Text(desk, "Tag", tag, Tokens.Fs2, FontRole.Display, ColorRole.OnInk, true);
            return desk;
        }

        /// <summary>
        /// parts.js:26-29 slotRow: the word with some letters hidden. A hidden letter is an empty dashed slot showing the
        /// blank (.slot.gap); a shown one is a card slot (.slot.fill); a letter given by a hint or the reveal is accent
        /// (.slot.given).
        /// </summary>
        public sealed class SlotRow
        {
            private readonly string _word;
            private readonly HashSet<int> _hide;
            private readonly HashSet<int> _given = new HashSet<int>();
            private readonly List<Image> _bg = new List<Image>();
            private readonly List<TMP_Text> _text = new List<TMP_Text>();
            private readonly List<WorldTour.Game.Map.UiLine> _dash = new List<WorldTour.Game.Map.UiLine>();
            private readonly float _letterSize;

            public readonly RectTransform Rect;

            public SlotRow(RectTransform desk, string word, IEnumerable<int> hide, char blank)
            {
                _word = word;
                _hide = new HashSet<int>(hide);
                int n = word.Length;
                float slotW = Mathf.Min(46f, (640f - (n - 1) * 5f) / n);
                _letterSize = Mathf.Min(28f, slotW * .66f);
                Rect = Row(desk, "Slots", 5, slotW * 1.25f);
                for (int i = 0; i < n; i++)
                {
                    RectTransform s = Box("Slot " + i, Rect, slotW, slotW * 1.25f);
                    bool gap = _hide.Contains(i);
                    _bg.Add(ShowUi.Panel(s, ColorRole.Card, gap ? EmptySlotAlpha : 1f));
                    _dash.Add(gap ? DashedBorder(s, slotW, slotW * 1.25f) : null);
                    // screens.css:233 a gap's blank is on-ink at half the slot (min 1.4rem)
                    TMP_Text t = ShowUi.Label(s, "Letter", "", gap ? Mathf.Min(22.4f, slotW * .5f) : _letterSize, FontRole.BodyBold,
                        gap ? ColorRole.OnInk : ColorRole.Ink);
                    t.text = gap ? blank.ToString() : word[i].ToString();
                    _text.Add(t);
                }
            }

            public bool IsGiven(int i) { return _given.Contains(i); }

            /// <summary>parts.js:79 a blank filled by a hint: the letter, accent, and a slap.</summary>
            public void Give(int i)
            {
                if (i < 0 || i >= _word.Length || !_hide.Contains(i) || _given.Contains(i)) return;
                Paint(i);
                Feedback.Slap((RectTransform)_bg[i].transform);
            }

            /// <summary>slots.js:122-124 fillAll: every blank still open gets its letter (.given), no motion.</summary>
            public void FillAll()
            {
                foreach (int i in _hide) if (!_given.Contains(i)) Paint(i);
            }

            private void Paint(int i)
            {
                _given.Add(i);
                _bg[i].color = Theme.Color(ColorRole.Accent);
                if (_dash[i] != null) _dash[i].enabled = false;
                _text[i].fontSize = _letterSize;
                _text[i].color = Theme.Color(ColorRole.Ink);
                _text[i].text = _word[i].ToString();
            }
        }

        /// <summary>screens.css:139 the controls row.</summary>
        public static RectTransform Controls(RectTransform area)
        {
            return Row(area, "Controls", 12, Tokens.Tap);
        }

        /// <summary>A control button's size in the row (remix's).</summary>
        public static ShowButton Control(ShowButton b, float width = 150)
        {
            if (b == null) return null;
            // not ??: in the Editor a missing component is a fake-null object that ?? treats as present
            var le = b.Go.GetComponent<LayoutElement>();
            if (le == null) le = b.Go.AddComponent<LayoutElement>();
            le.preferredHeight = Tokens.Tap;
            le.preferredWidth = width;
            return b;
        }

        public static RectTransform Box(string name, RectTransform parent, float width, float height)
        {
            RectTransform rt = ShowUi.Rect(name, parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            if (width > 0) le.preferredWidth = width;
            if (height > 0) le.preferredHeight = height;
            return rt;
        }

        public static RectTransform Row(RectTransform parent, string name, float gap, float height)
        {
            RectTransform rt = ShowUi.Rect(name, parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = gap; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            return rt;
        }

        public static TMP_Text Text(RectTransform parent, string name, string text, float size, FontRole font, ColorRole color, bool lower)
        {
            RectTransform rt = ShowUi.Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            Theme.ApplyFont(t, font);
            t.fontSize = size;
            t.color = Theme.Color(color);
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            if (lower) t.SetLower(text); else t.text = text;
            return t;
        }

        /// <summary>screens.css:200 the empty slot's dashed border (card at 80%), as RemixMode draws it.</summary>
        public static WorldTour.Game.Map.UiLine DashedBorder(RectTransform slot, float w, float h)
        {
            var go = new GameObject("Dash", typeof(RectTransform));
            go.layer = slot.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(slot, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(.5f, .5f);
            var line = go.AddComponent<WorldTour.Game.Map.UiLine>();
            line.raycastTarget = false;
            const float bw = 2.5f;
            float x = w / 2f - bw / 2f, y = h / 2f - bw / 2f;
            line.Width = bw;
            line.SetDash(5f, 4f);
            line.SetPoints(new[] { new Vector2(-x, y), new Vector2(x, y), new Vector2(x, -y), new Vector2(-x, -y), new Vector2(-x, y) });
            Color c = Theme.Color(ColorRole.Card); c.a = .8f;
            line.color = c;
            return line;
        }
    }
}
