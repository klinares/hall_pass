using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldTour.Game.Show
{
    /// <summary>
    /// Delayed actions for the show (ctx.later, ctx.next), on unscaled time, run by one hidden object that lives for the
    /// whole session. Unlike a coroutine on the screen, a timer here can't be dropped because the screen was deactivated
    /// or disabled for a moment; the caller's own guard decides whether it still applies when it lands. An action that
    /// throws is logged and the rest still run.
    /// </summary>
    public sealed class ShowTimers : MonoBehaviour
    {
        private struct Timer { public float Due; public long Order; public Action Action; }

        private static ShowTimers s_Instance;
        private readonly List<Timer> _timers = new List<Timer>();
        private readonly List<Timer> _due = new List<Timer>();
        private long _order;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_Instance = null; }

        private static ShowTimers Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    var go = new GameObject("ShowTimers");
                    go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                    if (Application.isPlaying) DontDestroyOnLoad(go);
                    s_Instance = go.AddComponent<ShowTimers>();
                }
                return s_Instance;
            }
        }

        /// <summary>Runs <paramref name="action"/> after <paramref name="seconds"/> of real time (on the next frame for 0).</summary>
        public static void After(float seconds, Action action)
        {
            if (action == null) return;
            ShowTimers t = Instance;
            t._timers.Add(new Timer { Due = Time.unscaledTime + Mathf.Max(0, seconds), Order = t._order++, Action = action });
        }

        /// <summary>Timers waiting (for tests and diagnostics).</summary>
        public static int Pending { get { return s_Instance != null ? s_Instance._timers.Count : 0; } }

        private void Update()
        {
            if (_timers.Count == 0) return;
            float now = Time.unscaledTime;
            _due.Clear();
            for (int i = _timers.Count - 1; i >= 0; i--)
                if (_timers[i].Due <= now) { _due.Add(_timers[i]); _timers.RemoveAt(i); }
            if (_due.Count == 0) return;
            _due.Sort((a, b) => a.Due != b.Due ? a.Due.CompareTo(b.Due) : a.Order.CompareTo(b.Order));
            foreach (Timer t in _due)
            {
                try { t.Action(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
