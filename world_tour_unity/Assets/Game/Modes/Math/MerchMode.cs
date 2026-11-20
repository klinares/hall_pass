using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;
using WorldTour.Game.Map;
using WorldTour.Game.Motion;
using WorldTour.Game.Show;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// MODE: merch table (world_tour/modes/math/merch.js). Fans buy stuff; ring it up or count back the change, typed on
    /// the keypad (or the real keys). The worked hint is the receipt, lined up on the decimal point: the sum appears with
    /// the partial step (2) and its answer with the worked solution (3). Rules: <see cref="TypedMathRound"/>,
    /// <see cref="MerchRules"/>.
    /// Logs "WT_MATH mode=merch kind= level= seed= answer=", "WT_MATH_SUBMIT val= result= tries=", "WT_HINT mode=merch n=",
    /// "WT_KEYPAD pos=" and "WT_RECEIPT rows=".
    /// </summary>
    public sealed class MerchMode : MonoBehaviour, ISongMode
    {
        private SongContext _ctx;
        private RectTransform _desk, _work;
        private ShowButton _next;

        public Problem Problem { get; private set; }
        public TypedMathRound Round { get; private set; }
        public MathKeypad Pad { get; private set; }

        public void Render(SongContext ctx)
        {
            _ctx = ctx;
            Problem p = Problem = ctx.Problem;
            Build(ctx.Area, p);
            Round = new TypedMathRound(new MathSongHost(ctx, "merch", () => _desk), p, Pad, s => ctx.Next(s), OnStep);
            Pad.Submitted += Submit;
            Debug.Log("WT_MATH mode=merch kind=" + p.Kind + " level=" + p.Level + " seed=" + p.Seed + " answer=" + TypedMathRound.KeyedAnswer(p.Answer));
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);   // Enter goes to the pad
            string opening = MerchRules.Paid(p) > 0 ? MathLines.MerchChange : MathLines.MerchTotal;
            ctx.Later(() => ctx.Say(opening, "idle"), .01f);                    // after the show's own song line (3.2)
            ctx.Later(() => { if (Pad != null) Pad.LogPositions(); }, .45f);
        }

        private void Submit(string text)
        {
            TypedMathResult r = Round.Submit(text);
            Debug.Log("WT_MATH_SUBMIT val=" + text + " result=" + r.ToString().ToLowerInvariant() + " tries=" + _ctx.Tries);
        }

        /// <summary>merch.js:50-54 onStep: the receipt from step 2, its answer at step 3 (not for decimal x decimal).</summary>
        private void OnStep(int n)
        {
            if (Problem.Work == null || Problem.Work.IsNull || n < 2) return;   // decimal x decimal: work is null
            DrawReceipt(MerchRules.Receipt(Problem.Work, n >= 3));
        }

        // ---------- drawing ----------

        private void Build(RectTransform area, Problem p)
        {
            MathUi.Columns(area, out RectTransform left, out RectTransform right);
            _desk = MathUi.Desk(left, MathLines.MerchTag);

            // the table: an item sticker card per line, then the bill she was paid with
            RectTransform table = SpellingUi.Row(_desk, "Merch", 12, 156);
            List<MerchLine> lines = MerchRules.Lines(p);
            for (int i = 0; i < lines.Count; i++) Item(table, lines[i], i);
            long paid = MerchRules.Paid(p);
            if (paid > 0) Bill(table, paid);

            _work = SpellingUi.Box("Work", _desk, -1, -1);
            var wl = _work.gameObject.AddComponent<HorizontalLayoutGroup>();
            wl.childAlignment = TextAnchor.UpperCenter;
            wl.childControlWidth = true; wl.childControlHeight = true; wl.childForceExpandWidth = false; wl.childForceExpandHeight = false;
            _work.gameObject.SetActive(false);

            _next = MathUi.Controls(_ctx, left, out _);

            MathUi.PromptCard(right, p.Prompt, MathLines.OrderUp);
            Pad = MathKeypad.Make(right, p.Answer.Type, MathLines.RingItUp, ShowNext);
        }

        private void ShowNext() { MathUi.ShowNext(_ctx, _next); }

        /// <summary>math.css .mitem: sticker, name, price tag, on a tilted card with a lilac shadow.</summary>
        private void Item(RectTransform table, MerchLine l, int i)
        {
            // a long tag (2.5 ounces x $1.20) widens its card instead of running past it (.ptag is nowrap on the web)
            string tag = MerchRules.Tag(l);
            float tw = tag.Length * .6f * 15.2f + 22f;
            RectTransform rt = SpellingUi.Box("Item " + i, table, Mathf.Max(112f, tw + 14f), 150);
            RectTransform body = ShowUi.Rect("Body", rt);
            ShowUi.Stretch(body);
            body.localEulerAngles = new Vector3(0, 0, -MerchRules.ItemTilts[i % 3]);
            RectTransform face = MathUi.Card(body, 3f, ColorRole.Card, ColorRole.Accent, 2.5f);

            MapArt art = _ctx.Art;
            string sn = l.Icon + "_sm";
            Sprite sp = art != null ? art.Get(sn) : null;
            if (sp != null)
            {
                RectTransform holder = ShowUi.Rect("Sticker", face);
                holder.anchorMin = holder.anchorMax = new Vector2(.5f, 1);
                holder.pivot = new Vector2(.5f, 1);
                holder.sizeDelta = new Vector2(62, 62);
                holder.anchoredPosition = new Vector2(0, -6);
                UiKit.Sticker(holder, "Art", sp, art.Box(sn), Vector2.zero, 62f);
            }
            RectTransform nameBox = ShowUi.Rect("Name", face);
            nameBox.anchorMin = new Vector2(0, 1); nameBox.anchorMax = new Vector2(1, 1); nameBox.pivot = new Vector2(.5f, 1);
            nameBox.sizeDelta = new Vector2(-8, 32); nameBox.anchoredPosition = new Vector2(0, -72);
            TMP_Text name = ShowUi.Label(nameBox, "Text", "", 12.5f, FontRole.BodyBold, ColorRole.Ink);
            name.text = "<noparse>" + l.Name + "</noparse>";
            name.textWrappingMode = TextWrappingModes.Normal;
            name.lineSpacing = -14f;

            // .ptag: mono bold on paper, ink border, a punched hole on the left
            RectTransform ptag = ShowUi.Rect("Price", face);
            ptag.anchorMin = ptag.anchorMax = new Vector2(.5f, 0);
            ptag.pivot = new Vector2(.5f, 0);
            ptag.sizeDelta = new Vector2(tw, 26);
            ptag.anchoredPosition = new Vector2(0, 9);
            RectTransform pf = MathUi.Card(ptag, 0f, ColorRole.Paper, ColorRole.Ink, 2f);
            pf.parent.Find("Shadow").gameObject.SetActive(false);
            RectTransform hole = ShowUi.Rect("Hole", pf);
            hole.anchorMin = hole.anchorMax = new Vector2(0, .5f);
            hole.sizeDelta = new Vector2(5, 5); hole.anchoredPosition = new Vector2(4.5f, 0);
            var hi = hole.gameObject.AddComponent<Image>();
            hi.sprite = UiShapes.Circle; hi.color = Theme.Color(ColorRole.Ink); hi.raycastTarget = false;
            TMP_Text pt = ShowUi.Label(pf, "Text", "", 15.2f, FontRole.BodyBold, ColorRole.Ink);
            pt.richText = false;
            pt.text = tag;
            pt.rectTransform.offsetMin = new Vector2(6, 0);
        }

        /// <summary>math.css .bill: the note she was paid with, striped, tilted 4deg.</summary>
        private void Bill(RectTransform table, long paid)
        {
            RectTransform rt = SpellingUi.Box("Bill", table, 120, 62);
            RectTransform body = ShowUi.Rect("Body", rt);
            ShowUi.Stretch(body);
            body.localEulerAngles = new Vector3(0, 0, -4f);
            RectTransform face = MathUi.Card(body, 3f, ColorRole.Card, ColorRole.PaperDeep, 2.5f);
            RectTransform stripes = ShowUi.Rect("Stripes", face);
            ShowUi.Stretch(stripes);
            var si = stripes.gameObject.AddComponent<Image>();
            si.sprite = MathUi.Stripes; si.type = Image.Type.Tiled; si.raycastTarget = false;
            Color sc = MathUi.Mix(ColorRole.Accent, .55f, ColorRole.Card); si.color = sc;
            si.pixelsPerUnitMultiplier = .6f;
            RectTransform paidBox = ShowUi.Rect("Paid", face);
            paidBox.anchorMin = new Vector2(0, .5f); paidBox.anchorMax = new Vector2(1, 1); paidBox.offsetMin = paidBox.offsetMax = Vector2.zero;
            ShowUi.Label(paidBox, "Text", MathLines.Paid, 16f, FontRole.Display, ColorRole.Ink);
            string amount = Money.Dollars(paid);
            RectTransform pill = ShowUi.Rect("Amount", face);
            pill.anchorMin = pill.anchorMax = new Vector2(.5f, .5f);
            pill.pivot = new Vector2(.5f, 1);
            pill.sizeDelta = new Vector2(amount.Length * .6f * 20.8f + 16, 26);
            pill.anchoredPosition = new Vector2(0, -1);
            MathUi.Card(pill, 0f, ColorRole.Card, ColorRole.Ink, 2f).parent.Find("Shadow").gameObject.SetActive(false);
            TMP_Text at = ShowUi.Label(pill, "Text", "", 20.8f, FontRole.BodyBold, ColorRole.Ink);
            at.richText = false;
            at.text = amount;
        }

        /// <summary>merch.js:15-23 the receipt: amounts right-aligned so the decimal points stack; the sum under a rule.</summary>
        private void DrawReceipt(List<MerchRules.ReceiptRow> rows)
        {
            for (int i = _work.childCount - 1; i >= 0; i--) { GameObject c = _work.GetChild(i).gameObject; c.SetActive(false); Destroy(c); }
            const float size = 20.8f, line = 31f;
            int chars = 0;
            foreach (var r in rows) chars = Mathf.Max(chars, r.Amount.Length);
            float w = (chars + 2) * .6f * size + 36f;
            float h = rows.Count * line + 3f + 12f + 18f;
            RectTransform rec = SpellingUi.Box("Receipt", _work, w, h);
            RectTransform face = MathUi.Card(rec, 3f, ColorRole.Card, ColorRole.Accent, 2f);
            float y = 10f;
            var sb = new System.Text.StringBuilder("WT_RECEIPT rows=");
            foreach (var r in rows)
            {
                if (r.Sum)
                {
                    RectTransform rule = ShowUi.Rect("Rule", face);
                    rule.anchorMin = new Vector2(0, 1); rule.anchorMax = new Vector2(1, 1); rule.pivot = new Vector2(.5f, 1);
                    rule.offsetMin = new Vector2(16, 0); rule.offsetMax = new Vector2(-16, 0);
                    rule.sizeDelta = new Vector2(-32, 3); rule.anchoredPosition = new Vector2(0, -y);
                    var ri = rule.gameObject.AddComponent<Image>(); ri.color = Theme.Color(ColorRole.Ink); ri.raycastTarget = false;
                    y += 5f;
                }
                RectTransform row = ShowUi.Rect(r.Sum ? "Sum" : "Row", face);
                row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1); row.pivot = new Vector2(.5f, 1);
                row.sizeDelta = new Vector2(-32, line); row.anchoredPosition = new Vector2(0, -y);
                TMP_Text t = ShowUi.Label(row, "Text", "", size, FontRole.BodyBold, ColorRole.Ink, TextAlignmentOptions.Left);
                t.richText = false;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                string op = string.IsNullOrEmpty(r.Op) ? " " : r.Op;
                t.text = op + " " + r.Amount;
                sb.Append(op.Trim()).Append(r.Amount.Trim()).Append('|');
                y += line;
            }
            Debug.Log(sb.ToString());
            _work.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_ctx.Area);
            Feedback.Slap(rec);
        }
    }
}
