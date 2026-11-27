using UnityEngine;

namespace WorldTour.World.Wardrobe
{
    /// <summary>
    /// Phase 10B.1 fix: patterns (stripes, dots, stars, checks) live IN the cloth, never as extra meshes. A pattern is a function of a point on the garment in "pattern space",
    /// which is the cloth laid flat: u round the body (or the limb), v up the body (or down the limb), both in model units. The painted body, arm and leg textures sample the
    /// same function over their canvas (see <see cref="StarWardrobe"/>) and the fabric parts (a hood, a skirt, a bib) wear a small tile of it, so a skirt and the legs
    /// under it agree on the size of a dot.
    /// </summary>
    public static class Fabric
    {
        /// <summary>How a part of the body lays its cloth flat: round the trunk (axis up), round a leg (axis up, offset), or round an arm (axis along x).</summary>
        public enum Mode { Body, Leg, Arm }

        public const float BodyRadius = 0.4f, LegRadius = 0.15f, ArmRadius = 0.14f;

        /// <summary>Pixels per model unit on the painted canvases.</summary>
        public const float Ppu = 100f;

        /// <summary>Where a model-space point lies on the flat cloth. <paramref name="cx"/> is the leg's own centre line (x). u is a plain angle times the radius, so
        /// the caller unwraps it per triangle (<see cref="Unwrap"/>).</summary>
        public static void Project(Mode mode, Vector3 p, float cx, out float u, out float v)
        {
            switch (mode)
            {
                case Mode.Leg: u = Mathf.Atan2(p.x - cx, p.z - 0.02f) * LegRadius; v = p.y; break;
                case Mode.Arm: u = Mathf.Atan2(p.y - 1.11f, p.z) * ArmRadius; v = Mathf.Abs(p.x); break;
                default: u = Mathf.Atan2(p.x, p.z) * BodyRadius; v = p.y; break;
            }
        }

        public static float Circumference(Mode mode) { return 2f * Mathf.PI * (mode == Mode.Leg ? LegRadius : (mode == Mode.Arm ? ArmRadius : BodyRadius)); }

        /// <summary>Makes the three u values of one triangle continuous across the seam at the back of the body (so a triangle never smears the pattern across the whole cloth).</summary>
        public static void Unwrap(Mode mode, ref float u0, ref float u1, ref float u2)
        {
            float c = Circumference(mode), half = c * 0.5f;
            float lo = Mathf.Min(u0, Mathf.Min(u1, u2)), hi = Mathf.Max(u0, Mathf.Max(u1, u2));
            if (hi - lo <= half) return;
            if (u0 < 0f) u0 += c;
            if (u1 < 0f) u1 += c;
            if (u2 < 0f) u2 += c;
        }

        // ------------------------------------------------------------------ the painted canvases
        // The runtime body textures are twice the palette's size: the palette stays in the lower left quarter (the model's UVs there are halved), the upper right quarter is
        // the trunk's cloth (and, in the legs texture, the trousers) and the lower right quarter is the sleeves'. One canvas pixel is 1 / Ppu model units.

        public const float CanvasU0 = -1.28f;

        /// <summary>The first v on a canvas (the bottom row): the trunk starts a little under the hem, legs and arms at 0.</summary>
        public static float CanvasV0(Mode mode) { return mode == Mode.Body ? 0.35f : 0f; }

        /// <summary>The pixel, inside its quarter of the texture, where cloth point (u, v) is painted.</summary>
        public static Vector2 CanvasPixel(Mode mode, float u, float v) { return new Vector2((u - CanvasU0) * Ppu, (v - CanvasV0(mode)) * Ppu); }

        /// <summary>The cloth point painted at a canvas pixel (the inverse of <see cref="CanvasPixel"/>).</summary>
        public static void CanvasPoint(Mode mode, float px, float py, out float u, out float v) { u = px / Ppu + CanvasU0; v = py / Ppu + CanvasV0(mode); }

        /// <summary>Where the quarter of a mode starts in a texture whose palette is <paramref name="w"/> by <paramref name="h"/> pixels (the sleeves use the lower right one).</summary>
        public static Vector2Int CanvasOrigin(Mode mode, int w, int h) { return new Vector2Int(w, mode == Mode.Arm ? 0 : h); }

        /// <summary>The UV in the doubled texture of a cloth point (kept half a pixel inside its quarter).</summary>
        public static Vector2 CanvasUv(Mode mode, float u, float v, int w, int h)
        {
            Vector2 p = CanvasPixel(mode, u, v);
            p.x = Mathf.Clamp(p.x, 0.5f, w - 0.5f); p.y = Mathf.Clamp(p.y, 0.5f, h - 0.5f);
            Vector2Int o = CanvasOrigin(mode, w, h);
            return new Vector2((o.x + p.x) / (2f * w), (o.y + p.y) / (2f * h));
        }

        // ------------------------------------------------------------------ the patterns

        /// <summary>The period of a pattern in u and v (the tile a fabric part wears covers exactly one).</summary>
        public static Vector2 Period(int pattern)
        {
            switch (pattern)
            {
                case 1: return new Vector2(0.32f, 0.16f);   // stripes: bands across
                case 2: return new Vector2(0.2f, 0.4f);     // dots: every other row shifted half a step
                case 3: return new Vector2(0.3f, 0.56f);    // stars: the same
                case 4: return new Vector2(0.34f, 0.34f);   // checks
            }
            return new Vector2(1f, 1f);
        }

        static float Frac(float x) { return x - Mathf.Floor(x); }

        /// <summary>How much of the pattern colour a point of the cloth shows (0 to 1, soft over about <paramref name="w"/> units so the edges are smooth). Pattern 0 is plain.</summary>
        public static float Sample(int pattern, float u, float v, float w)
        {
            switch (pattern)
            {
                case 1:
                    {
                        const float pv = 0.16f;
                        float dist = Mathf.Abs(Frac(v / pv) - 0.5f) * pv;
                        return Mathf.Clamp01((pv * 0.25f - dist) / w + 0.5f);
                    }
                case 2:
                    {
                        const float s = 0.2f;
                        int row = Mathf.FloorToInt(v / s);
                        float uu = u + ((row & 1) != 0 ? s * 0.5f : 0f);
                        float fx = (Frac(uu / s) - 0.5f) * s, fy = (Frac(v / s) - 0.5f) * s;
                        return Mathf.Clamp01((0.055f - Mathf.Sqrt(fx * fx + fy * fy)) / w + 0.5f);
                    }
                case 3:
                    {
                        const float su = 0.3f, sv = 0.28f;
                        int row = Mathf.FloorToInt(v / sv);
                        float uu = u + ((row & 1) != 0 ? su * 0.5f : 0f);
                        float fx = (Frac(uu / su) - 0.5f) * su, fy = (Frac(v / sv) - 0.5f) * sv;
                        float d = Mathf.Sqrt(fx * fx + fy * fy);
                        float a = Mathf.Atan2(fx, fy) * Mathf.Rad2Deg; if (a < 0f) a += 360f;
                        float t = Mathf.Abs((a % 72f) - 36f) / 36f;      // 0 at a notch, 1 at a point
                        float edge = Mathf.Lerp(0.04f, 0.105f, t * t * 0.5f + t * 0.5f);
                        return Mathf.Clamp01((edge - d) / w + 0.5f);
                    }
                case 4:
                    {
                        const float s = 0.17f;
                        float fu = Frac(u / s), fv = Frac(v / s);
                        bool a = fu < 0.5f, b = fv < 0.5f;
                        float du = Mathf.Min(fu, Mathf.Abs(fu - 0.5f), 1f - fu) * s, dv = Mathf.Min(fv, Mathf.Abs(fv - 0.5f), 1f - fv) * s;
                        float edge = Mathf.Min(du, dv);
                        float m = (a ^ b) ? 1f : 0f;
                        float soft = Mathf.Clamp01(edge / w);
                        return Mathf.Lerp(0.5f, m, soft);
                    }
            }
            return 0f;
        }

        // ------------------------------------------------------------------ tiles for fabric parts

        public const int TileSize = 64;

        /// <summary>A small repeating tile of one pattern in two colours, for the fabric parts (hoods, skirts, bibs); uv = (u / period.x, v / period.y).</summary>
        public static Texture2D MakeTile(int pattern, Color main, Color mark)
        {
            Vector2 per = Period(pattern);
            var px = new Color32[TileSize * TileSize];
            float w = Mathf.Max(per.x, per.y) / TileSize;
            for (int y = 0; y < TileSize; y++)
                for (int x = 0; x < TileSize; x++)
                {
                    float u = (x + 0.5f) / TileSize * per.x, v = (y + 0.5f) / TileSize * per.y;
                    px[y * TileSize + x] = Color.Lerp(main, mark, Sample(pattern, u, v, w));
                }
            var t = new Texture2D(TileSize, TileSize, TextureFormat.RGBA32, true) { name = "avf_tile" + pattern, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }
    }
}
