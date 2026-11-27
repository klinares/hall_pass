using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WorldTour.Game.Map;
using WorldTour.Game.Mascot;
using WorldTour.Game.Motion;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Meta
{
    /// <summary>The tour journal's copy, word for word from world_tour/screens/journal.js, plus the pager (new).</summary>
    public static class JournalText
    {
        public const string Title = "tour journal";
        /// <summary>journal.js:28 and :75.</summary>
        public const string Caption = "drag a sticker to move it, or tap it and then tap where it should go.";
        /// <summary>journal.js:73 and :91.</summary>
        public const string Stuck = "stuck.";
        /// <summary>journal.js:34.</summary>
        public const string Empty = "blank so far. finish a show and the first sticker lands here.";
        /// <summary>journal.js:43.</summary>
        public const string Full = "every sticker earned. the whole book is full.";
        /// <summary>journal.js:38.</summary>
        public const string StillToEarn = "still to earn";
        /// <summary>journal.js:44: "more stickers to stick in: backstage."</summary>
        public const string MoreStickers = "more stickers to stick in:";

        /// <summary>journal.js:58: "&lt;b&gt;name&lt;/b&gt;: note. tap an empty spot to move it there."</summary>
        public static string Selected(string name, string note)
        {
            return "<b><noparse>" + name + "</noparse></b>: <noparse>" + note + "</noparse>. tap an empty spot to move it there.";
        }

        /// <summary>New (the web journal is one page): "page 1 of 3".</summary>
        public static string PageOf(int page, int pages)
        {
            return "page " + (page + 1).ToString(CultureInfo.InvariantCulture) + " of " + pages.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The pager arrows (team-drawn glyphs in the TMP sprite asset).</summary>
        public static readonly string Prev = ((char)0x2190).ToString();
        public static readonly string Next = ((char)0x2192).ToString();
    }

    /// <summary>Small builders shared by the backstage and journal screens (uGUI in code, like the map and venue card).</summary>
    public static class MetaUi
    {
        /// <summary>A sticker button placed by its top-left, sized to its label (VenueCard's helper).</summary>
        public static StickerButton Button(StickerButton prefab, Transform parent, string name, string text, StickerButton.Style style,
            float fontSize, Vector2 topLeft, float height = Tokens.ButtonMinHeight, float width = 0f)
        {
            if (prefab == null) return null;
            StickerButton b = Object.Instantiate(prefab, parent, false);
            b.name = name;
            b.ButtonStyle = style;
            b.Text = text;
            TMP_Text label = b.GetComponentInChildren<TMP_Text>();
            if (label != null) label.fontSize = fontSize;
            float w = width > 0f ? width : (label != null ? UiKit.WidthOf(label) : 200f) + 2f * Tokens.ButtonPadX;
            UiKit.Place((RectTransform)b.transform, topLeft + new Vector2(w * 0.5f, height * 0.5f), new Vector2(w, height), UiKit.Center);
            return b;
        }

        /// <summary>
        /// paper.css .shout: card-coloured brush letters with an ink outline and a hard ink offset shadow. Uses the ticket's
        /// shout material preset with its ink set from the live palette (so it follows the theme).
        /// </summary>
        public static TextMeshProUGUI Shout(Transform parent, string name, string text, float size, Material shout)
        {
            TextMeshProUGUI t = UiKit.Text(parent, name, text, FontRole.Display, size, ColorRole.Card, TextAlignmentOptions.BottomLeft, false);
            if (shout != null) t.fontSharedMaterial = Themed(shout);
            else t.color = Theme.Color(ColorRole.Ink);
            return t;
        }

        static readonly Dictionary<string, Material> s_Themed = new Dictionary<string, Material>();

        /// <summary>
        /// A copy of a TMP material preset (the ticket's shout and stroke) whose outline and underlay are the live palette's ink.
        /// The presets are saved with black baked in, so used as they are they stay black in peach + navy. Cached per preset
        /// and palette, so a screen that rebuilds itself shares one material.
        /// </summary>
        public static Material Themed(Material src)
        {
            if (src == null) return null;
            Color ink = Theme.Color(ColorRole.Ink);
            string key = src.GetInstanceID() + "/" + Theme.Palette.Id;
            Material m;
            if (s_Themed.TryGetValue(key, out m) && m != null) return m;
            m = new Material(src);
            if (m.HasProperty(ShaderUtilities.ID_OutlineColor)) m.SetColor(ShaderUtilities.ID_OutlineColor, ink);
            if (m.HasProperty(ShaderUtilities.ID_UnderlayColor)) m.SetColor(ShaderUtilities.ID_UnderlayColor, ink);
            s_Themed[key] = m;
            return m;
        }

        /// <summary>A rectangle outline (rounded corners not needed): <paramref name="dash"/> 0 for solid.</summary>
        public static UiLine Outline(RectTransform parent, string name, Vector2 size, ColorRole color, float width, float dash = 0f, float gap = 0f, float alpha = 1f)
        {
            RectTransform rt = UiKit.Rect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            UiLine l = rt.gameObject.AddComponent<UiLine>();
            l.raycastTarget = false;
            Color c = Theme.Color(color); c.a *= alpha;
            l.color = c;
            l.Width = width;
            float x = size.x * 0.5f, y = size.y * 0.5f;
            l.SetPoints(new[] { new Vector2(-x, y), new Vector2(x, y), new Vector2(x, -y), new Vector2(-x, -y), new Vector2(-x, y) });
            if (dash > 0f) l.SetDash(dash, gap);
            return l;
        }

        /// <summary>riff on a square rect, posed and dressed (the 2.3 rig, as DogHosts makes it).</summary>
        public static DogRig Dog(RectTransform parent, string outfit, DogPose pose = DogPose.Idle)
        {
            DogArt art = Theme.Asset != null ? Theme.Asset.dogArt : null;
            if (art == null) return null;
            var go = new GameObject("Dog", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            DogRig rig = go.AddComponent<DogRig>();
            rig.Init(art, TweenerPort.Instance, MotionSettingsPort.Instance);
            rig.Show(pose, outfit);
            return rig;
        }

        /// <summary>
        /// fx.js toast(): a big .shout word at 32% of the height, tilted -5 deg, that pops in and goes after a moment.
        /// It never takes clicks.
        /// </summary>
        public static void Toast(MonoBehaviour host, RectTransform parent, string text, Material shout, Vector2? anchor = null,
            float size = 80f, float holdSeconds = 0.9f, Vector2 offset = default(Vector2), bool inkFace = false)
        {
            TextMeshProUGUI t = Shout(parent, "Toast", text, size, shout);
            if (inkFace) InkFace(t);
            t.alignment = TextAlignmentOptions.Center;
            RectTransform rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor ?? new Vector2(0.5f, 0.68f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Mathf.Min(900f, size * 11f), size * 1.4f);
            rt.anchoredPosition = offset;
            rt.localEulerAngles = new Vector3(0f, 0f, 5f);   // CSS rotate(-5deg)
            var g = t.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false; g.interactable = false;
            Feedback.Pop(rt);
            host.StartCoroutine(FadeAway(t.gameObject, g, holdSeconds));
        }

        /// <summary>
        /// A toast that is only a feel effect (the show's win word): advisor rule, "off when reduce motion is on", so with
        /// reduce motion it is skipped entirely (polish 6.2E item 3). Returns whether one was made.
        /// </summary>
        public static bool FeelToast(MonoBehaviour host, RectTransform parent, string text, Material shout, Vector2? anchor = null,
            float size = 80f, float holdSeconds = 0.9f, Vector2 offset = default(Vector2))
        {
            if (WorldTour.Game.Motion.MotionPrefs.Reduced) return false;
            Toast(host, parent, text, shout, anchor, size, holdSeconds, offset, true);
            return true;
        }

        static readonly Dictionary<string, Material> s_InkFace = new Dictionary<string, Material>();

        /// <summary>polish 6.2F item 4: an ink face with a card-coloured outline, so the win word reads on pink and on peach.</summary>
        static void InkFace(TextMeshProUGUI t)
        {
            Material src = t.fontSharedMaterial;
            if (src == null) { t.color = Theme.Color(ColorRole.Ink); return; }
            string key = src.GetInstanceID() + "/" + Theme.Palette.Id;
            Material m;
            if (!s_InkFace.TryGetValue(key, out m) || m == null)
            {
                m = new Material(src);
                m.SetColor(ShaderUtilities.ID_FaceColor, Theme.Color(ColorRole.Ink));
                if (m.HasProperty(ShaderUtilities.ID_OutlineWidth)) m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.08f);   // the preset's 0.3 swallows the face at 52 px
                if (m.HasProperty(ShaderUtilities.ID_OutlineColor)) m.SetColor(ShaderUtilities.ID_OutlineColor, Theme.Color(ColorRole.Card));
                if (m.HasProperty(ShaderUtilities.ID_UnderlayColor)) m.SetColor(ShaderUtilities.ID_UnderlayColor, Theme.Color(ColorRole.Card));
                s_InkFace[key] = m;
            }
            t.fontSharedMaterial = m;
            t.color = Color.white;   // the face colour lives on the material
        }

        static IEnumerator FadeAway(GameObject go, CanvasGroup g, float hold)
        {
            yield return new WaitForSecondsRealtime(hold);
            if (go == null) yield break;
            Tweener.AlphaTo(g, 0f, Tokens.TMed, () => { if (go != null) Object.Destroy(go); });
        }

        /// <summary>
        /// For the browser playthroughs: one line naming where each control is, as fractions of the window from the top-left
        /// ("WT_HITS screen=backstage back=0.0713,0.0677 ..."), so a test can click it with the mouse.
        /// </summary>
        /// <summary>The page was opened with ?wt_test (browser tests only).</summary>
        public static bool TestMode
        {
            get { string u = Application.absoluteURL; return !string.IsNullOrEmpty(u) && u.IndexOf("wt_test", System.StringComparison.OrdinalIgnoreCase) >= 0; }
        }

        public static void LogHits(string screen, IEnumerable<KeyValuePair<string, RectTransform>> controls)
        {
            var sb = new StringBuilder("WT_HITS screen=").Append(screen);
            float w = Mathf.Max(1, UnityEngine.Screen.width), h = Mathf.Max(1, UnityEngine.Screen.height);
            var corners = new Vector3[4];
            foreach (KeyValuePair<string, RectTransform> kv in controls)
            {
                if (kv.Value == null || !kv.Value.gameObject.activeInHierarchy) continue;
                kv.Value.GetWorldCorners(corners);
                Vector3 c = (corners[0] + corners[2]) * 0.5f;          // screen space overlay: world = screen pixels
                sb.Append(' ').Append(kv.Key).Append('=')
                  .Append((c.x / w).ToString("0.0000", CultureInfo.InvariantCulture)).Append(',')
                  .Append((1f - c.y / h).ToString("0.0000", CultureInfo.InvariantCulture));
            }
            Debug.Log(sb.ToString());
        }
    }
}
