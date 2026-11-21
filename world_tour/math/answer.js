/* Parse and compare typed answers.
   0.5 = .5 = .50; $3.50 = 3.5 for money; 1 1/2 = 3/2 = 1.5 for fractions;
   2/4 = 1/2 unless accept.simplest, in which case 2/4 is "right amount, now simplify". */
import { R, eq, isSimplest, str, dec, gcd } from './rational.js';
import { dollars } from './money.js';

/* text → { v: {n,d}, simplest, form } or null. form: 'int' | 'decimal' | 'fraction' | 'mixed' */
export function parseNumber(text) {
  if (text == null) return null;
  const s = String(text).trim().replace(/^\$\s*/, '').replace(/,/g, '').replace(/\s+/g, ' ');
  let m;
  if ((m = s.match(/^(\d+) (\d+)\/(\d+)$/))) {
    const w = +m[1], n = +m[2], d = +m[3];
    if (!d) return null;
    return { v: R(w * d + n, d), simplest: n < d && gcd(n, d) === 1, form: 'mixed' };
  }
  if ((m = s.match(/^(\d+)\/(\d+)$/))) {
    const n = +m[1], d = +m[2];
    if (!d) return null;
    return { v: R(n, d), simplest: gcd(n, d) === 1, form: 'fraction' };
  }
  if ((m = s.match(/^(\d*)\.(\d+)$/)) || (m = s.match(/^(\d+)\.$/))) {
    const whole = m[1] || '0', frac = m[2] || '';
    return { v: R(+(whole + frac), 10 ** frac.length), simplest: true, form: 'decimal' };
  }
  if ((m = s.match(/^\d+$/))) return { v: R(+s), simplest: true, form: 'int' };
  return null;
}

/* the answer's value as a fraction */
export function valueOf(answer) {
  switch (answer.type) {
    case 'money': return R(answer.value, 100);
    case 'int': return R(answer.value);
    case 'decimal': case 'fraction': return R(answer.value.n, answer.value.d);
    default: throw new Error('no numeric value for ' + answer.type);
  }
}

/* → { ok, simplify, empty, bad } */
export function check(text, answer, accept = {}) {
  if (answer.type === 'point') {
    const p = typeof text === 'object' ? text : parsePoint(text);
    return { ok: !!p && p.x === answer.value.x && p.y === answer.value.y, simplify: false, empty: !p };
  }
  if (String(text ?? '').trim() === '') return { ok: false, simplify: false, empty: true };
  const p = parseNumber(text);
  if (!p) return { ok: false, simplify: false, bad: true };
  const same = eq(p.v, valueOf(answer));
  if (same && accept.simplest && !p.simplest) return { ok: false, simplify: true };
  return { ok: same, simplify: false };
}

export function parsePoint(text) {
  const m = String(text ?? '').match(/^\s*\(?\s*(\d+)\s*,\s*(\d+)\s*\)?\s*$/);
  return m ? { x: +m[1], y: +m[2] } : null;
}

/* does a typed answer equal some other value of the same answer type (used to spot a known mistake) */
export function matches(text, value, type) {
  if (type === 'point') { const p = typeof text === 'object' ? text : parsePoint(text); return !!p && p.x === value.x && p.y === value.y; }
  const p = parseNumber(text);
  return !!p && eq(p.v, valueOf({ type, value }));
}

/* how the game writes an answer. Fractions come back as [[n/d]] markup, which the UI stacks. */
export function show(answer) {
  switch (answer.type) {
    case 'money': return dollars(answer.value);
    case 'int': return answer.value.toLocaleString('en-US');
    case 'decimal': return dec(answer.value);
    case 'fraction': return `[[${str(answer.value)}]]`;
    case 'point': return `(${answer.value.x}, ${answer.value.y})`;
    default: return String(answer.value);
  }
}
export const isSimplestForm = f => isSimplest(f);
