/* Pieces the math modes share: stacked fractions, the prompt card, the on-screen keypad,
   and the hint ladder (nudge → partial step → worked solution) that wrong answers and the
   bulb button share. */
import { esc } from '../../js/util.js';
import { check, matches, show } from '../../math/answer.js';
import { pop, wobble, slap } from '../../js/motion.js';
import * as snd from '../../js/sound.js';

/* [[3/4]], [[1 1/2]] and [[2]] → stacked fractions. Generator text is trusted markup. */
export const fracHTML = (n, d, w) =>
  `<span class="mixed">${w ? `<span class="whole">${w}</span>` : ''}<span class="frac"><span>${n}</span><span>${d}</span></span></span>`;
export const rich = s => String(s ?? '').replace(/\[\[(?:(\d+) )?(\d+)\/(\d+)\]\]|\[\[(\d+)\]\]/g,
  (_, w, n, d, whole) => whole != null ? whole : fracHTML(n, d, w));

export const promptCard = (html, label = 'tonight') => `
  <div class="scrap cluewrap">
    <div class="torn clue mathq">
      <span class="tape tl"></span>
      <span class="cluelbl">${label}</span>
      <p>${rich(html)}</p>
    </div>
  </div>`;

/* ---------- keypad ---------- */
const KEY_SVG = {
  back: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 5h11v14H9l-6-7z" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linejoin="round"/><path d="M12 9l5 6M17 9l-5 6" stroke="currentColor" stroke-width="2.4" stroke-linecap="round"/></svg>'
};

/* type: the answer type, so the pad shows a decimal point for money/decimals and
   a fraction bar + "and" (for mixed numbers) for fractions */
export function keypad(type, { checkLabel = 'lock it in' } = {}) {
  const dot = type === 'money' || type === 'decimal';
  const frac = type === 'fraction';
  const keys = ['7', '8', '9', '4', '5', '6', '1', '2', '3', frac ? '/' : dot ? '.' : '', '0', 'back'];
  return `
    <div class="padwrap">
      <div class="readout ${type}" id="readout" data-wobble role="status" aria-live="polite" aria-label="your answer">
        ${type === 'money' ? '<span class="pre">$</span>' : ''}<span id="ro" class="ro"></span><span class="caret" aria-hidden="true"></span>
      </div>
      <div class="keypad ${frac ? 'has-frac' : ''}" role="group" aria-label="number keys">
        ${keys.map(k => k === '' ? '<span class="key blank" aria-hidden="true"></span>'
          : k === 'back' ? `<button class="key fn" data-k="back" aria-label="delete">${KEY_SVG.back}</button>`
          : k === '/' ? `<button class="key fn" data-k="/" aria-label="fraction bar">${fracHTML('▢', '▢')}</button>`
          : `<button class="key" data-k="${k}">${k === '.' ? '•' : k}</button>`).join('')}
        ${frac ? `<button class="key fn wide" data-k=" " aria-label="whole number, then a fraction">and</button>` : ''}
        <button class="key go" id="go" data-k="go">${checkLabel}</button>
      </div>
    </div>`;
}

/* what she's typed, drawn the way it'll be read: 1 3/4 stacked as she goes */
function renderReadout(text) {
  if (!text) return '';
  const m = text.match(/^(?:(\d+) )?(\d*)\/(\d*)$/);
  if (m) return fracHTML(m[2] || '<i class="box"></i>', m[3] || '<i class="box"></i>', m[1]);
  const mw = text.match(/^(\d+) (\d*)$/);
  if (mw) return `<span class="mixed"><span class="whole">${mw[1]}</span><span class="frac"><span>${mw[2] || '<i class="box"></i>'}</span><span><i class="box"></i></span></span></span>`;
  return esc(text);
}

let activeKey = null;   /* one physical-keyboard listener at a time */
export function wireKeypad(root, { type, onSubmit, max = 9 }) {
  let text = '', locked = false;
  const ro = root.querySelector('#ro'), out = root.querySelector('#readout');
  const paint = () => { ro.innerHTML = renderReadout(text); out.classList.toggle('empty', !text); };
  const press = k => {
    if (locked) return;
    if (k === 'go') { onSubmit(text); return; }
    if (k === 'back') text = text.slice(0, -1);
    else if (k === '.') { if (!text.includes('.')) text += text ? '.' : '0.'; }
    else if (k === '/') { if (type === 'fraction' && !text.includes('/') && /\d$/.test(text)) text += '/'; }
    else if (k === ' ') { if (type === 'fraction' && /^\d+$/.test(text)) text += ' '; }
    else if (/^\d$/.test(k) && text.length < max) {
      if (type === 'money' && /\.\d\d$/.test(text)) return;
      text += k;
    }
    snd.tap(); paint();
  };
  root.querySelectorAll('.keypad [data-k]').forEach(b => b.addEventListener('click', () => press(b.dataset.k)));
  if (activeKey) removeEventListener('keydown', activeKey);
  const onKey = e => {
    if (!ro.isConnected) { removeEventListener('keydown', onKey); return; }
    if (e.target && e.target.tagName === 'SELECT') return;
    if ((e.key === 'Enter' || e.key === ' ') && e.target.closest?.('button,a') && !e.target.closest('.keypad')) return;
    const k = e.key === 'Enter' ? 'go' : e.key === 'Backspace' ? 'back' : e.key;
    if (/^[0-9./ ]$/.test(k) || k === 'go' || k === 'back') { e.preventDefault(); press(k); }
  };
  addEventListener('keydown', onKey);
  activeKey = onKey;
  paint();
  return {
    get text() { return text; },
    set(v) { text = v; paint(); },
    clear() { text = ''; paint(); },
    lock(cls) { locked = true; root.querySelectorAll('.keypad button').forEach(b => b.disabled = true); if (cls) out.classList.add(cls); },
    flash(cls) { out.classList.add(cls); setTimeout(() => out.classList.remove(cls), 650); }
  };
}

/* ---------- hint ladder ---------- */
const TRAP = {
  'no-carry': 'close. check your carrying: did a column go over 9?',
  'no-borrow': 'close. when the top digit is smaller, you have to borrow.',
  'gave-total': 'that’s what it costs. the question wants the change back.',
  'decimal-place': 'right digits, but the decimal point landed in the wrong spot.',
  'added-instead': 'looks like you added. this one multiplies.',
  'partial-only': 'that’s only part of it. multiply by the tens too.',
  'no-placeholder': 'the tens digit is worth ten times as much. don’t forget the zero.',
  'place-value': 'check your place value: that’s ten times off.',
  'subtracted-instead': 'that subtracted once. how many times does it fit?',
  'rounded-up': 'the last row isn’t full, so it doesn’t count.',
  'gave-remainder': 'that’s the leftover fans. how many full rows?',
  'added-denominators': 'adding the bottoms changes the size of the pieces. make them match first.',
  'subtracted-denominators': 'subtracting the bottoms changes the size of the pieces. make them match first.',
  'tops-not-rewritten': 'the bottoms match now, but the tops have to change too.',
  'swapped': 'you flipped them. across comes first, then up.',
  'moves-swapped': 'right and left change the first number, up and down the second.',
  'wrong-direction': 'check the direction of each move.',
  'added-sides': 'that adds the sides. volume multiplies them.',
  'one-layer': 'that’s one layer. how many layers are there?',
  'visible-faces': 'some crates are hiding behind the front ones.',
  'surface-area': 'that’s the outside. volume is what fits inside.',
  'one-box': 'that’s one of the blocks. add the other one too.',
  'subtracted': 'volume multiplies. try dividing instead.',
  'divided-once': 'you divided by one side. divide by the floor area.',
  'too-short': 'not full yet. there’s still a gap before the bar line.',
  'too-long': 'too much. notes are spilling past the bar line.',
  'left-to-right':'that works left to right. parentheses first, then × and ÷.',
  'ignored-parentheses': 'the parentheses go first.'
};

/* ctx: the show context. prob: the generated problem.
   onStep(n): draw the visual for ladder step n (1 nudge, 2 partial, 3 worked).
   The bulb gives steps 1 and 2. The worked solution only comes after the third miss,
   so a hint never hands over the whole answer. */
export function ladder(ctx, prob, { onStep, revealExtra = '' } = {}) {
  let shown = 0, over = false;
  const say = (html, pose) => ctx.say(rich(html), pose);
  ctx.setupHints(2, () => {
    if (over || shown >= 2) return false;
    shown++;
    say(prob.steps[shown - 1], 'think');
    onStep?.(shown);
  });
  return {
    get over() { return over; },
    get shown() { return shown; },
    /* a wrong answer: returns true once it's the third miss and the answer has been shown */
    wrong(text, why = null) {
      const tries = ctx.miss();
      const trap = why ? { why } : text != null ? prob.traps.find(t => matches(text, t.value, prob.answer.type)) : null;
      if (trap) ctx.mistake(trap.why);
      if (tries >= 3) {
        over = true; shown = 3;
        onStep?.(3);
        ctx.reveal(rich(`${prob.steps[2]}${revealExtra ? ' ' + revealExtra : ''} it’ll come back around.`));
        return true;
      }
      shown = Math.min(2, Math.max(shown, tries));
      const lead = trap && TRAP[trap.why] ? TRAP[trap.why] + ' ' : tries === 1 ? 'not quite. ' : 'keep going. ';
      say(lead + prob.steps[shown - 1], 'oops');
      onStep?.(shown);
      return false;
    },
    right(extra = 0) { over = true; ctx.win(extra); },
    end() { over = true; ctx.lockHints(); }
  };
}

/* typed answer + keypad + ladder, for the modes that are "work it out, type it in" */
export function typedAnswer(ctx, prob, { onStep, onRight, nextMs = 1500 } = {}) {
  const L = ladder(ctx, prob, { onStep });
  const pad = wireKeypad(ctx.el, {
    type: prob.answer.type,
    onSubmit(text) {
      if (L.over) return;
      const res = check(text, prob.answer, prob.accept);
      if (res.empty || res.bad) {
        pad.flash('nudge');
        ctx.say(res.bad ? 'that doesn’t read as a number yet. try again.' : 'type your answer on the keys first.', 'think');
        return;
      }
      if (res.simplify) {
        pad.flash('nudge');
        ctx.say('right amount! now simplify it: divide the top and bottom by the same number.', 'think');
        return;                                     /* not a miss */
      }
      if (res.ok) {
        pad.lock('right'); pop(ctx.el.querySelector('#readout'));
        onRight?.();
        L.right();
        ctx.next(nextMs);
        return;
      }
      wobble(ctx.el.querySelector('#readout'));
      if (L.wrong(text)) {
        pad.set(show(prob.answer).replace(/^\$/, '').replace(/\[\[|\]\]/g, '').replace(/,/g, ''));
        pad.lock('shown');
        showNext(ctx);
      } else pad.clear();
    }
  });
  return { L, pad };
}

/* after a reveal, a next button so she can read the worked solution first */
export function showNext(ctx) {
  const nx = ctx.el.querySelector('#nx');
  if (!nx) { ctx.next(3200); return; }
  nx.hidden = false; slap(nx);
  nx.onclick = () => ctx.next(0);
}

export const controls = ctx => `<div class="controls">${ctx.hintBtn()}<button class="btn" id="nx" hidden>next song →</button></div>`;
