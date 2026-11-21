import { test } from 'node:test';
import assert from 'node:assert/strict';
import { R, add, sub, mul, div, eq, str, dec, mixed, lcm, gcd } from '../../math/rational.js';
import { parseNumber, check, show, matches } from '../../math/answer.js';
import { dollars, noCarryAdd, noBorrowSub } from '../../math/money.js';
import { rng } from '../../math/rng.js';

test('rational arithmetic is exact', () => {
  assert.deepEqual(add(R(1, 3), R(1, 6)), R(1, 2));
  assert.deepEqual(sub(R(3, 4), R(1, 6)), R(7, 12));
  assert.deepEqual(mul(R(5, 2), R(6, 5)), R(3));
  assert.deepEqual(div(R(3, 4), R(3, 8)), R(2));
  assert.ok(eq(R(2, 4), R(1, 2)));
  assert.equal(str(R(3, 2)), '1 1/2');
  assert.equal(str(R(4, 2)), '2');
  assert.equal(str(R(3, 12)), '1/4');
  assert.deepEqual(mixed(R(7, 4)), { w: 1, n: 3, d: 4 });
  assert.equal(dec(R(13, 20)), '0.65');
  assert.equal(dec(R(52, 1000)), '0.052');
  assert.equal(dec(R(450)), '450');
  assert.equal(lcm(4, 6), 12);
  assert.equal(gcd(12, 18), 6);
  assert.throws(() => R(1, 0));
});

test('money stays in whole cents', () => {
  assert.equal(dollars(350), '$3.50');
  assert.equal(dollars(5), '$0.05');
  assert.equal(dollars(10 + 20), '$0.30');
  assert.equal(noCarryAdd([175, 250]), 325);      /* 1.75 + 2.50 without carrying: 3.25 */
  assert.equal(noCarryAdd([199, 101]), 290);
  assert.equal(noBorrowSub(1000, 375), 1375);     /* digit by digit, smaller from larger */
});

test('equivalent typed answers', () => {
  const half = { type: 'decimal', value: R(1, 2) };
  for (const t of ['0.5', '.5', '.50', '0.50', '1/2', '2/4']) assert.ok(check(t, half).ok, t);
  const money = { type: 'money', value: 350 };
  for (const t of ['$3.50', '3.5', '3.50', '$3.5', ' 3.50 ']) assert.ok(check(t, money).ok, t);
  assert.ok(!check('3.05', money).ok);
  assert.ok(!check('35', money).ok);
  const frac = { type: 'fraction', value: R(3, 2) };
  for (const t of ['1 1/2', '3/2', '1.5', '6/4']) assert.ok(check(t, frac).ok, t);
  assert.ok(check('1,000', { type: 'int', value: 1000 }).ok);
  assert.ok(check('12.0', { type: 'int', value: 12 }).ok);
});

test('simplest form: right amount, now simplify', () => {
  const a = { type: 'fraction', value: R(1, 2) };
  assert.deepEqual(check('2/4', a, { simplest: true }), { ok: false, simplify: true });
  assert.ok(check('1/2', a, { simplest: true }).ok);
  assert.ok(check('2/4', a, { simplest: false }).ok);
  const b = { type: 'fraction', value: R(5, 4) };
  assert.ok(check('1 1/4', b, { simplest: true }).ok);
  assert.ok(check('5/4', b, { simplest: true }).ok);
  assert.deepEqual(check('1 2/8', b, { simplest: true }), { ok: false, simplify: true });
});

test('bad input is not a miss-shaped crash', () => {
  const a = { type: 'int', value: 5 };
  assert.deepEqual(check('', a), { ok: false, simplify: false, empty: true });
  assert.ok(check('5/0', a).bad);
  assert.ok(check('abc', a).bad);
  assert.equal(parseNumber('1/'), null);
});

test('points and matches', () => {
  const p = { type: 'point', value: { x: 3, y: 5 } };
  assert.ok(check('(3, 5)', p).ok);
  assert.ok(check({ x: 3, y: 5 }, p).ok);
  assert.ok(!check({ x: 5, y: 3 }, p).ok);
  assert.ok(matches('2/4', R(1, 2), 'fraction'));
  assert.ok(matches('$1.25', 125, 'money'));
  assert.equal(show({ type: 'fraction', value: R(3, 2) }), '[[1 1/2]]');
  assert.equal(show({ type: 'money', value: 1205 }), '$12.05');
});

test('seeded rng repeats', () => {
  const a = rng(42), b = rng(42);
  for (let i = 0; i < 100; i++) assert.equal(a.next(), b.next());
  const r = rng(7);
  for (let i = 0; i < 1000; i++) { const n = r.int(3, 9); assert.ok(n >= 3 && n <= 9 && Number.isInteger(n)); }
});
