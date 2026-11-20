/* Save vectors for Assets/Tests/EditMode/SaveVectorTests.cs, made by the real web game code
   (world_tour/js/store.js migrate, js/progress.js, js/review.js; all imported read only).
   Run from the repo root:  node world_tour_unity/Tools/save/save_vectors.mjs [outfile]
   (default outfile: world_tour_unity/Assets/Tests/EditMode/save_vectors.json). Deterministic apart from "today".

   - migrate: raw inputs (good, partial, old, garbage) and store.migrate's output for each;
   - replay: a list of progress.js calls and the saved state they leave, so the C# port can replay them,
     plus that state as the web game stores it (JSON.stringify, insertion key order);
   - canonical JSON as in Tools/gen_vectors.mjs (sorted keys, JS String(n) numbers, non-ASCII as \u00xx). */
import { writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const WT = new URL('../../../world_tour/', import.meta.url).href;
/* store.js and progress.js touch window, document and addEventListener at load; give them inert stand-ins
   (no localStorage, so the store runs on its in-memory copy and never writes anywhere) */
globalThis.window = {};
globalThis.document = { visibilityState: 'hidden', dispatchEvent() {} };
globalThis.addEventListener = () => {};

const store = await import(WT + 'js/store.js');
const P = await import(WT + 'js/progress.js');
const { localISO } = await import(WT + 'js/util.js');
const { generate } = await import(WT + 'math/gen/index.js');

const BS = String.fromCharCode(92);   /* a backslash, kept out of the source so it stays plain ASCII */
const hex4 = n => BS + 'u' + n.toString(16).padStart(4, '0');
const esc = s => '"' + s.split('').map(c => {
  const n = c.charCodeAt(0);
  if (c === '"' || c === BS) return BS + c;
  return n < 0x20 || n > 0x7e ? hex4(n) : c;
}).join('') + '"';
function canon(v) {
  if (v === null || v === undefined) return 'null';
  if (typeof v === 'number') return Number.isFinite(v) ? String(v) : 'null';
  if (typeof v === 'boolean') return v ? 'true' : 'false';
  if (typeof v === 'string') return esc(v);
  if (Array.isArray(v)) return '[' + v.map(canon).join(',') + ']';
  const keys = Object.keys(v).filter(k => v[k] !== undefined).sort();
  return '{' + keys.map(k => esc(k) + ':' + canon(v[k])).join(',') + '}';
}

const today = localISO();
const item = (skill, level, seed) => ({ skill, level, seed, prob: generate(skill, level, seed) });

/* ---------- replay: progress.js calls, in order ---------- */
const OPS = [
  ['addFans', 25000], ['markSeen', 'records'], ['addFans', 40000],
  ['recordSkill', 'dec', true, false, null], ['recordSkill', 'dec', true, true, null], ['recordSkill', 'dec', true, false, null],
  ['recordSkill', 'dec', true, false, null], ['recordSkill', 'dec', false, false, 'no-borrow'], ['noteMistake', 'dec', 'gave-total'],
  ['recordSkill', 'spell:remix', false, false, null], ['recordSkill', 'spell:remix', false, true, null],
  ['recordWord', 'because', false, true, true, false], ['recordWord', 'because', true, false, false, true],
  ['recordWord', 'friend', true, false, false, false], ['recordWord', 'island', false, false, true, false],
  ['recordWord', 'island', true, false, false, false],
  ['recordMath', 'dec', 'dec', 2, 12345, false, true, false], ['recordMath', 'ops', 'ops', 3, 77, false, true, false],
  ['recordMath', 'frac', 'frac', 1, 5, false, true, false], ['recordMath', 'frac', 'frac', 1, 5, true, false, true],
  ['recordMath', 'xy', 'xy', 2, 9, false, true, false], ['recordMath', 'dec', 'dec', 2, 12345, true, false, false],
  ['recordMath', 'vol', 'vol', 3, 31, false, true, false], ['recordMath', 'flu', 'flu', 1, 8, false, true, false],
  ['recordMath', 'md', 'md', 2, 4242, false, true, false],
  ['finishShow', { mode: 'remix', venue: 'coffee', n: 10, first: 9, hinted: 1, fans: 30000, secs: 300, best: 6, sold: true }],
  ['finishShow', { mode: 'remix', venue: 'coffee', n: 1, first: 1, hinted: 0, fans: 500, secs: 20, best: 1, encore: true }],
  ['finishShow', { mode: 'merch', venue: 'records', n: 10, first: 5, hinted: 3, fans: 12000, secs: 610, best: 3 }],
  ['setSetting', 'motion', 'reduced'], ['setSetting', 'sound', false]
];
store.reset();
for (const [op, ...a] of OPS) {
  if (op === 'addFans') P.addFans(a[0]);
  else if (op === 'markSeen') P.markSeen(a[0]);
  else if (op === 'recordSkill') P.recordSkill(a[0], { correct: a[1], hinted: a[2], mistake: a[3] });
  else if (op === 'noteMistake') P.noteMistake(a[0], a[1]);
  else if (op === 'recordWord') P.recordWord(a[0], { clean: a[1], hinted: a[2], missed: a[3], keep: a[4] });
  else if (op === 'recordMath') P.recordMath(a[0], item(a[1], a[2], a[3]), { clean: a[4], missed: a[5], keep: a[6] });
  else if (op === 'finishShow') P.finishShow(a[0]);
  else if (op === 'setSetting') P.setSetting(a[0], a[1]);
  else throw new Error(op);
}
const replayed = JSON.parse(JSON.stringify(store.get()));
/* text: what the web game stores, JSON.stringify in insertion order (non-ASCII written as escapes, as the C# save does) */
const ascii = t => t.split('').map(c => c.charCodeAt(0) > 0x7e ? hex4(c.charCodeAt(0)) : c).join('');
const replay = { ops: OPS, out: canon(replayed), text: ascii(JSON.stringify(replayed)), unlocked: [...replayed.venues.unlocked], streak: replayed.streak };
const defaultsText = ascii(JSON.stringify(store.defaults()));

/* ---------- migrate ---------- */
const INPUTS = [
  ['null', null], ['number', 5], ['string', 'x'], ['true', true], ['empty array', []], ['array', [1, 2]],
  ['empty object', {}],
  ['partial: fans and a venue, no coffee', { fans: { lifetime: 30000 }, venues: { unlocked: ['records'] } }],
  ['partial: wrong containers', { v: 0, settings: { sound: false }, skills: [], words: null, time: { '2026-11-01': 300 }, shows: {}, review: { words: 'x' } }],
  ['old: no journal, short inventory', { v: 1, created: '2026-09-01', fans: { balance: 5, lifetime: 9 }, inventory: { owned: ['theme:tour-pink'], equipped: { dog: 'cap' } } }],
  ['duplicates kept when coffee is there', { venues: { unlocked: ['coffee', 'records', 'records'], seen: ['coffee'] } }],
  ['duplicates dropped when coffee is added', { venues: { unlocked: ['records', 'records'] } }],
  ['nested arrays for groups', { settings: [], fans: [], streak: { days: 3, best: 4, last: '2026-11-18' }, journal: { stickers: { 'first-show': '2026-11-18' }, layout: { 'first-show': { x: 0.5, y: 0.25, r: -3 } } } }],
  ['old seed-only review entry and a damaged one', { review: { words: ['because'], math: [{ skill: 'dec', level: 2, seed: 12345 }, { skill: 'dec', level: 2, seed: 9, prob: { prompt: 'x' } }] } }],
  ['the replayed save', replayed],
  ['the replayed save through text', JSON.parse(JSON.stringify(replayed))]
];
const migrate = INPUTS.map(([name, raw]) => ({ name, raw: canon(raw), out: canon(store.migrate(raw)) }));

const out = { today, defaultsText, note: 'made by Tools/save/save_vectors.mjs from the web game; created = today for inputs without one', replay, migrate };
const file = process.argv[2] || join(here, '../../Assets/Tests/EditMode/save_vectors.json');
writeFileSync(file, JSON.stringify(out, null, 1).split('').map(c => c.charCodeAt(0) > 0x7e ? hex4(c.charCodeAt(0)) : c).join('') + String.fromCharCode(10));
console.log('wrote ' + file + ' (' + migrate.length + ' migrate cases, ' + OPS.length + ' replay calls, today ' + today + ')');
process.exit(0);
