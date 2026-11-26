/* Writes Assets/StreamingAssets/weeks.json from the web game's world_tour/data/weeks.js
   (imported read-only), with the same weeks, words and fields, in the same order (newest week first).
   Used once, for the first import, so the checked words carry over exactly (QUESTIONS #3).

   node Tools/weeks_to_json.mjs                    writes the project copy (refuses to replace a different one)
   node Tools/weeks_to_json.mjs --force            replaces it even if it differs
   node Tools/weeks_to_json.mjs --out <file>       writes somewhere else
   node Tools/weeks_to_json.mjs --stdout           prints it instead
   node Tools/weeks_to_json.mjs --vectors <file>   also writes the test vector (weeks.js as-is plus this output)

   The output is the same bytes on every run: fixed key order, 2-space indent, \n line ends, one final \n.
   After the first build, the deployed world/StreamingAssets/weeks.json is the source of truth
   (BuildWeb copies it back here). Edit that copy, or delete it so the next build recreates it.
   Field guide for the owner: Tools/WEEKS_FORMAT.md. Check a file: node Tools/validate_data.mjs */
import { writeFileSync, readFileSync, existsSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join, resolve } from 'node:path';
import { WEEKS } from '../../world_tour/data/weeks.js';

const here = dirname(fileURLToPath(import.meta.url));
const argv = process.argv.slice(2);
const argOf = k => { const i = argv.indexOf(k); return i >= 0 ? argv[i + 1] : undefined; };

/* Copy only the fields the game knows, in a fixed order. Optional fields only when present. */
function word(v) {
  const o = { w: v.w, s: v.s, h: v.h };
  if (v.x !== undefined) o.x = v.x;
  if (v.prefix !== undefined) o.prefix = { p: v.prefix.p, root: v.prefix.root, means: v.prefix.means };
  return o;
}
const out = { weeks: WEEKS.map(wk => ({ id: wk.id, label: wk.label, words: wk.words.map(word) })) };

/* Every key in weeks.js must be one we copied; otherwise the import would silently drop a field. */
const KNOWN = { week: ['id', 'label', 'words'], word: ['w', 's', 'h', 'x', 'prefix'], prefix: ['p', 'root', 'means'] };
for (const wk of WEEKS) {
  for (const k of Object.keys(wk)) if (!KNOWN.week.includes(k)) throw new Error(`weeks.js week ${wk.id} has an unknown field "${k}"`);
  for (const v of wk.words) {
    for (const k of Object.keys(v)) if (!KNOWN.word.includes(k)) throw new Error(`weeks.js word ${v.w} has an unknown field "${k}"`);
    if (v.prefix) for (const k of Object.keys(v.prefix)) if (!KNOWN.prefix.includes(k)) throw new Error(`weeks.js word ${v.w} prefix has an unknown field "${k}"`);
  }
}

const json = JSON.stringify(out, null, 2) + '\n';
if (/[^\x00-\x7f]/.test(json)) console.warn('note: the output has non-ASCII characters (written as UTF-8)');

if (argv.includes('--stdout')) process.stdout.write(json);
else {
  const file = resolve(argOf('--out') || join(here, '..', 'Assets', 'StreamingAssets', 'weeks.json'));
  if (existsSync(file) && readFileSync(file, 'utf8') !== json && !argv.includes('--force')) {
    console.error(`not writing: ${file} already exists and differs from weeks.js (it may hold hand edits).\n` +
      'Pass --force to replace it, or --out <file> to write somewhere else.');
    process.exit(1);
  }
  mkdirSync(dirname(file), { recursive: true });
  writeFileSync(file, json);
  const words = out.weeks.reduce((n, wk) => n + wk.words.length, 0);
  console.log(`wrote ${file} (${out.weeks.length} weeks, ${words} words)`);
}

const vec = argOf('--vectors');
if (vec) {
  /* source: weeks.js exactly as the web game holds it (no reordering, no field picking).
     output: the converter's bytes. Tests parse both and compare every field. */
  const vectors = { version: 1, words: WEEKS.reduce((n, wk) => n + wk.words.length, 0), source: WEEKS, output: json };
  const vjson = JSON.stringify(vectors, null, 1).replace(/[^\x00-\x7f]/g, c => '\\u' + c.charCodeAt(0).toString(16).padStart(4, '0')) + '\n';
  mkdirSync(dirname(resolve(vec)), { recursive: true });
  writeFileSync(resolve(vec), vjson);
  console.log(`wrote ${resolve(vec)}`);
}
