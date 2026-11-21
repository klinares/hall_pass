/* MODE: missing vowels. Every vowel is gone (_mp_r_). Put them back. */
import { clue, desk, slotRow, typedField, wireTyped, letterHints } from './parts.js';
import { fillAll } from './slots.js';

export default {
  id: 'vowels',
  name: 'missing vowels',
  sub: 'every vowel is gone. put them back',
  sticker: 'phones',
  render(ctx) {
    const w = ctx.item.w;
    const hide = [...w].map((c, i) => 'aeiou'.includes(c) ? i : -1).filter(i => i >= 0);
    ctx.el.innerHTML = `
      ${clue(ctx.item.h)}
      ${desk('no vowels allowed', `${slotRow(w, hide, '_')}${typedField()}`)}
      <div class="controls">${ctx.hintBtn()}<button class="btn" id="go">check it</button></div>`;
    const t = wireTyped(ctx, { onReveal: () => fillAll(ctx.el, w) });
    ctx.focus = t.focus;
    letterHints(ctx, hide);
  }
};
