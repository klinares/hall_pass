using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldTour.World
{
    /// <summary>
    /// Friendly third-person view (phase 8.1 look pass): a fixed rotation, about 30 degrees down, tilted a little back up so she sits in
    /// the lower half of the frame and the fronts, awnings and sky above her read. The position glides after her and leans a short way
    /// into the direction she walks.
    /// Fix round 1: she must be visible everywhere. Every frame, any renderer (tree, sign, cart, awning, wall, crowd figure) that stands
    /// between the lens and her (a capsule around the lens-to-her lines, tested against cached bounds) is drawn shadows-only until the way is clear,
    /// and when the lens itself is inside a tall thing (a wall, a tree) the camera rises and backs off a little while it is.
    /// </summary>
    public class FollowCamera : MonoBehaviour
    {
        public Transform target;
        [Tooltip("From her feet to the lens. Height over distance gives the pitch (6.6 / 11.4 = 30 degrees).")]
        public Vector3 offset = new Vector3(0, 6.6f, -11.4f);
        [Tooltip("Extra tilt back up, degrees, so the view reaches the sky and she sits below the middle.")]
        public float tiltUp = 7f;
        public float smoothTime = 0.32f;
        [Tooltip("How far the camera leans ahead of her while she walks, metres. Kept short: the lead pulls her toward the bottom edge.")]
        public float lead = 1.2f;
        [Tooltip("Phase 8.1: while true another script (the stage view) drives the camera and this one leaves it alone.")]
        public bool frozen;
        [Tooltip("Radius of the clear tube between the lens and her, metres.")]
        public float clearRadius = 0.9f;
        [Tooltip("How much the lens rises (and 0.6 of it backs off) when it is inside something tall, metres.")]
        public float liftAmount = 2.6f;

        Vector3 vel, leadVel, leadNow;
        PlayerController _pc;
        float _lift, _liftVel, _liftHold;

        /// <summary>One renderer the camera keeps clear of her. Kept between rescans (a class, found again by renderer), so a rescan never resets a fade or a hold.</summary>
        sealed class Item
        {
            public Renderer R;
            public Bounds B;
            public OccluderFade Fade;
            public bool Hidden;      // the line to her is blocked by this one
            public float Since = -999f;   // when Hidden last changed
            public int Order;        // when it was first seen: the list keeps this order between rescans, so the attached indices stay valid
            public int[] Attached;   // indices of the thin pieces (windows, posters, stripes) fixed to this one; null until first needed
        }
        bool[] _mark = new bool[0];
        readonly List<Item> _items = new List<Item>();
        readonly Dictionary<Renderer, Item> _byRenderer = new Dictionary<Renderer, Item>();
        FadeBank _bank;
        int _nextOrder;
        // reused by every rescan, so a scan allocates only what Unity's own FindObjects hands back (less to collect, fewer stalls)
        readonly HashSet<Renderer> _seen = new HashSet<Renderer>();
        readonly List<Item> _fresh = new List<Item>();
        readonly List<Renderer> _drop = new List<Renderer>();
        float _nextScan = -1f;
        int _hiddenCount;
        /// <summary>Seconds between looks at the scene for new or moved props. A scan walks every renderer, so it is not done often; the fades never depend on it.</summary>
        const float RescanSeconds = 3f;
        /// <summary>A prop that has just hidden (or come back) stays that way at least this long, so nothing at the edge of the test can flicker.</summary>
        const float HoldSeconds = 0.6f;

        /// <summary>How many things are drawn shadows-only right now (tests and the status line).</summary>
        public int HiddenCount { get { return _hiddenCount; } }
        /// <summary>Test only: the names of up to four things that are hidden now (so a browser test can say which one toggled).</summary>
        public string HiddenNames()
        {
            var sb = new System.Text.StringBuilder();
            int n = 0;
            for (int i = 0; i < _items.Count && n < 4; i++)
                if (_items[i].Hidden && _items[i].R != null) { if (n++ > 0) sb.Append(','); sb.Append(_items[i].R.name).Append('@').Append(_items[i].R.transform.root.name); }
            return sb.ToString();
        }
        /// <summary>The extra height the lens has now.</summary>
        public float Lift { get { return _lift; } }

        Vector3 CurrentOffset { get { return offset + new Vector3(0f, _lift, -_lift * 0.6f); } }

        /// <summary>Where this camera sits when it follows the target now (no lead: used when handing the camera back).</summary>
        public Vector3 FollowPosition { get { return target != null ? target.position + offset : transform.position; } }
        /// <summary>The fixed follow rotation.</summary>
        public Quaternion FollowRotation { get { return RotationFor(offset); } }

        Quaternion RotationFor(Vector3 off) { return Quaternion.LookRotation(-off, Vector3.up) * Quaternion.Euler(-tiltUp, 0f, 0f); }

        void Start()
        {
            transform.rotation = FollowRotation;
            if (target != null) { transform.position = target.position + offset; _pc = target.GetComponent<PlayerController>(); }
        }

        void LateUpdate()
        {
            if (target == null) return;
            StepFades();
            if (frozen) return;
            if (_pc == null) _pc = target.GetComponent<PlayerController>();
            Vector3 want = Vector3.zero;
            if (_pc != null && _pc.planarSpeed > 0.3f)
            {
                Vector3 f = target.forward; f.y = 0f;
                want = f.normalized * (lead * Mathf.Clamp01(_pc.planarSpeed / 4f));
            }
            leadNow = Vector3.SmoothDamp(leadNow, want, ref leadVel, 0.6f);
            Vector3 off = CurrentOffset;
            transform.position = Vector3.SmoothDamp(transform.position, target.position + leadNow + off, ref vel, smoothTime);
            transform.rotation = RotationFor(off);

            bool first = _nextScan < 0f;   // the first look at a new place: what already stands in her way is simply not drawn, nothing fades in front of her on arrival
            if (Time.unscaledTime >= _nextScan) { Rescan(); _nextScan = Time.unscaledTime + RescanSeconds; }
            UpdateClear(transform.position, target.position);
            if (first)
                for (int i = 0; i < _items.Count; i++)
                    if (_items[i].Fade != null) _items[i].Fade.Snap(!(_items[i].Hidden || _mark[i]));
            if (first) WarmTwins();
        }

        /// <summary>Forget the glide speed (after the camera was moved by hand) and bring every hidden prop back.</summary>
        public void ResetVelocity()
        {
            vel = Vector3.zero; leadVel = Vector3.zero; leadNow = Vector3.zero;
        }

        /// <summary>The stage camera took over: every prop fades back in so the stage view shows the whole street (at once with <paramref name="instant"/>: a reset, a preview).</summary>
        public void ShowAllOccluders(bool instant = false)
        {
            for (int i = 0; i < Occluder.All.Count; i++) Occluder.All[i].Show();
            for (int i = 0; i < _items.Count; i++)
            {
                Item it = _items[i];
                it.Hidden = false;
                it.Since = Time.unscaledTime;
                if (it.Fade != null) { if (instant) it.Fade.Snap(true); else it.Fade.Set(true); }
            }
            _hiddenCount = 0;
            _lift = 0f; _liftVel = 0f; _liftHold = 0f;
        }

        /// <summary>
        /// The web player builds a shader program the first time something is drawn with it, which can stall a frame. The first fade in a place would pay for that, in front of her,
        /// so one tiny quad per kind of twin material is drawn for a moment right at the start (the arrival still hides the street). Nothing is seen: they are a centimetre wide and almost clear.
        /// </summary>
        void WarmTwins()
        {
            if (_bank == null || _bank.twins.Length == 0) return;
            var seen = new HashSet<string>();
            var warm = new List<Material>();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].R == null) continue;
                foreach (Material m in _items[i].R.sharedMaterials)
                {
                    Material twin = _bank.TwinOf(m);
                    if (twin == null) continue;
                    sb.Clear();
                    sb.Append(twin.shader.name);
                    foreach (LocalKeyword k in twin.enabledKeywords) sb.Append(' ').Append(k.name);
                    if (seen.Add(sb.ToString())) warm.Add(twin);
                }
            }
            if (warm.Count == 0) return;
            var mesh = new Mesh { name = "fade_warm_quad" };
            mesh.vertices = new[] { new Vector3(-.5f, -.5f, 0f), new Vector3(.5f, -.5f, 0f), new Vector3(.5f, .5f, 0f), new Vector3(-.5f, .5f, 0f) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var root = new GameObject("FadeWarm");
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < warm.Count; i++)
            {
                var q = new GameObject("w" + i);
                q.transform.SetParent(root.transform, false);
                q.transform.position = transform.position + transform.forward * 3f + transform.right * (i % 9 - 4) * 0.03f + transform.up * (i / 9) * 0.03f;
                q.transform.rotation = transform.rotation;
                q.transform.localScale = Vector3.one * 0.01f;
                q.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = q.AddComponent<MeshRenderer>();
                r.sharedMaterial = warm[i];
                r.shadowCastingMode = ShadowCastingMode.Off;
                block.Clear();
                block.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.003f));
                r.SetPropertyBlock(block);
            }
            Destroy(root, 0.3f);
            Destroy(mesh, 0.4f);
        }

        void StepFades()
        {
            float dt = Time.unscaledDeltaTime;
            int busy = 0; float lo = 1f, hi = 0f;
            for (int i = 0; i < _items.Count; i++)
            {
                OccluderFade f = _items[i].Fade;
                if (f == null || !f.Busy) continue;
                f.Step(dt);
                if (LogFades) { busy++; lo = Mathf.Min(lo, f.Alpha); hi = Mathf.Max(hi, f.Alpha); }
            }
            if (busy > 0) Debug.Log("WT_OCC_FADE frame=" + Time.frameCount + " t=" + Time.unscaledTime.ToString("0.000") + " busy=" + busy + " min=" + lo.ToString("0.000") + " max=" + hi.ToString("0.000"));
        }

        /// <summary>Test only (the page has ?wt_test): log the fade's alpha every frame it moves, so the browser test can count the steps.</summary>
        public static bool LogFades;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLogFades() { LogFades = false; }

        /// <summary>Editor previews and tests: put the lens where it would follow now (no glide, no lead), rescan and run the clear-view test once, with no lift.</summary>
        public void SnapForPreview()
        {
            if (target == null) return;
            ShowAllOccluders(true);
            transform.rotation = FollowRotation;
            transform.position = target.position + offset;
            Rescan();
            UpdateClear(transform.position, target.position);
            for (int i = 0; i < _items.Count; i++) if (_items[i].Fade != null) _items[i].Fade.Snap(!(_items[i].Hidden || _mark[i]));
            _lift = 0f;
        }

        /// <summary>Tests: what the camera does every 1.5 s while playing: rescan (which puts back and re-hides what was hidden) and test again.</summary>
        public void RefreshForTest()
        {
            if (target == null) return;
            Rescan();
            UpdateClear(transform.position, target.position);
        }

        // ---------- keeping her visible ----------

        /// <summary>
        /// True when the box stands in the tube of the given radius between the lens and the point it looks at: the lens is inside it, or the line from the lens
        /// enters it before the last <paramref name="endClear"/> metres (so things at her own feet and the ground do not count).
        /// </summary>
        public static bool Blocks(Bounds box, Vector3 cam, Vector3 to, float radius, float endClear)
        {
            box.Expand(radius * 2f);
            Vector3 d = to - cam;
            float len = d.magnitude;
            if (len < 0.01f) return false;
            if (box.Contains(cam)) return true;
            if (box.Contains(to)) return false;
            if (!box.IntersectRay(new Ray(cam, d / len), out float dist)) return false;
            return dist < len - endClear;
        }

        /// <summary>Whether a renderer with these bounds is a candidate at all: its top is above the counters and hips (a thin awning high up counts, a floor, kerb or tabletop does not) and it is not the whole map.</summary>
        public static bool IsCandidate(Bounds b)
        {
            return b.max.y >= 1.0f && b.size.x < 80f && b.size.z < 80f;
        }

        /// <summary>How long the last <see cref="Rescan"/> took, in ms (read by the test-only HitchProbe).</summary>
        public static float LastRescanMs;

        void Rescan()
        {
            float t0 = Time.realtimeSinceStartup;
            // Nothing is put back and re-hidden: the fade state stays with each renderer, and the scan only updates bounds and finds new and gone ones.
            if (_bank == null) _bank = FadeBank.Load();
            bool added = false;
            var seen = _seen; seen.Clear();
            var fresh = _fresh; fresh.Clear();
            foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                Item it;
                bool tracked = _byRenderer.TryGetValue(r, out it);
                if (!tracked && (!r.enabled || r.shadowCastingMode == ShadowCastingMode.ShadowsOnly)) continue;
                Bounds b = r.bounds;
                // a tracked prop that has not moved stays exactly as it was (its size drifting across the candidate limit must not drop it and bring it back)
                if (tracked && (b.center - it.B.center).sqrMagnitude < 0.75f * 0.75f) { seen.Add(r); fresh.Add(it); continue; }
                if (!IsCandidate(b)) continue;
                string root = r.transform.root.name;
                if (root == "Player" || root == "Riff" || r.name.StartsWith("String")) continue;   // the string lights are one thin mesh spanning the street: never worth hiding
                if (!tracked)
                {
                    it = new Item { R = r, Order = _nextOrder++ }; added = true;   // (its fade is made the first time it has to hide) crew and their markers too: nothing may stand in front of her
                    _byRenderer[r] = it;
                }
                // a thing that sways or bobs (a marker, a crowd figure) keeps the room it has swung through, so its bounds do not creep back and forth across the edge of the test
                // (kept exactly as first seen: the test result and the attached pieces then never change while nothing really moved; a prop that moved over 0.75 m gets its new bounds)
                it.B = b; it.Attached = null;
                seen.Add(r);
                fresh.Add(it);
            }
            // gone or no longer a candidate: put it back as it was and forget it
            var drop = _drop; drop.Clear();
            foreach (var kv in _byRenderer) if (!seen.Contains(kv.Key)) drop.Add(kv.Key);
            for (int i = 0; i < drop.Count; i++)
            {
                Item it = _byRenderer[drop[i]];
                if (it.Fade != null && drop[i] != null) it.Fade.Release();
                _byRenderer.Remove(drop[i]);
            }
            fresh.Sort((a, b) => a.Order.CompareTo(b.Order));
            if (added || drop.Count > 0) foreach (Item it in fresh) it.Attached = null;   // the indices moved
            _items.Clear();
            _items.AddRange(fresh);
            LastRescanMs = (Time.realtimeSinceStartup - t0) * 1000f;
        }

        static readonly int[] None = new int[0];

        /// <summary>
        /// The thin pieces (under 0.35 m in some direction: windows, window frames, posters, stripes, a star) that lie inside this renderer's bounds grown by 0.8 m, when it
        /// is itself a wall or pier (at least 3 m tall, under 2.5 m thick). Found once per scan.
        /// </summary>
        int[] AttachedTo(int index)
        {
            Item host = _items[index];
            if (host.Attached != null) return host.Attached;
            var found = new List<int>();
            Vector3 hs = host.B.size;
            bool wall = hs.y >= 3f && Mathf.Min(hs.x, hs.z) < 2.5f;
            if (wall)
            {
                Bounds big = host.B; big.Expand(1.6f);
                for (int j = 0; j < _items.Count; j++)
                {
                    if (j == index || _items[j].R == null) continue;
                    Bounds b = _items[j].B;
                    Vector3 sz = b.size;
                    if (Mathf.Min(sz.x, Mathf.Min(sz.y, sz.z)) > 0.35f) continue;
                    if (sz.magnitude >= hs.magnitude) continue;
                    if (big.Contains(b.min) && big.Contains(b.max)) found.Add(j);
                }
            }
            host.Attached = found.Count > 0 ? found.ToArray() : None;
            return host.Attached;
        }

        void UpdateClear(Vector3 cam, Vector3 who)
        {
            Vector3 chest = who + Vector3.up * 1.0f, head = who + Vector3.up * 1.7f;
            float now = Time.unscaledTime;
            // where the lens would be with no lift: the lift is decided from this (never from where it is now), so raising the lens cannot change the answer and start a bob
            Vector3 flat = cam - (CurrentOffset - offset);
            bool lensInside = false;
            for (int i = 0; i < _items.Count; i++)
            {
                Item it = _items[i];
                if (it.R == null) continue;
                // hysteresis: a thing already hidden needs a wider clear gap, and every change holds for HoldSeconds
                // (a grown box can swallow her and so stop blocking, which a small one does not: so a hidden prop stays hidden while EITHER size blocks. Testing only the grown one made it come back, then hide, then come back, every 1.2 s)
                bool block = Blocks(it.B, cam, chest, clearRadius, 1.2f) || Blocks(it.B, cam, head, clearRadius, 1.2f);
                if (!block && it.Hidden) block = Blocks(it.B, cam, chest, clearRadius + 0.35f, 1.2f) || Blocks(it.B, cam, head, clearRadius + 0.35f, 1.2f);
                if (block != it.Hidden && now - it.Since < HoldSeconds) block = it.Hidden;
                if (block != it.Hidden) { it.Hidden = block; it.Since = now; }
                if (it.B.size.y > 2.5f)
                {
                    Bounds e = it.B; e.Expand(1.6f);
                    if (e.Contains(flat) && (Blocks(it.B, flat, chest, clearRadius, 1.2f) || Blocks(it.B, flat, head, clearRadius, 1.2f))) lensInside = true;
                }
            }
            // a hidden wall takes its windows, posters and stripes with it (thin pieces fixed to it), so none is left floating
            if (_mark.Length < _items.Count) _mark = new bool[_items.Count];
            for (int i = 0; i < _items.Count; i++) _mark[i] = false;
            for (int i = 0; i < _items.Count; i++)
            {
                if (!_items[i].Hidden) continue;
                int[] att = AttachedTo(i);
                for (int a = 0; a < att.Length; a++) _mark[att[a]] = true;
            }
            int hidden = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                Item it = _items[i];
                if (it.R == null) continue;
                bool gone = it.Hidden || _mark[i];
                if (gone && it.Fade == null) it.Fade = new OccluderFade(it.R, _bank);
                if (it.Fade != null) it.Fade.Set(!gone);
                if (gone) hidden++;
            }
            _hiddenCount = hidden;
            // the lens is inside or hard against something tall (a wall, a tree): rise and back off a little until it has been clear for a moment
            if (lensInside) _liftHold = now + 1.2f;
            float wantLift = now < _liftHold ? liftAmount : 0f;
            _lift = Mathf.SmoothDamp(_lift, wantLift, ref _liftVel, 0.7f);
            if (Mathf.Abs(_lift) < 0.001f && wantLift == 0f) _lift = 0f;
        }
    }
}
