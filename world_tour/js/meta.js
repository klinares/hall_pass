/* Backstage, journal and manager's-office logic. Pure functions over the saved state:
   no DOM, no storage, so the unit tests run them in Node. Screens call these and then save. */
import { SHOP, itemById } from '../data/shop.js';
import { MILESTONES, milestoneById, skillKey } from '../data/milestones.js';

/* ---------- backstage ---------- */
export const owns = (s, id) => s.inventory.owned.includes(id) || itemById(id)?.price === 0;
export const equippedId = (s, slot) => `${slot}:${s.inventory.equipped[slot]}`;

/* spend fans to spend (balance) only; total fans never go down */
export function buy(s, id) {
  const it = itemById(id);
  if (!it) return { ok: false, reason: 'unknown' };
  if (owns(s, id)) return { ok: false, reason: 'owned' };
  if (s.fans.balance < it.price) return { ok: false, reason: 'short', need: it.price - s.fans.balance };
  s.fans.balance -= it.price;
  s.inventory.owned.push(id);
  return { ok: true };
}

export function equip(s, id) {
  const it = itemById(id);
  if (!it || it.slot === 'sticker' || !owns(s, id)) return false;
  s.inventory.equipped[it.slot] = id.slice(it.slot.length + 1);
  return true;
}

/* what the game should look like right now; anything unknown falls back to the free item */
export function look(s) {
  const pick = slot => { const it = itemById(equippedId(s, slot)); return it && owns(s, it.id) ? it : SHOP.find(i => i.slot === slot && i.price === 0); };
  const theme = pick('theme'), guitar = pick('guitar'), dog = pick('dog');
  return { theme: theme.id.slice(6), guitar: guitar.color, outfit: dog.id === 'dog:none' ? '' : dog.id.slice(4) };
}

/* ---------- tour journal ---------- */
/* award every milestone whose rule now holds; returns the newly earned ones */
export function earn(s, modes, today) {
  const ctx = { modes }, fresh = [];
  for (const m of MILESTONES) {
    if (s.journal.stickers[m.id]) continue;
    let ok = false;
    try { ok = !!m.test(s, ctx); } catch (e) { ok = false; }
    if (ok) { s.journal.stickers[m.id] = today; fresh.push(m); }
  }
  return fresh;
}

/* everything that sits on the journal page: earned milestones, then bought stickers */
export function pageItems(s) {
  const earned = MILESTONES.filter(m => s.journal.stickers[m.id])
    .map(m => ({ key: m.id, art: m.art, name: m.name, note: `earned ${s.journal.stickers[m.id]}` }));
  const bought = SHOP.filter(i => i.slot === 'sticker' && s.inventory.owned.includes(i.id))
    .map(i => ({ key: i.id, art: i.sticker, name: i.name, note: 'from backstage' }));
  return [...earned, ...bought];
}
export const toEarn = s => MILESTONES.filter(m => !s.journal.stickers[m.id]);

/* where a sticker sits before she moves it: a loose grid with a little jitter, as fractions of the page */
export function defaultSpot(i, cols = 5) {
  const r = Math.floor(i / cols), c = i % cols;
  const j = ((i * 37) % 11) / 11 - .5;
  return { x: +((c + .5) / cols + j * .06).toFixed(3), y: +Math.min(.94, .09 + r * .165 + j * .03).toFixed(3), r: Math.round(j * 14) };
}
export function spotOf(s, key, i, cols) { return s.journal.layout[key] || defaultSpot(i, cols); }
export function place(s, key, x, y, r = 0) {
  const cl = v => Math.min(.95, Math.max(.05, v));
  const prev = s.journal.layout[key];
  s.journal.layout[key] = { x: +cl(x).toFixed(3), y: +cl(y).toFixed(3), r: prev?.r ?? r };
}

/* ---------- manager's office ---------- */
export const MISTAKES = {
  'gave-total': 'gave the total instead of the change',
  'no-borrow': 'subtracted without regrouping (borrowing)',
  'no-carry': 'added without carrying',
  'decimal-place': 'decimal point in the wrong place',
  'added-instead': 'added instead of multiplying',
  'partial-only': 'multiplied by the ones digit only',
  'no-placeholder': 'left out the zero when multiplying by tens',
  'place-value': 'answer off by a factor of ten',
  'subtracted-instead': 'subtracted once instead of dividing',
  'rounded-up': 'counted a partly full row as full',
  'gave-remainder': 'gave the remainder instead of the quotient',
  'added-denominators': 'added the denominators',
  'subtracted-denominators': 'subtracted the denominators',
  'tops-not-rewritten': 'changed the denominator but not the numerator',
  'swapped': 'swapped the x and y coordinates',
  'moves-swapped': 'mixed up the across and up moves',
  'wrong-direction': 'moved or shifted the wrong way',
  'neighbor-fact': 'a neighboring times-table fact',
  'added': 'added instead of multiplying',
  'subtracted': 'subtracted instead of dividing',
  'zeros-off': 'wrong number of zeros or decimal places',
  'multiplied-exponent': 'read 10³ as 10 × 3',
  'wrote-digits': 'wrote 10³ as 103',
  'added-sides': 'added the side lengths instead of multiplying',
  'one-layer': 'counted one layer only',
  'visible-faces': 'counted only the cubes that show',
  'surface-area': 'found the outside surface, not the volume',
  'one-box': 'counted one of the two boxes',
  'divided-once': 'divided by one side instead of the floor area',
  'too-short': 'didn’t fill the measure',
  'too-long': 'overfilled the measure',
  'left-to-right': 'worked left to right, ignoring order of operations',
  'ignored-parentheses': 'ignored the parentheses',
  'missing-parentheses': 'picked an expression missing its parentheses',
  'extra-parentheses': 'picked an expression with parentheses that change the story',
  'added-all': 'added every number in the story',
  'wrong-grouping': 'grouped the wrong numbers together',
  'wrong-op': 'used the wrong operation'
};

const pct = (a, b) => b ? Math.round(100 * a / b) : null;

export function office(s, modes, today, days = 14) {
  const real = s.shows.filter(x => !x.encore);
  const totalSecs = Object.values(s.time).reduce((a, b) => a + b, 0);

  const skills = modes.map(m => {
    const k = s.skills[skillKey(m)];
    return { id: m.id, name: m.name, kind: m.kind, level: k?.level ?? 1, a: k?.a ?? 0, acc: pct(k?.c ?? 0, k?.a ?? 0), hint: pct(k?.h ?? 0, k?.a ?? 0),
             shows: real.filter(x => x.mode === m.id).length };
  });

  const words = Object.entries(s.words)
    .map(([w, e]) => ({ w, miss: e.miss, a: e.a, first: pct(e.first, e.a), last: e.last }))
    .filter(x => x.miss > 0)
    .sort((x, y) => y.miss - x.miss || (x.first ?? 0) - (y.first ?? 0) || x.w.localeCompare(y.w))
    .slice(0, 12);

  const byKey = Object.fromEntries(modes.map(m => [skillKey(m), m]));
  const mistakes = [];
  for (const [key, k] of Object.entries(s.skills)) {
    for (const [tag, n] of Object.entries(k.mistakes || {})) {
      if (n > 0 && tag !== 'near') mistakes.push({ tag, n, label: MISTAKES[tag] || tag, mode: byKey[key]?.name || key });
    }
  }
  mistakes.sort((x, y) => y.n - x.n || x.label.localeCompare(y.label));

  /* the last `days` calendar days, oldest first, zeros included */
  const t0 = Date.parse(today), daily = [];
  for (let i = days - 1; i >= 0; i--) {
    const d = new Date(t0 - i * 864e5).toISOString().slice(0, 10);
    daily.push({ d, secs: s.time[d] || 0, shows: real.filter(x => x.d === d).length });
  }

  return {
    shows: real.length, totalSecs, lifetime: s.fans.lifetime, balance: s.fans.balance,
    best: s.streak.best, review: { words: s.review.words.length, math: s.review.math.length },
    skills, words, mistakes: mistakes.slice(0, 10), daily,
    stickers: Object.keys(s.journal.stickers).length, stickersTotal: MILESTONES.length
  };
}

export const mins = secs => secs < 60 ? (secs ? '<1 min' : '0 min') : `${Math.round(secs / 60)} min`;
export { milestoneById };
