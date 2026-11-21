using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WorldTour.Game.Motion;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Show
{
    /// <summary>
    /// Confetti made of tiny outlined stickers (stars, hearts, bolts) on one uGUI mesh (fx.js burst). The bits fly and
    /// fall under the web game's own numbers (speeds and gravity scaled from per-frame to per-second) and are gone inside
    /// the burst's seconds: 0.4 s for a win, a little longer for the ticket's big finish (polish brief: effects stay
    /// short and never block input). Nothing here takes a raycast, and a burst does nothing at all when reduce motion is
    /// on (fx.js: if (reduced()) return). Colours come from the live palette, so peach + navy confetti is peach and navy.
    /// </summary>
    public sealed class ConfettiLayer : MaskableGraphic
    {
        /// <summary>A right answer's burst (under the 400 ms effect rule).</summary>
        public const float WinSeconds = 0.4f;
        /// <summary>The end-of-show burst when the ticket opens.</summary>
        public const float TicketSeconds = 0.7f;

        enum Kind { Star, Heart, Bolt }

        struct Bit
        {
            public Vector2 Pos, Vel;
            public float Rot, Spin, Size, Age, Life;
            public Color Fill;
            public Kind Kind;
        }

        sealed class Shape { public Vector2[] V; public int[] T; }

        static Shape[] s_Shapes;
        readonly List<Bit> _bits = new List<Bit>(128);
        Color _ink = Color.black;

        /// <summary>Bits in flight (for tests).</summary>
        public int Count { get { return _bits.Count; } }

        /// <summary>
        /// Throws n bits up from a point of host (0..1 each way, from the bottom-left).
        /// Returns the layer, or null when reduce motion is on (nothing is drawn).
        /// </summary>
        public static ConfettiLayer Burst(RectTransform host, int n, Vector2 at, float seconds, System.Random rng = null)
        {
            if (host == null || MotionPrefs.Reduced) return null;
            ConfettiLayer layer = host.GetComponentInChildren<ConfettiLayer>(true);
            if (layer == null)
            {
                var go = new GameObject("Confetti", typeof(RectTransform));
                go.layer = host.gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(host, false);
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                layer = go.AddComponent<ConfettiLayer>();
                layer.raycastTarget = false;
                layer.color = Color.white;
            }
            layer.transform.SetAsLastSibling();
            layer.enabled = true;
            layer.Throw(n, at, seconds, rng ?? new System.Random());
            return layer;
        }

        void Throw(int n, Vector2 at, float seconds, System.Random rng)
        {
            Rect r = rectTransform.rect;
            Vector2 origin = new Vector2(r.xMin + r.width * at.x, r.yMin + r.height * at.y);
            Palette p = Theme.Palette;
            Color[] cols =
            {
                Theme.ToColor(p[ColorRole.Accent]), Theme.ToColor(p[ColorRole.Card]), Theme.ToColor(p[ColorRole.PaperDeep]),
                Theme.ToColor(p[ColorRole.Accent]), Theme.ToColor(p[ColorRole.Card])
            };
            _ink = Theme.ToColor(p[ColorRole.Ink]);
            // fx.js: vx +-7, vy up 4..16 and gravity .38 per 60 Hz frame
            float k = 60f;
            for (int i = 0; i < n; i++)
            {
                _bits.Add(new Bit
                {
                    Pos = origin,
                    Vel = new Vector2(((float)rng.NextDouble() - .5f) * 14f * k, ((float)rng.NextDouble() * 12f + 4f) * k),
                    Rot = (float)rng.NextDouble() * 360f, Spin = ((float)rng.NextDouble() - .5f) * .3f * k * Mathf.Rad2Deg,
                    Size = 9f + (float)rng.NextDouble() * 9f,
                    Age = 0f, Life = seconds * (0.8f + 0.2f * (float)rng.NextDouble()),
                    Fill = cols[i % cols.Length], Kind = (Kind)(i % 3)
                });
            }
            SetVerticesDirty();
        }

        void Update()
        {
            if (_bits.Count == 0) { enabled = false; SetVerticesDirty(); return; }
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            const float gravity = .38f * 60f * 60f;
            Rect r = rectTransform.rect;
            for (int i = _bits.Count - 1; i >= 0; i--)
            {
                Bit b = _bits[i];
                b.Age += dt;
                b.Vel.y -= gravity * dt;
                b.Vel.x *= Mathf.Pow(.99f, dt * 60f);
                b.Pos += b.Vel * dt;
                b.Rot += b.Spin * dt;
                if (b.Age >= b.Life || b.Pos.y < r.yMin - 40f) _bits.RemoveAt(i); else _bits[i] = b;
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            EnsureShapes();
            foreach (Bit b in _bits)
            {
                // the last third shrinks away, so a bit never pops out
                float life = Mathf.Clamp01(b.Age / b.Life);
                float s = b.Size * (life < .65f ? 1f : Mathf.Clamp01((1f - life) / .35f));
                if (s <= .5f) continue;
                Shape sh = s_Shapes[(int)b.Kind];
                Draw(vh, sh, b.Pos, s + 2.6f, b.Rot, _ink);       // fx.js: lineWidth 2.6 ink stroke
                Draw(vh, sh, b.Pos, s, b.Rot, b.Fill);
            }
        }

        static void Draw(VertexHelper vh, Shape sh, Vector2 pos, float size, float rot, Color col)
        {
            int baseIndex = vh.currentVertCount;
            float c = Mathf.Cos(rot * Mathf.Deg2Rad), sn = Mathf.Sin(rot * Mathf.Deg2Rad);
            Color32 c32 = col;
            foreach (Vector2 v in sh.V)
            {
                Vector2 q = v * size;
                vh.AddVert(new Vector3(pos.x + q.x * c - q.y * sn, pos.y + q.x * sn + q.y * c), c32, Vector2.zero);
            }
            for (int i = 0; i < sh.T.Length; i += 3) vh.AddTriangle(baseIndex + sh.T[i], baseIndex + sh.T[i + 1], baseIndex + sh.T[i + 2]);
        }

        // ---- the three fx.js shapes (y down there, flipped here), triangulated once ----

        static void EnsureShapes()
        {
            if (s_Shapes != null) return;
            var star = new List<Vector2>();
            for (int i = 0; i < 10; i++)
            {
                float a = (-90f + i * 36f) * Mathf.Deg2Rad, rad = i % 2 == 1 ? .42f : 1f;
                star.Add(new Vector2(Mathf.Cos(a) * rad, -Mathf.Sin(a) * rad));
            }
            var heart = new List<Vector2>();
            Vector2[] h =
            {
                new Vector2(0f, .9f), new Vector2(-.9f, .3f), new Vector2(-1.2f, -.3f), new Vector2(-.8f, -.75f),
                new Vector2(-.45f, -1.05f), new Vector2(-.1f, -.9f), new Vector2(0f, -.55f),
                new Vector2(.1f, -.9f), new Vector2(.45f, -1.05f), new Vector2(.8f, -.75f),
                new Vector2(1.2f, -.3f), new Vector2(.9f, .3f), new Vector2(0f, .9f)
            };
            for (int s = 0; s + 3 < h.Length; s += 3)
                for (int j = 0; j < 8; j++)
                {
                    float t = j / 8f, u = 1f - t;
                    Vector2 p = u * u * u * h[s] + 3 * u * u * t * h[s + 1] + 3 * u * t * t * h[s + 2] + t * t * t * h[s + 3];
                    heart.Add(new Vector2(p.x, -p.y));
                }
            // fx.js bolt: M.2 -1 -.6 .15 h.5 L-.25 1 .65 -.2 h-.5 z
            var bolt = new List<Vector2>
            {
                new Vector2(.2f, 1f), new Vector2(-.6f, -.15f), new Vector2(-.1f, -.15f),
                new Vector2(-.25f, -1f), new Vector2(.65f, .2f), new Vector2(.15f, .2f)
            };
            s_Shapes = new[] { Make(star), Make(heart), Make(bolt) };
        }

        static Shape Make(List<Vector2> poly)
        {
            return new Shape { V = poly.ToArray(), T = Triangulate(poly) };
        }

        /// <summary>Ear clipping for a simple polygon (the shapes here are concave: the bolt's notches).</summary>
        public static int[] Triangulate(IList<Vector2> p)
        {
            int n = p.Count;
            var idx = new List<int>(n);
            float area = 0f;
            for (int i = 0; i < n; i++) { Vector2 a = p[i], b = p[(i + 1) % n]; area += a.x * b.y - b.x * a.y; }
            for (int i = 0; i < n; i++) idx.Add(area > 0f ? i : n - 1 - i);      // counter-clockwise
            var tris = new List<int>((n - 2) * 3);
            int guard = n * n;
            while (idx.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int i0 = idx[(i + idx.Count - 1) % idx.Count], i1 = idx[i], i2 = idx[(i + 1) % idx.Count];
                    Vector2 a = p[i0], b = p[i1], c = p[i2];
                    if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) <= 1e-6f) continue;   // reflex corner
                    bool inside = false;
                    foreach (int j in idx)
                    {
                        if (j == i0 || j == i1 || j == i2) continue;
                        if (InTri(p[j], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) { tris.Add(idx[0]); tris.Add(idx[1]); tris.Add(idx[2]); }
            return tris.ToArray();
        }

        static bool InTri(Vector2 q, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (q.x - b.x) * (a.y - b.y) - (a.x - b.x) * (q.y - b.y);
            float d2 = (q.x - c.x) * (b.y - c.y) - (b.x - c.x) * (q.y - c.y);
            float d3 = (q.x - a.x) * (c.y - a.y) - (c.x - a.x) * (q.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }
    }

    /// <summary>How many people stand in the show's crowd. Plain numbers, so a test can check it grows.</summary>
    public static class CrowdModel
    {
        public const int Min = 4;
        public const int Max = 28;
        /// <summary>Fans per extra person: a clean song is worth 1,000 to 5,000, so a full show fills the room.</summary>
        public const int FansPerPerson = 1500;

        public static int PeopleFor(int fansThisShow)
        {
            if (fansThisShow < 0) fansThisShow = 0;
            return Mathf.Clamp(Min + fansThisShow / FansPerPerson, Min, Max);
        }
    }

    /// <summary>
    /// One person in the crowd: a head, shoulders and two arms, drawn as a flat silhouette with an ink outline (the
    /// scrapbook sticker look), arms raised by ArmsUp (0..1).
    /// </summary>
    public sealed class CrowdPerson : MaskableGraphic
    {
        public const float Width = 56f, Height = 74f;
        public Color Fill = Color.white;
        public Color Ink = Color.black;
        float _arms;

        public float ArmsUp
        {
            get { return _arms; }
            set { value = Mathf.Clamp01(value); if (Mathf.Abs(value - _arms) > .001f) { _arms = value; SetVerticesDirty(); } }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            Vector2 foot = new Vector2(r.center.x, r.yMin);
            Pass(vh, foot, 2.5f, Ink);
            Pass(vh, foot, 0f, Fill);
        }

        void Pass(VertexHelper vh, Vector2 foot, float grow, Color col)
        {
            Color32 c = col;
            Ellipse(vh, foot, 22f + grow, 30f + grow, c, true);                  // shoulders: the top half of an ellipse
            Ellipse(vh, foot + new Vector2(0f, 43f), 12f + grow, 12f + grow, c, false);   // head
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 shoulder = foot + new Vector2(side * 15f, 22f);
                Vector2 hand = foot + new Vector2(side * Mathf.Lerp(18f, 25f, _arms), Mathf.Lerp(10f, 56f, _arms));
                Limb(vh, shoulder, hand, 4.5f + grow, c);
                Ellipse(vh, hand, 5f + grow, 5f + grow, c, false);               // fist
            }
        }

        static void Limb(VertexHelper vh, Vector2 a, Vector2 b, float half, Color32 c)
        {
            Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x) * half;
            int i = vh.currentVertCount;
            vh.AddVert(a - n, c, Vector2.zero); vh.AddVert(a + n, c, Vector2.zero);
            vh.AddVert(b + n, c, Vector2.zero); vh.AddVert(b - n, c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i + 2, i + 3, i);
        }

        static void Ellipse(VertexHelper vh, Vector2 mid, float rx, float ry, Color32 c, bool topHalf)
        {
            int segs = topHalf ? 12 : 20, i0 = vh.currentVertCount;
            vh.AddVert(mid, c, Vector2.zero);
            float span = topHalf ? Mathf.PI : Mathf.PI * 2f;
            for (int s = 0; s <= segs; s++)
            {
                float a = span * s / segs;
                vh.AddVert(mid + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry), c, Vector2.zero);
            }
            for (int s = 0; s < segs; s++) vh.AddTriangle(i0, i0 + 1 + s, i0 + 2 + s);
        }
    }

    /// <summary>
    /// The show's crowd: a row of people along the bottom of the screen, behind the play area. It grows with the fans
    /// earned this show (CrowdModel), cheers on a right answer (a short staggered hop with arms up) and sags gently on a
    /// miss (a slow slump and recover). Never red, never takes a click. Under reduce motion nothing moves: a cheer just
    /// holds the arms up for a moment.
    /// </summary>
    public sealed class ShowCrowd : MonoBehaviour
    {
        const int Slots = 28;
        /// <summary>Polish 6.2D item 4: the crowd's own strip along the bottom edge. The play area ends above it and the strip clips its people, so none can sit under or over a control.</summary>
        public const float StripHeight = 52f;
        const float HopSeconds = 0.32f, SagSeconds = 0.38f, ArmsHold = 0.9f;

        sealed class Member
        {
            public CrowdPerson G;
            public RectTransform Rt;
            public float X, Scale, Lean;
        }

        readonly List<Member> _members = new List<Member>();
        RectTransform _rt;
        int[] _order;
        float _cheerAt = -10f, _sagAt = -10f;

        public int People { get { return _members.Count; } }

        /// <summary>Whether anyone has their arms up right now (for tests).</summary>
        public bool ArmsRaised { get { foreach (Member m in _members) if (m.G.ArmsUp > .5f) return true; return false; } }

        /// <summary>The highest any person is lifted or sunk right now, px (0 at rest; for tests).</summary>
        public float Motion { get; private set; }

        public static ShowCrowd Create(RectTransform parent)
        {
            var go = new GameObject("Crowd", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(.5f, 0f);
            rt.offsetMin = new Vector2(0f, 0f); rt.offsetMax = new Vector2(0f, StripHeight);
            go.AddComponent<RectMask2D>();
            rt.SetAsFirstSibling();
            ShowCrowd c = go.AddComponent<ShowCrowd>();
            c._rt = rt;
            c.SetFans(0);
            return c;
        }

        /// <summary>Grows the crowd to match the fans earned so far this show. It never shrinks.</summary>
        public void SetFans(int fansThisShow)
        {
            if (_order == null) _order = Order();
            int want = CrowdModel.PeopleFor(fansThisShow);
            while (_members.Count < want) Add(_members.Count);
        }

        public void Cheer() { _cheerAt = Time.unscaledTime; }

        public void Sag()
        {
            if (MotionPrefs.Reduced) return;          // nothing moves under reduce motion
            _sagAt = Time.unscaledTime;
        }

        static int[] Order()
        {
            // a fixed shuffle (so the crowd fills in from all over the room, the same every show)
            var a = new int[Slots];
            for (int i = 0; i < Slots; i++) a[i] = i;
            var rng = new System.Random(7);
            for (int i = Slots - 1; i > 0; i--) { int j = rng.Next(i + 1); int t = a[i]; a[i] = a[j]; a[j] = t; }
            return a;
        }

        void Add(int i)
        {
            var go = new GameObject("Fan" + i, typeof(RectTransform), typeof(CanvasRenderer));
            go.layer = gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(.5f, 0f);
            rt.sizeDelta = new Vector2(CrowdPerson.Width, CrowdPerson.Height);
            var g = go.AddComponent<CrowdPerson>();
            g.raycastTarget = false;
            g.color = Color.white;
            Palette p = Theme.Palette;
            ColorRole[] fills = { ColorRole.Accent, ColorRole.Card, ColorRole.PaperDeep, ColorRole.Accent2 };
            g.Fill = Theme.ToColor(p[fills[i % fills.Length]]);
            g.Ink = Theme.ToColor(p[ColorRole.Ink]);
            var rng = new System.Random(31 + i);
            int slot = _order[i % Slots];
            var m = new Member
            {
                G = g, Rt = rt,
                X = (slot + .5f + ((float)rng.NextDouble() - .5f) * .5f) / Slots,
                Scale = .85f + (float)rng.NextDouble() * .3f,
                Lean = ((float)rng.NextDouble() - .5f) * 6f
            };
            _members.Add(m);
            Place(m, 0f, 1f, 0f);
            // the newest person pops in (not under reduce motion)
            if (!MotionPrefs.Reduced && i >= CrowdModel.Min) Feedback.Pop(rt);
        }

        void Place(Member m, float y, float scaleY, float lean)
        {
            float w = _rt.rect.width > 1f ? _rt.rect.width : 1366f;
            m.Rt.anchoredPosition = new Vector2(Mathf.Lerp(110f, w - 40f, m.X), y - 26f);   // 110 px in: the sound toggle sits in the strip's left corner         // feet well below the edge: heads and raised fists fit the 52 px strip
            m.Rt.localScale = new Vector3(m.Scale, m.Scale * scaleY, 1f);
            m.Rt.localRotation = Quaternion.Euler(0f, 0f, m.Lean + lean);
        }

        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        void Update() { Tick(); }

        /// <summary>One frame of the crowd's motion (Update calls it; tests call it directly).</summary>
        public void Tick()
        {
            float now = Time.unscaledTime;
            bool reduced = MotionPrefs.Reduced;
            float top = 0f;
            for (int i = 0; i < _members.Count; i++)
            {
                Member m = _members[i];
                float stagger = (i * 0.037f) % .22f;
                float cu = now - _cheerAt - stagger;
                m.G.ArmsUp = cu >= 0f && cu < ArmsHold ? 1f : 0f;
                if (reduced) { Place(m, 0f, 1f, 0f); continue; }
                float y = 0f, sy = 1f, lean = 0f;
                if (cu >= 0f && cu < HopSeconds)
                {
                    float s = Mathf.Sin(Mathf.PI * cu / HopSeconds);
                    y += s * 4f; sy += s * .04f;
                }
                float su = now - _sagAt;
                if (su >= 0f && su < SagSeconds + 0.8f)
                {
                    float k = su < SagSeconds ? Smooth(su / SagSeconds) : 1f - Smooth((su - SagSeconds) / 0.8f);
                    y -= k * 6f; sy -= k * .08f; lean += k * (i % 2 == 0 ? 3f : -3f);
                }
                top = Mathf.Max(top, Mathf.Abs(y));
                Place(m, y, sy, lean);
            }
            Motion = top;
        }
    }
}
