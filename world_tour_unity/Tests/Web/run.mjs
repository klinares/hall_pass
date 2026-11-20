// Browser tests for the web build in tour/. Plain Node plus the Playwright install already in
// world_tour/tests/e2e/node_modules (read only; nothing is installed or changed there).
//
//   node Tests/Web/run.mjs [--out <results.json>] [--shots <dir>] [--site <folder>] [--only <names>] [--budget-mb <n>]
//
//   --out        results file, default <project>/Temp/web-results.json (the results file is the evidence)
//   --shots      screenshot folder, default <project>/Temp/web-shots
//   --site       folder to serve instead of tour/ (used to prove the tests can fail)
//   --only       comma list of: cold, reload, storage, broken, size, data, edit, skip, clock, save, start   (default: all)
//   --budget-mb  size ceiling in MB (10^6 bytes), default 20; over 12 MB or a cold load over 8 s only warns
//
// Exits 1 if any test fails, 2 if the runner itself breaks. Server and browser are closed in finally.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { createRequire } from 'node:module';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { startServer } from './server.mjs';
import { loadWeeks } from '../../Tools/validate_data.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const project = path.resolve(here, '..', '..');           // world_tour_unity/
const repo = path.resolve(project, '..');                 // repo root
const requireFromE2E = createRequire(path.join(repo, 'world_tour', 'tests', 'e2e', 'package.json'));
const { chromium } = requireFromE2E('playwright');

// ---- options ----
const argv = process.argv.slice(2);
const opt = (name, def) => { const i = argv.indexOf(name); return i >= 0 && i + 1 < argv.length ? argv[i + 1] : def; };
const outFile = path.resolve(opt('--out', path.join(project, 'Temp', 'web-results.json')));
const shotsDir = path.resolve(opt('--shots', path.join(project, 'Temp', 'web-shots')));
const site = path.resolve(opt('--site', path.join(repo, 'tour')));
const TEST_NAMES = ['cold', 'reload', 'storage', 'broken', 'size', 'data', 'edit', 'skip', 'clock', 'save', 'start'];
const only = new Set((opt('--only', TEST_NAMES.join(','))).split(',').map(s => s.trim()));
const budgetMb = Number(opt('--budget-mb', '20'));
const WARN_MB = 12, WARN_MS = 8000, READY_TIMEOUT_MS = 60000, RELOAD_MAX_BYTES = 200 * 1024;
const MB = 1e6;

const results = {
  startedAt: new Date().toISOString(), endedAt: null,
  site, budgetMb, total: 0, passed: 0, failed: 0,
  tests: [], numbers: {}, screenshots: [], warnings: [],
};

function record(name, failures, details = {}) {
  const pass = failures.length === 0;
  results.tests.push({ name, pass, failures, ...details });
  console.log(`${pass ? 'PASS' : 'FAIL'} ${name}${pass ? '' : ': ' + failures.join('; ')}`);
}

/** Loads the page in `context` and waits for WT_BOOT_READY (or a visible failure message, or a timeout).
 *  With waitData it also waits for the WT_DATA_READY line (task 1.7). */
async function load(context, server, { shot, initScript, waitData, waitSave } = {}) {
  const page = await context.newPage();
  if (initScript) await page.addInitScript(initScript);
  const origin = new URL(server.url).origin;
  const errors = [], warnings = [], external = [], unityCache = [], dataRequests = [];
  let readyMs = null, t0 = 0, dataLine = null, saveLine = null;
  page.on('console', m => {
    const text = m.text();
    if (m.type() === 'error') errors.push(text);
    if (m.type() === 'warning') warnings.push(text);
    if (text.startsWith('[UnityCache]')) unityCache.push(text);
    if (readyMs === null && text.trim() === 'WT_BOOT_READY') readyMs = Date.now() - t0;
    if (dataLine === null && text.trim().startsWith('WT_DATA_READY ')) dataLine = text.trim();
    if (saveLine === null && text.trim().startsWith('WT_SAVE_READY ')) saveLine = text.trim();
  });
  page.on('pageerror', e => errors.push('pageerror: ' + e.message));
  page.on('request', r => {
    const u = r.url();
    // data: URLs never leave the browser; everything else must be the local server or a blob.
    if (!u.startsWith(origin + '/') && !u.startsWith('blob:') && !u.startsWith('data:')) external.push(u);
    if (/\/StreamingAssets\/(weeks|math_config)\.json/.test(u)) dataRequests.push({ url: u, cacheControl: r.headers()['cache-control'] || null });
  });
  server.resetCounters();
  t0 = Date.now();
  await page.goto(server.url, { waitUntil: 'load' });
  const deadline = Date.now() + READY_TIMEOUT_MS;
  while ((readyMs === null || (waitData && dataLine === null) || (waitSave && saveLine === null)) && Date.now() < deadline) {
    const failed = await page.evaluate(() => {
      const m = document.getElementById('message');
      return !!m && getComputedStyle(m).display !== 'none';
    }).catch(() => false);
    if (failed) break;
    await page.waitForTimeout(50);
  }
  await page.waitForTimeout(500); // a few frames after ready, and let trailing requests finish
  const layout = await page.evaluate(() => {
    const c = document.getElementById('unity-canvas'), d = document.documentElement;
    const r = c ? c.getBoundingClientRect() : null;
    return {
      innerW: innerWidth, innerH: innerHeight, scrollW: d.scrollWidth, scrollH: d.scrollHeight,
      canvas: r ? [r.left, r.top, r.width, r.height] : null,
      messageShown: (() => { const m = document.getElementById('message'); return !!m && getComputedStyle(m).display !== 'none'; })(),
    };
  });
  if (shot) {
    fs.mkdirSync(path.dirname(shot), { recursive: true });
    await page.screenshot({ path: shot });
    results.screenshots.push(shot);
  }
  const files = server.files;
  const largest = files.reduce((a, f) => (!a || f.bytes > a.bytes ? f : a), null);
  return { page, readyMs, errors, warnings, external, unityCache, layout, bytes: server.bytesServed, files, largest, dataLine, data: parseData(dataLine), dataRequests, saveLine, save: parseData(saveLine) };
}

/** "WT_DATA_READY weeks=2 words=22 week=2026-10-26 config=ok skipped=0" -> { weeks: 2, words: 22, week: '2026-10-26', ... } */
function parseData(line) {
  if (!line) return null;
  const o = {};
  for (const part of line.split(' ').slice(1)) { const [k, v] = part.split('='); o[k] = /^[0-9]+$/.test(v) ? Number(v) : v; }
  return o;
}

/** The page's own local date, YYYY-MM-DD, from new Date() with local getters (what clock.jslib returns). */
const browserToday = page => page.evaluate(() => {
  const d = new Date(), p = n => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
});

/** What the player should report for a weeks.json on `today`, from the Node validator (the same rules as Core, held together by the parity test). */
function expectedData(weeksFile, today) {
  const r = loadWeeks(fs.readFileSync(weeksFile, 'utf8'));
  const weeks = r.model ? r.model.weeks : [];
  const newest = [...weeks].sort((a, b) => (a.id < b.id ? 1 : a.id > b.id ? -1 : 0));
  const week = (newest.find(w => w.id <= today) || newest[newest.length - 1] || { id: 'none' }).id;
  return { weeks: weeks.length, words: weeks.reduce((n, w) => n + w.words.length, 0), skipped: r.skipped.length, week, today };
}

/** Compares a WT_DATA_READY line with what is expected. */
function dataFailures(r, exp) {
  const f = [];
  if (!r.data) { f.push('no WT_DATA_READY line'); return f; }
  for (const k of ['weeks', 'words', 'skipped', 'week', 'today']) if (r.data[k] !== exp[k]) f.push(`${k}=${r.data[k]}, expected ${exp[k]}`);
  if (r.data.config !== 'ok') f.push(`config=${r.data.config}, expected ok`);
  return f;
}

/** Every request for weeks.json or math_config.json must carry the ?v= cache-buster (or a no-store header). */
function cacheBusterFailures(r) {
  const f = [];
  for (const name of ['weeks.json', 'math_config.json']) {
    const reqs = r.dataRequests.filter(q => q.url.includes('/StreamingAssets/' + name));
    if (!reqs.length) f.push(`no request for ${name}`);
    for (const q of reqs) if (!/[?&]v=[0-9]+/.test(q.url) && !/no-store/.test(q.cacheControl || '')) f.push(`${name} requested without ?v= or no-store: ${q.url}`);
  }
  return f;
}

/** A temp copy of the site under the system temp folder; finally removes it. */
function siteCopy(prefix) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), prefix));
  tempDirs.push(dir);
  fs.cpSync(site, dir, { recursive: true });
  return dir;
}

const FOUR_WORDS = [
  { w: 'echo', s: 'ech-o', h: 'a sound that bounces back to you' },
  { w: 'tempo', s: 'tem-po', h: 'how fast a song goes' },
  { w: 'chorus', s: 'cho-rus', h: 'the part of a song that repeats' },
  { w: 'stage', s: 'stage', h: 'the raised floor where the band plays' },
];

/** Compares a WT_SAVE_READY line ("persistent=true fans=0 unlocked=1 v=1 recovered=false") with what is expected. */
function saveFailures(r, exp) {
  if (!r.save) return ['no WT_SAVE_READY line'];
  return Object.keys(exp).filter(k => String(r.save[k]) !== String(exp[k])).map(k => `${k}=${r.save[k]}, expected ${exp[k]}`);
}

const readStorage = (page, key) => page.evaluate(k => localStorage.getItem(k), key);
const writeStorage = (page, key, value) => page.evaluate(([k, v]) => localStorage.setItem(k, v), [key, value]);
const SAVE_KEY = 'worldtour.v1';

/** A save made by the real web game (world_tour/js/store.js migrate, imported read only) in a child process, as
 *  Tools/save/save_vectors.mjs does, so the browser stand-ins it needs never touch this runner. */
function webGameSave(raw) {
  const script = [
    "globalThis.window = {}; globalThis.document = { visibilityState: 'hidden', dispatchEvent() {} }; globalThis.addEventListener = () => {};",
    `const store = await import(${JSON.stringify(new URL('../../../world_tour/js/store.js', import.meta.url).href)});`,
    `process.stdout.write(JSON.stringify(store.migrate(${JSON.stringify(raw)})));`,
  ].join('\n');
  return execFileSync(process.execPath, ['--input-type=module', '-e', script], { encoding: 'utf8' });
}

function addWeekAtTop(weeksFile, week) {
  const data = JSON.parse(fs.readFileSync(weeksFile, 'utf8'));
  data.weeks.unshift(week);
  fs.writeFileSync(weeksFile, JSON.stringify(data, null, 2) + '\n');
}

function layoutFailures(l, vw, vh) {
  const f = [];
  if (!l.canvas) f.push('no #unity-canvas');
  else if (l.canvas[0] !== 0 || l.canvas[1] !== 0 || l.canvas[2] !== vw || l.canvas[3] !== vh)
    f.push(`canvas is ${l.canvas.join(',')} instead of 0,0,${vw},${vh}`);
  if (l.scrollW > l.innerW || l.scrollH > l.innerH) f.push(`page scrolls (${l.scrollW}x${l.scrollH} in ${l.innerW}x${l.innerH})`);
  return f;
}

function loadFailures(r) {
  const f = [];
  if (r.readyMs === null) f.push(`no WT_BOOT_READY within ${READY_TIMEOUT_MS} ms`);
  if (r.errors.length) f.push(`${r.errors.length} console/page error(s): ${r.errors.slice(0, 3).join(' | ').slice(0, 300)}`);
  if (r.external.length) f.push(`requests outside the local server: ${r.external.slice(0, 3).join(', ')}`);
  if (r.layout.messageShown) f.push('the failure message is showing');
  return f;
}

const STORAGE_BLOCKED = `(() => {
  const blocked = () => { throw new DOMException('storage blocked by test', 'SecurityError'); };
  try { IDBFactory.prototype.open = blocked; } catch (e) {}
  try { Object.defineProperty(window, 'localStorage', { configurable: true, get: blocked }); } catch (e) {}
})();`;

let browser = null;
const servers = [];
const tempDirs = [];
try {
  if (!fs.existsSync(path.join(site, 'index.html'))) throw new Error(`no index.html in ${site}; build first`);
  const server = await startServer(site);
  servers.push(server);
  browser = await chromium.launch({ headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });

  // 1. Cold loads (2. cached reload reuses the 1366x768 context).
  let ctx1366 = null, cold1366 = null;
  if (only.has('cold') || only.has('reload') || only.has('size')) {
    for (const [vw, vh] of [[1366, 768], [1920, 1080]]) {
      const name = `cold_load_${vw}x${vh}`;
      const ctx = await browser.newContext({ viewport: { width: vw, height: vh } });
      const r = await load(ctx, server, { shot: path.join(shotsDir, `${name}.png`) });
      results.numbers[name] = { bytes: r.bytes, readyMs: r.readyMs, largest: r.largest, requests: r.files.length };
      if (only.has('cold')) record(name, [...loadFailures(r), ...layoutFailures(r.layout, vw, vh)],
        { readyMs: r.readyMs, bytes: r.bytes, largest: r.largest, files: r.files, consoleErrors: r.errors, external: r.external });
      if (vw === 1366) { ctx1366 = ctx; cold1366 = r; await r.page.close(); } else await ctx.close();
      if (r.readyMs !== null && r.readyMs > WARN_MS) results.warnings.push(`${name}: cold load took ${r.readyMs} ms (over ${WARN_MS} ms)`);
    }
  }

  // 2. Cached reload in the same context: Unity's cache must serve framework, data and wasm.
  if (only.has('reload') && ctx1366) {
    const r = await load(ctx1366, server);
    const servedFromCache = r.unityCache.filter(l => l.includes('served from the browser cache'));
    const f = loadFailures(r);
    for (const kind of ['framework.js.unityweb', 'data.unityweb', 'wasm.unityweb'])
      if (!servedFromCache.some(l => l.includes(kind))) f.push(`no "served from the browser cache" line for ${kind}`);
    if (r.bytes >= RELOAD_MAX_BYTES) f.push(`reload transferred ${r.bytes} bytes (limit ${RELOAD_MAX_BYTES})`);
    results.numbers.cached_reload = { bytes: r.bytes, readyMs: r.readyMs, requests: r.files.map(x => x.path) };
    record('cached_reload', f, { bytes: r.bytes, readyMs: r.readyMs, files: r.files, unityCacheLines: servedFromCache });
  }
  if (ctx1366) await ctx1366.close();

  // 3. Storage blocked: indexedDB.open and localStorage throw; the game must still start cleanly.
  if (only.has('storage')) {
    const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 } });
    const r = await load(ctx, server, { initScript: STORAGE_BLOCKED, shot: path.join(shotsDir, 'storage_blocked.png'), waitData: true, waitSave: true });
    const blocked = await r.page.evaluate(() => {
      const throws = fn => { try { fn(); return false; } catch (e) { return true; } };
      return { indexedDB: throws(() => indexedDB.open('probe')), localStorage: throws(() => localStorage.getItem('probe')) };
    });
    const f = loadFailures(r);
    if (!blocked.indexedDB) f.push('indexedDB.open did not throw, so the block was not in effect');
    if (!blocked.localStorage) f.push('localStorage did not throw, so the block was not in effect');
    if (!r.dataLine) f.push('no WT_DATA_READY line with storage blocked');
    f.push(...saveFailures(r, { persistent: false }));
    record('storage_blocked', f, { readyMs: r.readyMs, blocked, dataLine: r.dataLine, saveLine: r.saveLine, consoleErrors: r.errors, consoleWarnings: r.warnings.slice(0, 10) });
    await ctx.close();
  }

  // 4. Broken build: a temp copy with the wasm removed must show the short lowercase failure message.
  if (only.has('broken')) {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'wt-broken-'));
    tempDirs.push(dir);
    fs.cpSync(site, dir, { recursive: true });
    const wasm = fs.readdirSync(path.join(dir, 'Build')).filter(n => n.endsWith('.wasm.unityweb'));
    for (const n of wasm) fs.rmSync(path.join(dir, 'Build', n));
    const broken = await startServer(dir);
    servers.push(broken);
    const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 } });
    const r = await load(ctx, broken, { shot: path.join(shotsDir, 'broken_build.png') });
    const ui = await r.page.evaluate(() => {
      const m = document.getElementById('message');
      const own = m ? [...m.childNodes].filter(n => n.nodeType === 3).map(n => n.textContent).join('').trim() : '';
      const smalls = m ? [...m.querySelectorAll('small')].map(s => s.textContent) : [];
      const extra = [...document.body.children]
        .filter(e => e.tagName !== 'SCRIPT' && !['unity-canvas', 'loading', 'message'].includes(e.id))
        .filter(e => getComputedStyle(e).display !== 'none')
        .map(e => e.tagName + (e.id ? '#' + e.id : ''));
      return { shown: !!m && getComputedStyle(m).display !== 'none', own, smalls, extra };
    });
    const f = [];
    if (wasm.length === 0) f.push('no wasm file to remove in the copy');
    if (!ui.shown) f.push('failure message not shown (blank page)');
    if (!ui.own) f.push('failure message has no text');
    else if (ui.own !== ui.own.toLowerCase()) f.push(`failure message is not lowercase: "${ui.own}"`);
    if (ui.smalls.length !== 1) f.push(`expected 1 detail line, found ${ui.smalls.length}`);
    else if (!ui.smalls[0] || ui.smalls[0].length > 120) f.push(`detail line is ${ui.smalls[0].length} chars (1..120)`);
    if (ui.extra.length) f.push(`unexpected visible elements (Unity banner?): ${ui.extra.join(', ')}`);
    if (r.readyMs !== null) f.push('WT_BOOT_READY fired with the wasm missing');
    record('broken_build', f, { message: ui.own, detail: ui.smalls[0] ?? null, removed: wasm });
    await ctx.close();
  }

  // 6-9. Word data (task 1.7): weeks.json and math_config.json load in the stripped player, an edit shows on reload,
  // a broken week is skipped, and "today" is the browser's local date. data_loads and local_date read tour/ as served;
  // the edit and skip tests use temp copies.
  if (only.has('data')) {
    const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 } });
    const r = await load(ctx, server, { waitData: true });
    const exp = expectedData(path.join(site, 'StreamingAssets', 'weeks.json'), await browserToday(r.page));
    const f = [...loadFailures(r), ...dataFailures(r, exp), ...cacheBusterFailures(r)];
    record('data_loads', f, { dataLine: r.dataLine, expected: exp, dataRequests: r.dataRequests, consoleErrors: r.errors });
    await ctx.close();
  }

  if (only.has('edit')) {
    const dir = siteCopy('wt-weeks-edit-');
    const weeksFile = path.join(dir, 'StreamingAssets', 'weeks.json');
    const copy = await startServer(dir);
    servers.push(copy);
    const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 } });
    const first = await load(ctx, copy, { waitData: true });
    const today = await browserToday(first.page);
    const f = [...loadFailures(first), ...dataFailures(first, expectedData(weeksFile, today))].map(x => 'first load: ' + x);
    await first.page.close();
    addWeekAtTop(weeksFile, { id: today, label: 'week added by the reload test', words: FOUR_WORDS });
    const exp = expectedData(weeksFile, today);
    const second = await load(ctx, copy, { waitData: true });
    f.push(...loadFailures(second), ...dataFailures(second, exp), ...cacheBusterFailures(second));
    if (second.data && second.data.week !== today) f.push(`week=${second.data.week}, expected the added week ${today}`);
    record('weeks_edit_shows_on_reload', f, { before: first.dataLine, after: second.dataLine, addedWeek: today, expected: exp, dataRequests: second.dataRequests });
    await ctx.close();
  }

  if (only.has('skip')) {
    const dir = siteCopy('wt-weeks-skip-');
    const weeksFile = path.join(dir, 'StreamingAssets', 'weeks.json');
    const goodWeeks = loadWeeks(fs.readFileSync(weeksFile, 'utf8')).model.weeks.length;
    addWeekAtTop(weeksFile, { id: '2026-11-30', label: 'broken week', words: [{ w: 'Echo', s: 'ech-o', h: 'a capital letter can never be typed right' }] });
    const copy = await startServer(dir);
    servers.push(copy);
    const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 } });
    const r = await load(ctx, copy, { waitData: true });
    const exp = expectedData(weeksFile, await browserToday(r.page));
    const f = [...loadFailures(r), ...dataFailures(r, exp)];
    if (exp.skipped !== 1) f.push(`the Node rules did not skip the test's broken week (skipped=${exp.skipped})`);
    if (r.data && r.data.weeks !== goodWeeks) f.push(`weeks=${r.data.weeks}, expected the ${goodWeeks} good weeks`);
    if (!r.warnings.some(w => w.includes('[data]') && w.includes('2026-11-30'))) f.push('no [data] console warning naming the skipped week');
    record('weeks_broken_week_skipped', f, { dataLine: r.dataLine, expected: exp, consoleErrors: r.errors, dataWarnings: r.warnings.filter(w => w.includes('[data]')).slice(0, 5) });
    await ctx.close();
  }

  // local_date: the date the game uses must be the browser's local date, not UTC. The browser's Date is pinned to
  // 2026-11-21 03:30 UTC (clock.setFixedTime fakes Date only; timers and animation frames run normally, so the Unity
  // loader is untouched). Then New York (UTC-5) is still on Nov 20 and Kiritimati (UTC+14) is already on Nov 21, so a
  // game reading UTC fails one of them, whatever time the test runs.
  if (only.has('clock')) {
    const f = [], seen = {};
    const PINNED = '2026-11-21T03:30:00Z';
    for (const [timezoneId, expected] of [['America/New_York', '2026-11-20'], ['Pacific/Kiritimati', '2026-11-21']]) {
      const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 }, timezoneId });
      let pinned = true;
      try { await ctx.clock.setFixedTime(new Date(PINNED)); } catch (e) { pinned = false; f.push(`${timezoneId}: could not pin the browser clock: ${e.message}`); }
      const r = await load(ctx, server, { waitData: true });
      const local = await browserToday(r.page);
      seen[timezoneId] = { pinnedUtc: pinned ? PINNED : null, gameUsed: r.data ? r.data.today : null, browserLocal: local, expected };
      f.push(...loadFailures(r).map(x => `${timezoneId}: ${x}`));
      if (!r.data) f.push(`${timezoneId}: no WT_DATA_READY line`);
      else {
        if (r.data.today !== local) f.push(`${timezoneId}: the game used ${r.data.today}, the browser's local date is ${local}`);
        if (pinned && r.data.today !== expected) f.push(`${timezoneId}: the game used ${r.data.today}, expected ${expected} at ${PINNED}`);
      }
      if (pinned && local !== expected) f.push(`${timezoneId}: the pinned browser date is ${local}, expected ${expected} (clock pin not in effect)`);
      await ctx.close();
    }
    record('local_date', f, { dates: seen });
  }

  // 10-12. The save (task 1.5, ADVISOR 23:37 #1): written on first run, read back after a reload (a save made by the
  // web game itself, read through the StrictJson/JsonValue mapping in the stripped player), and an unreadable save kept.
  if (only.has('save')) {
    {
      const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 } });
      const r = await load(ctx, server, { waitData: true, waitSave: true });
      const f = [...loadFailures(r), ...saveFailures(r, { persistent: true, fans: 0, unlocked: 1, v: 1, recovered: false })];
      const text = await readStorage(r.page, SAVE_KEY);
      let saved = null;
      try { saved = JSON.parse(text); } catch (e) { f.push(`localStorage['${SAVE_KEY}'] is not JSON: ${String(text).slice(0, 80)}`); }
      if (saved && saved.v !== 1) f.push(`saved v=${saved.v}, expected 1`);
      if (saved && !(saved.venues && Array.isArray(saved.venues.unlocked) && saved.venues.unlocked.includes('coffee'))) f.push('saved venues.unlocked does not include coffee');
      record('save_first_run', f, { saveLine: r.saveLine, savedBytes: text ? text.length : 0, consoleErrors: r.errors });
      await ctx.close();
    }
    {
      const fixture = webGameSave({ fans: { balance: 500, lifetime: 1234 }, venues: { unlocked: ['coffee', 'records'], seen: ['coffee', 'records'] } });
      const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 } });
      const first = await load(ctx, server, { waitSave: true });
      await writeStorage(first.page, SAVE_KEY, fixture);
      await first.page.close();
      const r = await load(ctx, server, { waitData: true, waitSave: true });
      const f = [...loadFailures(r), ...saveFailures(r, { persistent: true, fans: 1234, unlocked: 2, v: 1, recovered: false })];
      record('save_survives_reload', f, { saveLine: r.saveLine, fixtureBytes: fixture.length, consoleErrors: r.errors });
      await ctx.close();
    }
    {
      const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 } });
      const first = await load(ctx, server, { waitSave: true });
      await writeStorage(first.page, SAVE_KEY, '{not json');
      await first.page.close();
      const r = await load(ctx, server, { waitData: true, waitSave: true });
      const f = [...loadFailures(r), ...saveFailures(r, { persistent: true, fans: 0, recovered: true })];
      const rescued = await readStorage(r.page, SAVE_KEY + '.unreadable');
      if (rescued !== '{not json') f.push(`localStorage['${SAVE_KEY}.unreadable'] is ${JSON.stringify(rescued)}, expected "{not json"`);
      const fresh = await readStorage(r.page, SAVE_KEY);
      try { if (JSON.parse(fresh).v !== 1) f.push('the fresh save has no v: 1'); } catch (e) { f.push(`the main key does not hold a fresh valid save: ${String(fresh).slice(0, 80)}`); }
      record('save_unreadable_backed_up', f, { saveLine: r.saveLine, rescued, consoleErrors: r.errors });
      await ctx.close();
    }
    {
      // The backup write fails (storage full for that key): the unreadable save must stay in the main key, untouched,
      // and the session plays in memory (1.5/1.6 tester D5). Filling the real quota is slow and browser-dependent, so
      // setItem throws QuotaExceededError for the backup key only; the storage probe and other keys work normally.
      const QUOTA_ON_BACKUP = `(() => {
        const set = Storage.prototype.setItem;
        Storage.prototype.setItem = function (k, v) {
          if (String(k).startsWith('${SAVE_KEY}.unreadable')) throw new DOMException('quota exceeded (test)', 'QuotaExceededError');
          return set.call(this, k, v);
        };
      })();`;
      const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 } });
      const first = await load(ctx, server, { waitSave: true });
      await writeStorage(first.page, SAVE_KEY, '{not json');
      await first.page.close();
      const r = await load(ctx, server, { waitData: true, waitSave: true, initScript: QUOTA_ON_BACKUP });
      const f = [...loadFailures(r), ...saveFailures(r, { persistent: false, recovered: false })];
      const main = await readStorage(r.page, SAVE_KEY);
      if (main !== '{not json') f.push(`the main key was changed to ${JSON.stringify(String(main).slice(0, 60))}; it must stay "{not json"`);
      const backup = await readStorage(r.page, SAVE_KEY + '.unreadable');
      if (backup !== null) f.push(`the backup key holds ${JSON.stringify(backup)} though its write was refused`);
      if (!r.warnings.some(w => w.includes('[save]') && w.includes("couldn't be copied"))) f.push('no [save] warning about the failed copy');
      record('save_backup_fails_keeps_main', f, { saveLine: r.saveLine, main, consoleErrors: r.errors });
      await ctx.close();
    }
  }

  // 6. Start card (task 2.2): Boot shows the card once data and save are ready; Enter (the button has focus) or a click on
  // the button is the gesture that unlocks sound and loads Main. Screenshots of the card and of the empty Main screen.
  if (only.has('start')) {
    for (const [how, vw, vh] of [['enter', 1366, 768], ['click', 1920, 1080]]) {
      const name = `start_card_${how}`;
      const ctx = await browser.newContext({ viewport: { width: vw, height: vh } });
      const page = await ctx.newPage();
      const lines = [], errors = [], warnings = [];
      page.on('console', m => {
        const text = m.text().trim();
        lines.push(text);
        if (m.type() === 'error') errors.push(text);
        if (m.type() === 'warning') warnings.push(text);
      });
      page.on('pageerror', e => errors.push('pageerror: ' + e.message));
      const has = prefix => lines.some(l => l === prefix || l.startsWith(prefix + ' '));
      const waitFor = async (pred, ms) => { const end = Date.now() + ms; while (!pred() && Date.now() < end) await page.waitForTimeout(50); return pred(); };
      const f = [];
      server.resetCounters();
      await page.goto(server.url, { waitUntil: 'load' });
      if (!await waitFor(() => has('WT_START_SHOWN'), READY_TIMEOUT_MS)) f.push(`no WT_START_SHOWN within ${READY_TIMEOUT_MS} ms`);
      else {
        await page.waitForTimeout(600); // the card's peel-on (380 ms) has settled
        const cardShot = path.join(shotsDir, `start_card_${vw}x${vh}.png`);
        fs.mkdirSync(shotsDir, { recursive: true });
        await page.screenshot({ path: cardShot });
        results.screenshots.push(cardShot);
        if (has('WT_SOUND_UNLOCKED')) f.push('sound unlocked before any gesture');
        if (how === 'enter') {
          await page.evaluate(() => { const c = document.getElementById('unity-canvas'); if (c) c.focus(); });
          await page.keyboard.press('Enter');
        } else {
          // The button sits at the card's lower quarter: 85 canvas units below centre at the 1366x768 reference,
          // scaled by Scale With Screen Size (match 0.5).
          const scale = Math.sqrt((vw / 1366) * (vh / 768));
          await page.mouse.click(vw / 2, vh / 2 + 85 * scale);
        }
        for (const line of ['WT_STARTED', 'WT_SOUND_UNLOCKED', 'WT_MAIN_READY'])
          if (!await waitFor(() => has(line), 30000)) f.push(`no ${line} after the ${how}`);
        if (!await waitFor(() => has('WT_SOUND_READY') || warnings.some(w => w.startsWith('WT_SOUND_UNVERIFIED')), 10000))
          f.push('neither WT_SOUND_READY nor a WT_SOUND_UNVERIFIED warning');
        await page.waitForTimeout(600); // the first screen is placed without a transition; let a few frames draw
        const mainShot = path.join(shotsDir, `main_${vw}x${vh}.png`);
        await page.screenshot({ path: mainShot });
        results.screenshots.push(mainShot);
      }
      if (errors.length) f.push(`${errors.length} console error(s): ${errors.slice(0, 3).join(' | ')}`);
      const pick = prefix => lines.find(l => l.startsWith(prefix)) || null;
      record(name, f, {
        viewport: `${vw}x${vh}`, mainLine: pick('WT_MAIN_READY'), soundLine: pick('WT_SOUND_READY') || warnings.find(w => w.startsWith('WT_SOUND_UNVERIFIED')) || null,
        consoleErrors: errors, consoleWarnings: warnings.slice(0, 10),
      });
      await ctx.close();
    }
  }

  // 5. Size budget, from the 1366x768 cold load.
  if (only.has('size')) {
    const f = [];
    if (!cold1366) f.push('no cold load to measure');
    else {
      if (cold1366.bytes > budgetMb * MB) f.push(`download ${(cold1366.bytes / MB).toFixed(2)} MB is over the ${budgetMb} MB budget`);
      if (cold1366.bytes > WARN_MB * MB) results.warnings.push(`download ${(cold1366.bytes / MB).toFixed(2)} MB is over ${WARN_MB} MB`);
    }
    record('size_budget', f, cold1366 ? { bytes: cold1366.bytes, megabytes: +(cold1366.bytes / MB).toFixed(3), largest: cold1366.largest } : {});
  }
} catch (e) {
  results.tests.push({ name: 'runner', pass: false, failures: [String(e && e.stack || e)] });
  console.error(e);
} finally {
  if (browser) await browser.close().catch(() => {});
  for (const s of servers) await s.close().catch(() => {});
  for (const d of tempDirs) fs.rmSync(d, { recursive: true, force: true });
  // A typo in --only must never read as a pass: unknown names, or no tests run at all, fail the run.
  const unknown = [...only].filter(n => !TEST_NAMES.includes(n));
  if (unknown.length) results.tests.push({ name: 'arguments', pass: false, failures: [`unknown --only name(s): ${unknown.join(', ')}`] });
  if (!results.tests.some(t => t.name !== 'arguments')) results.tests.push({ name: 'arguments', pass: false, failures: ['no tests ran'] });
  results.total = results.tests.length;
  results.passed = results.tests.filter(t => t.pass).length;
  results.failed = results.total - results.passed;
  results.endedAt = new Date().toISOString();
  fs.mkdirSync(path.dirname(outFile), { recursive: true });
  fs.writeFileSync(outFile, JSON.stringify(results, null, 2));
  console.log(`${results.passed}/${results.total} passed, ${results.failed} failed -> ${outFile}`);
  process.exitCode = results.tests.some(t => t.name === 'runner') ? 2 : results.failed > 0 ? 1 : 0;
}
