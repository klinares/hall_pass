/* MODE: pack the truck. Volume of rectangular prisms, drawn in isometric SVG.
   count / lwh / two / missing: typed on the keypad.
   build: tap a spot on the truck floor to stack a crate there; load exactly the target
   with every stack the same height. */
import { generate, SKILL } from '../../math/gen/volume.js';
import { promptCard, keypad, typedAnswer, ladder, controls, showNext } from './parts.js';
import { pop, wobble, slap } from '../../js/motion.js';
import * as snd from '../../js/sound.js';

const C = Math.cos(Math.PI / 6), Sn = .5;
/* x runs right-down, y runs left-down, z up */
const P = (x, y, z) => [(x - y) * C, (x + y) * Sn - z];
const pts = a => a.map(([x, y]) => `${x.toFixed(3)},${y.toFixed(3)}`).join(' ');

function cube(x, y, z, cls = '') {
  const top = [P(x, y, z + 1), P(x + 1, y, z + 1), P(x + 1, y + 1, z + 1), P(x, y + 1, z + 1)];
  const right = [P(x + 1, y, z), P(x + 1, y + 1, z), P(x + 1, y + 1, z + 1), P(x + 1, y, z + 1)];
  const left = [P(x, y + 1, z), P(x + 1, y + 1, z), P(x + 1, y + 1, z + 1), P(x, y + 1, z + 1)];
  return `<g class="cube ${cls}"><polygon class="ft" points="${pts(top)}"/><polygon class="fr" points="${pts(right)}"/><polygon class="fl" points="${pts(left)}"/></g>`;
}

/* a solid box at (ox, oy, oz) with optional unit lines and labels on three edges */
function box(b, o = [0, 0, 0], { lines = true, labels = true, hideH = false, cls = '' } = {}) {
  const [ox, oy, oz] = o, { l, w, h } = b;
  const q = (x, y, z) => P(ox + x, oy + y, oz + z);
  const top = [q(0, 0, h), q(l, 0, h), q(l, w, h), q(0, w, h)];
  const right = [q(l, 0, 0), q(l, w, 0), q(l, w, h), q(l, 0, h)];
  const front = [q(0, w, 0), q(l, w, 0), q(l, w, h), q(0, w, h)];
  const ln = [];
  if (lines) {
    for (let i = 1; i < l; i++) ln.push([q(i, 0, h), q(i, w, h)], [q(i, w, 0), q(i, w, h)]);
    for (let j = 1; j < w; j++) ln.push([q(0, j, h), q(l, j, h)], [q(l, j, 0), q(l, j, h)]);
    for (let k = 1; k < h; k++) ln.push([q(l, 0, k), q(l, w, k)], [q(0, w, k), q(l, w, k)]);
  }
  const mid = (a, b2) => [(a[0] + b2[0]) / 2, (a[1] + b2[1]) / 2];
  const lab = labels ? [
    [mid(q(0, w, 0), q(l, w, 0)), l, 'ld', [.35, .75]],
    [mid(q(l, 0, 0), q(l, w, 0)), w, 'wd', [.45, .7]],
    [mid(q(l, 0, 0), q(l, 0, h)), hideH ? '?' : h, 'hd', [.55, 0]]
  ].map(([[x, y], t, c, [dx, dy]]) => `<text class="dim ${c}" x="${(x + dx).toFixed(2)}" y="${(y + dy).toFixed(2)}">${t}</text>`).join('') : '';
  return `<g class="solid ${cls}">
    <polygon class="ft" points="${pts(top)}"/><polygon class="fr" points="${pts(right)}"/><polygon class="fl" points="${pts(front)}"/>
    ${ln.map(([a, c]) => `<line class="ul" x1="${a[0]}" y1="${a[1]}" x2="${c[0]}" y2="${c[1]}"/>`).join('')}
    ${lab}</g>`;
}

function frame(inner, corners) {
  const xs = corners.map(c => c[0]), ys = corners.map(c => c[1]);
  const x0 = Math.min(...xs) - 1.2, y0 = Math.min(...ys) - .8, W = Math.max(...xs) - x0 + 1.4, H = Math.max(...ys) - y0 + 1.2;
  return `<svg class="iso" viewBox="${x0.toFixed(2)} ${y0.toFixed(2)} ${W.toFixed(2)} ${H.toFixed(2)}" role="img" aria-label="crates">${inner}</svg>`;
}
const cornersOf = (l, w, h, o = [0, 0, 0]) => [P(o[0], o[1], o[2]), P(o[0] + l, o[1], o[2]), P(o[0] + l, o[1] + w, o[2]), P(o[0], o[1] + w, o[2]), P(o[0], o[1], o[2] + h), P(o[0] + l, o[1], o[2] + h), P(o[0] + l, o[1] + w, o[2] + h), P(o[0], o[1] + w, o[2] + h)];

/* unit crates, back to front so the near ones cover the far ones */
function crates(stacks, l, w, { topLit = false, maxH = 0 } = {}) {
  const out = [];
  for (let s = 0; s <= l + w - 2; s++) for (let x = 0; x < l; x++) { const y = s - x; if (y < 0 || y >= w) continue;
    const hgt = stacks[y][x];
    for (let z = 0; z < hgt; z++) out.push(cube(x, y, z, topLit && z === hgt - 1 ? 'lit' : ''));
  }
  const floor = [P(0, 0, 0), P(l, 0, 0), P(l, w, 0), P(0, w, 0)];
  return frame(`<polygon class="truckfloor" points="${pts(floor)}"/>${out.join('')}`, cornersOf(l, w, Math.max(maxH, 1)));
}

export default {
  id: 'truck',
  name: 'pack the truck',
  sub: 'volume: fill the trailer',
  sticker: 'bus',
  kind: 'math',
  skill: SKILL,
  generate,
  render(ctx) {
    const p = ctx.prob;
    if (p.builder) return build(ctx, p);
    const bx = p.params.boxes;
    let stage = 0;
    const draw = () => {
      if (p.kind === 'count') {
        const b = bx[0], stacks = Array.from({ length: b.w }, () => Array(b.l).fill(b.h));
        return crates(stacks, b.l, b.w, { topLit: stage >= 2, maxH: b.h });
      }
      if (p.kind === 'two') {
        const [A, B] = bx;
        return frame(box(A, [0, 0, 0], { lines: stage >= 2, cls: 'a' }) + box(B, [0, 0, A.h], { lines: stage >= 2, cls: 'b' }), [...cornersOf(A.l, A.w, A.h), ...cornersOf(B.l, B.w, B.h, [0, 0, A.h])]);
      }
      const b = bx[0];
      return frame(box(b, [0, 0, 0], { lines: stage >= 2, hideH: p.params.hideH && stage < 3 }), cornersOf(b.l, b.w, b.h));
    };
    ctx.el.innerHTML = `
      <div class="desk truckdesk" data-wobble>
        <span class="desk-tag">the trailer</span>
        <div class="isowrap" id="iso">${draw()}</div>
        ${p.kind === 'count' ? '<p class="tap">every crate is 1 cubic unit</p>' : ''}
      </div>
      ${promptCard(p.prompt, 'load out')}
      ${keypad('int', { checkLabel: 'load it' })}
      ${controls(ctx)}`;
    const iso = ctx.el.querySelector('#iso');
    typedAnswer(ctx, p, { onStep(n) { stage = Math.max(stage, n); iso.innerHTML = draw(); } });
    ctx.say(p.kind === 'count' ? 'count them up. some are hiding in the back.' : 'length × width × height.', 'idle');
  }
};

/* the builder: a top-down floor plan of big buttons, with the isometric view above */
function build(ctx, p) {
  const { floor, target, maxH } = p.params, { l, w } = floor;
  const stacks = Array.from({ length: w }, () => Array(l).fill(0));
  const log = [];
  let done = false;
  ctx.el.innerHTML = `
    <div class="desk truckdesk" data-wobble>
      <span class="desk-tag">the trailer ✶ ${target} crates</span>
      <div class="isowrap" id="iso"></div>
      <div class="floorplan" id="fp" style="--l:${l}" role="group" aria-label="truck floor, tap a spot to stack a crate">
        ${Array.from({ length: w }, (_, y) => Array.from({ length: l }, (_, x) => `<button class="spot" data-x="${x}" data-y="${y}" aria-label="spot ${x + 1}, ${y + 1}"><b>0</b><small>+</small></button>`).join('')).join('')}
      </div>
      <p class="count" id="cnt" aria-live="polite"></p>
      <div class="row beatbtns">
        <button class="btn ghost sm" id="undo">undo</button>
        <button class="btn ghost sm" id="clear">clear</button>
        <button class="btn sm" id="go">load it</button>
      </div>
    </div>
    ${promptCard(p.prompt, 'load out')}
    ${controls(ctx)}`;
  const $ = s => ctx.el.querySelector(s);
  const total = () => stacks.flat().reduce((a, b) => a + b, 0);
  const paint = () => {
    $('#iso').innerHTML = crates(stacks, l, w, { maxH: Math.max(4, ...stacks.flat()) });
    ctx.el.querySelectorAll('.spot').forEach(b => { const v = stacks[+b.dataset.y][+b.dataset.x]; b.querySelector('b').textContent = v; b.classList.toggle('has', v > 0); });
    $('#cnt').textContent = `${total()} loaded`;
  };
  ctx.el.querySelectorAll('.spot').forEach(b => b.onclick = () => {
    if (done) return;
    const x = +b.dataset.x, y = +b.dataset.y;
    if (stacks[y][x] >= maxH) { ctx.say(`that stack’s at the roof. ${maxH} is the max.`, 'think'); return; }
    stacks[y][x]++; log.push([x, y]); snd.tap(); paint(); slap(b);
  });
  $('#undo').onclick = () => { if (done || !log.length) return; const [x, y] = log.pop(); stacks[y][x]--; snd.tap(); paint(); };
  $('#clear').onclick = () => { if (done) return; stacks.forEach(r => r.fill(0)); log.length = 0; snd.tap(); paint(); };

  const L = ladder(ctx, p, {
    onStep(n) {
      if (n >= 3) { stacks.forEach(r => r.fill(p.answer.value)); done = true; paint(); }
    }
  });
  $('#go').onclick = () => {
    if (done || L.over) return;
    const flat = stacks.flat(), even = flat.every(v => v === flat[0]);
    if (even && total() === target) {
      done = true; ctx.lockHints(); pop($('#iso'));
      ['#go', '#undo', '#clear'].forEach(s => $(s).disabled = true);
      L.right(); ctx.next(1400);
      return;
    }
    wobble($('#iso'));
    const why = total() === target ? 'uneven' : null;
    if (why) ctx.mistake('uneven-stacks');
    if (L.wrong(null)) { ['#go', '#undo', '#clear'].forEach(s => $(s).disabled = true); showNext(ctx); }
    else if (why) ctx.say(`right count, but the stacks aren’t all the same height. ${p.steps[Math.min(1, L.shown - 1)]}`, 'oops');
  };
  paint();
  ctx.say('tap a spot on the floor to stack a crate there.', 'idle');
}
