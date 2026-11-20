/* Prints test vectors from the web game's meta.js, shop.js and milestones.js (imported read-only), so the
   C# port (Core/Meta) can be checked against the real JS. meta.js imports only the two data files, so
   nothing needs stubbing.

   node Tools/meta_vectors.mjs                 writes Assets/Tests/EditMode/meta_vectors.txt
   node Tools/meta_vectors.mjs --stdout        prints it instead
   node Tools/meta_vectors.mjs --out <file>    writes it somewhere else
   WT_ROOT=<path to world_tour>                reads the web game from somewhere other than ../../world_tour

   Each case line is "<seed><TAB><trace>". A seed builds a sample save with mulberry32 (math/rng.js, which the
   C# Rng matches bit for bit), then runs a scripted mix of buy, equip, earn, place, finished shows and fan
   gains on it; the trace records every result and finally the look, inventory, stickers, journal page,
   what's left to earn and the office. MetaVectorTests.cs builds the same saves the same way and compares
   the traces. The recipe below and the one in MetaVectorTests.cs must stay step-for-step identical.
   Non-ASCII characters are written as \uXXXX escapes so the file stays plain ASCII. */
import { writeFileSync } from 'node:fs';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { dirname, join, resolve } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const WT = resolve(process.env.WT_ROOT || join(here, '..', '..', 'world_tour'));
const imp = p => import(pathToFileURL(join(WT, p)).href);
const M = await imp('js/meta.js');
const { SHOP } = await imp('data/shop.js');
const { MILESTONES, skillKey } = await imp('data/milestones.js');
const { rng } = await imp('math/rng.js');

const args = process.argv.slice(2);
const outAt = args.indexOf('--out');
const outFile = outAt >= 0 ? args[outAt + 1] : join(here, '..', 'Assets', 'Tests', 'EditMode', 'meta_vectors.txt');

export const SEEDS = 160;

/* modes/index.js MODES, in setlist order (id, name, kind, skill) */
const ALL_MODES = [
  ['slots', 'missing letters'], ['vowels', 'missing vowels'], ['remix', 'remix'], ['fanmail', 'fan mail'],
  ['studio', 'studio session'], ['lyric', 'lyric writer'], ['prefix', 'prefix mixer']
].map(([id, name]) => ({ id, name, kind: 'spelling' })).concat([
  ['merch', 'merch table', 'dec'], ['beat', 'beat maker', 'frac'], ['tickets', 'ticket sales', 'md'], ['stage', 'stage designer', 'xy'],
  ['truck', 'pack the truck', 'vol'], ['budget', 'tour budget', 'ops'], ['soundcheck', 'soundcheck', 'flu']
].map(([id, name, skill]) => ({ id, name, kind: 'math', skill })));
/* the four modes meta.test.mjs uses */
const TEST_MODES = [ALL_MODES[2], ALL_MODES[0], ALL_MODES[7], ALL_MODES[8]];
const WORDS = ['illegible', 'irregular', 'intercept', 'arrival', 'believe', 'calendar'];
const TAGS = ['no-borrow', 'gave-total', 'swapped', 'too-short', 'multiplied-exponent', 'near', 'made-up', 'wrong-op'];
const VENUES = ['records', 'rec', 'rooftop', 'club', 'theater', 'festival', 'arena'];
const ITEM_IDS = [...SHOP.map(i => i.id), 'nope:x'];
const KEYS = [...MILESTONES.map(m => m.id), ...SHOP.filter(i => i.slot === 'sticker').map(i => i.id), 'loose'];
const DAY0 = Date.UTC(2026, 9, 20);
const DAY = k => new Date(DAY0 + k * 864e5).toISOString().slice(0, 10);

const fresh = () => ({
  v: 1, settings: { sound: true, theme: 'tour-pink', motion: 'auto' },
  fans: { balance: 0, lifetime: 0 }, streak: { days: 0, best: 0, last: null },
  venues: { unlocked: ['coffee'], seen: ['coffee'] }, skills: {}, words: {}, review: { words: [], math: [] },
  shows: [], time: {},
  inventory: { owned: ['theme:tour-pink', 'guitar:classic', 'dog:none'], equipped: { theme: 'tour-pink', guitar: 'classic', dog: 'none' } },
  journal: { stickers: {}, layout: {} }
});

const esc = t => String(t).replace(/[^\x20-\x7e]/g, c => '\\u' + c.charCodeAt(0).toString(16).padStart(4, '0'));
const q = v => String(Math.round(v * 1000));
const spot = p => `${q(p.x)},${q(p.y)},${p.r}`;
const nul = v => v == null ? 'null' : String(v);
const buyText = r => r.ok ? 'ok' : r.reason === 'short' ? 'short:' + r.need : r.reason;

export function trace(seed) {
  const r = rng(seed), s = fresh(), out = [];
  const modes = [ALL_MODES, TEST_MODES, []][r.int(0, 2)];
  s.fans.balance = r.int(0, 80) * 1000;
  s.fans.lifetime = s.fans.balance + r.int(0, 50) * 1000;
  for (const it of SHOP) if (r.chance(.15) && !s.inventory.owned.includes(it.id)) s.inventory.owned.push(it.id);
  if (r.chance(.1)) s.inventory.owned.push('retired:item');
  for (const slot of ['theme', 'guitar', 'dog']) {
    const opts = SHOP.filter(i => i.slot === slot).map(i => i.id.slice(slot.length + 1));
    opts.push('gone');
    s.inventory.equipped[slot] = r.pick(opts);
  }
  s.streak.best = r.int(0, 11);
  for (const m of ALL_MODES) {
    if (!r.chance(.4)) continue;
    const level = r.int(1, 3), a = r.int(0, 30), c = r.int(0, a), h = r.int(0, a);
    const t3 = r.int(0, 15), c3 = r.int(0, 13);
    const k = { level, run: 0, missRun: 0, a, c, h, byLevel: { 1: [0, 0], 2: [0, 0], 3: [t3, c3] }, mistakes: {} };
    const nt = r.int(0, 3);
    for (let j = 0; j < nt; j++) { const tag = r.pick(TAGS); k.mistakes[tag] = (k.mistakes[tag] || 0) + r.int(0, 4); }
    s.skills[skillKey(m)] = k;
  }
  const nw = r.int(0, 5);
  for (let j = 0; j < nw; j++) {
    const w = r.pick(WORDS), a = r.int(0, 6);
    const c = r.int(0, a), first = r.int(0, a), miss = r.int(0, 3);
    const last = r.chance(.5) ? DAY(r.int(0, 30)) : null;
    s.words[w] = { a, c, first, h: 0, miss, last };
  }
  const ns = r.int(0, 14);
  for (let j = 0; j < ns; j++) {
    const d = DAY(r.int(0, 30)), n = r.int(1, 10);
    const first = r.int(0, n), mode = r.pick(ALL_MODES).id, sold = r.chance(.1), encore = r.chance(.2);
    s.shows.push({ d, mode, n, first, hinted: 0, fans: 0, sold, encore });
  }
  const nt = r.int(0, 6);
  for (let j = 0; j < nt; j++) { const d = DAY(r.int(0, 30)); s.time[d] = r.int(0, 1500); }
  for (const v of VENUES) if (r.chance(.25)) s.venues.unlocked.push(v);
  for (const m of MILESTONES) if (r.chance(.08)) s.journal.stickers[m.id] = r.chance(.25) ? '' : 'pre';
  if (r.chance(.1)) s.journal.stickers['old-thing'] = 'pre';
  if (r.chance(.3)) s.journal.layout['first-show'] = { x: .5, y: .5, r: r.int(-7, 7) };
  const nrw = r.int(0, 3);
  for (let j = 0; j < nrw; j++) s.review.words.push('w' + j);
  const nrm = r.int(0, 2);
  for (let j = 0; j < nrm; j++) s.review.math.push({ skill: 'dec', level: 1, seed: j, prob: null });
  const today = DAY(r.int(20, 30));

  const nops = r.int(5, 14);
  for (let i = 0; i < nops; i++) {
    const op = r.int(0, 5);
    if (op === 0) {
      const id = r.pick(ITEM_IDS), res = M.buy(s, id);
      out.push(`b ${id} ${buyText(res)} ${s.fans.balance} ${s.fans.lifetime}`);
    } else if (op === 1) {
      const id = r.pick(ITEM_IDS);
      out.push(`e ${id} ${M.equip(s, id) ? 1 : 0}`);
    } else if (op === 2) {
      out.push('n ' + M.earn(s, modes, today).map(m => m.id).join(','));
    } else if (op === 3) {
      const key = r.pick(KEYS), x = r.int(-30, 130) / 100, y = r.int(-30, 130) / 100, rot = r.int(-12, 12);
      M.place(s, key, x, y, rot);
      out.push(`p ${key} ${spot(s.journal.layout[key])}`);
    } else if (op === 4) {
      const n = r.int(1, 10), first = r.int(0, n), mode = r.pick(ALL_MODES).id, encore = r.chance(.2);
      s.shows.push({ d: today, mode, n, first, hinted: 0, fans: 0, sold: false, encore });
      s.streak.best += r.int(0, 2);
      out.push(`s ${mode} ${n} ${first} ${encore ? 1 : 0}`);
    } else {
      const add = r.int(1, 40) * 1000;
      s.fans.balance += add; s.fans.lifetime += add;
      out.push(`f ${s.fans.balance} ${s.fans.lifetime}`);
    }
  }

  const L = M.look(s);
  out.push(`look ${L.theme} ${L.guitar} ${L.outfit}`);
  out.push(`own ${s.inventory.owned.join(',')} eq ${s.inventory.equipped.theme},${s.inventory.equipped.guitar},${s.inventory.equipped.dog} fans ${s.fans.balance} ${s.fans.lifetime}`);
  out.push('st ' + Object.entries(s.journal.stickers).sort(([a], [b]) => a < b ? -1 : a > b ? 1 : 0).map(([k, v]) => `${k}=${v}`).join(','));
  out.push('page ' + M.pageItems(s).map((it, i) => `${it.key}|${it.art}|${it.name}|${it.note}|${spot(M.spotOf(s, it.key, i))}`).join(','));
  out.push('left ' + M.toEarn(s).map(m => m.id).join(','));
  const o = M.office(s, modes, today);
  out.push(`office ${o.shows} ${o.totalSecs} ${o.lifetime} ${o.balance} ${o.best} ${o.review.words} ${o.review.math} ${o.stickers} ${o.stickersTotal} ${M.mins(o.totalSecs)}`);
  out.push('sk ' + o.skills.map(k => `${k.id},${k.name},${k.kind},${k.level},${k.a},${nul(k.acc)},${nul(k.hint)},${k.shows}`).join('|'));
  out.push('wd ' + o.words.map(w => `${w.w},${w.miss},${w.a},${nul(w.first)},${nul(w.last)}`).join('|'));
  out.push('mi ' + o.mistakes.map(m => `${m.tag},${m.n},${m.label},${m.mode}`).join('|'));
  out.push('dy ' + o.daily.map(d => `${d.d},${d.secs},${d.shows}`).join('|'));
  return esc(out.join(' ; '));
}

/* fixed extras: default spots for several column counts, and mins() around its edges */
export function spots() {
  const out = [];
  for (const cols of [3, 4, 5, 6]) for (let i = 0; i < 60; i++) out.push(spot(M.defaultSpot(i, cols)));
  return out.join(' ');
}
export const MINS_SECS = [0, 1, 29, 59, 60, 89, 90, 91, 149, 150, 151, 3599, 3600, 5430, 86399];
export const minsLine = () => esc(MINS_SECS.map(M.mins).join('|'));

const lines = ['# meta vectors v1, written by Tools/meta_vectors.mjs from world_tour/js/meta.js', 'spots\t' + spots(), 'mins\t' + minsLine()];
for (let seed = 1; seed <= SEEDS; seed++) lines.push(seed + '\t' + trace(seed));
const text = lines.join('\n') + '\n';
if (args.includes('--stdout')) process.stdout.write(text);
else { writeFileSync(outFile, text); console.log(`wrote ${outFile} (${text.length} bytes, ${SEEDS} seeds)`); }
