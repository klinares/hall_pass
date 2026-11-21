using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldTour.Core.Progress;
using WorldTour.Game.Util;

namespace WorldTour.Game.Save
{
    /// <summary>
    /// The web game's play-time timer (progress.js:107-117), which the manager's office reads as "time played": every 15 s
    /// while the page is visible and she has pressed a key or the mouse in the last minute, 15 s go to today in the save
    /// (ProgressRules.AddPlayTime, which keeps the newest 120 days), and the save is written. Visibility is
    /// Application.isFocused: a hidden tab or a window in the background stops counting (the web counts a visible page in
    /// a background window too, but then she can't be pressing anything in it). MainRoot adds one to the Main scene.
    /// Logs "WT_PLAYTIME day=2026-11-20 secs=45" for each credited step.
    /// </summary>
    public sealed class PlayTimer : MonoBehaviour
    {
        public const string Log = "WT_PLAYTIME";

        /// <summary>The save to credit (Boot's by default).</summary>
        public Func<SaveStore> StoreSource = () => Boot.Store;
        /// <summary>Seconds on a steady clock (tests swap in a fake one).</summary>
        public Func<float> Clock = () => Time.realtimeSinceStartup;
        public Func<bool> Visible = () => Application.isFocused;
        public Func<string> Today = () => LocalClock.TodayIso();

        readonly PlayClock _clock = new PlayClock();

        void Start() { if (!_clock.Started) _clock.Start(Clock()); }

        /// <summary>Starts counting again from now (tests, after swapping the clock).</summary>
        public void Restart() { _clock.Start(Clock()); }

        /// <summary>Counts as a press now.</summary>
        public void NoteInput() { _clock.Input(Clock()); }

        void Update()
        {
            Mouse m = Mouse.current;
            Keyboard k = Keyboard.current;
            if ((m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame || m.middleButton.wasPressedThisFrame))
                || (k != null && k.anyKey.wasPressedThisFrame))
                NoteInput();
            Step();
        }

        /// <summary>Credits the ticks due now. Returns how many were credited.</summary>
        public int Step()
        {
            int n = _clock.Advance(Clock(), Visible());
            if (n <= 0) return 0;
            SaveStore store = StoreSource != null ? StoreSource() : null;
            if (store == null || store.Blob == null) return n;
            string day = Today();
            for (int i = 0; i < n; i++) ProgressRules.AddPlayTime(store.Blob, day);
            int secs;
            store.Blob.Time.TryGetValue(day, out secs);
            store.Save();
            Debug.Log(Log + " day=" + day + " secs=" + secs);
            return n;
        }
    }
}
