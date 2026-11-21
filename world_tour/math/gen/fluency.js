/* SOUNDCHECK: quick facts and powers of ten (5.NBT.1-2). Multiple choice, with a timer that only
   changes bonus fans.
   L1: × and ÷ facts to 10
   L2: facts to 12, × and ÷ by 10 and 100
   L3: × and ÷ by 10, 100, 1000 with decimals, and powers of ten (10³, 4.5 × 10²) */
import { R, mul, div, dec } from '../rational.js';
import { choiceSet } from '../mistakes.js';
import { guarded } from './guard.js';

export const SKILL = 'flu';
const SUP = { 1: '¹', 2: '²', 3: '³', 4: '⁴' };
const f = n => n.toLocaleString('en-US');

/* hint 2 for moving the decimal point (× or ÷ 10, 100, 1,000): place-value wording that is true for every problem.
   × moves every digit left, ÷ moves it right. when a zero is filled in or drops off the end it asks about
   the zero instead of saying it, so the last step stays hers. never prints the digits or the answer.
   (older texts: 'the digits stay 441' for 8.82 × 10, then 'the digits don’t change', false for 140 ÷ 10) */
const placeValue = (times, p, k, zeroChanges) => `${times ? '×' : '÷'} ${f(p)} moves every digit ${k} place${k === 1 ? '' : 's'} to the ${times ? 'left' : 'right'}.${zeroChanges ? (times ? ' what fills the empty place?' : ' what happens to the 0 at the end?') : ''}`;

export const generate = guarded(build);

function build(level, seed, r) {
  const kind = level === 1 ? r.pick(['mul', 'div'])
    : level === 2 ? r.pick(['mul', 'div', 'pow', 'pow'])
    : r.pick(['decpow', 'decpow', 'decdiv', 'exp']);
  let answer, prompt, label, traps, fillers, steps;

  if (kind === 'mul' || kind === 'div') {
    const top = level === 1 ? 10 : 12;
    const a = r.int(2, top), b = r.int(2, top), p = a * b;
    if (kind === 'mul') {
      answer = { type: 'int', value: p };
      prompt = `${a} × ${b}`; label = prompt;
      traps = [{ value: a * (b + 1), why: 'neighbor-fact' }, { value: a * (b - 1), why: 'neighbor-fact' }, { value: a + b, why: 'added' }];
      fillers = [(a + 1) * b, (a - 1) * b, p + 10, p - 2];
      steps = [`think of ${a} groups of ${b}.`, `${a} × ${b - 1} = ${a * (b - 1)}. one more ${a}…`, `${a} × ${b} = <b>${p}</b>`];
    } else {
      answer = { type: 'int', value: b };
      prompt = `${p} ÷ ${a}`; label = prompt;
      traps = [{ value: b + 1, why: 'neighbor-fact' }, { value: b - 1, why: 'neighbor-fact' }, { value: p - a, why: 'subtracted' }];
      fillers = [b + 2, b - 2, a];
      steps = [`what times ${a} makes ${p}?`, `${a} × ${b - 1} = ${a * (b - 1)}. a little more…`, `${a} × ${b} = ${p}, so <b>${b}</b>`];
    }
  } else if (kind === 'pow') {
    const k = r.int(1, 2), p = 10 ** k, times = r.chance(.5);
    if (times) {
      const n = r.int(2, 999);
      answer = { type: 'int', value: n * p };
      prompt = `${f(n)} × ${f(p)}`;
      traps = [{ value: n * p * 10, why: 'zeros-off' }, { value: k === 2 ? n * 10 : n * 100, why: 'zeros-off' }, { value: n + p, why: 'added' }];
      fillers = [n * p * 100, n];
      steps = [`multiplying by ${f(p)} moves every digit ${k} place${k > 1 ? 's' : ''} to the left.`, k === 2 ? `${f(n)} × 10 = ${f(n * 10)}. once more…` : `every digit shifts one place left, and a 0 fills the ones place.`, `${f(n)} × ${f(p)} = <b>${f(n * p)}</b>`];
    } else {
      const q = r.int(2, 999), n = q * p;
      answer = { type: 'int', value: q };
      prompt = `${f(n)} ÷ ${f(p)}`;
      traps = [{ value: n * p, why: 'wrong-direction' }, { value: k === 2 ? q * 10 : (q % 10 === 0 ? q / 10 : null), why: 'zeros-off' }, { value: n - p, why: 'subtracted' }];
      fillers = [q * 100, q + 10];
      steps = [`dividing by ${f(p)} moves every digit ${k} place${k > 1 ? 's' : ''} to the right.`, k === 2 ? `${f(n)} ÷ 10 = ${f(n / 10)}. once more…` : `every digit shifts one place right, so the 0 in the ones place drops off.`, `${f(n)} ÷ ${f(p)} = <b>${f(q)}</b>`];
    }
    label = prompt;
  } else if (kind === 'decpow' || kind === 'decdiv') {
    const k = r.int(1, 3), p = 10 ** k;
    let x;
    if (kind === 'decpow') {
      const places = r.int(1, 2);
      let m; do m = r.int(11, places === 1 ? 99 : 999); while (m % 10 === 0);
      x = R(m, 10 ** places);
      answer = { type: 'decimal', value: mul(x, R(p)) };
      prompt = `${dec(x)} × ${f(p)}`;
      traps = [{ value: mul(x, R(p * 10)), why: 'zeros-off' }, { value: k > 1 ? mul(x, R(p / 10)) : null, why: 'zeros-off' }, { value: div(x, R(p)), why: 'wrong-direction' }];
      fillers = [mul(x, R(p * 100)), mul(x, R(10))];
      steps = [`× ${f(p)} moves the decimal point ${k} place${k > 1 ? 's' : ''} to the right.`, placeValue(true, p, k, k > places), `${dec(x)} × ${f(p)} = <b>${dec(answer.value)}</b>`];
    } else {
      const n = r.int(2, 999);
      x = R(n);
      answer = { type: 'decimal', value: div(x, R(p)) };
      prompt = `${f(n)} ÷ ${f(p)}`;
      traps = [{ value: div(x, R(p * 10)), why: 'zeros-off' }, { value: k > 1 ? div(x, R(p / 10)) : mul(x, R(10)), why: k > 1 ? 'zeros-off' : 'wrong-direction' }, { value: mul(x, R(p)), why: 'wrong-direction' }];
      fillers = [div(x, R(p * 100)), R(n)];
      steps = [`÷ ${f(p)} moves the decimal point ${k} place${k > 1 ? 's' : ''} to the left.`, placeValue(false, p, k, n % 10 === 0), `${f(n)} ÷ ${f(p)} = <b>${dec(answer.value)}</b>`];
    }
    label = prompt;
  } else {
    const k = r.int(2, 4), withX = r.chance(.6);
    if (!withX) {
      answer = { type: 'decimal', value: R(10 ** k) };
      prompt = `10${SUP[k]}`;
      traps = [{ value: R(10 * k), why: 'multiplied-exponent' }, { value: R(10 ** (k + 1)), why: 'zeros-off' }, { value: R(+`10${k}`), why: 'wrote-digits' }];
      fillers = [R(10 ** (k - 1))];
      steps = [`10${SUP[k]} means ${k} tens multiplied together.`, `${Array(k).fill(10).join(' × ')}`, `10${SUP[k]} = <b>${f(10 ** k)}</b>: a 1 with ${k} zeros.`];
    } else {
      let m; do m = r.int(11, 99); while (m % 10 === 0);
      const x = R(m, 10), kk = Math.min(k, 3);
      answer = { type: 'decimal', value: mul(x, R(10 ** kk)) };
      prompt = `${dec(x)} × 10${SUP[kk]}`;
      traps = [{ value: mul(x, R(10 * kk)), why: 'multiplied-exponent' }, { value: mul(x, R(10 ** (kk + 1))), why: 'zeros-off' }, { value: mul(x, R(10 ** (kk - 1))), why: 'zeros-off' }];
      fillers = [div(x, R(10))];
      steps = [`10${SUP[kk]} = ${f(10 ** kk)}. so this is ${dec(x)} × ${f(10 ** kk)}.`, `× ${f(10 ** kk)} moves the decimal point ${kk} places to the right.`, `${dec(x)} × ${f(10 ** kk)} = <b>${dec(answer.value)}</b>`];
    }
    label = prompt;
  }

  const choices = choiceSet(r, answer, traps, fillers);
  return {
    skill: SKILL, level, kind, seed,
    params: { seconds: level === 1 ? 8 : level === 2 ? 9 : 12 },
    prompt: `${prompt} = ?`, label, answer, accept: { simplest: false },
    choices, traps: choices.filter(c => !c.correct).map(c => ({ value: c.value, why: c.why })), steps
  };
}
