/* Backstage, journal and office logic (js/meta.js). Pure functions, so plain Node runs them. */
import test from 'node:test';
import assert from 'node:assert/strict';
import * as M from '../../js/meta.js';
import { SHOP, SLOTS } from '../../data/shop.js';
import { MILESTONES } from '../../data/milestones.js';

/* same shape as store.defaults(); store.js itself touches window, so it isn't imported here */
const fresh = () => ({
  v: 1, settings: { sound: true, theme: 'tour-pink', motion: 'auto' },
  fans: { balance: 0, lifetime: 0 }, streak: { days: 0, best: 0, last: null },
  venues: { unlocked: ['coffee'], seen: ['coffee'] }, skills: {}, words: {}, review: { words: [], math: [] },
  shows: [], time: {},
  inventory: { owned: ['theme:tour-pink', 'guitar:classic', 'dog:none'], equipped: { theme: 'tour-pink', guitar: 'classic', dog: 'none' } },
  journal: { stickers: {}, layout: {} }
});
const MODES = [
  { id: 'remix', name: 'remix', kind: 'spelling' }, { id: 'slots', name: 'missing letters', kind: 'spelling' },
  { id: 'merch', name: 'merch table', kind: 'math', skill: 'dec' }, { id: 'beat', name: 'beat maker', kind: 'math', skill: 'frac' }
];

test('catalog: unique ids, a free item in every wearable slot, sane prices', () => {
  assert.equal(new Set(SHOP.map(i => i.id)).size, SHOP.length);
  for (const sl of SLOTS) assert.ok(SHOP.some(i => i.slot === sl.id), sl.id);
  for (const sl of ['theme', 'guitar', 'dog']) assert.equal(SHOP.filter(i => i.slot === sl && i.price === 0).length, 1, sl);
  for (const i of SHOP) { assert.ok(i.id.startsWith(i.slot + ':')); assert.ok(Number.isInteger(i.price) && i.price >= 0); }
  for (const i of SHOP.filter(i => i.slot === 'sticker')) assert.ok(!MILESTONES.some(m => m.art === i.sticker), `${i.id} duplicates a milestone sticker`);
});

test('buy spends balance only, never lifetime, and never twice', () => {
  const s = fresh(); s.fans.balance = 20000; s.fans.lifetime = 50000;
  assert.deepEqual(M.buy(s, 'guitar:sunburst'), { ok: true });
  assert.equal(s.fans.balance, 2000); assert.equal(s.fans.lifetime, 50000);
  assert.equal(M.buy(s, 'guitar:sunburst').reason, 'owned');
  const r = M.buy(s, 'dog:beanie');
  assert.equal(r.reason, 'short'); assert.equal(r.need, 18000); assert.equal(s.fans.balance, 2000);
  assert.equal(M.buy(s, 'nope:x').reason, 'unknown');
  assert.equal(M.buy(s, 'theme:tour-pink').reason, 'owned');
});

test('equip only what she owns; stickers are never equipped; look falls back safely', () => {
  const s = fresh(); s.fans.balance = 1e6;
  assert.equal(M.equip(s, 'dog:shades'), false);
  M.buy(s, 'dog:shades'); M.buy(s, 'theme:peach-navy'); M.buy(s, 'guitar:mint'); M.buy(s, 'sticker:pick');
  assert.ok(M.equip(s, 'dog:shades') && M.equip(s, 'theme:peach-navy') && M.equip(s, 'guitar:mint'));
  assert.equal(M.equip(s, 'sticker:pick'), false);
  assert.deepEqual(M.look(s), { theme: 'peach-navy', guitar: '#8fe3c4', outfit: 'shades' });
  s.inventory.equipped = { theme: 'gone', guitar: 'gone', dog: 'gone' };
  assert.deepEqual(M.look(s), { theme: 'tour-pink', guitar: 'var(--paper-deep)', outfit: '' });
  s.inventory.equipped.dog = 'collar';                   /* equipped but not owned (hand-edited save) */
  assert.equal(M.look(s).outfit, '');
});

test('milestones: earned once, kept, and dated', () => {
  const s = fresh();
  assert.deepEqual(M.earn(s, MODES, '2026-11-18'), []);
  s.shows.push({ d: '2026-11-18', mode: 'remix', n: 10, first: 9, hinted: 1, fans: 30000 });
  const got = M.earn(s, MODES, '2026-11-18').map(m => m.id);
  assert.deepEqual(got.sort(), ['first-show', 'sold-out']);
  assert.equal(s.journal.stickers['first-show'], '2026-11-18');
  assert.deepEqual(M.earn(s, MODES, '2026-11-19'), []);
  s.shows = [];                                          /* history trimmed: stickers stay */
  assert.equal(M.earn(s, MODES, '2026-11-19').length, 0);
  assert.ok(s.journal.stickers['sold-out']);
});

test('milestones: encores, streaks, levels, every mode, time, venues', () => {
  const s = fresh();
  s.shows.push({ mode: 'remix', n: 3, first: 3, encore: true });
  assert.deepEqual(M.earn(s, MODES, 'd'), [], 'an encore is not a show');
  s.streak.best = 5; s.skills['spell:remix'] = { level: 3, byLevel: { 3: [12, 10] } }; s.skills.dec = { level: 3, byLevel: { 3: [9, 9] } };
  s.time = { a: 1800, b: 1800 }; s.venues.unlocked.push('records', 'rec');
  for (const m of MODES) s.shows.push({ mode: m.id, n: 10, first: 10 });
  const ids = M.earn(s, MODES, 'd').map(m => m.id);
  for (const id of ['first-show', 'sold-out', 'clean-run', 'streak-3', 'streak-5', 'master-any', 'master-spell', 'every-mode', 'hour', 'venue-records', 'venue-rec'])
    assert.ok(ids.includes(id), id);
  for (const id of ['streak-10', 'master-math', 'ten-shows', 'venue-rooftop']) assert.ok(!ids.includes(id), id);
  s.skills.dec.byLevel[3] = [11, 10];
  assert.deepEqual(M.earn(s, MODES, 'd').map(m => m.id), ['master-math']);
});

test('journal page: earned then bought; spots stay on the page; placing clamps and keeps the tilt', () => {
  const s = fresh(); s.fans.balance = 1e5;
  s.journal.stickers = { 'first-show': 'd1', 'streak-3': 'd2' }; M.buy(s, 'sticker:note');
  assert.deepEqual(M.pageItems(s).map(i => i.key), ['first-show', 'streak-3', 'sticker:note']);
  assert.equal(M.toEarn(s).length, MILESTONES.length - 2);
  for (let i = 0; i < MILESTONES.length + SHOP.length; i++) {
    const p = M.defaultSpot(i);
    assert.ok(p.x > 0 && p.x < 1 && p.y > 0 && p.y < 1, `spot ${i}`);
  }
  M.place(s, 'first-show', 1.4, -2, 7);
  assert.deepEqual(s.journal.layout['first-show'], { x: .95, y: .05, r: 7 });
  M.place(s, 'first-show', .5, .5, -3);
  assert.deepEqual(s.journal.layout['first-show'], { x: .5, y: .5, r: 7 });
});

test('office: accuracy, most-missed words, mistakes, and 14 days of time', () => {
  const s = fresh();
  s.skills['spell:remix'] = { level: 2, a: 10, c: 8, h: 3, mistakes: {} };
  s.skills.dec = { level: 1, a: 4, c: 1, h: 0, mistakes: { 'no-borrow': 3, 'gave-total': 1, 'made-up': 2, near: 9 } };
  s.words = { illegible: { a: 4, c: 2, first: 1, h: 1, miss: 2, last: '2026-11-17' }, irregular: { a: 3, c: 3, first: 3, h: 0, miss: 0 },
              intercept: { a: 2, c: 1, first: 0, h: 0, miss: 2, last: '2026-11-16' } };
  s.time = { '2026-11-18': 600, '2026-11-05': 300, '2026-10-01': 900 };
  s.shows = [{ d: '2026-11-18', mode: 'remix', n: 10, first: 8 }, { d: '2026-11-18', mode: 'remix', n: 2, first: 2, encore: true }];
  const o = M.office(s, MODES, '2026-11-18');
  assert.equal(o.shows, 1); assert.equal(o.totalSecs, 1800);
  const remix = o.skills.find(k => k.id === 'remix');
  assert.deepEqual([remix.level, remix.a, remix.acc, remix.hint, remix.shows], [2, 10, 80, 30, 1]);
  assert.equal(o.skills.find(k => k.id === 'beat').acc, null);
  assert.deepEqual(o.words.map(w => w.w), ['intercept', 'illegible']);       /* tie on misses: lower first-try rate first */
  assert.deepEqual(o.mistakes.map(m => [m.tag, m.n, m.mode]), [['no-borrow', 3, 'merch table'], ['made-up', 2, 'merch table'], ['gave-total', 1, 'merch table']]);
  assert.equal(o.mistakes[0].label, 'subtracted without regrouping (borrowing)');
  assert.equal(o.daily.length, 14);
  assert.equal(o.daily[0].d, '2026-11-05'); assert.equal(o.daily[13].d, '2026-11-18');
  assert.deepEqual([o.daily[0].secs, o.daily[13].secs, o.daily[13].shows], [300, 600, 1]);
});

test('every mistake tag the generators produce has a plain label', async () => {
  const { generate } = await import('../../math/gen/index.js');
  const tags = new Set();
  for (const sk of ['dec', 'md', 'frac', 'xy', 'vol', 'ops', 'flu']) for (const lv of [1, 2, 3]) for (let seed = 1; seed <= 600; seed++) {
    const p = generate(sk, lv, seed);
    for (const x of [...(p.traps || []), ...(p.choices || [])]) if (x.why) tags.add(x.why);
  }
  /* the beat maker adds these two from the builder, not from traps */
  tags.add('too-short'); tags.add('too-long');
  assert.ok(tags.size > 25, 'found ' + tags.size);
  tags.delete('near');      /* filler choices; the office leaves them out */
  for (const tg of tags) assert.ok(M.MISTAKES[tg], 'no label for ' + tg);
});
