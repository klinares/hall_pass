using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WorldTour.Game.Mascot
{
    /// <summary>
    /// riff in uGUI, built from the exported layer sprites (DogArt). Put it on a square RectTransform; the dog fills
    /// its width (190 at 1920x1080 for the show coach). Layers are drawn in the manifest's stack order (every _ink,
    /// then every _pad, then the art in dog.js order), so the sticker border wraps the whole dog as in the web.
    /// Idle life (tail wag, blinks, zzz, a small bob) runs on unscaled time; reactions (hop, wobble) run through
    /// the injected ITweener. Nothing here takes raycasts, so the dog never blocks a click.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class DogRig : MonoBehaviour
    {
        [SerializeField] DogArt art;
        [SerializeField] DogPose pose = DogPose.Idle;
        [SerializeField] string outfit = "";

        ITweener _tweener;
        IMotionSettings _motion;
        ITweenHandle _reactionHandle;
        DogOffset _reaction = DogOffset.Rest;
        bool _warnedNoTweener;

        RectTransform _motionRoot, _frame;
        CanvasGroup _group;
        readonly Dictionary<string, Image> _pool = new Dictionary<string, Image>();
        readonly List<Image> _active = new List<Image>();
        readonly List<Image> _tail = new List<Image>();
        Image _lids;
        readonly List<Image>[] _z = { new List<Image>(), new List<Image>(), new List<Image>() };
        bool _built;

        public DogPose Pose => pose;
        public string Outfit => outfit;
        public DogArt Art => art;
        bool Reduced => _motion != null && _motion.ReduceMotion;

        /// <summary>Hand over the art and the 2.2 motion services. Safe to call again.</summary>
        public void Init(DogArt dogArt, ITweener tweener, IMotionSettings motion)
        {
            if (dogArt != null) art = dogArt;
            _tweener = tweener;
            _motion = motion;
            Rebuild();
        }

        void Awake() { if (art != null && !_built) Rebuild(); }

        public void SetPose(DogPose p) { if (p == pose && _built) return; pose = p; Rebuild(); }
        public void SetOutfit(string o) { o = o ?? ""; if (o == outfit && _built) return; outfit = o; Rebuild(); }
        public void Show(DogPose p, string o) { pose = p; outfit = o ?? ""; Rebuild(); }

        /// <summary>Play a reaction (it replaces any running one). Under reduce-motion it is a 120 ms fade only.</summary>
        public void Play(DogMotion m)
        {
            if (m == DogMotion.None) return;
            _reactionHandle?.Cancel();
            _reaction = DogOffset.Rest;
            if (_tweener == null)
            {
                if (!_warnedNoTweener) { Debug.LogWarning("DogRig: no ITweener set, reactions are skipped (call Init)."); _warnedNoTweener = true; }
                return;
            }
            bool reduced = Reduced;
            float d = (float)DogTiming.Duration(m, reduced);
            _reactionHandle = _tweener.Run(d, p => _reaction = DogTiming.Reaction(m, p * d, reduced), () => { _reaction = DogOffset.Rest; _reactionHandle = null; });
        }

        void EnsureRoots()
        {
            if (_motionRoot != null) return;
            var m = new GameObject("motion", typeof(RectTransform), typeof(CanvasGroup));
            _motionRoot = (RectTransform)m.transform;
            _motionRoot.SetParent(transform, false);
            _motionRoot.anchorMin = Vector2.zero; _motionRoot.anchorMax = Vector2.one;
            _motionRoot.offsetMin = Vector2.zero; _motionRoot.offsetMax = Vector2.zero;
            _motionRoot.pivot = new Vector2(.5f, .5f);                 // CSS transform-origin: centre of the dog host
            _group = m.GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false; _group.interactable = false;

            var f = new GameObject("frame", typeof(RectTransform));
            _frame = (RectTransform)f.transform;
            _frame.SetParent(_motionRoot, false);
            _frame.anchorMin = _frame.anchorMax = new Vector2(0, 1);
            _frame.pivot = new Vector2(0, 1);
            _frame.anchoredPosition = Vector2.zero;
        }

        void FitFrame()
        {
            if (_frame == null || art == null) return;
            _frame.sizeDelta = art.framePx;
            float w = ((RectTransform)transform).rect.width;
            float s = art.framePx.x > 0 && w > 0 ? w / art.framePx.x : 1f;
            _frame.localScale = new Vector3(s, s, 1);
        }

        void OnRectTransformDimensionsChange() => FitFrame();

        Image Get(DogArt.Layer l)
        {
            if (_pool.TryGetValue(l.name, out var img) && img != null) return img;
            var go = new GameObject(l.name, typeof(RectTransform), typeof(Image));
            img = go.GetComponent<Image>();
            img.sprite = WorldTour.Game.Ui.ThemeSkin.For(l.sprite);   // the live theme's twin (peach + navy riff)
            img.raycastTarget = false;
            img.type = Image.Type.Simple;
            var rt = (RectTransform)go.transform;
            rt.SetParent(_frame, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = l.size;
            _pool[l.name] = img;
            return img;
        }

        static Vector2 BasePos(DogArt.Layer l) => new Vector2(l.offset.x, -l.offset.y);

        /// <summary>Show the layers for the current pose and outfit, in stack order, at their manifest offsets.</summary>
        public void Rebuild()
        {
            if (art == null) return;
            EnsureRoots();
            FitFrame();
            foreach (var img in _active) if (img != null) img.gameObject.SetActive(false);
            _active.Clear(); _tail.Clear(); _lids = null;
            foreach (var z in _z) z.Clear();
            string[] stack = art.StackFor(pose, outfit);
            for (int i = 0; i < stack.Length; i++)
            {
                var l = art.Find(stack[i]);
                if (l == null || l.sprite == null) { Debug.LogWarning("DogRig: missing layer " + stack[i]); continue; }
                var img = Get(l);
                var rt = img.rectTransform;
                rt.anchoredPosition = BasePos(l);
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;
                img.color = Color.white;
                // resolved on every pose change, so a layer first shown after a theme switch is never the other theme's (no flicker)
                img.sprite = WorldTour.Game.Ui.ThemeSkin.For(l.sprite);
                img.gameObject.SetActive(true);
                rt.SetSiblingIndex(_active.Count);
                _active.Add(img);
                if (pose != DogPose.Sleepy && l.part == "tail") _tail.Add(img);
                else if (l.part == "lids") _lids = img;
                else if (l.part == "z1") _z[0].Add(img);
                else if (l.part == "z2") _z[1].Add(img);
                else if (l.part == "z3") _z[2].Add(img);
            }
            _built = true;
            Tick(Time.unscaledTime);
        }

        void Update() { if (_built) Tick(Time.unscaledTime); }

        void Tick(float time)
        {
            if (art == null || _motionRoot == null) return;
            bool reduced = Reduced;

            // whole dog: reaction plus the idle bob (no bob while napping)
            double bob = pose == DogPose.Sleepy ? 0 : DogTiming.BobY(time, reduced);
            var r = _reaction;
            _motionRoot.anchoredPosition = new Vector2((float)r.X, -(float)(r.Y + bob));   // CSS y down -> uGUI y up
            _motionRoot.localRotation = Quaternion.Euler(0, 0, -(float)r.Rotation);       // CSS clockwise -> uGUI negative z
            _motionRoot.localScale = new Vector3((float)r.ScaleX, (float)r.ScaleY, 1);
            _group.alpha = (float)r.Alpha;

            // tail wag about the manifest's wag point (dog.js units 103, 128)
            if (_tail.Count > 0)
            {
                float deg = (float)DogTiming.TailDegrees(time, reduced);
                var pivot = new Vector2(art.tailPivotPx.x, -art.tailPivotPx.y);
                var rot = Quaternion.Euler(0, 0, -deg);
                foreach (var img in _tail)
                {
                    var l = art.Find(img.name);
                    var rt = img.rectTransform;
                    rt.anchoredPosition = pivot + (Vector2)(rot * (BasePos(l) - pivot));
                    rt.localRotation = rot;
                }
            }

            // blink: scaleY about the eyes' centre line; 0 = hidden
            if (_lids != null)
            {
                float s = (float)DogTiming.LidScale(time, reduced);
                var l = art.Find(_lids.name);
                var b = BasePos(l);
                float py = -art.lidPivotPx.y;
                _lids.rectTransform.anchoredPosition = new Vector2(b.x, py + s * (b.y - py));
                _lids.rectTransform.localScale = new Vector3(1, s, 1);
            }

            // zzz: each z with its ink and pad drifts and fades together
            for (int i = 0; i < 3; i++)
            {
                if (_z[i].Count == 0) continue;
                var o = DogTiming.Zzz(time, i, reduced);
                var d = new Vector2((float)(o.X * art.unitsToPx), -(float)(o.Y * art.unitsToPx));
                foreach (var img in _z[i])
                {
                    img.rectTransform.anchoredPosition = BasePos(art.Find(img.name)) + d;
                    var c = img.color; c.a = (float)o.Alpha; img.color = c;
                }
            }
        }

        void OnDisable() { _reactionHandle?.Cancel(); _reactionHandle = null; _reaction = DogOffset.Rest; }
    }
}
