using UnityEngine;
using UnityEngine.UI;

namespace WorldTour.Game.Show
{
    /// <summary>
    /// Phase 8.1 stage show: a safety net for the compact panel. The song's content is laid out in one column; a longer prompt or an open
    /// hint picture can make it taller than the panel. This shrinks the play area just enough to fit (never below <see cref="MinScale"/>, and
    /// only ever smaller during one song), by laying the content out in a taller rect and drawing it at that scale. It does nothing when the
    /// content fits, and the 2D game never adds it.
    /// </summary>
    public sealed class CompactFit : MonoBehaviour
    {
        public const float MinScale = 0.8f;

        RectTransform _rt;
        float _w, _h, _top;
        float _scale = 1f;
        float _next;
        float _settleUntil;

        /// <summary>The play area's room: its size when drawn at scale 1, and the gap above it. Call once, before the first song.</summary>
        public void Setup(float width, float height, float top)
        {
            _rt = (RectTransform)transform;
            _w = width; _h = height; _top = top;
            Apply(1f);
        }

        /// <summary>A new song was mounted: start from full size again and watch closely for a few frames.</summary>
        public void Restart()
        {
            Apply(1f);
            _settleUntil = Time.unscaledTime + 0.6f;   // a few frames of layout and text measuring, during which the scale may also grow back
            _next = 0f;
        }

        public float Scale { get { return _scale; } }

        void Apply(float s)
        {
            _scale = s;
            _rt.anchorMin = _rt.anchorMax = new Vector2(.5f, 1f);
            _rt.pivot = new Vector2(.5f, 1f);
            _rt.sizeDelta = new Vector2(_w / s, _h / s);
            _rt.anchoredPosition = new Vector2(0f, -_top);
            _rt.localScale = new Vector3(s, s, 1f);
        }

        void LateUpdate()
        {
            if (_rt == null || _rt.childCount == 0) return;
            bool settling = Time.unscaledTime < _settleUntil;
            if (!settling && Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.1f;
            RectTransform content = null;
            for (int i = _rt.childCount - 1; i >= 0; i--)
            {
                var c = _rt.GetChild(i) as RectTransform;
                if (c != null && c.gameObject.activeSelf) { content = c; break; }
            }
            if (content == null) return;
            float pref = LayoutUtility.GetPreferredHeight(content);
            if (pref <= 1f) return;

            // the pictures give way first: every ink desk is drawn smaller by the same factor, down to DeskFit.MinScale
            DeskFit[] desks = content.GetComponentsInChildren<DeskFit>(false);
            bool desksAtMin = true;
            if (desks.Length > 0)
            {
                float footprint = 0f;
                foreach (DeskFit d in desks) footprint += d.Footprint;
                float over = pref - _h;                                // positive: too tall; negative: room to spare (judged at full size)
                if (footprint > 1f && (over > 0.5f || (settling && over < -12f)))
                {
                    float factor = Mathf.Clamp((footprint - over) / footprint, 0.5f, 1.6f);
                    bool grow = factor > 1f;
                    if (!grow || settling)
                        foreach (DeskFit d in desks) d.SetScale(d.Scale * (grow ? Mathf.Min(factor, 1.05f) : factor));
                    LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                    pref = LayoutUtility.GetPreferredHeight(content);
                }
                foreach (DeskFit d in desks) if (d.Scale > DeskFit.MinScale + 0.01f) desksAtMin = false;
            }

            // still too tall (the desks are as small as they go): the whole song is drawn smaller, never below MinScale
            float want = desksAtMin ? Mathf.Clamp(_h / pref, MinScale, 1f) : 1f;
            if (want < _scale - 0.004f || (settling && want > _scale + 0.03f)) Apply(want);
        }
    }
}
