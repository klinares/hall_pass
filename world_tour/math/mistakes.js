/* Distractors and traps. A trap is a wrong answer a kid might type, tagged with the mistake
   it models, so the game can answer that mistake directly and the office can count it. */
import { R, eq } from './rational.js';

/* keep traps that are valid, non-negative, differ from the answer and from each other.
   values are in the answer's own units (cents, integers, {n,d}, {x,y}) */
export function cleanTraps(answer, traps) {
  const out = [];
  const same = (a, b) => {
    if (answer.type === 'point') return a.x === b.x && a.y === b.y;
    if (typeof a === 'object') return eq(a, b);
    return a === b;
  };
  for (const t of traps) {
    const v = t.value;
    if (v == null) continue;
    if (answer.type === 'point') { if (v.x < 0 || v.y < 0) continue; }
    else if (typeof v === 'object') { if (!Number.isInteger(v.n) || !Number.isInteger(v.d) || v.d <= 0 || v.n < 0) continue; }
    else if (!Number.isInteger(v) || v < 0) continue;
    if (same(v, answer.value) || out.some(o => same(o.value, v))) continue;
    out.push({ value: typeof v === 'object' && answer.type !== 'point' ? R(v.n, v.d) : v, why: t.why });
  }
  return out;
}

/* multiple choice: the answer plus up to n-1 tagged distractors, shuffled.
   `fill` supplies extra plausible values (tagged 'near') when the tagged ones run short. */
export function choiceSet(rng, answer, traps, fill, n = 4) {
  const tagged = cleanTraps(answer, traps).slice(0, n - 1);
  const extra = cleanTraps(answer, [...tagged, ...fill.map(v => ({ value: v, why: 'near' }))]).slice(tagged.length);
  const wrong = [...tagged, ...extra].slice(0, n - 1);
  return rng.shuffle([{ value: answer.value, correct: true, why: null }, ...wrong.map(w => ({ value: w.value, correct: false, why: w.why }))]);
}
