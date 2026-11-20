using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WorldTour.Game.Mascot
{
    /// <summary>
    /// riff's speech bubble (components.css .bubble at 900px and wider): the exported decor 'bubble' drawn Sliced,
    /// its 'bubble_tail' at the bottom centre, and the line in the body font, lowercase, with the web's padding
    /// (3px border + 12px 16px). The RectTransform's width is the bubble's; its height grows with the text and
    /// never drops below minHeight (64 in the web, 110 for the show coach). It takes no raycasts.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class DogBubble : MonoBehaviour
    {
        [SerializeField] DogArt art;
        [SerializeField] TMP_FontAsset bodyFont;      // Courier Prime SDF (made at "go")
        [SerializeField] float minHeight = 64;
        [SerializeField] float fontSize = 16;         // .bubble font-size: 1rem
        [SerializeField] Color ink = Color.black;     // tokens.css --ink; the 2.2 Theme can set it
        /// <summary>The tail's x as a fraction of the width: .5 for the show coach, .48 on the home screen.</summary>
        [SerializeField] float tailAt = .5f;

        const float PadX = 3 + 16, PadY = 3 + 12;

        Image _body, _tail;
        TextMeshProUGUI _text;

        public string Text => _text != null ? _text.text : null;
        public float MinHeight { get => minHeight; set { minHeight = value; Layout(); } }

        public void Init(DogArt dogArt, TMP_FontAsset font)
        {
            if (dogArt != null) art = dogArt;
            if (font != null) bodyFont = font;
            Build();
        }

        void Awake() { if (art != null) Build(); }

        void Build()
        {
            if (_body != null || art == null) return;
            var rt = (RectTransform)transform;

            _body = NewChild<Image>("body");
            _body.sprite = art.bubble;
            _body.type = Image.Type.Sliced;
            _body.raycastTarget = false;
            var b = _body.rectTransform;
            b.anchorMin = Vector2.zero; b.anchorMax = Vector2.one;
            // the sprite is the box plus its shadow; grow right and down by the shadow (sprite px / 2 = canvas units)
            float shR = (art.bubble != null ? art.bubble.rect.width : 0) / 2f - art.bubbleBoxPx.z / 2f - art.bubbleBoxPx.x / 2f;
            float shB = (art.bubble != null ? art.bubble.rect.height : 0) / 2f - art.bubbleBoxPx.w / 2f - art.bubbleBoxPx.y / 2f;
            b.offsetMin = new Vector2(-art.bubbleBoxPx.x / 2f, -shB);
            b.offsetMax = new Vector2(shR, art.bubbleBoxPx.y / 2f);

            _tail = NewChild<Image>("tail");
            _tail.sprite = art.bubbleTail;
            _tail.raycastTarget = false;
            var t = _tail.rectTransform;
            t.anchorMin = t.anchorMax = new Vector2(tailAt, 0);
            t.pivot = new Vector2(0, 1);
            t.anchoredPosition = new Vector2(art.bubbleTailOffsetPx.x / 2f, -art.bubbleTailOffsetPx.y / 2f);
            if (art.bubbleTail != null) t.sizeDelta = art.bubbleTail.rect.size / 2f;

            _text = NewChild<TextMeshProUGUI>("text");
            _text.raycastTarget = false;
            _text.richText = true;
            _text.fontStyle = FontStyles.LowerCase;
            _text.fontSize = fontSize;
            _text.color = ink;
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.textWrappingMode = TextWrappingModes.Normal;
            if (bodyFont != null) _text.font = bodyFont;
            var x = _text.rectTransform;
            x.anchorMin = Vector2.zero; x.anchorMax = Vector2.one;
            x.offsetMin = new Vector2(PadX, PadY); x.offsetMax = new Vector2(-PadX, -PadY);
            Layout();
        }

        T NewChild<T>(string name) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(T));
            go.transform.SetParent(transform, false);
            return go.GetComponent<T>();
        }

        /// <summary>Show a line (rich text with b and i tags, as the web's lines carry). Null or empty hides the bubble.</summary>
        public void Say(string line)
        {
            Build();
            if (_text == null) return;
            gameObject.SetActive(!string.IsNullOrEmpty(line));
            _text.text = line ?? "";
            Layout();
        }

        void Layout()
        {
            if (_text == null) return;
            var rt = (RectTransform)transform;
            float w = Mathf.Max(0, rt.rect.width - 2 * PadX);
            float h = _text.GetPreferredValues(_text.text, w, 0).y + 2 * PadY;
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(minHeight, h));
        }
    }
}
