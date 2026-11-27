using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WorldTour.Core.Meta;
using WorldTour.Game.Prefs;
using WorldTour.Game.Save;

namespace WorldTour.Game.Ui
{
    /// <summary>
    /// Phase 10B.1: the two numbers in the top-left corner of the world, "fans 12,300" and "coins 85". Fans only grow and open places;
    /// coins are what the stores take. When coins are earned a small "+3" floats up from the coins number (still under reduced motion, then it just fades).
    /// Shows nothing else, takes no clicks and no focus. Built by <see cref="Hud.AddCounters"/>.
    /// </summary>
    public sealed class CoinChip : MonoBehaviour
    {
        /// <summary>Raised by the show screen for every answer that earned coins (the amount).</summary>
        public static event System.Action<long> Earned;

        public static void Notify(long coins) { var h = Earned; if (h != null && coins > 0) h(coins); }

        const float PopSeconds = 1.3f, Gather = 0.6f;

        SaveStore _store;
        TextMeshProUGUI _fans, _coins, _pop;
        RectTransform _popRt;
        CanvasGroup _popGroup;
        long _shownFans = -1, _shownCoins = -1, _pending;
        float _popAt = -1f, _gatherUntil;
        Vector2 _popHome;

        public string FansText { get { return _fans != null ? _fans.text : ""; } }
        public string CoinsText { get { return _coins != null ? _coins.text : ""; } }
        public string PopText { get { return _pop != null && _pop.gameObject.activeSelf ? _pop.text : ""; } }

        public static CoinChip Create(Transform parent, SaveStore store)
        {
            var go = new GameObject("CoinChip", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(Tokens.GutterMin, -Tokens.GutterMin);
            var chip = go.AddComponent<CoinChip>();
            chip.Build(store);
            return chip;
        }

        static RectTransform Child(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static void Fill(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = min; rt.offsetMax = max;
        }

        TextMeshProUGUI Label(RectTransform parent, string name, float size, FontRole font, TextAlignmentOptions align, Vector2 pos, Vector2 box, ColorRole role)
        {
            RectTransform rt = Child(parent, name);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, .5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            Theme.ApplyFont(t, font);
            t.fontSize = size;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            ThemeColor.Add(rt.gameObject, role);
            return t;
        }

        void Build(SaveStore store)
        {
            _store = store;
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(236f, 38f);
            // a card with a 2 px ink border and a hard shadow, as the other chips
            RectTransform shadow = Child(rt, "Shadow"); Fill(shadow, new Vector2(3f, -3f), new Vector2(3f, -3f));
            var si = shadow.gameObject.AddComponent<Image>(); si.raycastTarget = false; ThemeColor.Add(shadow.gameObject, ColorRole.Ink);
            var border = gameObject.AddComponent<Image>(); border.raycastTarget = false; ThemeColor.Add(gameObject, ColorRole.Ink);
            RectTransform face = Child(rt, "Face"); Fill(face, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            var fi = face.gameObject.AddComponent<Image>(); fi.raycastTarget = false; ThemeColor.Add(face.gameObject, ColorRole.Card);

            _fans = Label(face, "Fans", 17f, FontRole.BodyBold, TextAlignmentOptions.MidlineLeft, new Vector2(10f, 0f), new Vector2(120f, 30f), ColorRole.Ink);
            _coins = Label(face, "Coins", 17f, FontRole.BodyBold, TextAlignmentOptions.MidlineLeft, new Vector2(126f, 0f), new Vector2(104f, 30f), ColorRole.Ink);

            _popRt = Child(rt, "Pop");
            _popRt.anchorMin = _popRt.anchorMax = _popRt.pivot = new Vector2(0f, 1f);
            _popHome = new Vector2(128f, -44f);
            _popRt.anchoredPosition = _popHome;
            _popRt.sizeDelta = new Vector2(108f, 28f);
            var pb = _popRt.gameObject.AddComponent<Image>(); pb.raycastTarget = false; ThemeColor.Add(_popRt.gameObject, ColorRole.Ink);
            _popGroup = _popRt.gameObject.AddComponent<CanvasGroup>(); _popGroup.blocksRaycasts = false; _popGroup.interactable = false;
            RectTransform pf = Child(_popRt, "Face"); Fill(pf, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            var pfi = pf.gameObject.AddComponent<Image>(); pfi.raycastTarget = false; ThemeColor.Add(pf.gameObject, ColorRole.Card);
            RectTransform tr = Child(pf, "Text"); Fill(tr, Vector2.zero, Vector2.zero);
            _pop = tr.gameObject.AddComponent<TextMeshProUGUI>();
            Theme.ApplyFont(_pop, FontRole.BodyBold);
            _pop.fontSize = 17f; _pop.alignment = TextAlignmentOptions.Center; _pop.raycastTarget = false;
            _pop.textWrappingMode = TextWrappingModes.NoWrap;
            ThemeColor.Add(tr.gameObject, ColorRole.Ink);
            _popRt.gameObject.SetActive(false);

            Earned += OnEarned;
            Refresh(true);
        }

        void OnDestroy() { Earned -= OnEarned; }

        static string Fmt(long n) { return n.ToString("#,##0", CultureInfo.InvariantCulture); }

        void Refresh(bool force)
        {
            if (_store == null || _store.Blob == null) return;
            long f = _store.Blob.Fans.Lifetime, c = CoinRules.Coins(_store.Blob);
            if (!force && f == _shownFans && c == _shownCoins) return;
            _shownFans = f; _shownCoins = c;
            _fans.SetLower("fans " + Fmt(f));
            _coins.SetLower("coins " + Fmt(c));
            Layout();
        }

        /// <summary>The card grows to fit its two numbers, so "fans 2,023,600" and "coins 500" never touch at any count.</summary>
        void Layout()
        {
            float fw = Mathf.Ceil(_fans.GetPreferredValues(_fans.text).x) + 4f;
            float cw = Mathf.Ceil(_coins.GetPreferredValues(_coins.text).x) + 4f;
            const float pad = 12f, gap = 22f;
            SetBox(_fans, pad, fw);
            SetBox(_coins, pad + fw + gap, cw);
            ((RectTransform)transform).sizeDelta = new Vector2(pad + fw + gap + cw + pad, 38f);
            if (_popRt != null) { _popHome = new Vector2(pad + fw + gap - 2f, -44f); if (_popAt < 0f) _popRt.anchoredPosition = _popHome; }
        }

        static void SetBox(TextMeshProUGUI t, float x, float w)
        {
            var r = t.rectTransform; r.anchoredPosition = new Vector2(x, 0f); r.sizeDelta = new Vector2(w, 30f);
        }

        void OnEarned(long n)
        {
            _pending += n;
            _gatherUntil = Time.unscaledTime + Gather;   // answers a moment apart add up in one pop
            _popAt = -1f;
        }

        void Update()
        {
            Refresh(false);
            if (_pending > 0 && Time.unscaledTime >= _gatherUntil && _popAt < 0f)
            {
                _pop.SetLower("+" + _pending + (_pending == 1 ? " coin" : " coins"));
                _pending = 0;
                _popAt = Time.unscaledTime;
                _popGroup.alpha = 1f;
                _popRt.gameObject.SetActive(true);
            }
            if (_popAt >= 0f)
            {
                float t = (Time.unscaledTime - _popAt) / PopSeconds;
                if (t >= 1f) { _popRt.gameObject.SetActive(false); _popAt = -1f; if (_pending > 0) _gatherUntil = Time.unscaledTime; return; }
                bool still = GameSettings.ReducedMotion;
                _popRt.anchoredPosition = _popHome + (still ? Vector2.zero : new Vector2(0f, Mathf.Sin(t * Mathf.PI * .5f) * -10f));
                _popGroup.alpha = t < .6f ? 1f : 1f - (t - .6f) / .4f;
            }
        }
    }
}
