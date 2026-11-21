/* BEAT MAKER: adding and subtracting fractions (5.NF.1-2).
   A measure is one whole. Note blocks are unit fractions: ½ ¼ ⅛ ⅓ ⅙ 1⁄12.
   L1: fill one whole with halves, quarters, eighths
   L2: unlike denominators from {2,3,4,6,8,12}: fill the measure, or add two notes
   L3: mixed-number targets (two measures), taking notes out, or subtracting */
import { R, add, sub, sum, lcm, eq, cmp, str } from '../rational.js';
import { cleanTraps } from '../mistakes.js';
import { guarded } from './guard.js';

export const SKILL = 'frac';
export const NAMES = { 1: 'whole', 2: 'half', 3: 'third', 4: 'quarter', 6: 'sixth', 8: 'eighth', 12: 'twelfth' };
export const plural = d => d === 2 ? 'halves' : NAMES[d] + 's';

/* pairs of unlike denominators whose common grid stays at 12 or under */
const PAIRS = [[2, 3], [3, 4], [4, 6], [2, 6], [3, 6], [2, 12], [3, 12], [4, 12], [6, 12], [2, 8], [4, 8], [2, 4]];

const F = f => `[[${str(f)}]]`;
const unit = d => R(1, d);
const gridOf = ds => ds.reduce((a, b) => lcm(a, b), 1);

/* split a fraction into palette blocks, biggest first (the palette always includes 1/grid, so this ends) */
export function decompose(f, palette) {
  const out = [];
  let left = f;
  for (const d of [...palette].sort((a, b) => a - b)) {
    while (cmp(left, unit(d)) >= 0) { out.push(d); left = sub(left, unit(d)); }
  }
  if (left.n !== 0) throw new Error('could not decompose ' + str(f));
  return out;
}

/* "3/12" written over the grid, not reduced */
const inGrid = (f, g) => `[[${f.n * (g / f.d)}/${g}]]`;

export const generate = guarded(build);

function build(level, seed, r) {
  const kind = level === 1 ? 'fill' : level === 2 ? r.pick(['fill', 'fill', 'sum']) : r.pick(['mixed', 'takeout', 'diff']);
  let target = R(1), pre = [], palette, answer, accept = { simplest: false }, steps, prompt, label, traps = [], params;

  if (kind === 'fill' || kind === 'mixed') {
    let ds, t = null;
    if (level === 1) { ds = [2, 4, 8]; palette = [2, 4, 8]; }
    else {
      for (;;) {
        ds = r.pick(PAIRS);
        if (kind === 'mixed') { t = r.pick([2, 3, 4, 6]); if (gridOf([...ds, t]) > 12) continue; }
        break;
      }
    }
    if (kind === 'mixed') {
      let m; do m = r.int(1, t - 1); while (R(m, t).d !== t);
      target = add(R(1), R(m, t));
    }
    const grid = gridOf([...ds, ...(t ? [t] : [])]);
    if (level > 1) palette = [...new Set([...ds, grid])];
    /* written notes: draw from the pair's blocks until there's no room for more */
    const room = sub(target, unit(grid));
    const want = level === 1 ? r.int(1, 2) : kind === 'mixed' ? r.int(3, 5) : r.int(2, 3);
    for (let i = 0; i < 12 && pre.length < want; i++) {
      const d = level === 1 ? r.pick([2, 4, 8]) : ds[pre.length % 2 === 0 ? 0 : 1];
      if (cmp(add(sum(pre.map(unit)), unit(d)), room) <= 0) pre.push(d);
    }
    if (!pre.length) pre.push(Math.max(...ds));
    const have = sum(pre.map(unit)), need = sub(target, have);
    answer = { type: 'fraction', value: need };
    params = { target, grid, bars: Math.ceil(target.n / target.d), pre, palette, removable: false };
    prompt = kind === 'mixed'
      ? `this beat runs ${F(target)} measures. finish it so it ends right on the line.`
      : `finish the measure: add notes until it’s exactly one whole.`;
    label = `fill ${F(target)}${kind === 'mixed' ? ' measures' : ' measure'}`;
    const fill = decompose(need, palette);
    steps = [
      `${target.d === 1 ? "the measure holds one whole" : "the beat is " + F(target) + " measures long"}. how much is already written? count it in ${plural(grid)}.`,
      `written so far: ${inGrid(have, grid)}. you need ${inGrid(need, grid)} more.`,
      `${fill.length > 1 ? fill.map(d => F(unit(d))).join(' + ') + ' = ' + F(need) : 'one ' + F(need) + ' note'}${inGrid(need, grid) !== F(need) && fill.length === 1 ? ' (that’s ' + inGrid(need, grid) + ')' : ''}. that fills it to ${target.d === 1 ? "one whole" : F(target)}.`
    ];
  } else if (kind === 'takeout') {
    const ds = r.pick(PAIRS), grid = gridOf(ds);
    palette = [...new Set([...ds, grid])];
    /* a measure that's exactly one whole, plus one or two extra notes jammed in */
    const keep = [];
    while (cmp(sum(keep.map(unit)), R(1)) < 0) {
      const d = r.pick(ds);
      keep.push(cmp(add(sum(keep.map(unit)), unit(d)), R(1)) <= 0 ? d : grid);
    }
    const extra = r.chance(.5) ? [r.pick(ds)] : [r.pick(ds), r.pick(palette)];
    pre = r.shuffle([...keep, ...extra]);
    const over = sum(extra.map(unit));
    answer = { type: 'fraction', value: over };
    params = { target, grid, bars: 1, pre, palette, removable: true };
    prompt = `the drummer jammed in too many notes. take notes out until the measure is exactly one whole.`;
    label = 'trim to 1 measure';
    const total = sum(pre.map(unit));
    steps = [
      `add up what’s written, in ${plural(grid)}. how far past ${F(R(1))} does it go?`,
      `it adds to ${inGrid(total, grid)}. one whole is ${inGrid(R(1), grid)}, so take out ${inGrid(over, grid)}.`,
      extra.length > 1 ? `take out ${extra.map(d => F(unit(d))).join(' and ')}. that’s ${F(over)}, which leaves exactly ${F(R(1))}.` : `take out the ${F(over)} note. that leaves exactly ${F(R(1))}.`
    ];
  } else {
    /* typed: a/b + c/d (level 2) or a/b − c/d (level 3) */
    let a, b, x, y;
    for (;;) {
      [a, b] = r.shuffle(r.pick(PAIRS));
      x = r.int(1, a - 1 || 1); y = r.int(1, b - 1 || 1);
      if (a === 1 || b === 1) continue;
      if (R(x, a).d !== a || R(y, b).d !== b) continue;      /* keep the written fractions in simplest form */
      if (kind === 'diff' && cmp(R(x, a), R(y, b)) <= 0) continue;
      break;
    }
    const g = lcm(a, b), A = R(x, a), B = R(y, b);
    const res = kind === 'sum' ? add(A, B) : sub(A, B);
    palette = [...new Set([a, b, g])];
    answer = { type: 'fraction', value: res };
    if (kind === 'diff') accept = { simplest: true };
    const op = kind === 'sum' ? '+' : '−';
    params = { target: null, grid: g, bars: kind === 'sum' ? Math.max(1, Math.ceil(res.n / res.d)) : 1, a: A, b: B, op, palette, pre: [], removable: false };
    prompt = kind === 'sum'
      ? `the bass plays ${F(A)} of a measure, then the drums play ${F(B)}. how much is that together?`
      : `a riff is ${F(A)} of a measure long. cut ${F(B)} off the end. how long is it now? simplest form.`;
    label = `${F(A)} ${op} ${F(B)}`;
    const A2 = x * (g / a), B2 = y * (g / b), raw = kind === 'sum' ? A2 + B2 : A2 - B2;
    steps = [
      `the denominators don’t match yet. what can ${a} and ${b} both turn into?`,
      `rewrite both in ${plural(g)}: ${inGrid(A, g)} ${op} ${inGrid(B, g)}.`,
      `${inGrid(A, g)} ${op} ${inGrid(B, g)} = [[${raw}/${g}]]${R(raw, g).d !== g ? ` = ${F(res)}` : ''}`
    ];
    traps = [
      { value: kind === 'sum' ? R(x + y, a + b) : (a !== b && x > y && a > b ? R(x - y, a - b) : null), why: kind === 'sum' ? 'added-denominators' : 'subtracted-denominators' },
      { value: kind === 'sum' ? R(x + y, g) : (x > y ? R(x - y, g) : null), why: 'tops-not-rewritten' }
    ];
  }

  return {
    skill: SKILL, level, kind, seed, params, prompt, label, answer, accept,
    choices: null, traps: cleanTraps(answer, traps), steps,
    /* builder modes win when the measure adds to the target; typed modes use the answer */
    builder: kind !== 'sum' && kind !== 'diff'
  };
}

/* builder check: written notes plus placed minus removed equals the target */
export function filled(params, placed = [], removed = []) {
  const notes = [...params.pre.filter((_, i) => !removed.includes(i)), ...placed];
  return eq(sum(notes.map(unit)), params.target);
}
