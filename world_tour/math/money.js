/* Money held as integer cents, so 0.1 + 0.2 never turns into 0.30000000000000004. */
export const dollars = c => `$${Math.floor(c / 100)}.${String(c % 100).padStart(2, '0')}`;

/* the digit-by-digit sum or difference a kid gets by never carrying or borrowing.
   Used to spot that mistake in a typed answer. */
export function noCarryAdd(nums) {
  const len = Math.max(...nums.map(n => String(n).length));
  let out = 0, place = 1;
  for (let i = 0; i < len; i++) {
    const d = nums.reduce((s, n) => s + Math.floor(n / place) % 10, 0) % 10;
    out += d * place; place *= 10;
  }
  return out;
}
export function noBorrowSub(a, b) {
  let out = 0, place = 1;
  while (place <= Math.max(a, b)) {
    const x = Math.floor(a / place) % 10, y = Math.floor(b / place) % 10;
    out += Math.abs(x - y) * place; place *= 10;
  }
  return out;
}
