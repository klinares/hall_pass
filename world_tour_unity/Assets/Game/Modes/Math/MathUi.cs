using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WorldTour.Game.Map;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// The pieces the math modes share (world_tour/modes/math/parts.js promptCard, fracHTML; screens.css .desk, .clue;
    /// math.css .frac), built in code from the theme as the spelling modes are. A math song is laid out in two columns,
    /// because the web's single column (desk, prompt, keypad, controls) is taller than a 1366x768 window: the stage desk
    /// and the controls on the left, the prompt card and the keypad on the right.
    /// </summary>
    public static class MathUi
    {
        /// <summary>The right column's width (the keypad's: math.css .padwrap max-width 380, less its margins).</summary>
        public const float RightWidth = 350f;
        public const float ColumnGap = 26f;

        /// <summary>Two columns side by side, top aligned: left grows, right is fixed.</summary>
        public static void Columns(RectTransform area, out RectTransform left, out RectTransform right)
        {
            var row = area.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = ColumnGap;
            row.childAlignment = TextAnchor.UpperCenter;
            row.childControlWidth = true; row.childControlHeight = true;
            row.childForceExpandWidth = false; row.childForceExpandHeight = false;
            left = Column(area, "Left", 640f, 1f);
            right = Column(area, "Right", RightWidth, 0f);
        }

        private static RectTransform Column(RectTransform parent, string name, float width, float flex)
        {
            RectTransform rt = ShowUi.Rect(name, parent);
            TopPivot(rt);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.flexibleWidth = flex; le.minWidth = flex > 0 ? 480f : width;
            var col = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            col.spacing = 16;
            col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = true; col.childControlHeight = true;
            col.childForceExpandWidth = true; col.childForceExpandHeight = false;
            return rt;
        }

        /// <summary>
        /// Pivot at the top centre, so a layout group keeps the rect's position when its height changes (a receipt or a
        /// row appears). A wobble started just before such a change then comes back to the right place: a motion keeps
        /// the pose it started from as its rest (DECISIONS 2.2), and with a centre pivot that pose would be stale.
        /// </summary>
        public static void TopPivot(RectTransform rt) { rt.pivot = new Vector2(.5f, 1f); }

        /// <summary>parts.js:14-22 promptCard: the torn clue card with its label chip (.cluelbl) and the problem (.mathq p).</summary>
        public static RectTransform PromptCard(RectTransform parent, string text, string label)
        {
            RectTransform card = ShowUi.Rect("Prompt", parent);
            TopPivot(card);
            ShowUi.Panel(card, ColorRole.Card, 1f, SpriteRole.PaperPanel);
            var c = card.gameObject.AddComponent<VerticalLayoutGroup>();
            c.padding = new RectOffset(20, 20, 18, 30);                        // 30 at the bottom: a 4-line prompt clears the torn edge (P10)
            c.spacing = 8;
            c.childAlignment = TextAnchor.UpperLeft;
            c.childControlWidth = true; c.childControlHeight = true; c.childForceExpandWidth = true; c.childForceExpandHeight = false;

            // .cluelbl: hand font 1.25rem on ink, padding 3px 10px 4px, rotate(-3deg)
            RectTransform chipRow = ShowUi.Rect("LabelRow", card);
            var chipLe = chipRow.gameObject.AddComponent<LayoutElement>();
            chipLe.preferredHeight = 30;
            RectTransform chip = ShowUi.Rect("Label", chipRow);
            chip.anchorMin = chip.anchorMax = new Vector2(0, .5f);
            chip.pivot = new Vector2(0, .5f);
            ShowUi.Panel(chip, ColorRole.Ink);
            TMP_Text lt = ShowUi.Label(chip, "Text", label, Tokens.Fs2, FontRole.Display, ColorRole.OnInk);
            float w = lt.GetPreferredValues(lt.text, 1000, 0).x + 20;
            chip.sizeDelta = new Vector2(w, 28);
            chip.anchoredPosition = new Vector2(0, 0);
            chip.localEulerAngles = new Vector3(0, 0, 3f);                     // CSS rotate(-3deg) is clockwise-negative

            // .mathq p: 1.15rem, line-height 1.6; the problem keeps its own case (.clue p text-transform none)
            RectTransform pr = ShowUi.Rect("Text", card);
            var t = pr.gameObject.AddComponent<TextMeshProUGUI>();
            Theme.ApplyFont(t, FontRole.Body);
            t.fontSize = 18.4f;
            t.color = Theme.Color(ColorRole.Ink);
            t.alignment = TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.lineSpacing = 18f;
            t.raycastTarget = false;
            t.richText = true;
            t.text = MathText.ToTmp(text, MathText.PromptScale);
            pr.gameObject.AddComponent<PromptFit>();
            return card;
        }

        /// <summary>
        /// polish 6.2F item 1: stacked fractions make some lines taller than TMP's preferred height allows for, so the card's
        /// last line clipped. This asks the layout for the height the text really rendered at.
        /// </summary>
        sealed class PromptFit : MonoBehaviour
        {
            TextMeshProUGUI _t; LayoutElement _le;
            void Awake() { _t = GetComponent<TextMeshProUGUI>(); _le = gameObject.AddComponent<LayoutElement>(); }
            void LateUpdate()
            {
                if (_t == null || _t.textInfo == null || _t.textInfo.lineCount == 0) return;
                float h = Mathf.Ceil(_t.textBounds.size.y) + 4f;
                if (Mathf.Abs(h - _le.minHeight) < 1f) return;
                _le.minHeight = h;
                LayoutRebuilder.MarkLayoutForRebuild(transform.parent as RectTransform);
            }
        }

        /// <summary>
        /// screens.css .desk: the ink stage desk with its tag (.desk-tag, hand font on ink), children stacked inside. The tag
        /// may carry a fraction ("the beat * [[1 1/2]] measures"), drawn stacked beside the words.
        /// </summary>
        public static RectTransform Desk(RectTransform parent, string tag)
        {
            RectTransform desk = ShowUi.Rect("Desk", parent);
            TopPivot(desk);
            ShowUi.Panel(desk, ColorRole.Ink);
            var d = desk.gameObject.AddComponent<VerticalLayoutGroup>();
            d.padding = new RectOffset(16, 16, 14, 20);
            d.spacing = 14;
            d.childAlignment = TextAnchor.UpperCenter;
            d.childControlWidth = true; d.childControlHeight = true; d.childForceExpandWidth = true; d.childForceExpandHeight = false;
            RichRow(desk, "Tag", tag, Tokens.Fs2 + 2f, FontRole.Display, ColorRole.OnInk, 30f);
            return desk;
        }

        /// <summary>A one-line row of words and stacked fractions, centred (the desk tags).</summary>
        public static RectTransform RichRow(RectTransform parent, string name, string text, float size, FontRole font, ColorRole color, float height)
        {
            RectTransform row = SpellingUi.Row(parent, name, 6, height);
            foreach (MathSeg s in MathText.Parse(text))
            {
                if (s.IsFrac) Frac(row, s.Whole, s.Num, s.Den, size * .8f, color, true);
                else
                {
                    string piece = s.Text.Trim();
                    if (piece.Length == 0) continue;
                    TMP_Text t = SpellingUi.Text(row, "Words", piece, size, font, color, true);
                    var le = t.gameObject.AddComponent<LayoutElement>();
                    le.preferredWidth = t.GetPreferredValues(t.text, 2000, 0).x + 2;
                    le.preferredHeight = height;
                }
            }
            return row;
        }

        /// <summary>
        /// A stand-alone stacked fraction (parts.js fracHTML; math.css .mixed, .frac): numerator, a bar, denominator, and the
        /// whole number before it at full size, bold. An empty numerator or denominator is a dashed box (.frac .box), as
        /// the readout shows while she types. <paramref name="size"/> is the fraction digits' font size.
        /// </summary>
        public static RectTransform Frac(RectTransform parent, string whole, string num, string den, float size, ColorRole color, bool bold)
        {
            num = num ?? ""; den = den ?? "";
            int k = Mathf.Max(1, Mathf.Max(num.Length, den.Length));
            float colW = Mathf.Max(k * .6f * size, num.Length == 0 || den.Length == 0 ? .72f * size : 0f) + .16f * size;
            float digitH = .66f * size, gap = .12f * size, bar = Mathf.Max(2f, .085f * size);
            float h = 2 * digitH + 2 * gap + bar;
            float wholeW = 0, wholeSize = size / .82f;
            if (!string.IsNullOrEmpty(whole)) wholeW = whole.Length * .6f * wholeSize + .1f * size;

            RectTransform root = ShowUi.Rect("Frac", parent);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = wholeW + colW; le.preferredHeight = h;
            le.minWidth = wholeW + colW;
            root.sizeDelta = new Vector2(wholeW + colW, h);
            FontRole f = bold ? FontRole.BodyBold : FontRole.Body;

            if (wholeW > 0)
            {
                TMP_Text wt = Digits(root, "Whole", whole, wholeSize, FontRole.BodyBold, color);
                Box(wt.rectTransform, 0, 0, wholeW - .1f * size, h);
            }
            float x = wholeW;
            Part(root, "Num", num, x, 0, colW, digitH, size, f, color);
            RectTransform barRt = ShowUi.Rect("Bar", root);
            Box(barRt, x, digitH + gap, colW, bar);
            var bi = barRt.gameObject.AddComponent<Image>();
            bi.color = Theme.Color(color);
            bi.raycastTarget = false;
            Part(root, "Den", den, x, digitH + gap + bar + gap, colW, digitH, size, f, color);
            return root;
        }

        private static void Part(RectTransform root, string name, string digits, float x, float y, float w, float h, float size, FontRole f, ColorRole color)
        {
            if (digits.Length > 0)
            {
                TMP_Text t = Digits(root, name, digits, size, f, color);
                Box(t.rectTransform, x, y, w, h);
                return;
            }
            // .frac .box: .7em x .9em, 2px dashed currentColor, radius 3, opacity .55
            RectTransform b = ShowUi.Rect(name + "Box", root);
            float bw = .7f * size, bh = Mathf.Min(h, .9f * size);
            Box(b, x + (w - bw) / 2, y + (h - bh) / 2, bw, bh);
            var line = b.gameObject.AddComponent<UiLine>();
            line.raycastTarget = false;
            line.Width = 2f;
            line.SetDash(3f, 3f);
            float hx = bw / 2 - 1, hy = bh / 2 - 1;
            line.SetPoints(new[] { new Vector2(-hx, hy), new Vector2(hx, hy), new Vector2(hx, -hy), new Vector2(-hx, -hy), new Vector2(-hx, hy) });
            Color c = Theme.Color(color); c.a = .55f;
            line.color = c;
        }

        private static TMP_Text Digits(RectTransform root, string name, string text, float size, FontRole f, ColorRole color)
        {
            RectTransform rt = ShowUi.Rect(name, root);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            Theme.ApplyFont(t, f);
            t.fontSize = size;
            t.color = Theme.Color(color);
            t.alignment = TextAlignmentOptions.CenterGeoAligned;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.richText = false;
            t.text = text;
            return t;
        }

        /// <summary>Places a child by its top-left (y down) in a parent whose pivot is its centre.</summary>
        private static void Box(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        /// <summary>A card with a hard shadow and an ink ring (components.css .card / math.css .mitem); returns the face.</summary>
        public static RectTransform Card(RectTransform rt, float shadow, ColorRole face, ColorRole shadowColor, float border = Tokens.BorderWidth)
        {
            Image sh = UiKit.Box(rt, "Shadow", shadowColor);
            UiKit.Stretch(sh.rectTransform);
            sh.rectTransform.anchoredPosition = new Vector2(shadow, -shadow);
            Image ring = UiKit.Box(rt, "Ring", ColorRole.Ink);
            UiKit.Stretch(ring.rectTransform);
            Image f = UiKit.Box(rt, "Face", face);
            UiKit.Stretch(f.rectTransform, border);
            return f.rectTransform;
        }

        /// <summary>An opaque mix of two theme colours (CSS color-mix(in srgb, a p%, b)); the project blends UI in linear space.</summary>
        public static Color Mix(ColorRole a, float pa, ColorRole b)
        {
            Color ca = Theme.Color(a), cb = Theme.Color(b);
            return new Color(ca.r * pa + cb.r * (1 - pa), ca.g * pa + cb.g * (1 - pa), ca.b * pa + cb.b * (1 - pa), 1f);
        }

        /// <summary>A rect's centre in page pixels from the top left, "x:y" (for mouse-only browser tests).</summary>
        public static string Centre(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            Vector3 m = (c[0] + c[2]) * .5f;
            return Mathf.RoundToInt(m.x) + ":" + Mathf.RoundToInt(UnityEngine.Screen.height - m.y);
        }

        /// <summary>The song's controls row (hint, then next song, hidden), as the spelling modes have it.</summary>
        public static ShowButton Controls(SongContext ctx, RectTransform parent, out RectTransform row)
        {
            row = SpellingUi.Controls(parent);
            ShowButton hint = SpellingUi.Control(ctx.HintButton(row));
            ShowButton next = SpellingUi.Control(ShowUi.Button(ctx.ButtonPrefab, row, "Next", MathLines.NextSong, false, () => ctx.Next(0f)), 190);
            if (next != null) next.Go.SetActive(false);
            if (hint != null) ctx.Later(() => { if (hint.Go != null) Debug.Log("WT_HINT_POS pos=" + Centre(hint.Rect)); }, .45f);
            return next;
        }

        /// <summary>parts.js:202-207 showNext: the next-song button appears (with a slap) and takes the keyboard. Logs "WT_NEXT pos=x:y".</summary>
        public static void ShowNext(SongContext ctx, ShowButton next)
        {
            if (next == null) { ctx.Next(3.2f); return; }                    // parts.js:205, no button: move on by itself
            next.Go.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(ctx.Area);
            WorldTour.Game.Motion.Feedback.Slap(next.Rect);
            if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(next.Go);
            ctx.Later(() => { if (next.Go != null) Debug.Log("WT_NEXT pos=" + Centre(next.Rect)); }, .45f);
        }

        /// <summary>A stripe texture for patterned fills (math.css repeating-linear-gradient): white diagonal bands, tiled.</summary>
        public static Sprite Stripes
        {
            get
            {
                if (s_Stripes != null) return s_Stripes;
                const int n = 16;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "wt_stripes", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int d = (x + y) % n;                                 // -45deg bands: 2 of every 16 px lit
                        byte a = (byte)(d < 3 ? 255 : 0);
                        px[y * n + x] = new Color32(255, 255, 255, a);
                    }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                s_Stripes = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
                s_Stripes.name = "wt_stripes";
                return s_Stripes;
            }
        }
        static Sprite s_Stripes;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_Stripes = null; }
    }
}
