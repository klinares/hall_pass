/* Prints test vectors from the web game's rng.js, rational.js and money.js (imported read-only),
   so the C# ports in Assets/Core/Math can be checked against them bit for bit.

   node Tools/js_vectors.mjs            writes Assets/Tests/EditMode/js_vectors.json
   node Tools/js_vectors.mjs --stdout   prints the JSON instead

   Doubles are written as their 64-bit pattern in hex ("bits") as well as their raw 32-bit
   source ("u32"), so no JSON number parser can round them. */
import { writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { rng } from '../../world_tour/math/rng.js';
import * as Q from '../../world_tour/math/rational.js';
import { dollars, noCarryAdd, noBorrowSub } from '../../world_tour/math/money.js';

const here = dirname(fileURLToPath(import.meta.url));
const outFile = join(here, '..', 'Assets', 'Tests', 'EditMode', 'js_vectors.json');

const bits = x => { const v = new DataView(new ArrayBuffer(8)); v.setFloat64(0, x); return v.getBigUint64(0).toString(16).padStart(16, '0'); };
const times = (n, f) => Array.from({ length: n }, f);

/* ---------- rng ---------- */
const SEEDS = [1, 42, 123456789, 2147483646, 0, 4294967295, -1, 7, 1001, 8920];
const INT_RANGES = [[3, 9], [-5, 5], [0, 0], [10, 99], [5, 3], [0, 2147483646]];
const PICK_ARRAY = ['a', 'b', 'c', 'd', 'e', 'f', 'g'];
const SHUFFLE_ARRAY = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
const CHANCE_P = 0.3; /* the C# test uses the literal 0.3 too, so no parsing is involved */

function rngCase(seed) {
  let r = rng(seed);
  const raw = times(20, () => r.next());
  const next = { u32: raw.map(x => x * 4294967296), bits: raw.map(bits) };
  r = rng(seed); const int1to10 = times(20, () => r.int(1, 10));
  const intRanges = INT_RANGES.map(([lo, hi]) => { const q = rng(seed); return { lo, hi, values: times(20, () => q.int(lo, hi)) }; });
  r = rng(seed); const pick = times(20, () => r.pick(PICK_ARRAY));
  r = rng(seed); const shuffles = times(3, () => ({ values: r.shuffle(SHUFFLE_ARRAY) }));
  r = rng(seed); const chance03 = times(20, () => r.chance(CHANCE_P));
  /* interleaved calls on one generator, to prove every method consumes next() the same way */
  r = rng(seed);
  const mixed = [];
  for (let i = 0; i < 5; i++) {
    mixed.push({ op: 'int', lo: 1, hi: 6, value: r.int(1, 6), values: [] });
    mixed.push({ op: 'pick', lo: 0, hi: 0, value: PICK_ARRAY.indexOf(r.pick(PICK_ARRAY)), values: [] });
    mixed.push({ op: 'shuffle', lo: 0, hi: 0, value: 0, values: r.shuffle(SHUFFLE_ARRAY.slice(0, 5)) });
    mixed.push({ op: 'chance', lo: 0, hi: 0, value: r.chance(0.5) ? 1 : 0, values: [] });
    mixed.push({ op: 'next', lo: 0, hi: 0, value: r.next() * 4294967296, values: [] });
  }
  return { seed, next, int1to10, intRanges, pick, shuffles, chance03, mixed };
}

/* ---------- rational and money: { fn, args, out } with the result as text ---------- */
const fr = o => `${o.n}/${o.d}`;
const F = ([n, d]) => Q.R(n, d);
const calls = [];
function call(fn, args, f) {
  let out;
  try { out = f(); } catch (e) { out = 'throws'; }
  calls.push({ fn, args, out: String(out) });
}

const pairs = [[1, 3, 1, 6], [3, 4, 1, 6], [5, 2, 6, 5], [3, 4, 3, 8], [-1, 2, 1, 3], [1, 2, -3, 4], [-2, 3, -5, 7], [0, 1, 3, 4], [7, 12, 5, 18], [3, 4, 0, 1]];
for (const [n, d] of [[3, -6], [-3, -6], [-3, 6], [0, -5], [0, 5], [12, 18], [7, 1], [1, 0], [0, 0], [-4, 1]]) call('R', [n, d], () => fr(Q.R(n, d)));
for (const [a, b] of [[12, 18], [0, 0], [0, 5], [-12, 18], [12, -18], [17, 5], [1, 1]]) call('gcd', [a, b], () => Q.gcd(a, b));
for (const [a, b] of [[4, 6], [0, 0], [-4, 6], [4, -6], [3, 5], [0, 7], [12, 18]]) call('lcm', [a, b], () => Q.lcm(a, b));
for (const p of pairs) {
  const a = F(p), b = F(p.slice(2));
  call('add', p, () => fr(Q.add(a, b)));
  call('sub', p, () => fr(Q.sub(a, b)));
  call('mul', p, () => fr(Q.mul(a, b)));
  call('div', p, () => fr(Q.div(a, b)));
  call('eq', p, () => Q.eq(a, b));
  call('cmp', p, () => Q.cmp(a, b));
}
call('eq', [2, 4, 1, 2], () => Q.eq(Q.R(2, 4), Q.R(1, 2)));
call('sum', [1, 2, 1, 3, 1, 6], () => fr(Q.sum([Q.R(1, 2), Q.R(1, 3), Q.R(1, 6)])));
call('sum', [], () => fr(Q.sum([])));
call('sum', [-1, 4, 3, 8, 5, 1], () => fr(Q.sum([Q.R(-1, 4), Q.R(3, 8), Q.R(5)])));
for (const [n, d] of [[2, 4], [1, 2], [0, 1], [0, 5], [-3, 9], [5, 4]]) call('isSimplest', [n, d], () => Q.isSimplest({ n, d }));
for (const [n, d] of [[4, 2], [3, 2], [0, 7], [-6, 3], [1, 0]]) call('isInt', [n, d], () => Q.isInt({ n, d }));
for (const [n, d] of [[1, 3], [2, 3], [1, 10], [-7, 4], [22, 7], [1, 1]]) call('num', [n, d], () => bits(Q.num(Q.R(n, d))));
const shapes = [[3, 2], [4, 2], [3, 12], [7, 4], [0, 1], [-1, 2], [-7, 4], [-4, 2], [1, -3], [5, 1], [100, 3], [-100, 3]];
for (const [n, d] of shapes) {
  call('mixed', [n, d], () => { const m = Q.mixed(Q.R(n, d)); return `${m.w} ${m.n}/${m.d}`; });
  call('str', [n, d], () => Q.str(Q.R(n, d)));
}
for (const [n, d] of [[13, 20], [52, 1000], [450, 1], [1, 2], [-1, 8], [-13, 20], [1, 3], [1, 512], [1, 1024], [0, 1], [123, 1], [-5, 1], [7, 4], [1, 1000000000], [3, 1250]]) call('dec', [n, d], () => Q.dec(Q.R(n, d)));

for (const c of [350, 5, 30, 0, 99, 100, 101, 1205, 100000, -5, -150, -100, -1, 999999]) call('dollars', [c], () => dollars(c));
for (const list of [[175, 250], [199, 101], [5, 5], [999, 1], [], [12], [1, 22, 333], [-5, 7], [0, 0], [99, 99, 99]]) call('noCarryAdd', list, () => noCarryAdd(list));
for (const [a, b] of [[1000, 375], [375, 1000], [500, 125], [0, 0], [7, 3], [3, 7], [-5, 3], [-5, -3], [1234, 5678], [10, 1]]) call('noBorrowSub', [a, b], () => noBorrowSub(a, b));

const out = { generatedBy: 'world_tour_unity/Tools/js_vectors.mjs', pickArray: PICK_ARRAY, shuffleArray: SHUFFLE_ARRAY, rng: SEEDS.map(rngCase), calls };
const json = JSON.stringify(out, null, 1) + '\n';
if (process.argv.includes('--stdout')) process.stdout.write(json);
else { writeFileSync(outFile, json); console.log(`wrote ${outFile}: ${SEEDS.length} seeds, ${calls.length} calls`); }
