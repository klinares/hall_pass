/* The bulb gives hint steps 1 and 2; only step 3 (after the third miss) may state the answer.
   leaks(p) spots a step that writes "= <answer>". guarded(build) re-rolls with the same seeded
   rng until no step leaks, so a saved seed still regenerates the same problem. */
import { rng as makeRng } from '../rng.js';
import { show } from '../answer.js';

export function leaks(p) {
  if (!['int', 'money', 'decimal'].includes(p.answer.type)) return false;
  const ans = show(p.answer).replace(/[$.]/g, m => '\\' + m);
  const re = new RegExp(`= (<b>)?${ans}(</b>)?([.,:]|\\s|$)`);
  return p.steps.slice(0, 2).some(s => re.test(s));
}

export const guarded = build => (level, seed) => {
  const r = makeRng(seed);
  let p;
  for (let i = 0; i < 40; i++) { p = build(level, seed, r); if (!leaks(p)) return p; }
  return p;
};
