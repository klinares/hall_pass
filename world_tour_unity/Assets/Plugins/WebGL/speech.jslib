// Read-aloud for studio session: the browser's speechSynthesis, as world_tour/js/speech.js does it (QUESTIONS 1):
// cancel before every speak, the rate passed in (0.8, or 0.55 for the syllable hint), an en-US voice preferred.
// Voices often load late, so the voice is picked again on voiceschanged and Game/Speech/ReadAloud.cs waits for one.
// Used by Game/Speech/ReadAloud.cs.
mergeInto(LibraryManager.library, {
  $WT_Speech: { voice: null, hooked: false, pick: null },

  WT_SpeechAvailable__deps: ['$WT_Speech'],
  WT_SpeechAvailable: function () {
    try {
      if (typeof window === 'undefined' || !('speechSynthesis' in window)) return 0;
      if (!WT_Speech.hooked) {
        WT_Speech.hooked = true;
        WT_Speech.pick = function () {
          try {
            var vs = window.speechSynthesis.getVoices() || [];
            var f = function (t) { for (var i = 0; i < vs.length; i++) if (t(vs[i])) return vs[i]; return null; };
            WT_Speech.voice = f(function (v) { return /en[-_]US/i.test(v.lang) && /samantha|allison|ava|google us/i.test(v.name); })
              || f(function (v) { return /en[-_]US/i.test(v.lang); })
              || f(function (v) { return /^en/i.test(v.lang); })
              || null;
          } catch (e) { WT_Speech.voice = null; }
        };
        WT_Speech.pick();
        if (window.speechSynthesis.addEventListener) window.speechSynthesis.addEventListener('voiceschanged', WT_Speech.pick);
      }
      return 1;
    } catch (e) {
      return 0;
    }
  },

  WT_SpeechVoiceCount__deps: ['$WT_Speech'],
  WT_SpeechVoiceCount: function () {
    try {
      if (typeof window === 'undefined' || !('speechSynthesis' in window)) return 0;
      var n = (window.speechSynthesis.getVoices() || []).length;
      if (n > 0 && !WT_Speech.voice && WT_Speech.pick) WT_Speech.pick();
      return n;
    } catch (e) {
      return 0;
    }
  },

  WT_SpeechWarmUp: function () {
    try {
      if (typeof window === 'undefined' || !('speechSynthesis' in window)) return;
      var u = new SpeechSynthesisUtterance(' ');
      u.volume = 0;
      window.speechSynthesis.speak(u);
    } catch (e) { /* ignore */ }
  },

  WT_SpeechSay__deps: ['$WT_Speech'],
  WT_SpeechSay: function (textPtr, rate) {
    try {
      if (typeof window === 'undefined' || !('speechSynthesis' in window)) return 0;
      window.speechSynthesis.cancel();
      var u = new SpeechSynthesisUtterance(UTF8ToString(textPtr));
      u.rate = rate;
      if (WT_Speech.voice) u.voice = WT_Speech.voice;
      window.speechSynthesis.speak(u);
      return 1;
    } catch (e) {
      return 0;
    }
  },

  WT_SpeechHush: function () {
    try { if (typeof window !== 'undefined' && 'speechSynthesis' in window) window.speechSynthesis.cancel(); } catch (e) { /* ignore */ }
  }
});
