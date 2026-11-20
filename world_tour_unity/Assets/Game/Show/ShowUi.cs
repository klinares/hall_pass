using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Show
{
    /// <summary>A button made from the StickerButton prefab when there is one, else a plain uGUI button (tests, or before 2.2's prefab exists).</summary>
    public sealed class ShowButton
    {
        public readonly GameObject Go;
        public readonly StickerButton Sticker;
        public readonly Button Plain;
        private readonly TMP_Text _plainLabel;

        public ShowButton(GameObject go, StickerButton sticker, Button plain, TMP_Text plainLabel)
        {
            Go = go; Sticker = sticker; Plain = plain; _plainLabel = plainLabel;
        }

        public RectTransform Rect { get { return (RectTransform)Go.transform; } }

        /// <summary>The label, lowercased (TMP rich-text tags are fine).</summary>
        public void SetText(string text)
        {
            if (Sticker != null) Sticker.Text = text;
            else if (_plainLabel != null) _plainLabel.SetLower(text);
        }

        public bool Interactable
        {
            get { return Sticker != null ? Sticker.interactable : Plain.interactable; }
            set { if (Sticker != null) Sticker.interactable = value; else Plain.interactable = value; }
        }
    }

    /// <summary>Small builders for the show's code-made UI, coloured and set in type from the live theme (2.2).</summary>
    public static class ShowUi
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>Anchors a box of a fixed size at a point of its parent (0..1), centred on its own pivot.</summary>
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
        }

        /// <summary>A rounded-rect image in a theme colour (2.2's RoundedRect sprite, or uGUI's default).</summary>
        public static Image Panel(RectTransform rt, ColorRole color, float alpha = 1f, SpriteRole sprite = SpriteRole.RoundedRect)
        {
            // not ??: in the Editor a missing component is a fake-null object that ?? treats as present
            var img = rt.gameObject.GetComponent<Image>();
            if (img == null) img = rt.gameObject.AddComponent<Image>();
            Sprite s = Theme.Sprite(sprite) ?? Theme.Sprite(SpriteRole.RoundedRect);
            if (s != null) { img.sprite = s; img.type = Image.Type.Sliced; }
            Color c = Theme.Color(color); c.a *= alpha;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>A label in a theme colour, lowercased.</summary>
        public static TMP_Text Label(RectTransform parent, string name, string text, float size, FontRole font, ColorRole color,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            RectTransform rt = Rect(name, parent);
            Stretch(rt);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            Theme.ApplyFont(t, font);
            t.fontSize = size;
            t.alignment = align;
            t.color = Theme.Color(color);
            t.raycastTarget = false;
            t.richText = true;
            t.SetLower(text);
            return t;
        }

        /// <summary>A button: the StickerButton prefab if given, else a plain one. <paramref name="onClick"/> runs once per accepted press.</summary>
        public static ShowButton Button(StickerButton prefab, RectTransform parent, string name, string text, bool ghost, Action onClick)
        {
            if (prefab != null)
            {
                StickerButton b = UnityEngine.Object.Instantiate(prefab, parent, false);
                b.name = name;
                b.ButtonStyle = ghost ? StickerButton.Style.Ghost : StickerButton.Style.Ink;
                b.Text = text;
                b.OnClick.AddListener(() => onClick());
                return new ShowButton(b.gameObject, b, null, null);
            }
            RectTransform rt = Rect(name, parent);
            rt.sizeDelta = new Vector2(150, Tokens.Tap);
            Panel(rt, ghost ? ColorRole.Card : ColorRole.Ink).raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = rt.GetComponent<Image>();
            TMP_Text label = Label(rt, "Label", text, Tokens.FontButtonSm, FontRole.Display, ghost ? ColorRole.Ink : ColorRole.OnInk);
            btn.onClick.AddListener(() => onClick());
            return new ShowButton(rt.gameObject, null, btn, label);
        }
    }
}
