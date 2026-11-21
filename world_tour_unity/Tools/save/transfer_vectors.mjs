/* Import vectors for Assets/Tests/EditMode/SaveTransferTests.cs (task 5.2). The web game has no export or import,
   so this checks the two ways a save made by the web game could reach Unity's office:
     - the file: the text the web game keeps in localStorage under worldtour.v1 (JSON.stringify of store.js state),
       which is exactly what Unity's "download save file" writes, so a parent could save it as a .json and load it;
     - the code: Unity's WT1 format (DECISIONS.md, phase 1.5) made here independently with node's own zlib
       (raw DEFLATE, base64url without padding, CRC-32 of the JSON as 8 hex digits), so the C# reader is checked
       against a second implementation and not only against itself.
   The saves come from the real web game code (store.js, progress.js, review.js, the generators; imported read only),
   driven by random progress.js calls.

   Run from the repo root:  node world_tour_unity/Tools/save/transfer_vectors.mjs [outfile]
   (default outfile: world_tour_unity/Assets/Tests/EditMode/transfer_vectors.txt; WT_ROOT overrides ../../../world_tour)
   Lines: "<seed><TAB><today><TAB><stored json><TAB><WT1 code>", non-ASCII in the JSON as \uXXXX (still valid JSON). */
import { writeFileSync } from 'node:fs';
import { deflateRawSync, crc32 } from 'node:zlib';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { dirname, join, resolve } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const WT = resolve(process.env.WT_ROOT || join(here, '..', '..', '..', 'world_tour'));
const imp = p => import(pathToFileURL(join(WT, p)).href);
/* store.js and progress.js touch window, document and addEventListener at load: inert stand-ins (no localStorage,
   so the store runs on its in-memory copy and never writes anywhere) */
globalThis.window = {};
globalThis.document = { visibilityState: 'hidden', dispatchEvent() {} };
globalThis.addEventListener = () => {};
const store = await imp('js/store.js');
const P = await imp('js/progress.js');
const { rng } = await imp('math/rng.js');
const { generate } = await imp('math/gen/index.js');
const { localISO } = await imp('js/util.js');

const outFile = process.argv[2] || join(here, '..', '..', 'Assets', 'Tests', 'EditMode', 'transfer_vectors.txt');
const SEEDS = 60;
const esc = t => String(t).replace(/[^\x20-\x7e]/g, c => '\\u' + c.charCodeAt(0).toString(16).padStart(4, '0'));
const b64url = b => b.toString('base64').replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
const code = json => { const raw = Buffer.from(json, 'utf8'); return 'WT1-' + b64url(deflateRawSync(raw, { level: 9 })) + '-' + (crc32(raw) >>> 0).toString(16).padStart(8, '0'); };

const SKILLS = ['dec', 'frac', 'md', 'xy', 'vol', 'ops', 'flu'];
const SPELL = ['spell:slots', 'spell:vowels', 'spell:remix', 'spell:fanmail', 'spell:studio', 'spell:lyric', 'spell:prefix'];
const WORDS = ['illegible', 'irregular', 'intercept', 'arrival', 'believe', 'calendar', 'because', 'friend'];
const TAGS = ['no-borrow', 'gave-total', 'swapped', 'too-short', 'near', 'wrong-op'];
const MODES = ['remix', 'slots', 'merch', 'beat', 'soundcheck', 'truck'];
const today = localISO();

const lines = ['# transfer vectors v1, written by Tools/save/transfer_vectors.mjs from world_tour/js/store.js + progress.js (today ' + today + ')'];
for (let seed = 1; seed <= SEEDS; seed++) {
  const r = rng(seed * 7919);
  const st = store.get();
  for (const k of Object.keys(st)) delete st[k];
  Object.assign(st, store.defaults());
  const ops = seed === 1 ? 0 : r.int(1, seed < 50 ? 60 : 400);
  for (let i = 0; i < ops; i++) {
    const op = r.int(0, 7);
    if (op === 0) P.addFans(r.int(1, 90) * 100);
    else if (op === 1) { const id = r.pick([...SKILLS, ...SPELL]); P.recordSkill(id, { correct: r.chance(.6), hinted: r.chance(.3), mistake: r.chance(.3) ? r.pick(TAGS) : null }); }
    else if (op === 2) P.recordWord(r.pick(WORDS), { clean: r.chance(.5), hinted: r.chance(.3), missed: r.chance(.3), keep: r.chance(.2) });
    else if (op === 3) { const sk = r.pick(SKILLS), lv = r.int(1, 3), sd = r.int(1, 99999); P.recordMath(sk, { skill: sk, level: lv, seed: sd, prob: generate(sk, lv, sd) }, { clean: r.chance(.5), missed: r.chance(.4), keep: false }); }
    else if (op === 4) P.noteMistake(r.pick(SKILLS), r.pick(TAGS));
    else if (op === 5) { if (r.chance(.5)) P.setSetting('sound', r.chance(.5)); else P.setSetting('motion', r.pick(['reduced', 'auto'])); }
    else if (op === 6) { const n = r.int(1, 10); P.finishShow({ mode: r.pick(MODES), venue: 'coffee', n, first: r.int(0, n), hinted: r.int(0, 2), fans: r.int(0, 900), secs: r.int(30, 600), best: r.int(0, n), sold: r.chance(.2) || undefined, encore: r.chance(.2) || undefined }); }
    else P.markSeen(r.pick(['records', 'rec', 'rooftop']));
  }
  if (seed === 2) st.settings.theme = 'peach-navy';
  if (seed === 3) st.words['café'] = { a: 1, c: 1, first: 1, h: 0, miss: 0, last: today };
  st.updated = 1763600000000 + seed;
  const json = JSON.stringify(st);
  lines.push(seed + '\t' + today + '\t' + esc(json) + '\t' + code(json));
}
const text = lines.join('\n') + '\n';
writeFileSync(outFile, text);
console.log(`wrote ${outFile} (${text.length} bytes, ${SEEDS} saves)`);
process.exit(0);
