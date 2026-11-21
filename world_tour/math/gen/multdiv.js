/* TICKET SALES: multi-digit multiplication and division (5.NBT.5-6).
   L1: 2-digit × 1-digit, 3-digit ÷ 1-digit
   L2: 2-digit × 2-digit, 3-digit ÷ 2-digit
   L3: 3-digit × 2-digit, 4-digit ÷ 2-digit, and "how many full rows" with a remainder */
import { noCarryAdd } from '../money.js';
import { cleanTraps } from '../mistakes.js';
import { guarded } from './guard.js';

export const SKILL = 'md';
const f = n => n.toLocaleString('en-US');

/* split a number into place-value parts: 347 → [300, 40, 7] (zeros dropped) */
export const parts = n => String(n).split('').map((d, i, a) => +d * 10 ** (a.length - 1 - i)).filter(Boolean);

export const generate = guarded(build);

/* posters ship in the box sizes a real order would have */
export const BOX_SIZES = [12, 24, 25, 48, 50, 100, 144];

function build(level, seed, r) {
  const kinds = level === 3 ? ['mul', 'div', 'rows'] : ['mul', 'div'];
  const kind = r.pick(kinds);
  let a, b, answer, prompt, label, steps, traps = [], params, work = null;

  if (kind === 'mul') {
    const tale = r.pick(['rows', 'boxes', 'other']);
    if (tale === 'boxes') {
      /* posters ship in real box sizes. level 3 needs 3-digit × 2-digit, so either a 144 box
         (×100 is too easy to count) or a 2-digit box size and a 3-digit number of boxes */
      if (level === 1) { a = r.pick(BOX_SIZES.filter(n => n < 100)); b = r.int(3, 9); }
      else if (level === 2) { a = r.pick(BOX_SIZES.filter(n => n < 100)); b = r.int(12, 99); }
      else { const size = r.pick([...BOX_SIZES.filter(n => n < 100), 144]); if (size > 99) { a = size; b = r.int(12, 99); } else { a = r.int(102, 999); b = size; } }
    } else if (level === 1) { a = r.int(12, 99); b = r.int(3, 9); }
    else if (level === 2) { a = r.int(12, 99); b = r.int(12, 99); }
    else { a = r.int(102, 999); b = r.int(12, 99); }
    const p = a * b;
    answer = { type: 'int', value: p };
    const per = level === 3 && tale === 'boxes' && a !== 144 ? b : a, count = per === a ? b : a;
    prompt = tale === 'rows' ? `${a} fans fit in each row and there are ${b} rows. how many seats is that?`
      : tale === 'boxes' ? (count >= 100 ? `posters come ${per} to a box. the tour ships ${f(count)} boxes over the whole tour. how many posters?`
                                         : `each box holds ${per} posters. the crew unloads ${count} boxes. how many posters?`)
      : a >= 100 ? `a show sells ${a} tickets. the tour plays ${b} shows. how many tickets in all?`
      : `each fan-club bus brings ${a} fans. ${b} buses pull in. how many fans is that?`;
    label = `${f(a)} × ${b}`;
    /* break the smaller factor into tens and ones (or the 1-digit factor's partner, at level 1) */
    const split = level === 1 ? parts(a) : parts(b);
    const other = level === 1 ? b : a;
    const pp = split.map(s => s * other);
    work = { kind: 'area', rowParts: [other], colParts: split };   /* matches the hint text: one factor whole, the other split by place value */
    if (split.length > 1) steps = [
      `break ${level === 1 ? a : b} into ${split.map(f).join(' + ')}, and multiply each part by ${f(other)}.`,
      split.map((s, i) => `${f(other)} × ${f(s)} = ${f(pp[i])}`).join(', ') + '. now add those up.',
      `${pp.map(f).join(' + ')} = <b>${f(p)}</b>`
    ];
    else {
      /* a round number (70): one part only, so the partial step is "times 7, then ten times bigger" */
      const s = split[0], digit = s / 10;
      steps = [
        `${f(s)} is ${digit} tens. multiply ${f(other)} by ${digit} first, then by 10.`,
        `${f(other)} × ${digit} = ${f(other * digit)}. now make that ten times bigger.`,
        `${f(other * digit)} × 10 = <b>${f(p)}</b>`
      ];
    }
    traps = [
      { value: noCarryAdd(pp), why: 'no-carry' },
      level > 1 ? { value: a * (b % 10), why: 'partial-only' } : null,
      level > 1 ? { value: a * (Math.floor(b / 10) + (b % 10)), why: 'no-placeholder' } : null,
      { value: a + b, why: 'added-instead' }
    ].filter(Boolean);
    params = { a, b, op: '×' };
  } else if (kind === 'div') {
    let d, q;
    if (level === 1) { d = r.int(2, 9); q = r.int(Math.ceil(100 / d), Math.floor(999 / d)); }
    else if (level === 2) { d = r.int(11, 49); q = r.int(Math.max(2, Math.ceil(100 / d)), Math.floor(999 / d)); }
    else { d = r.int(11, 99); q = r.int(Math.ceil(1000 / d), Math.min(999, Math.floor(9999 / d))); }
    const N = d * q;
    answer = { type: 'int', value: q };
    const story = r.pick([
      [`${f(N)} tickets are split evenly into ${d} sections. how many tickets per section?`],
      [`the crew packs ${f(N)} wristbands into ${d} equal bags. how many in each bag?`],
      [`${f(N)} fans line up in ${d} equal lines at the gates. how many fans per line?`]
    ]);
    prompt = story[0]; label = `${f(N)} ÷ ${d}`;
    /* partial quotients: first as many tens of the divisor as fit (189 → 18 tens = 180),
       then the ones with what's left (9), keeping a running total */
    const tens = Math.floor(q / 10) * 10, ones = q % 10, chunks = [];
    let left = N;
    for (const c of [tens, ones].filter(Boolean)) { chunks.push({ c, take: c * d, left: left - c * d }); left -= c * d; }
    /* one chunk (q = 4, or q = 40): the first chunk IS the answer, so the partial step
       stops short of it and asks whether one more fits */
    const lower = chunks.length > 1 ? null : q >= 20 ? q - 10 : q - 1;
    if (lower != null) { const take = lower * d; work = { kind: 'chunks', d, N, chunks, lower: { c: lower, take, left: N - take } }; }
    else work = { kind: 'chunks', d, N, chunks };
    const first = lower ?? tens, rest = q - first, after = N - first * d;
    const tensWord = n => `${f(n / 10)} ten${n === 10 ? '' : 's'}`;
    steps = [
      q === 10 ? `what happens to a number when you multiply it by 10? try it with ${d}.`
        : q >= 10 ? `how many tens of ${d} fit in ${f(N)}? one ten of ${d} is ${d} × 10 = ${f(d * 10)}.`
        : q <= 3 ? `how many ${d}s fit in ${f(N)}? ${d} × ${q - 1} = ${f(d * (q - 1))}. is there room for another?`
        : `how many ${d}s fit in ${f(N)}? try a few: ${d} × 2 = ${f(d * 2)}, ${d} × 3 = ${f(d * 3)}…`,
      q >= 10
        ? `${lower != null ? (first % 10 ? 'start with ' : `start with ${tensWord(first)} of ${d}: `) : `${tensWord(first)} of ${d} ${first === 10 ? 'fits' : 'fit'}: `}${d} × ${f(first)} = ${f(first * d)}. ${f(N)} − ${f(first * d)} = ${f(after)}. running total: ${f(first)}. now how many ${d}s fit in ${f(after)}?`
        : `${d} × ${f(first)} = ${f(first * d)}, which leaves ${f(after)}. running total: ${f(first)}. how many more ${d}s fit in that?`,
      `${d} × ${f(rest)} = ${f(rest * d)}, and ${f(after)} − ${f(rest * d)} = 0. running total: ${f(first)} + ${f(rest)} = <b>${f(q)}</b>.`
    ];
    traps = [
      { value: q * 10, why: 'place-value' },
      { value: q % 10 === 0 ? q / 10 : null, why: 'place-value' },
      { value: N - d, why: 'subtracted-instead' }
    ];
    params = { N, d, op: '÷' };
  } else {
    let d, q, rem;
    d = r.int(12, 48); q = r.int(Math.ceil(100 / d) + 1, Math.floor(999 / d)); rem = r.int(1, d - 1);
    const N = d * q + rem;
    answer = { type: 'int', value: q };
    prompt = `${f(N)} fans are coming and each row has ${d} seats. how many rows will be completely full?`;
    label = `full rows of ${d}`;
    work = { kind: 'remainder', d, N, q, rem };
    steps = [
      `divide ${f(N)} by ${d}. the leftover fans won’t fill a whole row.`,
      `${d} × ${f(q)} = ${f(d * q)} and ${d} × ${f(q + 1)} = ${f(d * (q + 1))}. which one fits under ${f(N)}?`,
      `${f(N)} ÷ ${d} = ${f(q)} remainder ${rem}. the last ${rem} fans sit in a row that isn’t full, so <b>${f(q)}</b> full rows.`
    ];
    traps = [
      { value: q + 1, why: 'rounded-up' },
      { value: rem, why: 'gave-remainder' },
      { value: N - d, why: 'subtracted-instead' }
    ];
    params = { N, d, op: '÷' };
  }

  return {
    skill: SKILL, level, kind, seed, params, prompt, label, answer,
    accept: { simplest: false }, choices: null,
    traps: cleanTraps(answer, traps), steps, work
  };
}
