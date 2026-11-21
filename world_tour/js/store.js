/* Saved progress: one versioned localStorage key. Every read and write is wrapped,
   and if storage is missing or throws, the game runs on an in-memory copy. */
import { localISO } from './util.js';

export const KEY = 'worldtour.v1';

export function defaults() {
  return {
    v: 1,
    created: localISO(), updated: 0,
    settings: { sound: true, theme: 'tour-pink', motion: 'auto' },
    fans: { balance: 0, lifetime: 0 },
    streak: { days: 0, best: 0, last: null },
    venues: { unlocked: ['coffee'], seen: ['coffee'] },
    skills: {},
    words: {},
    review: { words: [], math: [] },
    shows: [],
    time: {},
    inventory: { owned: ['theme:tour-pink', 'guitar:classic', 'dog:none'], equipped: { theme: 'tour-pink', guitar: 'classic', dog: 'none' } },
    journal: { stickers: {}, layout: {} }
  };
}

/* fill in anything missing so older or partial saves keep working */
export function migrate(raw) {
  const d = defaults();
  if (!raw || typeof raw !== 'object') return d;
  const out = { ...d, ...raw, v: 1 };
  for (const k of ['settings', 'fans', 'streak', 'venues', 'review', 'inventory', 'journal']) {
    out[k] = { ...d[k], ...(raw[k] && typeof raw[k] === 'object' ? raw[k] : {}) };
  }
  out.inventory.equipped = { ...d.inventory.equipped, ...(raw.inventory?.equipped || {}) };
  for (const k of ['skills', 'words', 'time']) if (!out[k] || typeof out[k] !== 'object' || Array.isArray(out[k])) out[k] = {};
  if (!Array.isArray(out.shows)) out.shows = [];
  if (!Array.isArray(out.review.words)) out.review.words = [];
  if (!Array.isArray(out.review.math)) out.review.math = [];
  if (!Array.isArray(out.venues.unlocked) || !out.venues.unlocked.includes('coffee')) out.venues.unlocked = ['coffee', ...(out.venues.unlocked || [])].filter((v, i, a) => a.indexOf(v) === i);
  return out;
}

let state = null;
let persistent = false;
let timer = 0;

function storage() {
  try {
    const s = window.localStorage;
    const probe = '__wt_probe';
    s.setItem(probe, '1'); s.removeItem(probe);
    return s;
  } catch (e) { return null; }
}

export function load() {
  const s = storage();
  persistent = !!s;
  let raw = null;
  if (s) { try { raw = JSON.parse(s.getItem(KEY) || 'null'); } catch (e) { raw = null; } }
  state = migrate(raw);
  return state;
}

export const get = () => state || load();
export const canSave = () => persistent;

export function saveNow() {
  clearTimeout(timer);
  if (!persistent || !state) return;
  state.updated = Date.now();
  try { window.localStorage.setItem(KEY, JSON.stringify(state)); }
  catch (e) { persistent = false; document.dispatchEvent(new CustomEvent('wt:nosave')); }
}

export function save() { clearTimeout(timer); timer = setTimeout(saveNow, 500); }

export function reset() {
  state = defaults();
  try { window.localStorage.removeItem(KEY); } catch (e) { /* nothing to remove */ }
  saveNow();
  return state;
}

addEventListener('visibilitychange', () => { if (document.visibilityState === 'hidden') saveNow(); });
addEventListener('pagehide', saveNow);
