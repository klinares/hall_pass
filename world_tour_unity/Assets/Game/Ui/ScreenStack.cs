using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using WorldTour.Game.Motion;

namespace WorldTour.Game.Ui
{
    /// <summary>
    /// The screens of the Main scene, as a stack (PLAN.md: one scene, screens swap with transitions, no scene loads
    /// mid-flow). Push, Pop and Replace instantiate and destroy <see cref="GameScreen"/> prefabs under one container.
    /// A change has weight: the new screen slides in like paper (motion.js slideIn, 280 ms, from the right; from the
    /// left when going back) while the old one slides off in 160 ms. Input is never blocked: the old screen stops
    /// taking input the moment the change starts and the new one takes it at once. A change requested during a change
    /// finishes the running one instantly first, so the stack is always exact. Reduce motion turns it into a short fade.
    /// </summary>
    public sealed class ScreenStack : MonoBehaviour
    {
        [SerializeField] RectTransform container;

        readonly List<GameScreen> _stack = new List<GameScreen>();
        TweenHandle _in, _out;

        /// <summary>Raised after the top screen changes (the new top, or null when emptied).</summary>
        public event Action<GameScreen> TopChanged;

        public GameScreen Top { get { return _stack.Count > 0 ? _stack[_stack.Count - 1] : null; } }

        public int Count { get { return _stack.Count; } }

        RectTransform Container { get { return container != null ? container : (RectTransform)transform; } }

        /// <summary>Clears the stack (no animation) and shows <paramref name="prefab"/> as the only screen. Use once at start.</summary>
        public T SetRoot<T>(T prefab) where T : GameScreen
        {
            FinishRunning();
            for (int i = _stack.Count - 1; i >= 0; i--) Remove(_stack[i]);
            _stack.Clear();
            T s = Create(prefab);
            _stack.Add(s);
            s.OnPushed();
            Show(s, null, back: false, animate: false);
            TopChanged?.Invoke(s);
            return s;
        }

        /// <summary>Puts a new screen on top; the one below is kept (hidden) for <see cref="Pop"/>.</summary>
        public T Push<T>(T prefab) where T : GameScreen
        {
            FinishRunning();
            GameScreen below = Top;
            T s = Create(prefab);
            _stack.Add(s);
            s.OnPushed();
            Show(s, below, back: false, animate: true, destroyOutgoing: false);
            TopChanged?.Invoke(s);
            return s;
        }

        /// <summary>Removes the top screen and goes back to the one below. The last screen is never popped.</summary>
        public bool Pop()
        {
            FinishRunning();
            if (_stack.Count < 2) { Debug.LogWarning("[ui] the last screen can't be popped"); return false; }
            GameScreen leaving = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            GameScreen back = Top;
            Show(back, leaving, back: true, animate: true, destroyOutgoing: true);
            TopChanged?.Invoke(back);
            return true;
        }

        /// <summary>Swaps the top screen for a new one (show to ticket, for example); the old one is destroyed.</summary>
        public T Replace<T>(T prefab) where T : GameScreen
        {
            FinishRunning();
            GameScreen leaving = null;
            if (_stack.Count > 0) { leaving = _stack[_stack.Count - 1]; _stack.RemoveAt(_stack.Count - 1); }
            T s = Create(prefab);
            _stack.Add(s);
            s.OnPushed();
            Show(s, leaving, back: false, animate: true, destroyOutgoing: true);
            TopChanged?.Invoke(s);
            return s;
        }

        /// <summary>Pops back to the first screen (the map), destroying everything above it, with one transition.</summary>
        public void PopToRoot()
        {
            FinishRunning();
            if (_stack.Count < 2) return;
            GameScreen leaving = _stack[_stack.Count - 1];
            for (int i = _stack.Count - 2; i >= 1; i--) { Remove(_stack[i]); _stack.RemoveAt(i); }
            _stack.RemoveAt(_stack.Count - 1);
            Show(_stack[0], leaving, back: true, animate: true, destroyOutgoing: true);
            TopChanged?.Invoke(_stack[0]);
        }

        /// <summary>True while a change is still animating (input is not blocked meanwhile).</summary>
        public bool IsTransitioning { get { return _in.IsAlive || _out.IsAlive; } }

        // ------------------------------------------------------------------ internals

        T Create<T>(T prefab) where T : GameScreen
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            T s = Instantiate(prefab, Container, false);
            RectTransform rt = s.Rect;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one; rt.localRotation = Quaternion.identity;
            s.Stack = this;
            return s;
        }

        void Show(GameScreen incoming, GameScreen outgoing, bool back, bool animate, bool destroyOutgoing = false)
        {
            if (outgoing != null)
            {
                CanvasGroup og = outgoing.Group;
                og.interactable = false;
                og.blocksRaycasts = false;
                outgoing.OnHidden();
                GameScreen leaving = outgoing;
                Action done = () =>
                {
                    if (leaving == null) return;
                    if (destroyOutgoing) Remove(leaving);
                    else leaving.gameObject.SetActive(false);
                };
                if (animate) _out = Tweener.Play(leaving.Rect, back ? Presets.SlideOutBack : Presets.SlideOut, done);
                else done();
            }

            incoming.gameObject.SetActive(true);
            incoming.transform.SetAsLastSibling();
            CanvasGroup ig = incoming.Group;
            ig.interactable = true;
            ig.blocksRaycasts = true;
            if (animate) _in = Tweener.Play(incoming.Rect, back ? Presets.SlideInBack : Presets.SlideIn);
            else ig.alpha = 1f;
            incoming.OnShown();
            if (incoming.FirstSelected != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(incoming.FirstSelected.gameObject);
        }

        void FinishRunning()
        {
            _in.Complete();
            _out.Complete();
            _in = default;
            _out = default;
        }

        static void Remove(GameScreen s)
        {
            if (s == null) return;
            try { s.OnRemoved(); }
            catch (Exception e) { Debug.LogException(e, s); }
            Destroy(s.gameObject);
        }
    }
}
