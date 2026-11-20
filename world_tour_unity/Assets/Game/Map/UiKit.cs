using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Map
{
    /// <summary>
    /// Small helpers the map and venue card use to build their uGUI hierarchy in code. Everything is placed from the
    /// top-left of its parent (anchor and pivot as given), with sizes in canvas units (CSS px at 1366 wide).
    /// </summary>
    public static class UiKit
    {
        public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Places <paramref name="rt"/> with its <paramref name="pivot"/> at <paramref name="pos"/> from the parent's top-left (y down positive).</summary>
        public static RectTransform Place(RectTransform rt, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            rt.anchorMin = rt.anchorMax = TopLeft;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(pos.x, -pos.y);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, FontRole font, float size, ColorRole color,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft, bool wrap = true)
        {
            RectTransform rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            Theme.ApplyFont(t, font);
            t.fontSize = size;
            t.color = Theme.Color(color);
            t.alignment = align;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.richText = true;
            t.raycastTarget = false;
            t.SetLower(text);
            return t;
        }

        /// <summary>The height a text needs at a width (TMP preferred size).</summary>
        public static float HeightFor(TMP_Text t, float width) { return t.GetPreferredValues(t.text, width, 0f).y; }

        public static float WidthOf(TMP_Text t) { return t.GetPreferredValues(t.text, 10000f, 0f).x; }

        public static Image Img(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            RectTransform rt = Rect(name, parent);
            var i = rt.gameObject.AddComponent<Image>();
            i.sprite = sprite;
            i.color = color;
            i.raycastTarget = raycast;
            i.preserveAspect = false;
            return i;
        }

        /// <summary>A rounded box in a palette colour (the theme's rounded rect, sliced; a plain rect before 2.1's art).</summary>
        public static Image Box(Transform parent, string name, ColorRole color, float alpha = 1f)
        {
            Sprite rr = Theme.Sprite(SpriteRole.RoundedRect);
            Color c = Theme.Color(color); c.a *= alpha;
            Image i = Img(parent, name, rr, c);
            if (rr != null) i.type = Image.Type.Sliced;
            return i;
        }

        /// <summary>
        /// A paper card (screens.css .statcard / .moderow): a hard ink offset shadow, an ink border and a card face.
        /// Returns the face; content goes inside it. <paramref name="rt"/> is the card's own rect (already placed).
        /// </summary>
        public static RectTransform Card(RectTransform rt, float shadow, ColorRole face = ColorRole.Card, ColorRole shadowColor = ColorRole.Ink)
        {
            Image sh = Box(rt, "Shadow", shadowColor);
            Stretch(sh.rectTransform);
            sh.rectTransform.anchoredPosition = new Vector2(shadow, -shadow);
            Image ring = Box(rt, "Ring", ColorRole.Ink);
            Stretch(ring.rectTransform);
            Image f = Box(rt, "Face", face);
            Stretch(f.rectTransform, Tokens.BorderWidth);
            return f.rectTransform;
        }

        /// <summary>A sprite placed by its web box: the element is <paramref name="boxWidth"/> wide with its top-left at <paramref name="at"/>.</summary>
        public static Image Sticker(Transform parent, string name, Sprite sprite, SpriteBox box, Vector2 at, float boxWidth)
        {
            Vector2 off, size;
            box.Fit(boxWidth, out off, out size);
            Image i = Img(parent, name, sprite, Color.white);
            Place(i.rectTransform, at + off, size, TopLeft);
            return i;
        }
    }
}
