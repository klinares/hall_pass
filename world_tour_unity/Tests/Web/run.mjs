// Browser tests for the web build in tour/. Plain Node plus the Playwright install already in
// world_tour/tests/e2e/node_modules (read only; nothing is installed or changed there).
//
//   node Tests/Web/run.mjs [--out <results.json>] [--shots <dir>] [--site <folder>] [--only <names>] [--budget-mb <n>]
//
//   --out        results file, default <project>/Temp/web-results.json (the results file is the evidence)
//   --shots      screenshot folder, default <project>/Temp/web-shots
//   --site       folder to serve instead of tour/ (used to prove the tests can fail)
//   --only       comma list of: cold, reload, storage, broken, size   (default: all)
//   --budget-mb  size ceiling in MB (10^6 bytes), default 20; over 12 MB or a cold load over 8 s only warns
//
// Exits 1 if any test fails, 2 if the runner itself breaks. Server and browser are closed in finally.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { startServer } from './server.mjs';

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
const only = new Set((opt('--only', 'cold,reload,storage,broken,size')).split(',').map(s => s.trim()));
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

/** Loads the page in `context` and waits for WT_BOOT_READY (or a visible failure message, or a timeout). */
async function load(context, server, { shot, initScript } = {}) {
  const page = await context.newPage();
  if (initScript) await page.addInitScript(initScript);
  const origin = new URL(server.url).origin;
  const errors = [], warnings = [], external = [], unityCache = [];
  let readyMs = null, t0 = 0;
  page.on('console', m => {
    const text = m.text();
    if (m.type() === 'error') errors.push(text);
    if (m.type() === 'warning') warnings.push(text);
    if (text.startsWith('[UnityCache]')) unityCache.push(text);
    if (readyMs === null && text.trim() === 'WT_BOOT_READY') readyMs = Date.now() - t0;
  });
  page.on('pageerror', e => errors.push('pageerror: ' + e.message));
  page.on('request', r => {
    const u = r.url();
    // data: URLs never leave the browser; everything else must be the local server or a blob.
    if (!u.startsWith(origin + '/') && !u.startsWith('blob:') && !u.startsWith('data:')) external.push(u);
  });
  server.resetCounters();
  t0 = Date.now();
  await page.goto(server.url, { waitUntil: 'load' });
  const deadline = Date.now() + READY_TIMEOUT_MS;
  while (readyMs === null && Date.now() < deadline) {
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
  return { page, readyMs, errors, warnings, external, unityCache, layout, bytes: server.bytesServed, files, largest };
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
    const r = await load(ctx, server, { initScript: STORAGE_BLOCKED, shot: path.join(shotsDir, 'storage_blocked.png') });
    const blocked = await r.page.evaluate(() => {
      const throws = fn => { try { fn(); return false; } catch (e) { return true; } };
      return { indexedDB: throws(() => indexedDB.open('probe')), localStorage: throws(() => localStorage.getItem('probe')) };
    });
    const f = loadFailures(r);
    if (!blocked.indexedDB) f.push('indexedDB.open did not throw, so the block was not in effect');
    if (!blocked.localStorage) f.push('localStorage did not throw, so the block was not in effect');
    record('storage_blocked', f, { readyMs: r.readyMs, blocked, consoleErrors: r.errors, consoleWarnings: r.warnings.slice(0, 10) });
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
  const unknown = [...only].filter(n => !['cold', 'reload', 'storage', 'broken', 'size'].includes(n));
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
