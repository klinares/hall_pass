/* Prints test vectors for the ticket screen (task 2.6) by running the web game's real renderTicket()
   (world_tour/screens/ticket.js) and the real pct line of show.js finish(), both read-only, so the C# port
   (Game/Ticket/TicketBarcode.cs and TicketText.cs) can be checked against them.

   node Tools/ticket_vectors.mjs                 writes Assets/Tests/EditMode/Ui/ticket_vectors.txt
   node Tools/ticket_vectors.mjs --stdout        prints it instead
   node Tools/ticket_vectors.mjs --out <file>    writes it somewhere else
   WT_ROOT=<path to world_tour>                  reads the web game from somewhere other than ../../world_tour

   Nothing in world_tour/ is changed. The browser globals ticket.js and its imports touch at load time
   (matchMedia, document, window, getComputedStyle) are stubbed, the clock is pinned per case so localISO()
   gives that case's date, and the root element is a stub whose innerHTML is captured and parsed.

   File format (plain ASCII; a non-ASCII character is written as a backslash, "u" and four hex digits):
     case <n>
     in.<key>=<value>      the inputs (see below)
     out.<key>=<value>     what the real ticket.js printed
     end
   A separate block "barcode <seed>" / "bars=<x>:<w>,..." / "end" lists barcode() rects for extra seeds.
   This file is kept ASCII: non-ASCII test strings are built with String.fromCharCode. */
import { writeFileSync, readFileSync } from 'node:fs';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { dirname, join, resolve } from 'node:path';

process.env.TZ = 'UTC';

const here = dirname(fileURLToPath(import.meta.url));
const WT = resolve(process.env.WT_ROOT || join(here, '..', '..', 'world_tour'));
const imp = p => import(pathToFileURL(join(WT, p)).href);

// ---- browser stubs (load-time only; renderTicket gets a stub root and never animates) ----
globalThis.matchMedia = () => ({ matches: false, addEventListener() {}, addListener() {} });
globalThis.document = { documentElement: { dataset: {} }, body: { classList: { add() {}, remove() {} } }, querySelector: () => null };
globalThis.window = { scrollTo() {}, addEventListener() {} };
globalThis.getComputedStyle = () => ({ getPropertyValue: () => '' });

const RealDate = Date;
function pinClock(iso) {
  const at = RealDate.parse(iso + 'T12:00:00Z');
  globalThis.Date = class extends RealDate {
    constructor(...a) { if (a.length) super(...a); else super(at); }
    static now() { return at; }
  };
}
function unpinClock() { globalThis.Date = RealDate; }

const { renderTicket } = await imp('screens/ticket.js');
const { MILESTONES } = await imp('data/milestones.js');
const { VENUES } = await imp('data/venues.js');

// the real pct expression from show.js finish(), evaluated as written
const showSrc = readFileSync(join(WT, 'js', 'show.js'), 'utf8');
const pctLine = showSrc.match(/const pct = (\(G\.firstTry \+ \.5 \* G\.hintOnly\) \/ Math\.max\(1, n\));/);
if (!pctLine) throw new Error('show.js: the pct line was not found (did finish() change?)');
const pctOf = new Function('G', 'n', 'return ' + pctLine[1] + ';');

// the real barcode() from ticket.js (not exported), evaluated as written
const ticketSrc = readFileSync(join(WT, 'screens', 'ticket.js'), 'utf8');
const bcSrc = ticketSrc.match(/function barcode\(seed\) \{[\s\S]*?\n\}/);
if (!bcSrc) throw new Error('ticket.js: barcode() was not found');
const barcode = new Function(bcSrc[0] + '\nreturn barcode;')();

const args = process.argv.slice(2);
const outAt = args.indexOf('--out');
const outFile = outAt >= 0 ? args[outAt + 1] : join(here, '..', 'Assets', 'Tests', 'EditMode', 'Ui', 'ticket_vectors.txt');

// mulberry32, only to vary the cases; every input is written out, so the C# side never re-derives them
function rng(a) { return () => { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }

const ch = (...codes) => String.fromCharCode(...codes);
const WORDS = ['because', 'friend', 'different', 'separate', 'receive', 'believe', 'library', 'february', 'necessary', 'rhythm',
  'calendar', 'definitely', 'beautiful', 'weird', 'island', 'answer', 'favorite', 'business', 'enough', 'through'];
const MODES = [['remix', 'remix'], ['slots', 'missing letters'], ['vowels', 'missing vowels'], ['fanmail', 'fan mail']];
const LABELS = ['week 1', 'week of nov 17', 'Week 12: Tricky Ones', 'b & c <words>', 'it' + ch(0x2019) + 's week 4'];

const ESCX = s => s.replace(/[^\x20-\x7e]/g, c => '\\u' + c.charCodeAt(0).toString(16).padStart(4, '0'));
const unesc = s => s.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, "'").replace(/&amp;/g, '&');
const text = h => unesc(h.replace(/<br>/g, '\n').replace(/<[^>]+>/g, '')).replace(/\s+\n/g, '\n').trim();
const one = (html, re) => { const m = html.match(re); return m ? m[1] : null; };

function bars(svg) {
  return [...svg.matchAll(/<rect x="(\d+)" y="0" width="(\d+)" height="30"\/>/g)].map(m => m[1] + ':' + m[2]).join(',');
}

function makeCase(i) {
  const r = rng(1000 + i);
  const pickN = (arr, k) => { const a = [...arr]; const out = []; while (out.length < k && a.length) out.push(a.splice(Math.floor(r() * a.length), 1)[0]); return out; };
  const encore = i % 5 === 3;
  const n = encore ? 1 + Math.floor(r() * 4) : (i % 7 === 6 ? 1 + Math.floor(r() * 4) : 10);
  const words = pickN(WORDS, n);
  const tags = words.map(() => { const x = r(); return x < .6 ? 'ok' : x < .8 ? 'hint' : 'miss'; });
  if (i % 4 === 0) tags.fill('ok');
  if (i % 9 === 1) tags.fill('hint');
  const hinted = new Set(), missed = new Set();
  words.forEach((w, k) => { if (tags[k] === 'hint') hinted.add(w); if (tags[k] === 'miss') { missed.add(w); if (r() < .4) hinted.add(w); } });
  // first-try and hint-only counts consistent with the tags (a miss can still add a clean bonus replay)
  const okN = tags.filter(t => t === 'ok').length, hintN = tags.filter(t => t === 'hint').length, missN = tags.filter(t => t === 'miss').length;
  const firstTry = okN + (missN && r() < .5 ? 1 : 0);
  const hintOnly = hintN;
  const fans = Math.floor(r() * (i % 3 === 0 ? 60000 : 1500)) * (i % 8 === 2 ? 0 : 1);
  const best = Math.floor(r() * (n + 1));
  const hintTotal = hinted.size + Math.floor(r() * 3);
  const [modeId, modeName] = MODES[i % MODES.length];
  const day = 1 + (i * 7) % 28, month = 1 + (i * 5) % 12;
  const date = `${2026 + (i % 2)}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
  const venue = VENUES[i % VENUES.length];
  const nst = i % 6 === 0 ? 0 : i % 6 === 1 ? 1 : i % 6 === 2 ? 3 : i % 6 === 3 ? 5 : i % 6 === 4 ? 2 : 4;
  const stickers = pickN(MILESTONES, nst);
  const nun = i % 4 === 1 ? 1 : i % 4 === 2 ? 2 : 0;
  const unlocks = pickN(VENUES.slice(1), nun);
  // bonus replays sit in the queue too; ticket.js filters them out
  const q = words.map(w => ({ v: { w }, bonus: false }));
  if (missN && !encore) q.splice(Math.min(4, q.length), 0, { v: { w: words[tags.indexOf('miss')] }, bonus: true });
  const G = { q, missed, hinted, unlocks, math: false, week: { label: LABELS[i % LABELS.length] }, encore, fans, firstTry, best, hintTotal, hintOnly };
  return { i, G, n, date, modeId, modeName, venue, stickers, words, tags, hinted };
}

function runCase(c) {
  const { G, n } = c;
  const pct = pctOf(G, n);
  let html = null;
  const root = { set innerHTML(v) { html = v; }, get innerHTML() { return html; }, querySelector: () => null };
  pinClock(c.date);
  try {
    renderTicket(root, { G, pct, n, mode: { id: c.modeId, name: c.modeName }, venue: c.venue, stickers: c.stickers, onEncore() {}, onAgain() {} });
  } finally { unpinClock(); }

  const L = [];
  const put = (k, v) => L.push(k + '=' + ESCX(String(v)));
  L.push('case ' + c.i);
  put('in.date', c.date); put('in.mode', c.modeId); put('in.modeName', c.modeName); put('in.week', G.week.label);
  put('in.encore', G.encore ? 1 : 0); put('in.venue', c.venue.id); put('in.n', n); put('in.fans', G.fans);
  put('in.first', G.firstTry); put('in.hintOnly', G.hintOnly); put('in.best', G.best); put('in.hintTotal', G.hintTotal);
  // songs: word:missed:hinted, main songs in order (the engine's Setlist order)
  put('in.songs', c.words.map(w => `${w}:${G.missed.has(w) ? 1 : 0}:${G.hinted.has(w) ? 1 : 0}`).join(','));
  put('in.stickers', c.stickers.map(m => m.id).join(','));
  put('in.unlocks', G.unlocks.map(v => v.id).join(','));

  put('out.pct', pct);
  put('out.pctRound', Math.round(pct * 100));
  put('out.eyebrow', text(one(html, /<p class="eyebrow">([\s\S]*?)<\/p>/)));
  put('out.head', text(one(html, /<h1 class="shout">([\s\S]*?)<\/h1>/)));
  put('out.sub', text(one(html, /<p class="lede">([\s\S]*?)<\/p>/)));
  put('out.top', text(one(html, /<div class="tk-top">([\s\S]*?)<\/div>/)));
  put('out.venue', text(one(html, /<div class="tk-venue">([\s\S]*?)<\/div>/)));
  put('out.fansNum', text(one(html, /<div class="tk-fans"><b>([\s\S]*?)<\/b>/)));
  put('out.fans', text(one(html, /<div class="tk-fans">([\s\S]*?)<\/div>/)));
  const rows = [...html.matchAll(/<div><dt>([\s\S]*?)<\/dt><dd>([\s\S]*?)<\/dd><\/div>/g)].map(m => text(m[1]) + ':' + text(m[2]));
  put('out.rows', rows.join('|'));
  put('out.class', text(one(html, /<span class="tk-class">([\s\S]*?)<\/span>/)));
  put('out.bars', bars(one(html, /(<svg class="barcode"[\s\S]*?<\/svg>)/)));
  put('out.barSeed', c.date + c.modeId + G.fans);
  put('out.date', text(one(html, /<span class="tk-date">([\s\S]*?)<\/span>/)));
  const stamp = one(html, /<span class="stamp">([\s\S]*?)<\/span>/);
  put('out.stamp', stamp == null ? '' : text(stamp));
  const newstk = /<a class="unlock newstk"[\s\S]*?<\/a>/.test(html);
  put('out.stkTitle', newstk ? text(one(html, /<a class="unlock newstk"[\s\S]*?<p class="eyebrow">([\s\S]*?)<\/p>/)) : '');
  put('out.stkNames', newstk ? text(one(html, /<a class="unlock newstk"[\s\S]*?<p class="hand-h">([\s\S]*?)<\/p>/)) : '');
  put('out.stkArt', newstk ? (one(html, /<span class="newstk-row">([\s\S]*?)<\/span>\s*<div>/).match(/<svg/g) || []).length : 0);
  const unl = one(html, /<p class="eyebrow">new venue unlocked<\/p><p class="hand-h">([\s\S]*?)<\/p>/);
  put('out.unlockTitle', unl == null ? '' : 'new venue unlocked');
  put('out.unlock', unl == null ? '' : text(unl));
  put('out.setTitle', text(one(html, /<h2 class="hand-h">([\s\S]*?)<\/h2>/)));
  const lis = [...html.matchAll(/<li class="(\w+)"><span class="w">([\s\S]*?)<\/span><span class="t">([\s\S]*?)<\/span><\/li>/g)]
    .map(m => m[1] + ':' + text(m[2]) + ':' + text(m[3]));
  put('out.setlist', lis.join('|'));
  const btn = html.match(/<button class="btn grow" id="(encore|again)">([\s\S]*?)<\/button>/);
  put('out.button', btn ? btn[1] + ':' + text(btn[2]) : '');
  put('out.back', text(one(html, /<a class="btn ghost grow" href="#\/map">([\s\S]*?)<\/a>/)));
  put('out.bubble', text(one(html, /<div class="bubble">([\s\S]*?)<\/div>/)));
  L.push('end');
  return L.join('\n');
}

const CASES = 60;
const blocks = [];
for (let i = 0; i < CASES; i++) blocks.push(runCase(makeCase(i)));

// extra barcode seeds: edge cases and the shapes a real seed takes (date + mode id + fans)
const seeds = ['', 'a', '2026-11-20remix0', '2026-11-20remix1', '2026-11-20remix999999', '2027-02-28fanmail123456',
  '2026-01-01slots42', 'zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz', ch(0xe9) + 't' + ch(0xe9), ch(0xd83c, 0xdfb8) + 'x'];
const r = rng(77);
for (let k = 0; k < 200; k++) seeds.push(`2026-${String(1 + Math.floor(r() * 12)).padStart(2, '0')}-${String(1 + Math.floor(r() * 28)).padStart(2, '0')}${MODES[k % 4][0]}${Math.floor(r() * 250000)}`);
for (const s of seeds) blocks.push('barcode ' + ESCX(s) + '\nbars=' + bars(barcode(s)) + '\nend');

const out = '# ticket_vectors.txt: written by world_tour_unity/Tools/ticket_vectors.mjs from world_tour/screens/ticket.js; do not edit by hand\n' + blocks.join('\n') + '\n';
if (/[^\x00-\x7f]/.test(out)) throw new Error('vector file is not ASCII');
if (args.includes('--stdout')) process.stdout.write(out);
else { writeFileSync(outFile, out); console.log(`ticket_vectors: ${CASES} cases, ${seeds.length} barcode seeds -> ${outFile}`); }
