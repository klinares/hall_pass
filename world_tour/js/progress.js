/* Everything that changes saved progress goes through here. */
import * as store from './store.js';
import { VENUES } from '../data/venues.js';
import { localISO, daysBetween } from './util.js';
import * as M from './meta.js';
import { recordReview } from './review.js';

const S = () => store.get();

/* ---------- fans ---------- */
export function addFans(n) {
  const s = S();
  s.fans.balance += n; s.fans.lifetime += n;
  const newly = VENUES.filter(v => s.fans.lifetime >= v.fans && !s.venues.unlocked.includes(v.id));
  newly.forEach(v => s.venues.unlocked.push(v.id));
  store.save();
  return newly;
}

export const unlocked = id => S().venues.unlocked.includes(id);
export function newestVenue() { const s = S(); return [...VENUES].reverse().find(v => s.venues.unlocked.includes(v.id)) || VENUES[0]; }
export function nextVenue() { const s = S(); return VENUES.find(v => !s.venues.unlocked.includes(v.id)) || null; }
export function unseenUnlocks() { const s = S(); return s.venues.unlocked.filter(id => !s.venues.seen.includes(id)); }
export function markSeen(id) { const s = S(); if (!s.venues.seen.includes(id)) { s.venues.seen.push(id); store.save(); } }

/* ---------- day streak ---------- */
export function dayStreak() {
  const st = S().streak;
  if (!st.last) return 0;
  const gap = daysBetween(st.last, localISO());
  return gap <= 1 ? st.days : 0;
}
function tickStreak() {
  const st = S().streak, today = localISO();
  if (st.last === today) return;
  st.days = st.last && daysBetween(st.last, today) === 1 ? st.days + 1 : 1;
  st.last = today;
  st.best = Math.max(st.best, st.days);
}
export const daysAway = () => { const l = S().streak.last; return l ? daysBetween(l, localISO()) : null; };

/* ---------- skills: level 1..3, up after 4 right in a row, down after 2 misses in a row ---------- */
function skill(id) {
  const sk = S().skills;
  return sk[id] || (sk[id] = { level: 1, run: 0, missRun: 0, a: 0, c: 0, h: 0, byLevel: { 1: [0, 0], 2: [0, 0], 3: [0, 0] }, mistakes: {} });
}
export const level = id => skill(id).level;
export function recordSkill(id, { correct, hinted = false, mistake = null }) {
  const k = skill(id), lv = String(k.level);
  k.a++; k.byLevel[lv][0]++;
  if (hinted) k.h++;
  if (mistake) k.mistakes[mistake] = (k.mistakes[mistake] || 0) + 1;
  let moved = 0;
  if (correct) {
    k.c++; k.byLevel[lv][1]++; k.missRun = 0; k.run++;
    if (k.run >= 4 && k.level < 3) { k.level++; k.run = 0; moved = 1; }
  } else {
    k.run = 0; k.missRun++;
    if (k.missRun >= 2 && k.level > 1) { k.level--; k.missRun = 0; moved = -1; }
  }
  store.save();
  return moved;
}

/* ---------- words ---------- */
/* keep: the word was missed earlier in this show, so a clean bonus replay doesn't clear it from review */
export function recordWord(w, { clean, hinted, missed, keep = false }) {
  const words = S().words;
  const e = words[w] || (words[w] = { a: 0, c: 0, first: 0, h: 0, miss: 0, last: null });
  e.a++; e.last = localISO();
  if (!missed) e.c++;
  if (clean) e.first++;
  if (hinted) e.h++;
  if (missed) e.miss++;
  const rv = S().review.words;
  if (missed && !rv.includes(w)) rv.push(w);
  if (clean && !keep) S().review.words = rv.filter(x => x !== w);
  store.save();
}
export const wordStats = w => S().words[w] || null;

/* math: a missed problem is saved whole so the same problem comes back next session (js/review.js) */
export function recordMath(skill, v, opts) {
  const s = S();
  s.review.math = recordReview(s.review.math, skill, v, opts);
  store.save();
}
export const reviewMath = skill => S().review.math.filter(r => r.skill === skill);
/* which wrong answer she gave, tagged by the mistake it models */
export function noteMistake(id, tag) {
  if (!tag) return;
  const k = skill(id);
  k.mistakes[tag] = (k.mistakes[tag] || 0) + 1;
  store.save();
}
export const reviewWords = () => [...S().review.words];

/* ---------- shows + time ---------- */
export function finishShow(summary) {
  const s = S();
  tickStreak();
  s.shows.push({ d: localISO(), ...summary });
  if (s.shows.length > 200) s.shows.splice(0, s.shows.length - 200);
  store.saveNow();
}

let lastInput = Date.now();
['pointerdown', 'keydown'].forEach(e => addEventListener(e, () => { lastInput = Date.now(); }, { passive: true }));
setInterval(() => {
  if (document.visibilityState !== 'visible' || Date.now() - lastInput > 60000) return;
  const t = S().time, d = localISO();
  t[d] = (t[d] || 0) + 15;
  const keys = Object.keys(t).sort();
  if (keys.length > 120) keys.slice(0, keys.length - 120).forEach(k => delete t[k]);
  store.save();
}, 15000);

/* ---------- settings ---------- */
export const settings = () => S().settings;
export function setSetting(k, v) { S().settings[k] = v; store.save(); }

/* ---------- backstage + journal (rules live in meta.js) ---------- */
export function buyItem(id) { const r = M.buy(S(), id); if (r.ok) store.saveNow(); return r; }
export function equipItem(id) { const ok = M.equip(S(), id); if (ok) store.saveNow(); return ok; }
/* the mode list is handed in by the screens (not imported) so progress.js stays free of the mode modules */
let ALL = [];
export const setModes = m => { ALL = m; };
export function awardStickers(modes = ALL) { const fresh = M.earn(S(), modes, localISO()); if (fresh.length) store.saveNow(); return fresh; }
export function placeSticker(key, x, y, r) { M.place(S(), key, x, y, r); store.saveNow(); }
