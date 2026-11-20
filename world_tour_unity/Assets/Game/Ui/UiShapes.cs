using UnityEngine;

namespace WorldTour.Game.Ui
{
    /// <summary>
    /// A plain anti-aliased white disc, made once in code, for round UI parts (components.css .iconbtn border-radius:50%).
    /// It's a geometric primitive (like uGUI's built-in sprites), not decoration: decor art comes from task 2.1.
    /// </summary>
    public static class UiShapes
    {
        const int Size = 128;
        static Sprite s_Circle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_Circle = null; }

        public static Sprite Circle
        {
            get
            {
                if (s_Circle != null) return s_Circle;
                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "wt_circle", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[Size * Size];
                float r = Size / 2f, c = (Size - 1) / 2f;
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                        float a = Mathf.Clamp01(r - d);   // one pixel of edge softening
                        px[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                s_Circle = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), 100f);
                s_Circle.name = "wt_circle";
                return s_Circle;
            }
        }
    }
}
