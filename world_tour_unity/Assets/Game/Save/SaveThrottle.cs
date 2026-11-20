namespace WorldTour.Game.Save
{
    /// <summary>
    /// Saves at most once every <see cref="Interval"/> seconds (the 1.5 tester's D6; the web game's store.save waits
    /// 500 ms). The first change after a save schedules one save <see cref="Interval"/> later; more changes before then
    /// ride along instead of pushing it back, so a burst of answers can never put off saving. <see cref="Flush"/> saves
    /// at once (show end, leaving the screen). Time comes in from the caller, so this is plain C# and testable.
    /// </summary>
    public sealed class SaveThrottle
    {
        public const float DefaultInterval = 0.5f;

        public readonly float Interval;
        private bool _dirty;
        private float _due;

        public SaveThrottle(float interval = DefaultInterval) { Interval = interval; }

        /// <summary>True while a change is waiting to be saved.</summary>
        public bool Dirty { get { return _dirty; } }

        /// <summary>Something in the save changed at <paramref name="now"/> (seconds).</summary>
        public void MarkDirty(float now)
        {
            if (_dirty) return;
            _dirty = true;
            _due = now + Interval;
        }

        /// <summary>Call every frame: true when it is time to save (and the change counts as saved).</summary>
        public bool Due(float now)
        {
            if (!_dirty || now < _due) return false;
            _dirty = false;
            return true;
        }

        /// <summary>True when there was something to save (and it now counts as saved): save at once.</summary>
        public bool Flush()
        {
            if (!_dirty) return false;
            _dirty = false;
            return true;
        }
    }
}
