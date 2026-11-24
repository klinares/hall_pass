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

        struct Item
        {
            public Renderer R;
            public Bounds B;
            public ShadowCastingMode Orig;
            public bool Hidden;
            public float Since;
        }
        readonly List<Item> _items = new List<Item>();
        float _nextScan = -1f;
        int _hiddenCount;

        /// <summary>How many things are drawn shadows-only right now (tests and the status line).</summary>
        public int HiddenCount { get { return _hiddenCount; } }
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
            if (target == null || frozen) return;
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

            if (Time.unscaledTime >= _nextScan) { Rescan(); _nextScan = Time.unscaledTime + 1.5f; }
            UpdateClear(transform.position, target.position);
        }

        /// <summary>Forget the glide speed (after the camera was moved by hand) and bring every hidden prop back.</summary>
        public void ResetVelocity()
        {
            vel = Vector3.zero; leadVel = Vector3.zero; leadNow = Vector3.zero;
        }

        /// <summary>The stage camera took over: put every prop back so the stage view shows the whole street.</summary>
        public void ShowAllOccluders()
        {
            for (int i = 0; i < Occluder.All.Count; i++) Occluder.All[i].Show();
            for (int i = 0; i < _items.Count; i++)
            {
                Item it = _items[i];
                if (it.Hidden) SetHidden(it, false);
                it.Hidden = false;
                _items[i] = it;
            }
            _hiddenCount = 0;
            _lift = 0f; _liftVel = 0f; _liftHold = 0f;
        }

        /// <summary>Editor previews and tests: put the lens where it would follow now (no glide, no lead), rescan and run the clear-view test once, with no lift.</summary>
        public void SnapForPreview()
        {
            if (target == null) return;
            ShowAllOccluders();
            transform.rotation = FollowRotation;
            transform.position = target.position + offset;
            Rescan();
            UpdateClear(transform.position, target.position);
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

        /// <summary>Hides a renderer: shadows-only when it normally casts shadows, else switched off (the markers cast none).</summary>
        static void SetHidden(Item it, bool hide)
        {
            if (it.R == null) return;
            if (it.Orig == ShadowCastingMode.Off) it.R.enabled = !hide;
            else it.R.shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : it.Orig;
        }

        void Rescan()
        {
            // put back whatever was hidden, scan, and let this frame's test hide again (all before the frame is drawn)
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Hidden) SetHidden(_items[i], false);
            var hiddenBefore = new HashSet<Renderer>();
            for (int i = 0; i < _items.Count; i++) if (_items[i].Hidden && _items[i].R != null) hiddenBefore.Add(_items[i].R);
            _items.Clear();
            _hiddenCount = 0;
            foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled || r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                Bounds b = r.bounds;
                if (!IsCandidate(b)) continue;
                string root = r.transform.root.name;
                if (root == "Player" || root == "Riff" || r.name.StartsWith("String")) continue;   // the string lights are one thin mesh spanning the street: never worth hiding
                var item = new Item { R = r, B = b, Orig = r.shadowCastingMode };   // crew and their markers too: nothing may stand in front of her
                if (hiddenBefore.Contains(r)) { item.Hidden = true; item.Since = Time.unscaledTime; SetHidden(item, true); }   // put back above, so hide it again at once
                _items.Add(item);
            }
        }

        void UpdateClear(Vector3 cam, Vector3 who)
        {
            Vector3 chest = who + Vector3.up * 1.0f, head = who + Vector3.up * 1.7f;
            float now = Time.unscaledTime;
            bool lensInside = false;
            int hidden = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                Item it = _items[i];
                if (it.R == null) continue;
                // hysteresis: a thing already hidden needs a wider clear gap (and 0.3 s) before it comes back
                float radius = it.Hidden ? clearRadius + 0.35f : clearRadius;
                bool block = Blocks(it.B, cam, chest, radius, 1.2f) || Blocks(it.B, cam, head, radius, 1.2f);
                if (!block && it.Hidden && now - it.Since < 0.3f) block = true;
                if (block != it.Hidden)
                {
                    it.Hidden = block;
                    it.Since = now;
                    SetHidden(it, block);
                    _items[i] = it;
                }
                if (it.Hidden)
                {
                    hidden++;
                    if (it.B.size.y > 2.5f) { Bounds e = it.B; e.Expand(1.6f); if (e.Contains(cam)) lensInside = true; }
                }
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
