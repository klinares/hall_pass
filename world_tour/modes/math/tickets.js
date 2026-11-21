/* MODE: ticket sales. Multi-digit multiplication and division on the box-office board.
   Typed on the keypad. Worked hints: the area model for ×, chunking for ÷,
   and full rows + leftovers for remainder problems. */
import { generate, SKILL } from '../../math/gen/multdiv.js';
import { promptCard, keypad, typedAnswer, controls } from './parts.js';
import { slap } from '../../js/motion.js';

const f = n => n.toLocaleString('en-US');

function area(w, n) {
  const { rowParts, colParts } = w;
  return `
    <div class="area" style="--c:${colParts.length}" aria-label="area model">
      <span></span>${colParts.map(c => `<b class="ahead">${f(c)}</b>`).join('')}
      ${rowParts.map(r => `<b class="ahead">${f(r)}</b>${colParts.map(c => `<span class="acell">${n >= 2 ? f(r * c) : '?'}</span>`).join('')}`).join('')}
    </div>
    ${n >= 3 ? `<p class="asum">${rowParts.flatMap(r => colParts.map(c => f(r * c))).join(' + ')} = <b>${f(rowParts.reduce((s, r) => s + colParts.reduce((t, c) => t + r * c, 0), 0))}</b></p>` : ''}`;
}

function chunks(w, n) {
  /* a one-chunk answer shows a smaller chunk at step 2, so the hint doesn't hand it over */
  const shown = n >= 3 ? w.chunks : w.lower ? [w.lower] : w.chunks.slice(0, 1);
  return `
    <div class="chunks" aria-label="dividing in chunks">
      <div class="crow top"><span>${f(w.N)}</span><small>start</small></div>
      ${(run => shown.map(c => `<div class="crow"><span>− ${f(c.take)}</span><small>${w.d} × ${f(c.c)}</small></div><div class="crow left"><span>${f(c.left)}</span><small>left · total ${f(run += c.c)}</small></div>`).join(''))(0)}
      ${n >= 3 ? `<div class="crow total"><span>${w.chunks.map(c => f(c.c)).join(' + ')} = ${f(w.chunks.reduce((s, c) => s + c.c, 0))}</span></div>` : ''}
    </div>`;
}

function rows(w, n) {
  const shown = Math.min(w.q, 12);
  return `
    <div class="seatrows" aria-label="rows of seats">
      ${Array.from({ length: shown }, (_, i) => `<i class="full" title="row ${i + 1}"></i>`).join('')}${w.q > 12 ? `<span class="more">… ${f(w.q - 12)} more full rows</span>` : ''}
      <i class="part" style="--p:${w.rem / w.d}"></i>
    </div>
    <p class="asum">${n >= 3 ? `<b>${f(w.q)}</b> full rows of ${w.d}, and ${w.rem} fans in a row that isn’t full.` : `${w.d} seats per row. does the last row fill up?`}</p>`;
}

export default {
  id: 'tickets',
  name: 'ticket sales',
  sub: 'multiply and divide the crowd',
  sticker: 'ticket',
  kind: 'math',
  skill: SKILL,
  generate,
  render(ctx) {
    const p = ctx.prob, pr = p.params;
    const board = pr.op === '×' ? [f(pr.a), '×', f(pr.b)] : [f(pr.N), '÷', f(pr.d)];
    ctx.el.innerHTML = `
      <div class="desk boxoffice" data-wobble>
        <span class="desk-tag">box office</span>
        <div class="letterboard" aria-label="${board.join(' ')}">
          ${board.map(t => `<span class="${t === '×' || t === '÷' ? 'op' : ''}">${[...t].map(ch => `<i>${ch}</i>`).join('')}</span>`).join('')}
        </div>
        <div class="workslot" id="work"></div>
      </div>
      ${promptCard(p.prompt, p.kind === 'rows' ? 'seating chart' : 'box office')}
      ${keypad('int', { checkLabel: 'sell it' })}
      ${controls(ctx)}`;
    const work = ctx.el.querySelector('#work');
    typedAnswer(ctx, p, {
      onStep(n) {
        if (n < 2) return;
        const w = p.work;
        work.innerHTML = `<div class="workpaper">${w.kind === 'area' ? area(w, n) : w.kind === 'chunks' ? chunks(w, n) : rows(w, n)}</div>`;
        slap(work.firstElementChild);
      }
    });
    ctx.say(p.kind === 'rows' ? 'careful: only full rows count.' : 'work it out on paper if you want. i’ll wait.', 'idle');
  }
};
