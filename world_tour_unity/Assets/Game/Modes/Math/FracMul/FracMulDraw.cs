using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WorldTour.Core.Maths.Gen;
using WorldTour.Game.Map;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// Draws a <see cref="FmPicture"/> on the snack table's paper: the area diagram (the whole as a grid of rows x columns,
    /// the part lilac, the product ink blue) and the tape diagram (one bar per whole, cut into pieces). Plain rects and the
    /// theme colours, so both themes work; drawn from the top left of a fixed-size paper.
    /// </summary>
    public static class FracMulDraw
    {
        /// <summary>The paper's size: one picture never needs more.</summary>
        public const float PaperWidth = 520f, PaperHeight = 254f;

        public static void Draw(RectTransform paper, FmPicture pic)
        {
            for (int i = paper.childCount - 1; i >= 0; i--) { GameObject c = paper.GetChild(i).gameObject; c.SetActive(false); Object.Destroy(c); }
            // everything is drawn on a fixed-size sheet centred in the paper, whatever width the layout gives the paper
            RectTransform sheet = ShowUi.Rect("Picture", paper);
            sheet.anchorMin = sheet.anchorMax = sheet.pivot = new Vector2(.5f, .5f);
            sheet.sizeDelta = new Vector2(PaperWidth, PaperHeight);
            sheet.anchoredPosition = Vector2.zero;
            if (pic.Kind == "area") Area(sheet, pic);
            else if (pic.Kind == "tape") Tape(sheet, pic);
            if (pic.Caption.Length > 0) Caption(sheet, pic.Caption);
        }

        static Image Rect(RectTransform parent, string name, float x, float y, float w, float h, Color c)
        {
            Image img = UiKit.Img(parent, name, null, c);
            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
            return img;
        }

        static Color Ink { get { return Theme.Color(ColorRole.Ink); } }
        static Color Card { get { return Theme.Color(ColorRole.Card); } }
        static Color Light { get { return MathUi.Mix(ColorRole.Accent, .55f, ColorRole.Card); } }
        static Color Dark { get { return Theme.Color(ColorRole.Accent2); } }

        static void Label(RectTransform parent, string text, float x, float y, float w, float h, float size, TextAlignmentOptions align, ColorRole color = ColorRole.Ink)
        {
            RectTransform box = ShowUi.Rect("Label", parent);
            box.anchorMin = box.anchorMax = new Vector2(0, 1);
            box.pivot = new Vector2(0, 1);
            box.sizeDelta = new Vector2(w, h);
            box.anchoredPosition = new Vector2(x, -y);
            TMP_Text t = ShowUi.Label(box, "T", "", size, FontRole.BodyBold, color, align);
            t.richText = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.text = text;
        }

        static void Caption(RectTransform paper, string text)
        {
            RectTransform row = ShowUi.Rect("Caption", paper);
            row.anchorMin = new Vector2(0, 0); row.anchorMax = new Vector2(1, 0);
            row.pivot = new Vector2(.5f, 0);
            row.sizeDelta = new Vector2(0, 40);
            row.anchoredPosition = new Vector2(0, 12);   // clear of the torn bottom edge
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter; h.spacing = 6;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            foreach (MathSeg s in MathText.Parse(text))
            {
                if (s.IsFrac) MathUi.Frac(row, s.Whole, s.Num, s.Den, 15f, ColorRole.Ink, true);
                else
                {
                    string piece = s.Text.Trim();
                    if (piece.Length == 0) continue;
                    TMP_Text t = SpellingUi.Text(row, "Words", piece, 17f, FontRole.BodyBold, ColorRole.Ink, false);
                    t.richText = false;
                    var le = t.gameObject.AddComponent<LayoutElement>();
                    le.preferredWidth = t.GetPreferredValues(t.text, 2000, 0).x + 2;
                    le.preferredHeight = 30;
                }
            }
        }

        // ---------- area diagram ----------

        static void Area(RectTransform paper, FmPicture a)
        {
            // as big as the paper allows: the grid's height leaves room for the columns label and the caption, its width for the rows label
            float cell = Mathf.Min(64f, 156f / a.Rows, 300f / a.Cols);
            float gw = a.Cols * cell, gh = a.Rows * cell;
            float x0 = Mathf.Round((PaperWidth - gw) / 2f - 20f), y0 = 12f;
            Rect(paper, "Ring", x0 - 2, y0 - 2, gw + 4, gh + 4, Ink);
            Rect(paper, "Whole", x0, y0, gw, gh, Card);
            for (int c = 0; c < a.Cols; c++)
                if (c < a.ColsOn) Rect(paper, "Part", x0 + c * cell, y0, cell, gh, Light);
            if (a.Overlap) Rect(paper, "Product", x0, y0, a.OverlapCols * cell, a.RowsOn * cell, Dark);
            for (int c = 1; c < a.Cols; c++) Rect(paper, "Column line", x0 + c * cell - 1, y0, 2, gh, Ink);
            for (int r = 1; r < a.Rows; r++)
            {
                float w = a.CutAll ? gw : a.CutShadedOnly ? a.ColsOn * cell : 0f;
                if (w > 0) Rect(paper, "Row line", x0, y0 + r * cell - 1, w, 2, Ink);
            }
            if (a.RowsLabel.Length > 0) Label(paper, a.RowsLabel, x0 + gw + 12, y0 + gh / 2 - 12, 150, 24, 16f, TextAlignmentOptions.MidlineLeft);
            if (a.ColsLabel.Length > 0) Label(paper, a.ColsLabel, x0, y0 + gh + 6, gw, 24, 16f, TextAlignmentOptions.Midline);
        }

        // ---------- tape diagram ----------

        static void Tape(RectTransform paper, FmPicture t)
        {
            const float w = 480f, gap = 14f;
            float x0 = Mathf.Round((PaperWidth - w) / 2f), y = 14f;
            // bars as tall as the paper allows for this many wholes (4 trays still fit above the caption)
            float h = Mathf.Min(60f, 168f / t.Wholes - gap);
            // world-hosted only: one or two bars grow to fill the sheet, so the paper has no empty white under the bar (the caption still fits below)
            if (ShowLayout.Compact) h = Mathf.Max(h, Mathf.Min(150f, (t.Caption.Length > 0 ? 150f : 196f) / t.Wholes - gap));
            if (t.Total.Length > 0)
            {
                Label(paper, t.Total + " in all", x0, y - 4, w, 24, 17f, TextAlignmentOptions.Midline);
                y += 26f;
            }
            int number = 0;
            for (int b = 0; b < t.Wholes; b++)
            {
                bool cut = b < t.WholesCut;
                int parts = cut ? t.Parts : 1;
                Rect(paper, "Ring", x0 - 2, y - 2, w + 4, h + 4, Ink);
                Rect(paper, "Bar", x0, y, w, h, Card);
                // world-hosted only: the uncut "n in all" bar shows its n single cells, so it is not an empty box (never the answer: the cells are the whole, not the groups)
                if (!cut && ShowLayout.Compact && t.Total.Length > 0 && int.TryParse(t.Total, out int units) && units >= 2 && units <= 48)
                    for (int u = 1; u < units; u += 2) Rect(paper, "Unit cell", x0 + u * (w / units), y, w / units, h, MathUi.Mix(ColorRole.Ink, .14f, ColorRole.Card));   // every other cell greyed: thin lines vanish when the panel is scaled down
                float pw = w / parts;
                for (int k = 0; k < parts; k++)
                {
                    float px = x0 + k * pw;
                    bool first = b == 0 && k == 0;
                    if (first && t.MarkFirst) Rect(paper, "Piece", px, y, pw, h, Light);
                    bool splitHere = t.Split > 0 && (t.SplitAll || first);
                    if (splitHere)
                    {
                        float sw = pw / t.Split;
                        if (first && t.Shade) Rect(paper, "Share", px, y, sw, h, Dark);
                        for (int s = 1; s < t.Split; s++) Rect(paper, "Share line", px + s * sw - 1, y, 2, h, Ink);
                    }
                    else if (first && t.Shade) Rect(paper, "Piece", px, y, pw, h, Dark);
                    if (k > 0) Rect(paper, "Piece line", px - 1, y - 2, 3, h + 4, Ink);
                    if (t.Numbered && cut)
                    {
                        number++;
                        Label(paper, number.ToString(), px, y + 3, pw, h - 6, h >= 40f ? 18f : 14f, TextAlignmentOptions.Midline);
                    }
                }
                y += h + gap;
            }
        }
    }
}
