// Phase 8.4 P0a browser checks for the 3D world build in world/: WebGL context loss and GPU memory.
//
//   node Tests/Web/world_gpu.mjs [--site <folder>] [--gpu d3d11|swiftshader] [--out <results.json>] [--only loss|memory|nowebgl]
//
//   loss:    force a WebGL context loss with WEBGL_lose_context (a) mid-walk and (b) mid-show. Each time: the page shows the one lowercase line and a
//            single "reload" button, none of Unity's own error texts show, the save is flushed (and survives the reload), and the reload works.
//   nowebgl: a browser with no WebGL at all gets "this browser can't draw 3D right now..." and never Unity's "does not support WebGL".
//   memory:  travels coffee -> records -> ... -> arena -> coffee, twice, in ONE session (?wt_test hook TestTravel) and records, after each arrival, the live WebGL
//            resources (counted by wrapping the WebGL calls: textures, buffers, renderbuffers and their estimated bytes), the wasm heap and the JS heap.
//            It must not grow place over place: the second lap may not exceed the first at the same place, and coffee at the end may not exceed coffee at the start.
//   also checks that the context was created with powerPreference "high-performance".
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { startServer } from './server.mjs';
import { makePage, loadReady } from './world_places.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const project = path.resolve(here, '..', '..');
const repo = path.resolve(project, '..');
const { chromium } = createRequire(path.join(repo, 'world_tour', 'tests', 'e2e', 'package.json'))('playwright');
const argv = process.argv.slice(2);
const opt = (n, d) => { const i = argv.indexOf(n); return i >= 0 && i + 1 < argv.length ? argv[i + 1] : d; };
const site = path.resolve(opt('--site', path.join(repo, 'world')));
const gpuMode = opt('--gpu', 'd3d11');
const only = opt('--only', '');
const outFile = path.resolve(opt('--out', path.join(project, 'Temp', 'world-gpu.json')));
const shotsDir = path.join(project, 'Temp', 'world-gpu-shots');
const READY = 90000;
const sleep = ms => new Promise(r => setTimeout(r, ms));
const results = { site, gpuMode, startedAt: new Date().toISOString(), failures: [], loss: {}, memory: {} };
const fail = m => { results.failures.push(m); console.log('FAIL', m); };

const LOST_LINE = 'the picture stopped. press reload and your tour comes right back.';
const NO3D_LINE = "this browser can't draw 3D right now. close the browser, open it again, and come back.";
// anything Unity or the loader would say by itself
const RAW = /does not support webgl|unity content|error occurred|unable to parse|invalid or unexpected|out of memory|abort\(|uncaught|runtime error|couldn.t start/i;

function launchOptions() {
  const extra = ['--enable-precise-memory-info', '--js-flags=--expose-gc'];
  if (gpuMode === 'd3d11') return { headless: true, args: ['--use-gl=angle', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl', ...extra] };
  return { headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', ...extra] };
}

// Wraps WebGL2 so the page can report how many GPU resources are alive and roughly how many bytes they hold.
const GL_COUNTER = `(() => {
  const P = WebGL2RenderingContext.prototype;
  const st = { tex: 0, buf: 0, rb: 0, texBytes: 0, bufBytes: 0, rbBytes: 0 };
  const info = new WeakMap();
  const bound = {};
  let unit = 0x84C0;
  const bpp = f => {
    if (f === 0x83F0 || f === 0x83F1 || f === 0x9274 || f === 0x9275 || f === 0x9270 || f === 0x9271) return 0.5;
    if (f >= 0x83F2 && f <= 0x83F3) return 1;
    if (f >= 0x8E8C && f <= 0x8E8F) return 1;
    if (f >= 0x93B0 && f <= 0x93DD) return 1;
    switch (f) {
      case 0x881A: return 8; case 0x8814: return 16; case 0x8C3A: return 4; case 0x81A6: return 4; case 0x8CAC: return 4;
      case 0x88F0: return 4; case 0x8CAD: return 8; case 0x8229: return 1; case 0x822B: return 2; case 0x8051: return 3;
      case 0x1907: return 3; case 0x1906: case 0x1909: return 1; case 0x190A: return 2; case 0x822D: return 2; case 0x822F: return 4; case 0x81A5: return 2;
      case 0x8D48: return 1; case 0x8D46: return 1;
      default: return 4;
    }
  };
  const texOf = t => { const c = (t >= 0x8515 && t <= 0x851A) ? 0x8513 : t; return bound['tex:' + unit + ':' + c]; };
  const setLevel = (target, level, bytes) => {
    const o = texOf(target); if (!o) return;
    const i = info.get(o); if (!i) return;
    const key = target + ':' + level;
    const prev = i.levels[key] || 0;
    i.levels[key] = bytes; i.bytes += bytes - prev; st.texBytes += bytes - prev;
  };
  const chain = (levels, f, w, h, d) => { let b = 0; for (let l = 0; l < levels; l++) b += Math.max(1, w >> l) * Math.max(1, h >> l) * Math.max(1, d >> l) * bpp(f); return b; };
  const wrap = (name, fn) => { const o = P[name]; P[name] = function (...a) { try { fn(...a); } catch (e) {} return o.apply(this, a); }; };
  const oct = P.createTexture; P.createTexture = function () { const t = oct.call(this); if (t) { info.set(t, { bytes: 0, levels: {} }); st.tex++; } return t; };
  const odt = P.deleteTexture; P.deleteTexture = function (t) { const i = t && info.get(t); if (i) { st.tex--; st.texBytes -= i.bytes; info.delete(t); for (const k in bound) if (bound[k] === t) bound[k] = null; } return odt.call(this, t); };
  const ocb = P.createBuffer; P.createBuffer = function () { const b = ocb.call(this); if (b) { info.set(b, { bytes: 0 }); st.buf++; } return b; };
  const odb = P.deleteBuffer; P.deleteBuffer = function (b) { const i = b && info.get(b); if (i) { st.buf--; st.bufBytes -= i.bytes; info.delete(b); for (const k in bound) if (bound[k] === b) bound[k] = null; } return odb.call(this, b); };
  const ocr = P.createRenderbuffer; P.createRenderbuffer = function () { const r = ocr.call(this); if (r) { info.set(r, { bytes: 0 }); st.rb++; } return r; };
  const odr = P.deleteRenderbuffer; P.deleteRenderbuffer = function (r) { const i = r && info.get(r); if (i) { st.rb--; st.rbBytes -= i.bytes; info.delete(r); if (bound.rb === r) bound.rb = null; } return odr.call(this, r); };
  wrap('activeTexture', u => { unit = u; });
  wrap('bindTexture', (t, o) => { bound['tex:' + unit + ':' + t] = o; });
  wrap('bindBuffer', (t, o) => { bound['buf:' + t] = o; });
  wrap('bindRenderbuffer', (t, o) => { bound.rb = o; });
  wrap('texStorage2D', (t, lv, f, w, h) => setLevel(t, 'all', chain(lv, f, w, h, 1)));
  wrap('texStorage3D', (t, lv, f, w, h, d) => setLevel(t, 'all', chain(lv, f, w, h, t === 0x806F ? d : 1)));
  wrap('texImage2D', (...a) => {
    if (a.length >= 9) setLevel(a[0], a[1], a[3] * a[4] * bpp(a[2]));
    else if (a.length === 6 && a[5]) setLevel(a[0], a[1], (a[5].width || 0) * (a[5].height || 0) * bpp(a[2]));
  });
  wrap('texImage3D', (t, lv, f, w, h, d) => setLevel(t, lv, w * h * d * bpp(f)));
  wrap('compressedTexImage2D', (t, lv, f, w, h) => setLevel(t, lv, Math.ceil(w * h * bpp(f))));
  wrap('compressedTexImage3D', (t, lv, f, w, h, d) => setLevel(t, lv, Math.ceil(w * h * d * bpp(f))));
  wrap('generateMipmap', t => { const o = texOf(t); const i = o && info.get(o); if (i && !i.mipped) { i.mipped = true; const add = Math.round(i.bytes / 3); i.bytes += add; st.texBytes += add; } });
  wrap('bufferData', (...a) => { const o = bound['buf:' + a[0]]; const i = o && info.get(o); if (!i) return; const d = a[1]; /* emscripten passes the whole wasm heap plus an offset and a length in elements */ const n = typeof d === 'number' ? d : (!d ? 0 : (a.length >= 5 ? a[4] * (d.BYTES_PER_ELEMENT || 1) : d.byteLength)); st.bufBytes += n - i.bytes; i.bytes = n; });
  const rbs = (r, f, w, h, s) => { const i = r && info.get(r); if (!i) return; const n = w * h * bpp(f) * Math.max(1, s); st.rbBytes += n - i.bytes; i.bytes = n; };
  wrap('renderbufferStorage', (t, f, w, h) => rbs(bound.rb, f, w, h, 1));
  wrap('renderbufferStorageMultisample', (t, s, f, w, h) => rbs(bound.rb, f, w, h, s));
  window.__glStats = () => ({ ...st, gpuBytes: st.texBytes + st.bufBytes + st.rbBytes });
})();`;

const NO_WEBGL = `(() => {
  const o = HTMLCanvasElement.prototype.getContext;
  HTMLCanvasElement.prototype.getContext = function (t, ...a) { return /webgl/i.test(String(t)) ? null : o.call(this, t, ...a); };
})();`;

const loseContext = page => page.evaluate(() => {
  const c = document.getElementById('unity-canvas');
  const gl = c.getContext('webgl2') || c.getContext('webgl');
  const ext = gl && gl.getExtension('WEBGL_lose_context');
  if (!ext) return false;
  ext.loseContext();
  return true;
});

async function checkOverlay(h, label) {
  const { page, gfail, step } = h;
  try { await page.waitForSelector('#reload', { state: 'visible', timeout: 8000 }); }
  catch { gfail(`${label}: the reload button never showed`); return false; }
  const view = await page.evaluate(() => {
    const m = document.getElementById('message');
    const btn = document.getElementById('reload');
    const r = btn.getBoundingClientRect();
    return {
      line: m.firstChild ? m.firstChild.textContent : '',
      body: document.body.innerText,
      buttons: [...document.querySelectorAll('button')].filter(b => b.offsetParent !== null).length,
      btnW: r.width, btnH: r.height, btnText: btn.textContent,
    };
  });
  if (view.line !== LOST_LINE) gfail(`${label}: the line reads "${view.line}"`);
  if (view.buttons !== 1) gfail(`${label}: ${view.buttons} visible buttons, want exactly 1`);
  if (view.btnText !== 'reload') gfail(`${label}: the button reads "${view.btnText}"`);
  if (view.btnW < 120 || view.btnH < 44) gfail(`${label}: the reload button is small (${view.btnW}x${view.btnH})`);
  if (RAW.test(view.body)) gfail(`${label}: raw error text on the page: ${view.body.slice(0, 200)}`);
  if (view.line !== view.line.toLowerCase()) gfail(`${label}: the line is not lowercase`);
  step(`${label}: overlay "${view.line}" + [${view.btnText}] ${Math.round(view.btnW)}x${Math.round(view.btnH)}`);
  return true;
}

async function lossTests(browser, server) {
  const size = [1366, 768];
  // ---------- (a) mid-walk
  {
    const h = await makePage(browser, server, size, 'loss_walk', results, fail, shotsDir, 'loss_walk');
    const { page, gfail, step, mark, waitLine, focus } = h;
    const g = results.loss.walk = { numbers: {} };
    try {
      if (!await loadReady(h, server, '?wt_test=1', READY)) return;
      const attrs = await page.evaluate(() => { const c = document.getElementById('unity-canvas'); const gl = c.getContext('webgl2'); return gl ? gl.getContextAttributes() : null; });
      g.contextAttributes = attrs;
      if (!attrs || attrs.powerPreference !== 'high-performance') gfail('the WebGL context was not created with powerPreference high-performance: ' + JSON.stringify(attrs));
      else step('context attributes: powerPreference ' + attrs.powerPreference);
      await focus(); await sleep(1500);
      await page.keyboard.down('KeyD'); await sleep(900);
      await page.mouse.click(Math.round(size[0] * 0.3), Math.round(size[1] * 0.75));
      await sleep(500);
      const m0 = mark();
      if (!await loseContext(page)) { gfail('no WEBGL_lose_context extension in this browser'); return; }
      await page.keyboard.up('KeyD');
      if (!await checkOverlay(h, 'walk')) return;
      if (!await waitLine(/^WT_SAVE_FLUSH_JS/, 5000, m0)) gfail('walk: the game did not flush the save when the context was lost');
      const blob = await page.evaluate(() => localStorage.getItem('worldtour.v1'));
      const saved = blob ? JSON.parse(blob) : null;
      g.saved = saved && saved.world;
      if (!saved || !saved.world || saved.world.place !== 'coffee' || !saved.world.position) gfail('walk: the save has no place and position after the loss: ' + JSON.stringify(saved && saved.world));
      await page.screenshot({ path: path.join(shotsDir, 'loss_walk.png') });
      const m1 = mark();
      await page.click('#reload');
      await page.waitForLoadState('load');
      const ready = await waitLine(/^WT_WORLD_GAME_READY/, READY, m1);
      if (!ready) gfail('walk: no ready line after pressing reload');
      else { step('walk: back after reload: ' + ready.text); g.reloaded = true; }
      const blob2 = await page.evaluate(() => localStorage.getItem('worldtour.v1'));
      if (!blob2 || JSON.parse(blob2).world.place !== 'coffee') gfail('walk: the save did not survive the reload');
      if (h.g.errors.length) gfail(`walk: ${h.g.errors.length} console error(s): ${h.g.errors.slice(0, 2).join(' | ').slice(0, 300)}`);
    } finally { await h.ctx.close(); }
  }
  // ---------- (b) mid-show
  {
    const h = await makePage(browser, server, size, 'loss_show', results, fail, shotsDir, 'loss_show');
    const { page, gfail, step, mark, waitLine, waitAny, target, focus, storedFans, patchSave } = h;
    const g = results.loss.show = { numbers: {} };
    try {
      if (!await loadReady(h, server, '?wt_test=1', READY)) return;
      await patchSave(12345, ['coffee']);
      let m0 = mark();
      await page.reload({ waitUntil: 'load' });
      if (!await waitLine(/^WT_WORLD_GAME_READY/, READY, m0)) { gfail('show: no ready after the save patch'); return; }
      await focus(); await sleep(2500);
      const stageT = target('stage');
      if (!stageT) { gfail('show: no stage position'); return; }
      m0 = mark();
      await page.mouse.click(stageT.x, stageT.y);
      if (!await waitLine(/^WT_WORLD_STAGE_OFFER/, 30000, m0)) { gfail('show: no stage offer'); return; }
      await sleep(500);
      m0 = mark();
      await page.keyboard.press('Enter');
      if (!await waitLine(/^WT_SHOW_START/, 15000, m0)) { gfail('show: the show never started'); return; }
      const q = await waitAny([/^WT_MATH mode=/], 25000, m0);
      if (!q) { gfail('show: no question'); return; }
      const ans = /answer=(\S+)/.exec(q.text)[1].replace(/[^0-9.\/\-]/g, '');
      await sleep(900);
      await page.keyboard.type(ans, { delay: 70 }); await page.keyboard.press('Enter');
      if (!await waitLine(/^WT_SONG_RESULT/, 12000, q.i + 1)) { gfail('show: no song result'); return; }
      await sleep(1500);
      const before = await storedFans();
      g.numbers.fansBeforeLoss = before;
      const m1 = mark();
      if (!await loseContext(page)) { gfail('show: no lose_context extension'); return; }
      if (!await checkOverlay(h, 'show')) return;
      if (!await waitLine(/^WT_SAVE_FLUSH_JS/, 5000, m1)) gfail('show: no save flush on the loss');
      const after = await storedFans();
      g.numbers.fansAfterLoss = after;
      if (!(after >= before) || !(after > 12345)) gfail(`show: fans ${before} before the loss, ${after} after (patched 12345)`);
      await page.screenshot({ path: path.join(shotsDir, 'loss_show.png') });
      const m2 = mark();
      await page.click('#reload');
      await page.waitForLoadState('load');
      const ready = await waitLine(/^WT_WORLD_GAME_READY/, READY, m2);
      if (!ready) { gfail('show: no ready after reload'); return; }
      const fansNow = +/fans=(\d+)/.exec(ready.text)[1];
      g.numbers.fansAfterReload = fansNow;
      if (fansNow !== after) gfail(`show: reload shows ${fansNow} fans, the save had ${after}`);
      step(`show: fans ${before} -> ${after} -> reload ${fansNow}`);
      if (h.g.errors.length) gfail(`show: ${h.g.errors.length} console error(s): ${h.g.errors.slice(0, 2).join(' | ').slice(0, 300)}`);
    } finally { await h.ctx.close(); }
  }
}

async function noWebglTest(browser, server) {
  const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 } });
  await ctx.addInitScript(NO_WEBGL);
  const page = await ctx.newPage();
  try {
    await page.goto(server.url, { waitUntil: 'load' });
    await sleep(6000);
    const body = await page.evaluate(() => document.body.innerText);
    results.loss.noWebgl = body;
    if (!body.toLowerCase().includes(NO3D_LINE.toLowerCase())) fail(`nowebgl: expected "${NO3D_LINE}", page says "${body.slice(0, 200)}"`);
    if (RAW.test(body)) fail('nowebgl: raw Unity text on the page: ' + body.slice(0, 200));
    console.log('  nowebgl: ' + JSON.stringify(body));
  } finally { await ctx.close(); }
}

async function memoryTest(browser, server) {
  const places = ['coffee', 'records', 'rec', 'rooftop', 'club', 'theater', 'festival', 'arena'];
  const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 } });
  await ctx.addInitScript(GL_COUNTER);
  const page = await ctx.newPage();
  const lines = [], errors = [];
  page.on('console', m => { lines.push(m.text()); if (m.type() === 'error') errors.push(m.text()); });
  page.on('pageerror', e => errors.push('pageerror: ' + e.message));
  const mem = results.memory;
  mem.samples = [];
  const waitLine = async (re, ms, from) => { const end = Date.now() + ms; for (;;) { for (let i = from; i < lines.length; i++) if (re.test(lines[i])) return i; if (Date.now() > end) return -1; await sleep(50); } };
  const MB = v => (v / 1048576).toFixed(1);
  const sample = async (label, place, lap) => {
    await sleep(3000);
    const s = await page.evaluate(() => {
      if (window.gc) window.gc();
      const gl = window.__glStats();
      const pm = performance.memory || {};
      const inst = window.__wtInstance;
      const heap = inst && inst.Module && inst.Module.HEAPU8 ? inst.Module.HEAPU8.length : null;
      return { ...gl, jsHeap: pm.usedJSHeapSize || null, wasmHeap: heap };
    });
    s.label = label; s.place = place; s.lap = lap;
    mem.samples.push(s);
    console.log(`  ${label.padEnd(15)} textures ${String(s.tex).padStart(4)} (${MB(s.texBytes)} MB)  buffers ${String(s.buf).padStart(4)} (${MB(s.bufBytes)} MB)  rbs ${s.rb} (${MB(s.rbBytes)} MB)  gpu ${MB(s.gpuBytes)} MB  wasm ${s.wasmHeap ? MB(s.wasmHeap) : '?'} MB  js ${s.jsHeap ? MB(s.jsHeap) : '?'} MB`);
    return s;
  };
  try {
    const t0 = lines.length;
    await page.goto(server.url + '?wt_test=1', { waitUntil: 'load' });
    if (await waitLine(/^WT_WORLD_GAME_READY/, READY, t0) < 0) { fail('memory: no ready line'); return; }
    await page.evaluate(() => { const c = document.getElementById('unity-canvas'); if (c) c.focus(); });
    await sleep(1500);
    await sample('coffee (start)', 'coffee', 0);
    const route = [];
    for (let lap = 1; lap <= 2; lap++) { for (const p of places.slice(1)) route.push([p, lap]); route.push(['coffee', lap]); }
    for (const [p, lap] of route) {
      const m = lines.length;
      let ok = false;
      for (let tries = 0; tries < 6 && !ok; tries++) {   // the hook only fires while she is free; retry until the travel starts
        await page.evaluate(id => window.__wtInstance.SendMessage('WorldGame', 'TestTravel', id), p);
        ok = await waitLine(new RegExp('^WT_WORLD_TRAVEL next=' + p), 2500, m) >= 0;
      }
      if (!ok) { fail(`memory: travel to ${p} never started`); return; }
      if (await waitLine(new RegExp('^WT_WORLD_ARRIVED place=' + p), 40000, m) < 0) { fail(`memory: never arrived at ${p}`); return; }
      if (await waitLine(/^WT_WORLD_MODE mode=Free/, 15000, m) < 0) { fail(`memory: not free at ${p}`); return; }
      await sample(`${p} (lap ${lap})`, p, lap);
    }
    const first = mem.samples[0];
    const at = (p, lap) => mem.samples.find(s => s.place === p && s.lap === lap);
    const tol = 1.10, slack = 1048576;
    const checks = [];
    for (const p of places.slice(1)) {
      const a = at(p, 1), b = at(p, 2);
      checks.push([`${p}: gpu bytes lap 2 vs lap 1`, b.gpuBytes, a.gpuBytes, b.gpuBytes <= a.gpuBytes * tol + slack]);
      checks.push([`${p}: live textures lap 2 vs lap 1`, b.tex, a.tex, b.tex <= a.tex * tol + 5]);
    }
    const c2 = at('coffee', 2), c1 = at('coffee', 1);
    checks.push(['coffee at the end vs start: gpu bytes', c2.gpuBytes, first.gpuBytes, c2.gpuBytes <= first.gpuBytes * tol + slack]);
    checks.push(['coffee at the end vs start: textures', c2.tex, first.tex, c2.tex <= first.tex * tol + 5]);
    checks.push(['coffee lap 2 vs lap 1: gpu bytes', c2.gpuBytes, c1.gpuBytes, c2.gpuBytes <= c1.gpuBytes * tol + slack]);
    if (first.wasmHeap) {
      const l1 = Math.max(...mem.samples.filter(s => s.lap <= 1).map(s => s.wasmHeap));
      const l2 = Math.max(...mem.samples.filter(s => s.lap === 2).map(s => s.wasmHeap));
      checks.push(['wasm heap peak lap 2 vs lap 1', l2, l1, l2 <= l1 * 1.05]);
    }
    if (first.jsHeap) checks.push(['js heap at the end vs end of lap 1', c2.jsHeap, c1.jsHeap, c2.jsHeap <= c1.jsHeap * 1.25 + 2097152]);
    // the live resources are one place's worth, never a running total of all eight
    const maxPlace = Math.max(...mem.samples.filter(s => s.place !== 'coffee').map(s => s.gpuBytes));
    checks.push(['gpu bytes at the end vs the largest place seen', c2.gpuBytes, maxPlace, c2.gpuBytes <= maxPlace * tol + slack]);
    mem.checks = checks.map(([n, v, ref, okc]) => ({ name: n, value: v, reference: ref, ok: okc }));
    for (const c of mem.checks) { console.log(`  ${c.ok ? 'ok  ' : 'FAIL'} ${c.name}: ${Math.round(c.value)} vs ${Math.round(c.reference)}`); if (!c.ok) fail('memory: ' + c.name + ` ${Math.round(c.value)} vs ${Math.round(c.reference)}`); }
    if (errors.length) fail(`memory: ${errors.length} console error(s): ${errors.slice(0, 2).join(' | ').slice(0, 300)}`);
  } finally { await ctx.close(); }
}

let browser = null, server = null;
try {
  if (!fs.existsSync(path.join(site, 'index.html'))) throw new Error(`no index.html in ${site}`);
  fs.mkdirSync(shotsDir, { recursive: true });
  server = await startServer(site);
  browser = await chromium.launch(launchOptions());
  results.browserVersion = browser.version();
  if (!only || only === 'loss') await lossTests(browser, server);
  if (!only || only === 'nowebgl') await noWebglTest(browser, server);
  if (!only || only === 'memory') await memoryTest(browser, server);
} catch (e) {
  results.failures.push('runner: ' + (e && e.stack || e));
  console.error(e);
} finally {
  if (browser) await browser.close().catch(() => {});
  if (server) await server.close().catch(() => {});
  results.endedAt = new Date().toISOString();
  fs.mkdirSync(path.dirname(outFile), { recursive: true });
  fs.writeFileSync(outFile, JSON.stringify(results, null, 2));
  console.log(results.failures.length ? `FAILED (${results.failures.length}); results in ${outFile}` : `PASSED; results in ${outFile}`);
  process.exitCode = results.failures.length ? 1 : 0;
}
