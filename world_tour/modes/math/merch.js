/* MODE: merch table. Fans buy stuff; ring it up or count back the change.
   Typed on the keypad. Worked hint: the receipt, lined up on the decimal point. */
import { generate, SKILL } from '../../math/gen/decimals.js';
import { dollars } from '../../math/money.js';
import { sticker } from '../../art/sticker.js';
import { esc } from '../../js/util.js';
import { promptCard, keypad, typedAnswer, controls } from './parts.js';
import { slap } from '../../js/motion.js';

const tag = l => l.unit
  ? `${(l.amount / 10).toFixed(1)} ${l.unit}s × ${dollars(l.each)}`
  : l.qty > 1 ? `${l.qty} × ${dollars(l.each)}` : dollars(l.each);

/* the receipt: amounts right-aligned so the decimal points stack */
function receipt(work, showResult) {
  const w = Math.max(...[...work.rows, work.result].map(c => dollars(c).length));
  const pad = c => dollars(c).padStart(w, ' ');
  return `
    <div class="receipt" aria-label="worked on the receipt">
      ${work.rows.map((c, i) => `<div><span class="op">${i === work.rows.length - 1 ? work.op : ''}</span><span>${esc(pad(c))}</span></div>`).join('')}
      <div class="sum"><span class="op">=</span><span>${showResult ? esc(pad(work.result)) : '?'.padStart(w, ' ')}</span></div>
    </div>`;
}

export default {
  id: 'merch',
  name: 'merch table',
  sub: 'ring up fans and count their change',
  sticker: 'tee',
  kind: 'math',
  skill: SKILL,
  generate,
  render(ctx) {
    const p = ctx.prob, { lines, paid } = p.params;
    ctx.el.innerHTML = `
      <div class="desk merchdesk" data-wobble>
        <span class="desk-tag">the merch table</span>
        <div class="merch">
          ${lines.map((l, i) => `
            <div class="mitem" style="--r:${[-3, 2, -1.5][i % 3]}deg">
              ${sticker(l.icon, { small: true })}
              <span class="mname">${esc(l.name)}</span>
              <span class="ptag">${esc(tag(l))}</span>
            </div>`).join('')}
          ${paid ? `<div class="bill" aria-label="paid with ${dollars(paid)}"><span>paid</span><b>${dollars(paid)}</b></div>` : ''}
        </div>
        <div class="workslot" id="work"></div>
      </div>
      ${promptCard(p.prompt, 'order up')}
      ${keypad('money', { checkLabel: 'ring it up' })}
      ${controls(ctx)}`;

    const work = ctx.el.querySelector('#work');
    typedAnswer(ctx, p, {
      onStep(n) {
        if (!p.work || n < 2) return;
        work.innerHTML = receipt(p.work, n >= 3);
        slap(work.firstElementChild);
      }
    });
    ctx.say(p.params.paid ? 'count back the change. take your time.' : 'ring it up. the keypad’s right there.', 'idle');
  }
};
