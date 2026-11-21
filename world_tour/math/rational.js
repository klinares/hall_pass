/* Exact fractions: { n, d } with integer numerator and positive denominator. */
export const gcd = (a, b) => { a = Math.abs(a); b = Math.abs(b); while (b) [a, b] = [b, a % b]; return a || 1; };
export const lcm = (a, b) => a / gcd(a, b) * b;

/* reduced */
export function R(n, d = 1) {
  if (!Number.isInteger(n) || !Number.isInteger(d) || d === 0) throw new Error(`bad fraction ${n}/${d}`);
  if (d < 0) { n = -n; d = -d; }
  const g = gcd(n, d);
  return { n: n / g, d: d / g };
}
export const add = (a, b) => R(a.n * b.d + b.n * a.d, a.d * b.d);
export const sub = (a, b) => R(a.n * b.d - b.n * a.d, a.d * b.d);
export const mul = (a, b) => R(a.n * b.n, a.d * b.d);
export const div = (a, b) => R(a.n * b.d, a.d * b.n);
export const eq = (a, b) => a.n * b.d === b.n * a.d;
export const cmp = (a, b) => a.n * b.d - b.n * a.d;
export const sum = list => list.reduce(add, R(0));
export const isSimplest = f => gcd(f.n, f.d) === 1;
export const isInt = f => R(f.n, f.d).d === 1;
export const num = f => f.n / f.d;

/* whole part and leftover, for mixed numbers: 3/2 → { w: 1, n: 1, d: 2 } */
export function mixed(f) {
  const r = R(f.n, f.d), w = Math.floor(r.n / r.d);
  return { w, n: r.n - w * r.d, d: r.d };
}

/* "3/4", "1 1/2", "2" */
export function str(f) {
  const m = mixed(f);
  if (m.n === 0) return String(m.w);
  return m.w ? `${m.w} ${m.n}/${m.d}` : `${m.n}/${m.d}`;
}

/* a fraction with a terminating decimal, written out: 13/20 → "0.65" */
export function dec(f) {
  const r = R(f.n, f.d);
  let k = 0, p = 1;
  while ((p * r.n) % r.d !== 0) { k++; p *= 10; if (k > 9) throw new Error('not a terminating decimal'); }
  const scaled = (p * r.n) / r.d, s = String(Math.abs(scaled)).padStart(k + 1, '0');
  const out = k ? `${s.slice(0, -k)}.${s.slice(-k)}` : s;
  return (scaled < 0 ? '-' : '') + out;
}
