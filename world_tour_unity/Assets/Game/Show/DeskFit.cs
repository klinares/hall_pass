using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WorldTour.Game.Show
{
    /// <summary>
    /// Phase 8.1 stage show: the ink desk of a math song (the picture, plot, beat bar or tray) in the compact panel. It lays its children out at its natural
    /// height but tells its column it is only <see cref="Scale"/> of that tall, and is drawn at that scale (from its top edge). <see cref="CompactFit"/> turns
    /// the scale down when the song is too tall, so the pictures give way before the words do. At scale 1 it is the desk as the 2D game draws it.
    /// It works inside the layout pass (it is both a layout element and a self controller), so a picture that appears mid-song is placed in the same pass.
    /// Add it after the desk's layout group.
    /// </summary>
    [RequireComponent(typeof(VerticalLayoutGroup))]
    public sealed class DeskFit : MonoBehaviour, ILayoutElement, ILayoutSelfController
    {
        public const float MinScale = 0.55f;

        VerticalLayoutGroup _group;
        RectTransform _rt;
        float _natural;
        float _scale = 1f;

        /// <summary>1 = natural size.</summary>
        public float Scale { get { return _scale; } }
        /// <summary>The desk's natural height at the last layout pass.</summary>
        public float NaturalHeight { get { return _natural; } }
        /// <summary>The room the desk takes in the column right now.</summary>
        public float Footprint { get { return _natural * _scale; } }

        void Hook()
        {
            if (_group == null) _group = GetComponent<VerticalLayoutGroup>();
            if (_rt == null) _rt = (RectTransform)transform;
        }

        /// <summary>
        /// Round 8.4: the desk's words and stacked fractions never draw smaller than <see cref="MinPx"/> on the screen, however far the desk has shrunk (the snack table).
        /// Text is set larger and its layout row grows with it; a fraction is drawn bigger as a whole. Only ever grows, so it settles.
        /// </summary>
        public bool KeepReadable;
        public const float MinPx = 14.4f;
        float _nextReadable;

        void LateUpdate()
        {
            if (!KeepReadable || Time.unscaledTime < _nextReadable) return;
            _nextReadable = Time.unscaledTime + 0.15f;
            foreach (TMP_Text t in GetComponentsInChildren<TMP_Text>(false))
            {
                if (string.IsNullOrEmpty(t.text) || t.GetComponentInParent<FracMark>() != null) continue;
                float px = t.fontSize * t.transform.lossyScale.x;
                if (px <= 0f || px >= MinPx - 0.05f) continue;
                t.fontSize = MinPx / t.transform.lossyScale.x;
                var le = t.GetComponent<LayoutElement>();
                if (le != null)
                {
                    le.preferredHeight = Mathf.Max(le.preferredHeight, t.fontSize * 1.25f);
                    le.preferredWidth = Mathf.Max(le.preferredWidth, t.GetPreferredValues(t.text, 2000, 0).x + 2f);
                }
                LayoutRebuilder.MarkLayoutForRebuild(_rt != null ? _rt : (RectTransform)transform);
            }
            foreach (FracMark f in GetComponentsInChildren<FracMark>(false))
            {
                var fr = (RectTransform)f.transform;
                float px = f.Size * fr.lossyScale.x / Mathf.Max(0.01f, fr.localScale.x);   // the digits' px at scale 1 of this fraction
                float want = Mathf.Max(1f, MinPx / Mathf.Max(0.01f, px));
                if (want > fr.localScale.x + 0.01f) fr.localScale = new Vector3(want, want, 1f);
            }
        }

        public void SetScale(float s)
        {
            Hook();
            float c = Mathf.Clamp(s, MinScale, 1f);
            if (Mathf.Abs(c - _scale) < 0.0005f) return;
            _scale = c;
            _rt.localScale = new Vector3(_scale, _scale, 1f);
            LayoutRebuilder.MarkLayoutForRebuild(_rt);
        }

        // ---- ILayoutElement: the column sees the scaled height, and the width of the layout group as usual ----
        public float minWidth { get { return -1f; } }
        public float preferredWidth { get { return -1f; } }
        public float flexibleWidth { get { return -1f; } }
        public float minHeight { get { return _natural * _scale; } }
        public float preferredHeight { get { return _natural * _scale; } }
        public float flexibleHeight { get { return -1f; } }
        public int layoutPriority { get { return 5; } }

        public void CalculateLayoutInputHorizontal() { }

        public void CalculateLayoutInputVertical()
        {
            Hook();
            _natural = _group.preferredHeight;   // the group (added before this) has just calculated its own
        }

        // ---- ILayoutSelfController: the column gave the desk its scaled height; its children get the natural one ----
        public void SetLayoutHorizontal() { }

        public void SetLayoutVertical()
        {
            Hook();
            if (_natural > 0f && Mathf.Abs(_rt.rect.height - _natural) > 0.1f) _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _natural);
        }
    }

    /// <summary>Marks a stacked fraction (MathUi.Frac) with its digit font size, so <see cref="DeskFit"/> can draw it bigger as a whole.</summary>
    public sealed class FracMark : MonoBehaviour { public float Size; }
}
