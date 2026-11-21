/* MODE: missing letters. About a third of the letters are blanked; she types the whole word.
   Hints: syllables first, then one blank filled per press (never the last one). */
import { shuffle } from '../../js/util.js';
import { clue, desk, slotRow, typedField, wireTyped, letterHints } from './parts.js';

export default {
  id: 'slots',
  name: 'missing letters',
  sub: 'some letters skipped town. fill them in',
  sticker: 'mic',
  render(ctx) {
    const w = ctx.item.w;
    const n = Math.max(2, Math.round(w.length / 3)) + (ctx.level - 1);
    const hide = shuffle([...w].map((_, i) => i)).slice(0, Math.min(n, w.length - 1)).sort((a, b) => a - b);
    ctx.el.innerHTML = `
      ${clue(ctx.item.h)}
      ${desk('soundcheck', `${slotRow(w, hide, '?')}${typedField()}`)}
      <div class="controls">${ctx.hintBtn()}<button class="btn" id="go">check it</button></div>`;
    const t = wireTyped(ctx, { onReveal: () => fillAll(ctx.el, w) });
    ctx.focus = t.focus;
    letterHints(ctx, hide);
  }
};

export function fillAll(root, w) {
  [...root.querySelector('#row').children].forEach((el, i) => { if (el.classList.contains('gap')) { el.textContent = w[i]; el.classList.replace('gap', 'given'); } });
}
