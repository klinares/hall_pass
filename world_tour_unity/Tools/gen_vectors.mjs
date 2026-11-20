/* Prints problem vectors from all seven of the web game's generators (imported read-only), so the C# ports
   (WorldTour.Core.Maths.Gen) can be checked problem for problem against the real JS.

   node Tools/gen_vectors.mjs                     writes Assets/Tests/EditMode/problem_vectors.json
   node Tools/gen_vectors.mjs --stdout            prints that JSON instead
   node Tools/gen_vectors.mjs --out <file>        writes it to <file> instead
   node Tools/gen_vectors.mjs --one dec 2 1234    prints one problem's canonical JSON (for diffing a failure)

   Canonical JSON (the C# Problem.ToCanonicalJson writes the same bytes):
   - object keys sorted by UTF-16 code unit, no whitespace;
   - numbers as JS String(n); NaN and Infinity as null (as JSON.stringify);
   - object keys whose value is undefined are left out, undefined in an array is null (as JSON.stringify);
   - strings escape " and \ with a backslash, and every character below 0x20 or above 0x7e as \u00xx
     (lowercase hex), so the text is plain ASCII.
   A problem's hash is the sha256 (lowercase hex) of its canonical JSON's ASCII bytes.

   For every skill and level the file holds:
   - seeds/hashes: per-problem hashes for 200 seeds (every 20th seed of helpers.mjs seeds(level), which
     is the 3,000-seed volume set, plus 50 edge and spread seeds);
   - allDigest: sha256 of the 3,000 seeds(level) hashes joined with "\n", so the C# volume set is
     checked against the JS as a whole;
   - samples: full canonical JSON for the first problem of each kind and the first two seeds, so a
     failure can be diffed field by field.
   The output is deterministic: rerunning gives the same bytes. */
import { writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import * as dec from '../../world_tour/math/gen/decimals.js';
import * as md from '../../world_tour/math/gen/multdiv.js';
import * as frac from '../../world_tour/math/gen/fractions.js';
import * as xy from '../../world_tour/math/gen/coords.js';
import * as vol from '../../world_tour/math/gen/volume.js';
import * as ops from '../../world_tour/math/gen/orderops.js';
import * as flu from '../../world_tour/math/gen/fluency.js';
import { seeds, RUNS } from '../../world_tour/tests/math/helpers.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const outArg = process.argv.indexOf('--out');
const outFile = outArg > 0 ? process.argv[outArg + 1] : join(here, '..', 'Assets', 'Tests', 'EditMode', 'problem_vectors.json');
/* dec, md and frac first (task 1.3a), then the 1.3b skills: appending keeps the 1.3a groups and samples
   byte-identical. Same order as world_tour/math/gen/index.js. */
const GENS = { dec, md, frac, xy, vol, ops, flu };
const LEVELS = [1, 2, 3];

const esc = s => {
  let out = '"';
  for (const ch of s) {
    for (let i = 0; i < ch.length; i++) {
      const c = ch.charCodeAt(i);
      if (c === 0x22) out += '\\"';
      else if (c === 0x5c) out += '\\\\';
      else if (c < 0x20 || c > 0x7e) out += '\\u' + c.toString(16).padStart(4, '0');
      else out += ch[i];
    }
  }
  return out + '"';
};

export function canon(v) {
  if (v === null || v === undefined) return 'null';
  if (typeof v === 'number') return Number.isFinite(v) ? String(v) : 'null';
  if (typeof v === 'boolean') return v ? 'true' : 'false';
  if (typeof v === 'string') return esc(v);
  if (Array.isArray(v)) return '[' + v.map(canon).join(',') + ']';
  if (typeof v === 'object') {
    const keys = Object.keys(v).filter(k => v[k] !== undefined).sort();
    return '{' + keys.map(k => esc(k) + ':' + canon(v[k])).join(',') + '}';
  }
  throw new Error('cannot canonicalise ' + typeof v);
}

const sha = s => createHash('sha256').update(Buffer.from(s, 'ascii')).digest('hex');

/* 50 extra seeds per level: edges of the 32-bit range, then a fixed LCG spread over newSeed's range */
function spread(level) {
  const out = [0, 1, 2, 3, 12345, 2147483646, 2147483647, 2147483648, 4294967295, 4294967294];
  let x = 20261120 + level;
  while (out.length < 50) {
    x = (Math.imul(x, 1103515245) + 12345) >>> 0;
    out.push(1 + (x % 2147483646));
  }
  return out;
}

if (process.argv[2] === '--one') {
  const [, , , skill, level, seed] = process.argv;
  process.stdout.write(canon(GENS[skill].generate(+level, +seed)) + '\n');
  process.exit(0);
}

const groups = [], samples = [];
for (const skill of Object.keys(GENS)) {
  for (const level of LEVELS) {
    const gen = s => canon(GENS[skill].generate(level, s));
    const all = seeds(level);
    if (all.length !== RUNS) throw new Error('seeds() changed length');
    const allDigest = sha(all.map(s => sha(gen(s))).join('\n'));
    const picked = [...all.filter((_, i) => i % 20 === 0), ...spread(level)];
    const hashes = picked.map(s => sha(gen(s)));
    const kinds = new Set();
    picked.forEach((s, i) => {
      const json = gen(s), kind = JSON.parse(json).kind;
      if (i < 2 || !kinds.has(kind)) samples.push({ skill, level, seed: s, json });
      kinds.add(kind);
    });
    groups.push({ skill, level, seeds: picked, hashes, allFirst: all[0], allStep: 7919, allCount: all.length, allDigest });
  }
}

const text = canon({ version: 1, groups, samples }) + '\n';
if (process.argv.includes('--stdout')) process.stdout.write(text);
else {
  writeFileSync(outFile, text);
  console.log(`wrote ${outFile}: ${groups.length} groups, ${groups.reduce((s, g) => s + g.seeds.length, 0)} hashed problems, ` +
    `${groups.reduce((s, g) => s + g.allCount, 0)} in digests, ${samples.length} samples, ${text.length} bytes`);
}
