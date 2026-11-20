using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldTour.Game.Motion
{
    /// <summary>A running motion. Default (no motion) is never alive; Complete and Kill on it do nothing.</summary>
    public readonly struct TweenHandle
    {
        internal readonly int Slot;
        internal readonly int Version;

        internal TweenHandle(int slot, int version) { Slot = slot; Version = version; }

        public bool IsAlive { get { return Tweener.IsAlive(this); } }

        /// <summary>Jumps to the end pose now and calls its done callback.</summary>
        public void Complete() { Tweener.Complete(this); }

        /// <summary>Stops it where it is, without the done callback. A preset puts the element back at rest.</summary>
        public void Kill() { Tweener.Kill(this); }
    }

    /// <summary>
    /// A tiny tweener for uGUI: keyframed presets (<see cref="Presets"/>) relative to an element's resting pose, and
    /// plain value tweens (move, scale, alpha, any float). One hidden runner object; slots are pooled, so a running
    /// game allocates nothing per tween once warm (callbacks you pass are your own allocations).
    /// Rules: unscaled time (pausing never freezes the UI); nothing here waits on or blocks input; starting a motion
    /// on a channel another motion of the same element is using takes over that element's resting pose and stops the
    /// old one, so motions never fight or drift; and every motion honours <see cref="MotionPrefs.Reduced"/>.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class Tweener : MonoBehaviour
    {
        enum Kind { Preset, Move, Scale, Alpha, Float }

        sealed class Slot
        {
            public bool Active, Fresh, Hold, Finished;
            public int Version;
            public Kind Kind;
            public RectTransform Target;
            public CanvasGroup Group;
            public UnityEngine.Object Owner;
            public bool HasOwner;
            public Channel Channels;
            public MotionPreset Preset;
            public CubicBezier Ease;
            public Vector3 From, To;
            public Action<float> OnValue;
            public Action OnDone;
            public float Duration, Elapsed;
            public Vector2 BasePos;
            public float BaseRot;
            public Vector3 BaseScale;
        }

        /// <summary>An element's resting pose, handed from a stopped preset to the next one.</summary>
        struct RestPose { public bool Has; public Vector2 Pos; public float Rot; public Vector3 Scale; }

        static Tweener s_Instance;
        readonly List<Slot> _slots = new List<Slot>(64);
        bool _stepping;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_Instance = null; }

        static Tweener Instance
        {
            get
            {
                if (s_Instance != null) return s_Instance;
                if (!Application.isPlaying) return null;
                var go = new GameObject("[Tweener]");
                go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                DontDestroyOnLoad(go);
                s_Instance = go.AddComponent<Tweener>();
                return s_Instance;
            }
        }

        // ------------------------------------------------------------------ presets

        /// <summary>
        /// Plays a keyframed motion on <paramref name="target"/>, relative to its resting pose, then puts it back at rest
        /// (alpha ends at the last keyframe's value, so Shrink and SlideOut end invisible).
        /// With reduce motion on, movement is dropped: a motion that ends hidden is applied at once, and any other
        /// motion becomes the 120 ms fade (or nothing, when <paramref name="fadeWhenReduced"/> is false).
        /// <paramref name="hold"/> keeps the last pose until another motion takes the element over (button press).
        /// </summary>
        public static TweenHandle Play(RectTransform target, MotionPreset preset, Action onDone = null, bool hold = false, bool fadeWhenReduced = true)
        {
            if (target == null || preset == null) { onDone?.Invoke(); return default; }

            if (MotionPrefs.Reduced)
            {
                float endAlpha = preset.Keys[preset.Keys.Length - 1].Alpha;
                bool endsHidden = (preset.Channels & Channel.Alpha) != 0 && endAlpha < 1f;
                if (endsHidden || !fadeWhenReduced)
                {
                    TakeOver(target, preset.Channels, out _);
                    if (endsHidden) GroupOf(target, true).alpha = endAlpha;
                    onDone?.Invoke();
                    return default;
                }
                preset = Presets.ReducedFade;
            }

            Tweener runner = Instance;
            if (runner == null)
            {
                // Edit mode: no runner, so land on the end state at once.
                TakeOver(target, preset.Channels, out _);
                if ((preset.Channels & Channel.Alpha) != 0) GroupOf(target, true).alpha = preset.Keys[preset.Keys.Length - 1].Alpha;
                onDone?.Invoke();
                return default;
            }

            TakeOver(target, preset.Channels, out RestPose inherited);
            Slot s = runner.Rent(out int index);
            s.Kind = Kind.Preset;
            s.Target = target;
            s.Owner = target; s.HasOwner = true;
            s.Group = (preset.Channels & Channel.Alpha) != 0 ? GroupOf(target, true) : null;
            s.Channels = preset.Channels;
            s.Preset = preset;
            s.Duration = preset.Duration;
            s.Hold = hold;
            s.OnDone = onDone;
            if (inherited.Has) { s.BasePos = inherited.Pos; s.BaseRot = inherited.Rot; s.BaseScale = inherited.Scale; }
            else { s.BasePos = target.anchoredPosition; s.BaseRot = target.localEulerAngles.z; s.BaseScale = target.localScale; }
            ApplyPose(s, preset.Sample(0f));
            return new TweenHandle(index, s.Version);
        }

        // ------------------------------------------------------------------ value tweens

        /// <summary>Moves anchoredPosition to <paramref name="to"/>. Instant with reduce motion on.</summary>
        public static TweenHandle MoveTo(RectTransform target, Vector2 to, float duration, CubicBezier ease, Action onDone = null)
        {
            if (target == null) { onDone?.Invoke(); return default; }
            TakeOver(target, Channel.Move, out _);
            if (MotionPrefs.Reduced || duration <= 0f || Instance == null) { target.anchoredPosition = to; onDone?.Invoke(); return default; }
            Slot s = Instance.Rent(out int i);
            s.Kind = Kind.Move; s.Target = target; s.Owner = target; s.HasOwner = true; s.Channels = Channel.Move;
            s.From = target.anchoredPosition; s.To = to; s.Duration = duration; s.Ease = ease; s.OnDone = onDone;
            return new TweenHandle(i, s.Version);
        }

        /// <summary>Scales to <paramref name="to"/>. Instant with reduce motion on.</summary>
        public static TweenHandle ScaleTo(RectTransform target, Vector3 to, float duration, CubicBezier ease, Action onDone = null)
        {
            if (target == null) { onDone?.Invoke(); return default; }
            TakeOver(target, Channel.Scale, out _);
            if (MotionPrefs.Reduced || duration <= 0f || Instance == null) { target.localScale = to; onDone?.Invoke(); return default; }
            Slot s = Instance.Rent(out int i);
            s.Kind = Kind.Scale; s.Target = target; s.Owner = target; s.HasOwner = true; s.Channels = Channel.Scale;
            s.From = target.localScale; s.To = to; s.Duration = duration; s.Ease = ease; s.OnDone = onDone;
            return new TweenHandle(i, s.Version);
        }

        /// <summary>Fades a CanvasGroup. With reduce motion on it still fades, but never longer than 120 ms (a fade is not movement).</summary>
        public static TweenHandle AlphaTo(CanvasGroup group, float to, float duration, Action onDone = null)
        {
            if (group == null) { onDone?.Invoke(); return default; }
            RectTransform rt = group.transform as RectTransform;
            if (rt != null) TakeOver(rt, Channel.Alpha, out _);
            if (MotionPrefs.Reduced) duration = Mathf.Min(duration, Presets.ReducedFade.Duration);
            if (duration <= 0f || Instance == null) { group.alpha = to; onDone?.Invoke(); return default; }
            Slot s = Instance.Rent(out int i);
            s.Kind = Kind.Alpha; s.Target = rt; s.Group = group; s.Owner = group; s.HasOwner = true; s.Channels = Channel.Alpha;
            s.From = new Vector3(group.alpha, 0f, 0f); s.To = new Vector3(to, 0f, 0f); s.Duration = duration; s.Ease = Ease.Linear; s.OnDone = onDone;
            return new TweenHandle(i, s.Version);
        }

        /// <summary>
        /// Any float, through <paramref name="onValue"/> (counters, material blends). With reduce motion on it jumps to
        /// <paramref name="to"/>, unless <paramref name="honourReduced"/> is false (a caller that reads the setting itself,
        /// like the mascot). If <paramref name="owner"/> is given and gets destroyed, the tween stops quietly.
        /// </summary>
        public static TweenHandle Float(float from, float to, float duration, CubicBezier ease, Action<float> onValue, Action onDone = null, UnityEngine.Object owner = null, bool honourReduced = true)
        {
            if ((honourReduced && MotionPrefs.Reduced) || duration <= 0f || Instance == null) { onValue?.Invoke(to); onDone?.Invoke(); return default; }
            Slot s = Instance.Rent(out int i);
            s.Kind = Kind.Float; s.Owner = owner; s.HasOwner = owner != null; s.Channels = Channel.None;
            s.From = new Vector3(from, 0f, 0f); s.To = new Vector3(to, 0f, 0f); s.Duration = duration; s.Ease = ease; s.OnValue = onValue; s.OnDone = onDone;
            onValue?.Invoke(from);
            return new TweenHandle(i, s.Version);
        }

        // ------------------------------------------------------------------ control

        public static bool IsAlive(TweenHandle h)
        {
            Slot s = Get(h);
            return s != null && !s.Finished;
        }

        public static void Complete(TweenHandle h)
        {
            Slot s = Get(h);
            if (s == null) return;
            Finish(s);
            if (!s.Hold) s_Instance.Free(s);
        }

        public static void Kill(TweenHandle h)
        {
            Slot s = Get(h);
            if (s == null) return;
            if (s.Kind == Kind.Preset && s.Target != null) Rest(s);
            s_Instance.Free(s);
        }

        /// <summary>Stops every motion on <paramref name="target"/>; presets put it back at rest. No done callbacks.</summary>
        public static void KillAll(RectTransform target)
        {
            if (s_Instance == null || target == null) return;
            foreach (Slot s in s_Instance._slots)
            {
                if (!s.Active || s.Target != target) continue;
                if (s.Kind == Kind.Preset) Rest(s);
                s_Instance.Free(s);
            }
        }

        /// <summary>Advances every motion by <paramref name="dt"/> seconds. Play-mode tests call this to step time.</summary>
        public static void Tick(float dt)
        {
            if (s_Instance != null) s_Instance.Step(dt);
        }

        void Update() { Step(Time.unscaledDeltaTime); }

        void Step(float dt)
        {
            _stepping = true;
            try { StepSlots(dt); }
            finally { _stepping = false; }
        }

        void StepSlots(float dt)
        {
            int n = _slots.Count;
            for (int i = 0; i < n; i++)
            {
                Slot s = _slots[i];
                if (!s.Active) continue;
                if (s.Fresh) { s.Fresh = false; continue; }
                if (s.HasOwner && s.Owner == null) { Free(s); continue; }
                if (s.Finished) continue;   // a held pose
                s.Elapsed += dt;
                if (s.Elapsed >= s.Duration)
                {
                    Finish(s);
                    if (!s.Hold && s.Active) Free(s);
                    continue;
                }
                Apply(s, s.Elapsed);
            }
        }

        // ------------------------------------------------------------------ internals

        static Slot Get(TweenHandle h)
        {
            if (s_Instance == null || h.Version == 0 || h.Slot < 0 || h.Slot >= s_Instance._slots.Count) return null;
            Slot s = s_Instance._slots[h.Slot];
            return s.Active && s.Version == h.Version ? s : null;
        }

        Slot Rent(out int index)
        {
            for (int i = 0; i < _slots.Count; i++)
                if (!_slots[i].Active) { index = i; return Init(_slots[i]); }
            var s = new Slot();
            _slots.Add(s);
            index = _slots.Count - 1;
            return Init(s);
        }

        Slot Init(Slot s)
        {
            // A motion started from a callback during a step waits for the next step, so it never skips its first frame.
            s.Active = true; s.Fresh = _stepping; s.Hold = false; s.Finished = false;
            s.Version++;
            if (s.Version == 0) s.Version = 1;
            s.Elapsed = 0f;
            return s;
        }

        void Free(Slot s)
        {
            s.Active = false; s.Finished = false;
            s.Target = null; s.Group = null; s.Owner = null; s.HasOwner = false;
            s.Preset = null; s.OnValue = null; s.OnDone = null;
        }

        /// <summary>
        /// Stops any running motion on <paramref name="target"/> that shares a channel, without its callback. If one was a
        /// preset, its resting pose is handed back so the next preset measures from rest, not from mid-motion.
        /// </summary>
        static void TakeOver(RectTransform target, Channel channels, out RestPose inheritedPreset)
        {
            inheritedPreset = default;
            if (s_Instance == null) return;
            foreach (Slot s in s_Instance._slots)
            {
                if (!s.Active || s.Target != target || (s.Channels & channels) == 0) continue;
                if (s.Kind == Kind.Preset)
                {
                    // Channels the old motion drove but the new one won't are put back at rest now.
                    Channel leftover = s.Channels & ~channels;
                    if (leftover != Channel.None) Rest(s, leftover);
                    if (!inheritedPreset.Has)
                        inheritedPreset = new RestPose { Has = true, Pos = s.BasePos, Rot = s.BaseRot, Scale = s.BaseScale };
                }
                s_Instance.Free(s);
            }
        }

        static void Finish(Slot s)
        {
            if (s.Finished) return;
            s.Finished = true;
            switch (s.Kind)
            {
                case Kind.Preset:
                    if (s.Target != null)
                    {
                        if (s.Hold) ApplyPose(s, s.Preset.Keys[s.Preset.Keys.Length - 1]);
                        else
                        {
                            Rest(s);
                            if (s.Group != null) s.Group.alpha = s.Preset.Keys[s.Preset.Keys.Length - 1].Alpha;
                        }
                    }
                    break;
                case Kind.Move: if (s.Target != null) s.Target.anchoredPosition = s.To; break;
                case Kind.Scale: if (s.Target != null) s.Target.localScale = s.To; break;
                case Kind.Alpha: if (s.Group != null) s.Group.alpha = s.To.x; break;
                case Kind.Float: s.OnValue?.Invoke(s.To.x); break;
            }
            Action done = s.OnDone;
            s.OnDone = null;
            if (done != null)
            {
                try { done(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        static void Apply(Slot s, float elapsed)
        {
            float t = s.Duration <= 0f ? 1f : elapsed / s.Duration;
            switch (s.Kind)
            {
                case Kind.Preset:
                    if (s.Target != null) ApplyPose(s, s.Preset.Sample(elapsed));
                    break;
                case Kind.Move:
                    if (s.Target != null) s.Target.anchoredPosition = Vector2.LerpUnclamped(s.From, s.To, s.Ease.Evaluate(t));
                    break;
                case Kind.Scale:
                    if (s.Target != null) s.Target.localScale = Vector3.LerpUnclamped(s.From, s.To, s.Ease.Evaluate(t));
                    break;
                case Kind.Alpha:
                    if (s.Group != null) s.Group.alpha = Mathf.Lerp(s.From.x, s.To.x, t);
                    break;
                case Kind.Float:
                    s.OnValue?.Invoke(Mathf.LerpUnclamped(s.From.x, s.To.x, s.Ease.Evaluate(t)));
                    break;
            }
        }

        static void ApplyPose(Slot s, Pose p)
        {
            RectTransform rt = s.Target;
            Channel c = s.Channels;
            // CSS y points down and CSS rotate() turns clockwise; Unity's y points up and +z turns counter-clockwise.
            if ((c & Channel.Move) != 0) rt.anchoredPosition = s.BasePos + new Vector2(p.X, -p.Y);
            if ((c & Channel.Rotate) != 0) rt.localEulerAngles = new Vector3(0f, 0f, s.BaseRot - p.Rot);
            if ((c & Channel.Scale) != 0) rt.localScale = new Vector3(s.BaseScale.x * p.Sx, s.BaseScale.y * p.Sy, s.BaseScale.z);
            if ((c & Channel.Alpha) != 0 && s.Group != null) s.Group.alpha = p.Alpha;
        }

        static void Rest(Slot s) { Rest(s, s.Channels); }

        static void Rest(Slot s, Channel c)
        {
            RectTransform rt = s.Target;
            if (rt == null) return;
            if ((c & Channel.Move) != 0) rt.anchoredPosition = s.BasePos;
            if ((c & Channel.Rotate) != 0) rt.localEulerAngles = new Vector3(0f, 0f, s.BaseRot);
            if ((c & Channel.Scale) != 0) rt.localScale = s.BaseScale;
            if ((c & Channel.Alpha) != 0 && s.Group != null) s.Group.alpha = 1f;
        }

        static CanvasGroup GroupOf(RectTransform rt, bool add)
        {
            CanvasGroup g = rt.GetComponent<CanvasGroup>();
            if (g == null && add) g = rt.gameObject.AddComponent<CanvasGroup>();
            return g;
        }
    }
}
