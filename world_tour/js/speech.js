/* Read-aloud, carried over from the spelling game: a silent warm-up on the first tap
   (iPad Safari needs it), cancel before every speak, slower rate for syllable hints. */
export const canSpeak = () => 'speechSynthesis' in window;

let voice = null;
function pickVoice() {
  if (!canSpeak()) return;
  const vs = speechSynthesis.getVoices();
  voice = vs.find(v => /en[-_]US/i.test(v.lang) && /samantha|allison|ava|google us/i.test(v.name))
       || vs.find(v => /en[-_]US/i.test(v.lang)) || vs.find(v => /^en/i.test(v.lang)) || null;
}
if (canSpeak()) { pickVoice(); speechSynthesis.addEventListener?.('voiceschanged', pickVoice); }

export function warmUp() {
  try { if (canSpeak()) { const u = new SpeechSynthesisUtterance(' '); u.volume = 0; speechSynthesis.speak(u); } } catch (e) { /* ignore */ }
}

export function say(text, rate = .8) {
  if (!canSpeak()) return;
  try {
    speechSynthesis.cancel();
    const u = new SpeechSynthesisUtterance(text);
    u.rate = rate; if (voice) u.voice = voice;
    speechSynthesis.speak(u);
  } catch (e) { /* ignore */ }
}

export function hush() { try { if (canSpeak()) speechSynthesis.cancel(); } catch (e) { /* ignore */ } }
