/* MODE: studio session. Hear the word, spell it (closest thing to test day).
   Hints: the clue, the syllables read slowly, first + last letters, then consonants only.
   Without speech, the clue is on screen from the start and the clue hint is skipped. */
import { esc } from '../../js/util.js';
import { sticker } from '../../art/sticker.js';
import { canSpeak, say } from '../../js/speech.js';
import { chunks } from '../../js/show.js';
import { clue, desk, typedField, wireTyped, pat } from './parts.js';

export default {
  id: 'studio',
  name: 'studio session',
  sub: 'hear it, spell it. test-day practice',
  sticker: 'vinyl',
  render(ctx) {
    const v = ctx.item, w = v.w, can = canSpeak();
    ctx.el.innerHTML = `
      ${can ? '' : clue(v.h)}
      ${desk('recording', `
        ${can ? `<button class="record" id="spk" aria-label="play the word again">${sticker('vinyl')}</button>
                 <p class="tap">tap the record to hear it again</p>` : ''}
        ${typedField('spell it like it’s test day')}`, 'studio')}
      <div class="controls">${ctx.hintBtn()}<button class="btn" id="go">submit</button></div>`;
    if (can) {
      const s = ctx.el.querySelector('#spk');
      s.onclick = () => { s.classList.remove('spin'); void s.offsetWidth; s.classList.add('spin'); say(w); };
      ctx.later(() => s.click(), 380);
    }
    const t = wireTyped(ctx);
    ctx.focus = t.focus;
    const steps = can ? ['clue', 'chunks', 'ends', 'cons'] : ['chunks', 'ends', 'cons'];
    ctx.setupHints(steps.length, level => {
      const s = steps[level - 1];
      if (s === 'clue') ctx.say(`clue: <b>${esc(v.h.toLowerCase())}</b>`, 'think');
      if (s === 'chunks') { ctx.say(`sounds like: <b>${chunks(v)}</b>`, 'think'); if (can) say(w, .55); }
      if (s === 'ends') ctx.say(`${w.length} letters: <span class="pat">${pat(w, (c, i) => i === 0 || i === w.length - 1)}</span>`, 'think');
      if (s === 'cons') ctx.say(`just the consonants: <span class="pat">${pat(w, c => !'aeiou'.includes(c))}</span>`, 'think');
      t.focus();
    });
  }
};
