/* Task 4.2 (builder M2). Runs the web game's real stage designer and pack the truck modes
   (world_tour/modes/math/stage.js, truck.js, parts.js, imported read-only) against a tiny fake DOM, with sound and
   motion stubbed by m2_hooks.mjs, and prints what they do for scripted presses: every ctx call (say, miss, mistake,
   lockHints, win, reveal, next), the tap/pop/wobble feedback, and a snapshot of what's on screen after each press
   (the pair line, the set button, the lamps and route on the stage; the spot counts, the "n loaded" line and the
   crates in the truck; the isometric pictures of the typed problems at each ladder stage).
   The C# tests replay the same presses through StageGame / TruckBuild / IsoGeometry and compare.

   node Tools/math/builder_vectors.mjs             writes Assets/Tests/EditMode/Ui/builder_vectors.json
   node Tools/math/builder_vectors.mjs --stdout    prints it instead
   Deterministic: the scripts come from a fixed mulberry32 seed; problems from the real generators. */
import { register } from 'node:module';
register('./builder_hooks.mjs', import.meta.url);

globalThis.window = {};
globalThis.addEventListener = () => {};
globalThis.removeEventListener = () => {};
globalThis.document = { documentElement: { dataset: {} } };

import { El, makeCtx, hint, T } from './fake_dom.mjs';

const base = process.env.WT_WEB_URL ?? new URL('../../../world_tour/', import.meta.url).href;
const stage = (await import(base + 'modes/math/stage.js')).default;
const truck = (await import(base + 'modes/math/truck.js')).default;
const coords = await import(base + 'math/gen/coords.js');
const volume = await import(base + 'math/gen/volume.js');


/* ---------------- snapshots ---------------- */
const num = s => +(+s).toFixed(4);
function plotSnap(el) {
  const svg = el.querySelector('#plot svg');
  const lamps = svg.querySelectorAll('g').filter(g => g.cls().has('lamp')).map(g => {
    const [, x, y] = g.attrs.transform.match(/translate\(([-\d.]+) ([-\d.]+)\)/);
    return [[...g.cls()].filter(c => c !== 'lamp')[0], num(x), num(y)];
  });
  const route = svg.querySelector('path') ? svg.querySelector('path').attrs.d : null;
  const on = svg.querySelectorAll('text').filter(t => t.cls().has('on')).map(t => t.textContent);
  return {
    pair: el.querySelector('#pair').textContent, setDisabled: el.querySelector('#set').disabled,
    lamps, route, on: on.length ? on[0] : null,
    pvals: el.querySelectorAll('.pval').map(p => p.textContent), nx: !el.querySelector('#nx').hidden
  };
}
function isoSnap(isoEl) {
  const svg = isoEl.querySelector('svg');
  const polys = [];
  for (const p of svg.querySelectorAll('polygon')) {
    const g = p.parent.tag === 'g' ? [...p.parent.cls()] : [];
    polys.push([[...p.cls()].join(' '), g.join(' '), p.attrs.points.split(' ').map(xy => xy.split(',').map(num))]);
  }
  const lines = svg.querySelectorAll('line').map(l => [num(l.attrs.x1), num(l.attrs.y1), num(l.attrs.x2), num(l.attrs.y2)]);
  const texts = svg.querySelectorAll('text').map(t => [[...t.cls()].join(' '), num(t.attrs.x), num(t.attrs.y), t.textContent]);
  return { viewBox: svg.attrs.viewBox.split(' ').map(num), polys, lines, texts };
}
function truckSnap(el) {
  const iso = isoSnap(el.querySelector('#iso'));
  return {
    cnt: el.querySelector('#cnt').textContent,
    spots: el.querySelectorAll('.spot').map(b => b.querySelector('b').textContent + (b.cls().has('has') ? '+' : '')),
    goDisabled: el.querySelector('#go').disabled, undoDisabled: el.querySelector('#undo').disabled,
    nx: !el.querySelector('#nx').hidden,
    cubes: iso.polys.filter(p => p[1].includes('cube')).length / 3, lit: iso.polys.filter(p => p[1].includes('lit')).length / 3,
    viewBox: iso.viewBox
  };
}

/* ---------------- deterministic scripts ---------------- */
function mulberry32(a) { return () => { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }
const R = mulberry32(4202);
const ri = (lo, hi) => lo + Math.floor(R() * (hi - lo + 1));
const PAD = 1.1;

function runStage(level, seed, plan) {
  const prob = coords.generate(level, seed);
  const max = prob.params.max, S = max + PAD + .5;
  T.EV = []; globalThis.__ev = T.EV;
  const { ctx, st } = makeCtx(prob);
  stage.render(ctx);
  const el = ctx.el, read = prob.kind === 'read';
  const actions = [], snaps = [];
  const snap = a => { actions.push(a); snaps.push({ ev: T.EV.splice(0), ...plotSnap(el) }); };
  snap(['begin']);
  const svgXY = p => [+(p.x + PAD).toFixed(3), +(max - p.y + .5).toFixed(3)];
  const tap = (ux, uy) => { el.querySelector('#plot').fire('pointerdown', { clientX: ux, clientY: uy }); snap(['tap', ux, uy]); };
  const nudge = (dx, dy) => { const b0 = el.querySelectorAll('.nudges button').find(b => (+b.dataset.dx || 0) === dx && (+b.dataset.dy || 0) === dy); if (!b0) return; b0.fire('click'); snap(['nudge', dx, dy]); };
  const pick = (axis, d) => { const b0 = el.querySelectorAll(`#${axis === 'x' ? 'px' : 'py'} button`).find(b => +b.dataset.d === d); if (!b0) return; b0.fire('click'); snap(['pick', axis, d]); };
  const set = () => { el.querySelector('#set').fire('click'); snap(['set']); };
  const doHint = () => { hint(st); snap(['hint']); };
  const pickTo = (pt) => {
    const cur = el.querySelectorAll('.pval').map(p => p.textContent === '?' ? null : +p.textContent);
    ['x', 'y'].forEach((axis, i) => {
      let v = cur[i];
      if (v == null) { pick(axis, pt[axis] > 0 ? 1 : -1); v = pt[axis] > 0 ? 1 : 0; }
      while (v < pt[axis]) { pick(axis, 1); v++; }
      while (v > pt[axis]) { pick(axis, -1); v--; }
    });
  };
  const placeAt = p => tap(...svgXY(p));
  for (const step of plan) {
    switch (step) {
      case 'answer': read ? pickTo(prob.answer.value) : placeAt(prob.answer.value); set(); break;
      case 'trap': for (const t of prob.traps.slice(0, 1)) { read ? pickTo(t.value) : placeAt(t.value); set(); } break;
      case 'traps': for (const t of prob.traps) { read ? pickTo(t.value) : placeAt(t.value); set(); } break;
      case 'wrong': {
        const a = prob.answer.value; const p = { x: a.x === max ? a.x - 1 : a.x + 1, y: a.y };
        read ? pickTo(p) : placeAt(p); set(); break;
      }
      case 'set': set(); break;
      case 'hint': doHint(); break;
      case 'randtaps': for (let k = 0; k < 4; k++) tap(+(R() * (S + 1) - .5).toFixed(3), +(R() * (S + 1) - .5).toFixed(3)); break;
      case 'halfs': { const k = ri(0, max - 1); tap(+(k + .5 + PAD).toFixed(3), +(max - k + .5 - .5).toFixed(3)); tap(PAD - .5, max + .5 + .5); tap(PAD - .51, .4); break; }
      case 'nudges': for (let k = 0; k < 6; k++) { const d = [[1, 0], [-1, 0], [0, 1], [0, -1]][ri(0, 3)]; nudge(...d); } break;
      case 'edge': for (let k = 0; k <= max; k++) nudge(-1, 0); nudge(0, -1); break;
      case 'picks': for (let k = 0; k < 5; k++) pick(R() < .5 ? 'x' : 'y', R() < .4 ? -1 : 1); break;
      case 'picklow': pick('x', -1); pick('y', -1); pick('y', -1); break;
      case 'pickhigh': for (let k = 0; k < max + 2; k++) pick('x', 1); break;
      default: throw new Error(step);
    }
  }
  return { level, seed, kind: prob.kind, actions, snaps };
}

function runTruckBuild(seed, plan) {
  let prob, s = seed;
  do prob = volume.generate(2, s++); while (prob.kind !== 'build');
  T.EV = []; globalThis.__ev = T.EV;
  const { ctx, st } = makeCtx(prob);
  truck.render(ctx);
  const el = ctx.el, { l, w } = prob.params.floor, h = prob.answer.value;
  const actions = [], snaps = [];
  const snap = a => { actions.push(a); snaps.push({ ev: T.EV.splice(0), ...truckSnap(el) }); };
  snap(['begin']);
  const spot = (x, y) => { el.querySelectorAll('.spot').find(b => +b.dataset.x === x && +b.dataset.y === y).fire('click'); snap(['spot', x, y]); };
  const btn = id => { el.querySelector('#' + id).fire('click'); snap([id]); };
  for (const step of plan) {
    switch (step) {
      case 'fill': for (let z = 0; z < h; z++) for (let y = 0; y < w; y++) for (let x = 0; x < l; x++) spot(x, y); break;
      case 'uneven': { let left = l * w * h; for (let y = 0; y < w; y++) for (let x = 0; x < l; x++) { const n = Math.min(left, 6); for (let k = 0; k < n; k++) spot(x, y); left -= n; } } break;
      case 'short': for (let y = 0; y < w; y++) for (let x = 0; x < l; x++) spot(x, y); break;
      case 'roof': for (let k = 0; k < 7; k++) spot(0, 0); break;
      case 'rand': for (let k = 0; k < 6; k++) spot(ri(0, l - 1), ri(0, w - 1)); break;
      case 'undo': case 'clear': case 'go': btn(step); break;
      case 'hint': hint(st); snap(['hint']); break;
      default: throw new Error(step);
    }
  }
  return { seed: prob.seed, actions, snaps };
}

/* the typed truck problems: the picture at ladder stages 0, 1, 2 (bulb) and 3 (third miss) */
function runTruckTyped(level, seed) {
  const prob = volume.generate(level, seed);
  if (prob.builder) return null;
  T.EV = []; globalThis.__ev = T.EV;
  const { ctx, st } = makeCtx(prob);
  truck.render(ctx);
  const el = ctx.el;
  const pics = [isoSnap(el.querySelector('#iso'))];
  hint(st); pics.push(isoSnap(el.querySelector('#iso')));
  hint(st); pics.push(isoSnap(el.querySelector('#iso')));
  const wrong = String(prob.answer.value + 1);
  for (let k = 0; k < 3; k++) {
    for (const ch of wrong) el.querySelectorAll('.keypad [data-k]').find(b => b.dataset.k === ch).fire('click');
    el.querySelector('#go').fire('click');
  }
  pics.push(isoSnap(el.querySelector('#iso')));
  const caption = el.querySelector('.tap') ? el.querySelector('.tap').textContent : null;
  const tag = el.querySelector('.desk-tag').textContent;
  const lbl = el.querySelector('.cluelbl').textContent;
  return { level, seed, kind: prob.kind, pics, caption, tag, lbl, intro: T.EV.find(e => e[0] === 'say')[1] };
}

const out = { stage: [], truckBuild: [], truckTyped: [] };
const stagePlans = [
  ['answer'], ['set', 'wrong', 'hint', 'hint', 'hint', 'answer'], ['wrong', 'wrong', 'wrong', 'randtaps', 'nudges', 'set', 'hint'],
  ['traps', 'hint'], ['randtaps', 'halfs', 'set'], ['nudges', 'set', 'edge', 'set'], ['hint', 'wrong', 'answer'],
  ['picks', 'set', 'picklow', 'pickhigh', 'hint', 'hint', 'answer'], ['trap', 'trap', 'answer', 'answer'],
];
for (const level of [1, 2, 3]) for (let i = 0; i < 18; i++) out.stage.push(runStage(level, 1000 * level + i * 37 + 5, stagePlans[i % stagePlans.length]));
const truckPlans = [
  ['fill', 'go'], ['hint', 'uneven', 'go', 'go', 'go', 'fill'], ['short', 'go', 'hint', 'undo', 'clear', 'fill', 'go'],
  ['roof', 'undo', 'go', 'hint', 'hint', 'hint', 'uneven', 'go'], ['rand', 'go', 'clear', 'rand', 'undo', 'go', 'go', 'rand', 'clear'],
  ['uneven', 'go', 'clear', 'fill', 'go', 'go'],
];
/* the web counts "load it" on an empty floor as a miss; Unity doesn't (QUESTIONS 15, intended difference 15). Kept
   out of the replay scripts above and recorded here on its own, so the C# test can pin the difference. */
out.truckEmpty = runTruckBuild(7001, ['go', 'go']);
for (let i = 0; i < 24; i++) out.truckBuild.push(runTruckBuild(7000 + i * 53, truckPlans[i % truckPlans.length]));
for (const level of [1, 2, 3]) for (let i = 0; i < 16; i++) { const r = runTruckTyped(level, 500 + level * 100 + i * 11); if (r) out.truckTyped.push(r); }
const json = JSON.stringify(out);
if (process.argv.includes('--stdout')) process.stdout.write(json);
else {
  const { writeFileSync } = await import('node:fs');
  const file = new URL('../../Assets/Tests/EditMode/Ui/builder_vectors.json', import.meta.url);
  writeFileSync(file, json);
  console.log('wrote', file.pathname, json.length, 'bytes');
}
