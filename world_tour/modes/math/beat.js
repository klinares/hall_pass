/* MODE: beat maker. Fill the measure with note blocks (½ ¼ ⅛ ⅓ ⅙ 1⁄12) drawn on the
   common-denominator grid. Tap a block to add it (or drag it onto the measure); tap a placed
   block to take it back. "play it" plays the beat when the measure is exactly full; too much
   spills past the bar line. Levels 2-3 also have typed add/subtract problems. */
import { generate, SKILL, NAMES, decompose, filled } from '../../math/gen/fractions.js';
import { R, sum, sub, num, str } from '../../math/rational.js';
import { promptCard, keypad, typedAnswer, ladder, controls, fracHTML, rich, showNext } from './parts.js';
import { makeDnD } from '../../js/dnd.js';
import { pop, slap, wobble } from '../../js/motion.js';
import * as snd from '../../js/sound.js';
import { esc } from '../../js/util.js';

const unit = d => R(1, d);
const SHADE = { 2: 'n2', 3: 'n3', 4: 'n4', 6: 'n6', 8: 'n8', 12: 'n12' };

/* lay blocks end to end, one row per whole; a block that crosses a bar line is drawn in two pieces */
function layout(blocks, rows) {
  const out = Array.from({ length: rows }, () => []);
  let at = 0;
  blocks.forEach((b, i) => {
    const len = num(b.len);
    let s = at, left = len, first = true;
    while (left > 1e-9) {
      const row = Math.floor(s + 1e-9);
      if (row >= rows) break;
      const room = row + 1 - s, piece = Math.min(room, left);
      out[row].push({ ...b, i, x: s - row, w: piece, first, cut: piece < len - 1e-9 });
      s += piece; left -= piece; first = false;
    }
    at += len;
  });
  return out;
}

function trackHTML({ blocks, rows, target, grid, showGrid }) {
  const t = target ? num(target) : null;
  const rowsHTML = layout(blocks, rows).map((items, r) => {
    const end = t == null ? null : Math.max(0, Math.min(1, t - r));       /* where the bar line sits in this row */
    return `
      <div class="mrow" style="--g:${grid}">
        ${end != null && end < 1 ? `<div class="spill" style="left:${end * 100}%"></div>` : ''}
        ${end != null && end > 0 && end <= 1 ? `<div class="barline" style="left:${end * 100}%"></div>` : ''}
        ${items.map(b => {
          const lab = b.first && b.w >= 1 / 13 ? `<span class="nlab">${b.label}</span>` : '';
          const tagName = b.tap ? 'button' : 'div';
          return `<${tagName} class="note ${b.cls || ''} ${b.cut ? 'cut' : ''}" ${b.tap ? `data-i="${b.i}" aria-label="${esc(b.aria || '')}"` : ''} style="left:${b.x * 100}%;width:${b.w * 100}%">${lab}</${tagName}>`;
        }).join('')}
        ${showGrid ? `<div class="gridnums">${Array.from({ length: grid }, (_, k) => `<i>${k + 1}</i>`).join('')}</div>` : ''}
      </div>`;
  }).join('');
  return rowsHTML;
}

export default {
  id: 'beat',
  name: 'beat maker',
  sub: 'fill the measure with fraction notes',
  sticker: 'note',
  kind: 'math',
  skill: SKILL,
  generate,
  render(ctx) {
    const p = ctx.prob;
    if (!p.builder) return typed(ctx, p);
    const pr = p.params;
    let placed = [], removed = [], stage = 0, done = false;
    const removable = pr.removable;

    ctx.el.innerHTML = `
      <div class="desk beatdesk" data-wobble>
        <span class="desk-tag">the beat ✶ ${pr.bars > 1 ? `${rich(`[[${str(pr.target)}]]`)} measures` : 'one measure'}</span>
        <div class="track" id="track" aria-label="the measure"></div>
        ${removable ? `<div class="cuttray" id="cut" aria-label="notes you took out"></div>` : `
        <div class="palette" role="group" aria-label="note blocks">
          ${pr.palette.map(d => `<button class="nblock ${SHADE[d]}" data-d="${d}" aria-label="add a ${NAMES[d]} note"><span class="bar" style="--w:${Math.min(1, 2 / d)}"></span>${fracHTML(1, d)}<small>${NAMES[d]}</small></button>`).join('')}
        </div>`}
        <div class="row beatbtns">
          <button class="btn ghost sm" id="undo">${removable ? 'put back' : 'undo'}</button>
          <button class="btn sm" id="playit">▶ play it</button>
        </div>
      </div>
      ${promptCard(p.prompt, removable ? 'trim it' : 'fill it')}
      ${controls(ctx)}`;

    const $ = s => ctx.el.querySelector(s);
    const track = $('#track');
    const blocks = () => {
      const pre = pr.pre.map((d, i) => ({ d, i, src: 'pre' })).filter(b => !removed.includes(b.i));
      return [...pre, ...placed.map((d, i) => ({ d, i, src: 'new' }))].map(b => ({
        len: unit(b.d), label: fracHTML(1, b.d),
        cls: `${SHADE[b.d]} ${b.src === 'pre' ? 'pre' : 'new'}`,
        tap: !done && (b.src === 'new' || removable),
        aria: b.src === 'new' ? `take back a ${NAMES[b.d]} note` : `take out a ${NAMES[b.d]} note`,
        ref: b
      }));
    };
    const total = () => sum(blocks().map(b => b.len));
    const paint = () => {
      const bl = blocks();
      const rows = Math.max(pr.bars, Math.min(pr.bars + 1, Math.ceil(num(total()) - 1e-9)));
      track.innerHTML = trackHTML({ blocks: bl, rows, target: pr.target, grid: pr.grid, showGrid: stage >= 2 });
      track.classList.toggle('over', num(total()) > num(pr.target) + 1e-9);
      track.querySelectorAll('button.note').forEach(el => el.onclick = () => {
        if (done) return;
        const b = bl[+el.dataset.i];                      /* data-i = position in the drawn blocks */
        if (!b || !b.tap) return;
        snd.tap();
        if (b.ref.src === 'new') placed.splice(b.ref.i, 1);
        else removed.push(b.ref.i);
        paint(); paintCut();
      });
    };
    const paintCut = () => {
      const c = $('#cut'); if (!c) return;
      c.innerHTML = removed.length ? removed.map(i => `<span class="nchip ${SHADE[pr.pre[i]]}">${fracHTML(1, pr.pre[i])}</span>`).join('') : '<span class="hint-sm">tap a note in the measure to take it out</span>';
    };

    const add = d => {
      if (done) return;
      if (num(sum([total(), unit(d)])) > pr.bars + 1 + 1e-9) { ctx.say('no more room on the page. take something back first.', 'think'); return; }
      placed.push(d); snd.tap(); paint();
      const news = track.querySelectorAll('.note.new'); if (news.length) slap(news[news.length - 1]);
    };
    if (!removable) {
      const chips = [...ctx.el.querySelectorAll('.nblock')];
      makeDnD({ items: chips, zones: [track], onTap: el => add(+el.dataset.d), onDrop: el => add(+el.dataset.d) });
    }
    $('#undo').onclick = () => {
      if (done) return;
      if (removable) removed.pop(); else placed.pop();
      snd.tap(); paint(); paintCut();
    };

    const L = ladder(ctx, p, {
      onStep(n) {
        stage = Math.max(stage, n);
        if (n === 1) { const bl = track.querySelector('.barline:last-of-type') || track.querySelector('.barline'); if (bl) pop(bl); }
        if (n >= 3) {
          /* worked: show it filled the right way */
          if (removable) removed = cheapestCut(pr, p.answer.value);
          else placed = decompose(sub(pr.target, sum(pr.pre.map(unit))), pr.palette);
          done = true;
        }
        paint(); paintCut();
      }
    });

    $('#playit').onclick = () => {
      if (done || L.over) return;
      const t = total();
      if (filled(pr, placed, removed)) {
        done = true; paint();
        ctx.lockHints();
        const notes = blocks().map(b => ({ len: num(b.len), d: b.ref.d }));
        const secs = snd.beat(notes);
        playhead(track, notes, pr.bars);
        $('#playit').disabled = true; $('#undo').disabled = true;
        ctx.el.querySelectorAll('.nblock').forEach(b => b.disabled = true);
        L.right();
        ctx.next(Math.max(1600, (secs || 1.6 * pr.bars) * 1000 + 500));
        return;
      }
      const over = num(t) > num(pr.target);
      wobble(track);
      if (L.wrong(null, over ? 'too-long' : 'too-short')) {
        $('#playit').disabled = true; $('#undo').disabled = true;
        ctx.el.querySelectorAll('.nblock').forEach(b => b.disabled = true);
        showNext(ctx);
      }
    };

    paint(); paintCut();
    ctx.say(removable ? 'too many notes. tap the ones that have to go.' : 'tap a note block to add it, or drag it onto the measure.', 'idle');
  }
};

/* the fewest written notes that add up to exactly the extra (for the worked hint) */
function cheapestCut(pr, over) {
  const n = pr.pre.length, target = num(over);
  let best = null;
  for (let mask = 1; mask < 1 << n; mask++) {
    let s = 0, k = 0;
    for (let i = 0; i < n; i++) if (mask & (1 << i)) { s += 1 / pr.pre[i]; k++; }
    if (Math.abs(s - target) < 1e-9 && (!best || k < best.k)) best = { mask, k };
  }
  return best ? pr.pre.map((_, i) => i).filter(i => best.mask & (1 << i)) : [];
}

/* a little cursor that runs along the measure while the beat plays */
function playhead(track, notes, bars) {
  const total = notes.reduce((s, n) => s + n.len, 0);
  const rows = [...track.querySelectorAll('.mrow')];
  if (matchMedia('(prefers-reduced-motion: reduce)').matches || document.documentElement.classList.contains('reduced')) {
    rows.forEach(r => r.classList.add('lit')); return;
  }
  rows.slice(0, Math.ceil(total - 1e-9) || bars).forEach((r, i) => {
    const h = document.createElement('div');
    h.className = 'playhead';
    r.appendChild(h);
    h.animate([{ left: '0%' }, { left: '100%' }], { duration: 1600, delay: i * 1600, fill: 'both', easing: 'linear' }).onfinish = () => h.remove();
  });
}

/* typed add/subtract: the two notes drawn on the grid, the answer on the keypad */
function typed(ctx, p) {
  const pr = p.params;
  const A = { len: pr.a, label: rich(`[[${str(pr.a)}]]`), cls: 'n3 pre' };
  const B = { len: pr.b, label: rich(`[[${str(pr.b)}]]`), cls: pr.op === '+' ? 'n4 pre' : 'n4 gone' };
  const rows = pr.op === '+' ? Math.max(1, Math.ceil(num(pr.a) + num(pr.b) - 1e-9)) : 1;
  let stage = 0;
  const draw = () => {
    const bl = pr.op === '+' ? [A, B] : [{ ...A, len: sub(pr.a, pr.b) }, B];
    return trackHTML({ blocks: bl, rows, target: null, grid: pr.grid, showGrid: stage >= 2 });
  };
  ctx.el.innerHTML = `
    <div class="desk beatdesk" data-wobble>
      <span class="desk-tag">the beat ✶ ${pr.op === '+' ? 'add them' : 'cut it'}</span>
      <div class="track" id="track">${draw()}</div>
      <p class="tap">${pr.op === '+' ? 'bass, then drums' : 'the faded part gets cut'}</p>
    </div>
    ${promptCard(p.prompt, pr.op === '+' ? 'add' : 'subtract')}
    ${keypad('fraction')}
    ${controls(ctx)}`;
  const track = ctx.el.querySelector('#track');
  typedAnswer(ctx, p, {
    onStep(n) { stage = Math.max(stage, n); track.innerHTML = draw(); },
    onRight() { snd.beat([{ len: num(pr.a), d: pr.a.d }, { len: num(pr.b), d: pr.b.d }]); }
  });
  ctx.say(pr.op === '+' ? 'how long are they together? fractions use the ▢⁄▢ key.' : 'how much is left? simplest form, please.', 'idle');
}
