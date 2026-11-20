// The browser's "reduce motion" preference (matchMedia prefers-reduced-motion), which world_tour/js/motion.js
// honours on its own. Used by Game/Prefs/GameSettings.cs when settings.motion is "auto". Returns 1, 0, or -1 when
// the browser can't tell.
mergeInto(LibraryManager.library, {
  WT_PrefersReducedMotion: function () {
    try {
      if (typeof window === 'undefined' || !window.matchMedia) return -1;
      return window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 1 : 0;
    } catch (e) {
      return -1;
    }
  }
});
