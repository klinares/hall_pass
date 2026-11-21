/* Pieces the spelling modes share: the clue card, the stage desk, the typed answer,
   letter hints, and multiple-choice buttons. Behavior matches spelling_4_U. */
import { esc, shuffle, pick } from '../../js/util.js';
import { sticker } from '../../art/sticker.js';
import { chunks } from '../../js/show.js';
import { slap, pop, wobble } from '../../js/motion.js';

export const clue = (text, label = 'clue') => `
  <div class="scrap cluewrap">
    <div class="torn clue">
      <span class="tape tl"></span>
      <span class="cluelbl">${label}</span>
      <p>${esc(text)}</p>
    </div>
  </div>`;

export const desk = (tag, inner, cls = '') => `
  <div class="desk ${cls}">
    <span class="desk-lights l">${sticker('lights', { small: true })}</span>
    <span class="desk-lights r">${sticker('lights', { small: true })}</span>
    <span class="desk-tag">${tag}</span>
    ${inner}
  </div>`;

/* the word with some letters hidden: hide = indexes shown as blanks */
export const slotRow = (word, hide, blank = '') => `
  <div class="slots" id="row" data-wobble style="--n:${word.length}" aria-label="the word with letters missing">
    ${[...word].map((c, i) => hide.includes(i) ? `<div class="slot gap">${blank}</div>` : `<div class="slot fill">${c}</div>`).join('')}
  </div>`;

export const typedField = (placeholder = 'type the whole word') =>
  `<input class="ans" id="ans" type="text" placeholder="${placeholder}" autocomplete="off" autocapitalize="off" autocorrect="off" spellcheck="false" enterkeyhint="done" aria-label="${placeholder}">`;

export const pat = (w, show) => [...w].map((c, i) => show(c, i) ? c : '_').join(' ');

/* typed answer, with the same miss ladder as the original game:
   miss 1 → length + first letter, miss 2 → first and last syllable, miss 3 → show it, move on */
export function wireTyped(ctx, { onReveal } = {}) {
  const root = ctx.el, inp = root.querySelector('#ans'), go = root.querySelector('#go'), w = ctx.item.w;
  let done = false;
  const check = () => {
    if (done) return;
    const val = inp.value.trim().toLowerCase();
    if (!val) { inp.focus(); return; }
    if (val === w) {
      done = true; inp.disabled = true; go.disabled = true;
      inp.classList.add('right'); pop(inp);
      ctx.win(); ctx.next(1400);
      return;
    }
    const tries = ctx.miss();
    wobble(inp);
    if (tries >= 3) {
      done = true; inp.disabled = true; go.disabled = true;
      inp.value = w; inp.classList.add('shown');
      onReveal?.();
      ctx.reveal(); ctx.next(2800);
    } else {
      ctx.say(ctx.ladder(), 'oops');
      inp.select();
    }
  };
  inp.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); check(); } });
  go.onclick = check;
  /* don't yank the keyboard up on a phone before she's read the clue */
  if (matchMedia('(hover:hover)').matches) setTimeout(() => inp.focus({ preventScroll: true }), 50);
  return { check, focus: () => inp.focus({ preventScroll: true }) };
}

/* missing letters + missing vowels: hint 1 = syllables, then one blank filled per press;
   the last blank is always left for her */
export function letterHints(ctx, hide) {
  const w = ctx.item.w, shown = new Set();
  ctx.setupHints(Math.max(1, hide.length), level => {
    const i = level > 1 ? hide.find(k => !shown.has(k)) : undefined;
    if (i === undefined) { ctx.say(`sounds like: <b>${chunks(ctx.item)}</b>`, 'think'); return; }
    shown.add(i);
    const el = ctx.el.querySelector('#row').children[i];
    el.textContent = w[i]; el.classList.remove('gap'); el.classList.add('given'); slap(el);
    ctx.say(`here’s a letter for you: <b>${w[i]}</b>`, 'think');
    ctx.focus?.();
  });
}

/* multiple choice buttons */
export const choiceButtons = (opts, cls = '') =>
  `<div class="choices ${cls}" role="group">${opts.map((o, i) => `<button class="choice" data-v="${esc(o)}" style="--r:${[-1, 1.2, -.6, .8][i % 4]}deg">${esc(o)}</button>`).join('')}</div>`;

/* cross out one wrong, unpicked choice (used by hints) */
export function crossOut(btns, correct) {
  const live = btns.filter(b => b.dataset.v !== correct && !b.classList.contains('gone') && !b.disabled);
  if (!live.length) return false;
  const b = pick(live); b.classList.add('gone'); b.disabled = true; wobble(b);
  return true;
}

export const optionsFrom = (correct, pool, n = 4) => shuffle([correct, ...shuffle(pool.filter(x => x !== correct)).slice(0, n - 1)]);
