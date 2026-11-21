export const $ = (sel, root = document) => root.querySelector(sel);
export const $$ = (sel, root = document) => [...root.querySelectorAll(sel)];

export const shuffle = a => { const b = [...a]; for (let i = b.length - 1; i > 0; i--) { const j = Math.floor(Math.random() * (i + 1)); [b[i], b[j]] = [b[j], b[i]]; } return b; };
export const pick = a => a[Math.floor(Math.random() * a.length)];
export const clamp = (n, lo, hi) => Math.max(lo, Math.min(hi, n));
export const tilt = (max = 3) => +((Math.random() * 2 - 1) * max).toFixed(1);

const ESC = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };
export const esc = s => String(s).replace(/[&<>"']/g, c => ESC[c]);

/* local calendar date as YYYY-MM-DD (not UTC, so a late-evening show counts for today) */
export const localISO = (d = new Date()) => new Date(d - d.getTimezoneOffset() * 6e4).toISOString().slice(0, 10);
export const daysBetween = (a, b) => Math.round((Date.parse(b) - Date.parse(a)) / 864e5);

export const fmt = n => Math.round(n).toLocaleString('en-US');
export const short = n => n >= 1e6 ? (n / 1e6).toFixed(n % 1e6 ? 1 : 0) + 'm' : n >= 1e3 ? Math.round(n / 1e3) + 'k' : String(n);
