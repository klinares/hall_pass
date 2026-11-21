/* Office screen vectors for Assets/Tests/EditMode/Ui/OfficeVectorTests.cs (task 5.2), made by the web game's
   own manager's office: world_tour/screens/office.js mount() runs on a fake root, and its HTML is read back
   into a plain trace. Everything in world_tour/ is imported read only; the browser-only modules it pulls in
   (sound, motion, speech, sticker and dog art, look) are swapped for no-op stubs by hooks.mjs.

   Run from the repo root:
     node --import ./world_tour_unity/Tools/office/register.mjs world_tour_unity/Tools/office/office_vectors.mjs [outfile]
   (default outfile: world_tour_unity/Assets/Tests/EditMode/Ui/office_vectors.txt; WT_ROOT overrides ../../../world_tour)

   Lines:
     "# ..."                                     header
     "day<TAB>iso=label|iso=label|..."           office.js day() for a spread of dates (the formula copied word for word)
     "<seed><TAB><today><TAB><save><TAB><trace>" one office render: the save as store.js would store it after mount
                                                 (mount awards stickers), and the trace of what the screen shows.
   The trace format is OfficeText.Trace's: parts joined by "|", fields by ";", non-ASCII as \uXXXX. */
import { writeFileSync } from 'node:fs';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { dirname, join, resolve } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const WT = resolve(process.env.WT_ROOT || join(here, '..', '..', '..', 'world_tour'));
const imp = p => import(pathToFileURL(join(WT, p)).href);
const store = await imp('js/store.js');
const { rng } = await imp('math/rng.js');
const { MILESTONES, skillKey } = await imp('data/milestones.js');
const { MODES } = await imp('modes/index.js');
const { localISO } = await imp('js/util.js');
const office = await imp('screens/office.js');

const outFile = process.argv[2] || join(here, '..', '..', 'Assets', 'Tests', 'EditMode', 'Ui', 'office_vectors.txt');
export const SEEDS = 120;

const esc = t => String(t).replace(/[^\x20-\x7e]/g, c => '\\u' + c.charCodeAt(0).toString(16).padStart(4, '0'));
const unhtml = s => s.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, "'").replace(/&amp;/g, '&');

/* office.js:15, copied word for word (it isn't exported) */
const day = d => new Date(d + 'T12:00').toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' }).toLowerCase();

const TODAY = localISO();
const back = k => new Date(Date.parse(TODAY) - k * 864e5).toISOString().slice(0, 10);
const WORDS = ['illegible', 'irregular', 'intercept', 'arrival', 'believe', 'calendar', 'because', 'friend', 'island', 'separate', 'necessary', 'rhythm', 'library', 'february'];
const TAGS = ['no-borrow', 'gave-total', 'swapped', 'too-short', 'multiplied-exponent', 'near', 'made-up', 'wrong-op', 'added-denominators',
  'decimal-place', 'left-to-right', 'one-layer', 'neighbor-fact', 'zeros-off', 'wrote-digits', 'gave-remainder', 'rounded-up'];
const VENUES = ['records', 'rec', 'rooftop', 'club', 'theater', 'festival', 'arena'];

function makeSave(seed) {
  const r = rng(seed), s = store.defaults();
  s.created = back(r.int(0, 90));
  const big = r.chance(.3);
  s.fans.lifetime = big ? r.int(0, 2000) * 1000 + r.int(0, 999) : r.int(0, 9999);
  s.fans.balance = r.int(0, s.fans.lifetime);
  s.streak.best = r.int(0, 12); s.streak.days = r.int(0, s.streak.best); s.streak.last = r.chance(.7) ? back(r.int(0, 20)) : null;
  for (const v of VENUES) if (r.chance(.3)) s.venues.unlocked.push(v);
  for (const m of MODES) {
    if (!r.chance(.55)) continue;
    const a = r.chance(.1) ? 0 : r.int(1, 120), c = r.int(0, a), h = r.int(0, a);
    const k = { level: r.int(1, 3), run: r.int(0, 3), missRun: r.int(0, 1), a, c, h, byLevel: { 1: [r.int(0, 9), 0], 2: [0, 0], 3: [0, 0] }, mistakes: {} };
    if (m.kind === 'math') {
      const nt = r.int(0, 4);
      for (let j = 0; j < nt; j++) { const tag = r.pick(TAGS); k.mistakes[tag] = (k.mistakes[tag] || 0) + r.int(0, 9); }
    }
    s.skills[skillKey(m)] = k;
  }
  const nw = r.int(0, 16);
  for (let j = 0; j < nw; j++) {
    const w = r.pick(WORDS), a = r.int(0, 12);
    s.words[w] = { a, c: r.int(0, a), first: r.int(0, a), h: r.int(0, a), miss: r.int(0, 5), last: r.chance(.7) ? back(r.int(0, 40)) : null };
  }
  const ns = r.int(0, 30);
  for (let j = 0; j < ns; j++) {
    const n = r.int(1, 10);
    s.shows.push({ d: back(r.int(0, 20)), mode: r.pick(MODES).id, n, first: r.int(0, n), hinted: r.int(0, 2), fans: r.int(0, 900), sold: r.chance(.2), encore: r.chance(.2) });
  }
  const nt = r.int(0, 18);
  for (let j = 0; j < nt; j++) s.time[back(r.int(0, 25))] = r.chance(.15) ? r.int(0, 59) : r.int(0, 4000);
  for (const m of MILESTONES) if (r.chance(.1)) s.journal.stickers[m.id] = back(r.int(0, 30));
  const nrw = r.pick([0, 1, 2, 5]);
  for (let j = 0; j < nrw; j++) s.review.words.push(r.pick(WORDS));
  const nrm = r.pick([0, 1, 3]);
  for (let j = 0; j < nrm; j++) s.review.math.push({ skill: 'dec', level: 1, seed: j });
  return s;
}

/* a root that keeps what office.js writes into it, and buttons that accept their handlers */
function fakeRoot() {
  const els = {};
  return {
    html: '',
    set innerHTML(v) { this.html = v; },
    get innerHTML() { return this.html; },
    querySelector(sel) { return els[sel] ||= { hidden: false, onclick: null, onchange: null, focus() {} }; }
  };
}

function rowsOf(html) {
  const out = [];
  const re = /<tr(?: class="([^"]*)")?><th scope="row">([\s\S]*?)<\/th>((?:<td>[\s\S]*?<\/td>)*)<\/tr>/g;
  let m;
  while ((m = re.exec(html))) {
    const cells = [...m[3].matchAll(/<td>([\s\S]*?)<\/td>/g)].map(x => unhtml(x[1]));
    out.push({ cls: m[1] || '', cells: [unhtml(m[2]), ...cells] });
  }
  return out;
}

function traceOf(html) {
  const t = [];
  const glance = [...html.matchAll(/<div><dt>([\s\S]*?)<\/dt><dd>([\s\S]*?)<\/dd><\/div>/g)].map(m => unhtml(m[1]) + '=' + unhtml(m[2]));
  t.push('g;' + glance.join(';'));
  for (const m of html.matchAll(/<li><span class="dd">([\s\S]*?)<\/span><span class="bar"><i style="width:([\d.]+)%"><\/i><\/span><span class="dv">([\s\S]*?)<\/span><\/li>/g))
    t.push('d;' + unhtml(m[1]) + ';' + m[2] + ';' + unhtml(m[3]));
  const cards = html.split('<div class="ocard');
  const card = head => cards.find(c => c.includes('<h2>' + head + '</h2>')) || '';
  const acc = card('accuracy by mode');
  const [, spell, math] = acc.split(/<h3>(?:spelling|math)<\/h3>/);
  for (const [kind, part] of [['spelling', spell], ['math', math]])
    for (const r of rowsOf(part)) t.push('s;' + kind + ';' + r.cells.join(';') + (r.cls === 'unplayed' ? ';unplayed' : ''));
  const words = card('words missed most');
  const wempty = words.match(/<p class="oempty">([\s\S]*?)<\/p>/);
  if (wempty) t.push('w0;' + unhtml(wempty[1]));
  for (const r of rowsOf(words)) t.push('w;' + r.cells.join(';'));
  const mist = card('math mistakes seen most');
  const mempty = mist.match(/<p class="oempty">([\s\S]*?)<\/p>/);
  if (mempty) t.push('m0;' + unhtml(mempty[1]));
  for (const r of rowsOf(mist)) t.push('m;' + r.cells.join(';'));
  return esc(t.join('|'));
}

/* every fixed string office.js shows, so the C# copy can be compared word for word */
function copyOf(html) {
  const strip = s => unhtml(s.replace(/<[^>]+>/g, '')).replace(/\s+/g, ' ').trim();
  const bits = [];
  for (const m of html.matchAll(/<(h1|h2|h3|p|button|label|th)( [^>]*)?>([\s\S]*?)<\/\1>/g)) {
    if (m[1] === 'th' && /scope="row"/.test(m[2] || '')) continue;
    if (m[1] === 'label') { bits.push('label=' + strip(m[3])); continue; }
    bits.push(m[1] + '=' + strip(m[3]));
  }
  const back = html.match(/<a class="backlink"[^>]*>([\s\S]*?)<\/a>/);
  if (back) bits.unshift('back=' + strip(back[1]));
  return esc(bits.join('|'));
}

const lines = ['# office vectors v1, written by Tools/office/office_vectors.mjs from world_tour/screens/office.js (today ' + TODAY + ')'];
const days = [];
for (let k = -400; k <= 400; k += 5) days.push(new Date(Date.UTC(2026, 10, 20) + k * 864e5).toISOString().slice(0, 10));
days.push('2024-02-29', '2026-01-01', '2026-12-31', '2027-09-05', '2028-02-29');
lines.push('day\t' + esc(days.map(d => d + '=' + day(d)).join('|')));
let copied = false;
for (let seed = 1; seed <= SEEDS; seed++) {
  const raw = seed === 1 ? store.defaults() : makeSave(seed);
  const st = store.get();
  for (const k of Object.keys(st)) delete st[k];
  Object.assign(st, store.migrate(JSON.parse(JSON.stringify(raw))));
  const root = fakeRoot();
  office.mount(root);
  if (!copied) { lines.push('copy\t' + copyOf(root.html)); copied = true; }
  lines.push(seed + '\t' + TODAY + '\t' + esc(JSON.stringify(store.get())) + '\t' + traceOf(root.html));
}
const text = lines.join('\n') + '\n';
writeFileSync(outFile, text);
console.log(`wrote ${outFile} (${text.length} bytes, ${SEEDS} seeds, today ${TODAY})`);
process.exit(0);
