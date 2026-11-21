/* TOUR BUDGET: order of operations (5.OA.1-2).
   L1: 2 operations with parentheses
   L2: 3 operations
   L3: pick the expression for a 2-step story, then work it out
   Every step along the way stays a whole number, never negative, every division exact. */
import { cleanTraps } from '../mistakes.js';
import { guarded } from './guard.js';

export const SKILL = 'ops';
const OPS = { '+': (a, b) => a + b, '−': (a, b) => a - b, '×': (a, b) => a * b, '÷': (a, b) => a / b };

export const tokenize = s => s.match(/\d+|[-+−×÷()]/g).map(t => /\d/.test(t) ? +t : t === '-' ? '−' : t);
export const untokenize = toks => toks.join(' ').replace(/\( /g, '(').replace(/ \)/g, ')');

/* one step: innermost parentheses first, then × ÷ left to right, then + − left to right.
   returns null if the step would leave whole, non-negative numbers */
export function step(toks) {
  let lo = 0, hi = toks.length;
  const close = toks.indexOf(')');
  if (close >= 0) { hi = close; lo = toks.lastIndexOf('(', close) + 1; }
  const seg = toks.slice(lo, hi);
  let i = seg.findIndex(t => t === '×' || t === '÷');
  if (i < 0) i = seg.findIndex(t => t === '+' || t === '−');
  let out;
  if (i < 0) {
    out = [...toks.slice(0, lo - 1), seg[0], ...toks.slice(hi + 1)];   /* (12) → 12 */
    return { toks: out, did: null };
  }
  const a = seg[i - 1], op = seg[i], b = seg[i + 1], v = OPS[op](a, b);
  if (!Number.isInteger(v) || v < 0) return null;
  const nseg = [...seg.slice(0, i - 1), v, ...seg.slice(i + 2)];
  if (nseg.length === 1 && close >= 0) out = [...toks.slice(0, lo - 1), v, ...toks.slice(hi + 1)];
  else out = [...toks.slice(0, lo), ...nseg, ...toks.slice(hi)];
  return { toks: out, did: `${a} ${op} ${b} = ${v}` };
}

/* every rewrite from the start to a single number, or null if some step breaks the rules */
export function solve(expr) {
  let toks = tokenize(expr);
  const states = [untokenize(toks)], did = [];
  for (let guard = 0; toks.length > 1 && guard < 20; guard++) {
    const s = step(toks);
    if (!s) return null;
    toks = s.toks;
    if (s.did) { did.push(s.did); states.push(untokenize(toks)); }
  }
  return { value: toks[0], states, did };
}

/* the two classic mistakes, for spotting them in a typed answer */
export function leftToRight(expr) {
  const t = tokenize(expr).filter(x => x !== '(' && x !== ')');
  let v = t[0];
  for (let i = 1; i < t.length; i += 2) v = OPS[t[i]](v, t[i + 1]);
  return v;
}
export function noParens(expr) {
  const s = solve(expr.replace(/[()]/g, ''));
  return s ? s.value : null;
}

const L1 = ['(a + b) × c', 'a × (b + c)', '(a − b) × c', 'a × (b − c)', '(a + b) ÷ c', 'a − (b + c)', '(a − b) ÷ c'];
const L2 = ['a + b × (c − d)', '(a + b) × c − d', 'a × b − c × d', '(a − b) × (c + d)', 'a ÷ b + c × d', 'a × (b + c) − d', 'a + (b + c) ÷ d'];

function fill(r, tpl, ranges) {
  for (let tries = 0; tries < 400; tries++) {
    const vals = { a: r.int(...ranges.a), b: r.int(...ranges.b), c: r.int(...ranges.c), d: r.int(...ranges.d) };
    const expr = tpl.replace(/[abcd]/g, k => vals[k]);
    const s = solve(expr);
    if (!s || s.value > 300 || s.value === 0) continue;
    if (tpl.includes('(') && noParens(expr) === s.value) continue;     /* the parentheses have to matter */
    return { expr, s };
  }
  return null;
}

const STORIES = [
  (r) => { const n = r.int(3, 9), a = r.int(2, 9), b = r.int(4, 15);
    return { text: `${n} fans each buy a $${a} pin and a $${b} poster. how much do they spend in all?`,
      right: `${n} × (${a} + ${b})`, wrong: [[`${n} × ${a} + ${b}`, 'missing-parentheses'], [`${n} + ${a} + ${b}`, 'added-all'], [`(${n} + ${a}) × ${b}`, 'wrong-grouping']] }; },
  (r) => { const n = r.int(3, 8), a = r.int(4, 12), m = n * a + r.int(5, 60);
    return { text: `the band has $${m}. they buy ${n} sets of strings at $${a} a set. how much is left?`,
      right: `${m} − ${n} × ${a}`, wrong: [[`(${m} − ${n}) × ${a}`, 'wrong-grouping'], [`${m} − ${n} − ${a}`, 'added-all'], [`${m} − ${n} + ${a}`, 'wrong-op']] }; },
  (r) => { const n = r.int(3, 9), a = r.int(6, 24), b = r.int(2, 11);
    return { text: `the crew unpacks ${n} boxes with ${a} shirts each, plus ${b} loose shirts. how many shirts?`,
      right: `${n} × ${a} + ${b}`, wrong: [[`${n} × (${a} + ${b})`, 'extra-parentheses'], [`${n} + ${a} + ${b}`, 'added-all'], [`(${n} + ${b}) × ${a}`, 'wrong-grouping']] }; },
  (r) => { const k = r.int(2, 6), tot = k * r.int(6, 30), a = r.int(Math.ceil(tot / 4), Math.floor(tot * 3 / 4)), b = tot - a;
    return { text: `gas costs $${a} and food costs $${b}. the ${k} band members split it evenly. how much does each pay?`,
      right: `(${a} + ${b}) ÷ ${k}`, wrong: [[`${a} + ${b} ÷ ${k}`, 'missing-parentheses'], [`${a} ÷ ${k} + ${b}`, 'wrong-grouping'], [`(${a} + ${b}) × ${k}`, 'wrong-op']] }; },
  (r) => { const n = r.int(5, 9), a = r.int(8, 20), b = r.int(2, n - 1);
    return { text: `the venue has ${n} rows of ${a} seats, but ${b} seats are broken. how many seats can fans use?`,
      right: `${n} × ${a} − ${b}`, wrong: [[`${n} × (${a} − ${b})`, 'extra-parentheses'], [`${n} + ${a} − ${b}`, 'wrong-op'], [`(${n} − ${b}) × ${a}`, 'wrong-grouping']] }; },
  (r) => { const k = r.int(2, 6), q = r.int(3, 12), m = k * q, b = r.int(1, Math.min(9, q - 1));
    return { text: `${k} fans share ${m} stickers equally. each fan already had ${b}. how many stickers does each fan have now?`,
      right: `${m} ÷ ${k} + ${b}`, wrong: [[`${m} ÷ (${k} + ${b})`, 'extra-parentheses'], [`(${m} + ${b}) ÷ ${k}`, 'wrong-grouping'], [`${m} ÷ ${k} − ${b}`, 'wrong-op']] }; }
];

export const generate = guarded(build);

function build(level, seed, r) {
  if (level === 3) return story(r, seed);
  const tpl = r.pick(level === 1 ? L1 : L2);
  const ranges = level === 1
    ? { a: [2, 15], b: [2, 12], c: [2, 9], d: [2, 9] }
    : { a: [2, 20], b: [2, 12], c: [2, 12], d: [2, 9] };
  let got = fill(r, tpl, ranges);
  if (!got) got = fill(r, level === 1 ? '(a + b) × c' : '(a + b) × c − d', { a: [2, 9], b: [2, 9], c: [2, 5], d: [1, 5] });
  const { expr, s } = got;
  const answer = { type: 'int', value: s.value };
  return {
    skill: SKILL, level, kind: 'eval', seed,
    params: { expr, states: s.states },
    prompt: `the budget sheet says <b class="expr">${expr}</b>. what does it come to?`,
    label: expr, answer, accept: { simplest: false }, choices: null,
    traps: cleanTraps(answer, [{ value: leftToRight(expr), why: 'left-to-right' }, { value: noParens(expr), why: 'ignored-parentheses' }]),
    steps: [
      `parentheses first, then × and ÷, then + and −.`,
      `first: <b>${s.did[0]}</b>. that leaves ${s.states[1]}.`,
      s.states.join(' → ').replace(/→ ([^→]+)$/, '→ <b>$1</b>')
    ]
  };
}

function story(r, seed) {
  let st, s;
  for (let i = 0; i < 50; i++) {
    st = r.pick(STORIES)(r);
    s = solve(st.right);
    if (s && st.wrong.every(([w]) => { const ws = solve(w); return !ws || ws.value !== s.value; })) break;
  }
  const answer = { type: 'int', value: s.value };
  const choices = r.shuffle([{ value: st.right, correct: true, why: null }, ...st.wrong.map(([value, why]) => ({ value, correct: false, why }))]);
  return {
    skill: SKILL, level: 3, kind: 'story', seed,
    params: { expr: st.right, states: s.states },
    prompt: st.text, label: st.right, answer, accept: { simplest: false }, choices,
    traps: cleanTraps(answer, [{ value: leftToRight(st.right), why: 'left-to-right' }, { value: noParens(st.right), why: 'ignored-parentheses' }]),
    steps: [
      `first pick the expression that matches the story, then work it out: parentheses, then × and ÷, then + and −.`,
      `it’s <b>${st.right}</b>. first: ${s.did[0]}.`,
      s.states.join(' → ').replace(/→ ([^→]+)$/, '→ <b>$1</b>')
    ]
  };
}
