// Phase 10B.1 memory soak (the two "Target crashed" renderer crashes in long runs): the avatar editor and the boutique must not grow memory.
//
//   node Tests/Web/world_memory.mjs [--site <folder>] [--gpu d3d11|swiftshader] [--changes 200] [--visits 20] [--out <results.json>]
//
//   1. 200 editor changes by mouse (colors, patterns, items, on six tabs), sampled at the start, then every 50 changes.
//   2. 20 boutique visits in one session (in through the door, try 12 items on at the mirror, out), sampled at the start, then every 5 visits.
//      The hooks are ?wt_test only: WorldGame.TestBoutique("in" | "browse" | "out").
//   A sample is taken after a forced JS garbage collection and is the same set world_gpu.mjs reads: live WebGL textures, buffers and renderbuffers with their estimated
//   bytes (the wrapped WebGL calls), the wasm heap and the JS heap. The first lap is warm-up (50 changes, 5 visits): after it, every sample must stay within 10 percent of
//   the warm-up sample (a small slack for 1 MB and a few textures). The numbers are printed and written to the results file.
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { startServer } from './server.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const project = path.resolve(here, '..', '..');
const repo = path.resolve(project, '..');
const { chromium } = createRequire(path.join(here, 'package.json'))('playwright');
const argv = process.argv.slice(2);
const opt = (n, d) => { const i = argv.indexOf(n); return i >= 0 && i + 1 < argv.length ? argv[i + 1] : d; };
const site = path.resolve(opt('--site', path.join(repo, 'world')));
const gpuMode = opt('--gpu', 'd3d11');
const CHANGES = +opt('--changes', 200), VISITS = +opt('--visits', 20);
const outFile = path.resolve(opt('--out', path.join(project, 'Temp', 'world-memory.json')));
const READY = 120000;
const sleep = ms => new Promise(r => setTimeout(r, ms));
const results = { site, gpuMode, startedAt: new Date().toISOString(), failures: [], editor: [], boutique: [], stages: [] };
const fail = m => { results.failures.push(m); console.log('FAIL', m); };

// the WebGL resource counter lives in world_gpu.mjs; reuse its text so the two tests count the same way
const GL_COUNTER = /const GL_COUNTER = `([\s\S]*?)`;\r?\n/.exec(fs.readFileSync(path.join(here, 'world_gpu.mjs'), 'utf8'))[1];

const launch = () => {
  const extra = ['--enable-precise-memory-info', '--js-flags=--expose-gc'];
  return gpuMode === 'd3d11'
    ? { headless: true, args: ['--use-gl=angle', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl', ...extra] }
    : { headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', ...extra] };
};

const MB = v => (v / 1048576).toFixed(1);
let server = null, browser = null;
try {
  if (!fs.existsSync(path.join(site, 'index.html'))) throw new Error(`no index.html in ${site}`);
  server = await startServer(site);
  browser = await chromium.launch(launch());
  const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 } });
  await ctx.addInitScript(GL_COUNTER);
  const page = await ctx.newPage();
  const lines = [], errors = [];
  let crashed = false;
  page.on('console', m => { lines.push(m.text().trim()); if (m.type() === 'error') errors.push(m.text()); });
  page.on('pageerror', e => errors.push('pageerror: ' + e.message));
  page.on('crash', () => { crashed = true; fail('the page crashed (Target crashed)'); });
  const wait = async (re, ms, from = 0) => { const end = Date.now() + ms; for (;;) { for (let i = from; i < lines.length; i++) if (re.test(lines[i])) return i; if (Date.now() > end || crashed) return -1; await sleep(40); } };
  page.on('console', m => { const t = m.text(); if (t.startsWith('WT_BROWSE_HEAP')) console.log('    ' + t); });
  const last = re => { for (let i = lines.length - 1; i >= 0; i--) if (re.test(lines[i])) return lines[i]; return null; };
  const hitsMap = () => {
    const l = last(/^WT_HITS screen=avatar /);
    const out = {};
    if (l) for (const m of l.matchAll(/ (\S+?)=([\d.]+),([\d.]+)/g)) out[m[1]] = [Math.round(+m[2] * 1366), Math.round(+m[3] * 768)];
    return out;
  };
  const click = async xy => { await page.mouse.move(xy[0], xy[1]); await page.mouse.down(); await sleep(40); await page.mouse.up(); };
  const sample = async (list, label) => {
    await sleep(2500);
    const s = await page.evaluate(() => {
      if (window.gc) window.gc();
      const gl = window.__glStats();
      const inst = window.__wtInstance;
      return { ...gl, jsHeap: (performance.memory || {}).usedJSHeapSize || 0, wasmHeap: inst && inst.Module && inst.Module.HEAPU8 ? inst.Module.HEAPU8.length : 0 };
    });
    // the game's own numbers (WorldGame.TestMem, ?wt_test only): live Unity objects and managed and native memory in MB
    const m0 = lines.length;
    await page.evaluate(t => window.__wtInstance.SendMessage('WorldGame', 'TestMem', t), label);
    const mi = await wait(/^WT_MEM tag=/, 5000, m0);
    if (mi >= 0) for (const kv of lines[mi].matchAll(/ (\w+)=(\d+)/g)) s['u_' + kv[1]] = +kv[2];
    s.label = label;
    list.push(s);
    console.log(`  ${label.padEnd(14)} textures ${String(s.tex).padStart(4)}  buffers ${String(s.buf).padStart(4)}  gpu ${MB(s.gpuBytes).padStart(6)} MB  wasm ${MB(s.wasmHeap).padStart(6)} MB  js ${MB(s.jsHeap).padStart(6)} MB`
      + `  | unity meshes ${s.u_meshes} materials ${s.u_materials} textures ${s.u_textures} objects ${s.u_objects} mono ${s.u_mono}/${s.u_monoHeap} MB native ${s.u_native} MB reserved ${s.u_reserved} MB`);
    return s;
  };
  const judge = (list, name, warmIndex) => {
    const ref = list[warmIndex];
    const slack = 1048576, tol = 1.10;
    for (let i = warmIndex + 1; i < list.length; i++) {
      const s = list[i];
      const checks = [['gpu bytes', s.gpuBytes, ref.gpuBytes, s.gpuBytes <= ref.gpuBytes * tol + slack],
        ['textures', s.tex, ref.tex, s.tex <= ref.tex * tol + 5],
        ['buffers', s.buf, ref.buf, s.buf <= ref.buf * tol + 5],
        ['js heap', s.jsHeap, ref.jsHeap, s.jsHeap <= ref.jsHeap * tol + 2 * slack]];
      // the wasm heap only ever grows, in big steps, when the managed heap first outgrows it, so it is judged from the second sample on (the first lap may take one step)
      if (i > warmIndex + 1) { const r2 = list[warmIndex + 1]; checks.push(['wasm heap', s.wasmHeap, r2.wasmHeap, s.wasmHeap <= r2.wasmHeap * tol + slack]); }
      if (ref.u_meshes !== undefined && s.u_meshes !== undefined) {
        checks.push(['unity meshes', s.u_meshes, ref.u_meshes, s.u_meshes <= ref.u_meshes * tol + 5]);
        checks.push(['unity materials', s.u_materials, ref.u_materials, s.u_materials <= ref.u_materials * tol + 5]);
        checks.push(['unity textures', s.u_textures, ref.u_textures, s.u_textures <= ref.u_textures * tol + 5]);
        checks.push(['unity objects', s.u_objects, ref.u_objects, s.u_objects <= ref.u_objects * tol + 50]);
        checks.push(['unity native MB', s.u_native, ref.u_native, s.u_native <= ref.u_native * tol + 2]);
        checks.push(['unity managed MB', s.u_mono, ref.u_mono, s.u_mono <= ref.u_mono * tol + 2]);
      }
      for (const [what, v, r, ok] of checks) {
        console.log(`  ${ok ? 'ok  ' : 'FAIL'} ${name} ${s.label} vs ${ref.label}: ${what} ${Math.round(v)} vs ${Math.round(r)}`);
        if (!ok) fail(`${name} ${s.label} vs ${ref.label}: ${what} grew ${Math.round(v)} vs ${Math.round(r)}`);
      }
    }
  };

  // ---------------------------------------------------------------- 1. editor changes
  const t0 = lines.length;
  await page.goto(server.url + '?wt_test=1&wt_fans=300000&wt_buy=' + encodeURIComponent('outfit:peach-navy,outfit:cherry,hair:blonde,hair:lilac,hair:black') + '&wt_shop=1', { waitUntil: 'load' });
  if (await wait(/^WT_WORLD_GAME_READY/, READY, t0) < 0) { fail('no ready line'); throw new Error('not ready'); }
  await page.evaluate(() => { const c = document.getElementById('unity-canvas'); if (c) c.focus(); });
  if (await wait(/^WT_AVATAR_READY/, 30000, t0) < 0) { fail('the editor never opened (wt_shop)'); throw new Error('no editor'); }
  await sleep(1200);
  console.log(`editor: ${CHANGES} changes`);
  await sample(results.editor, 'start');
  const tabs = ['Tab_top', 'Tab_bottom', 'Tab_hair', 'Tab_hat', 'Tab_shoes', 'Tab_glasses'];
  const picks0 = lines.filter(l => l.startsWith('WT_AVATAR_PICK')).length;
  let changes = 0, tabAt = -1;
  while (changes < CHANGES && !crashed) {
    if (changes % 33 === 0 && tabAt !== (changes / 33 | 0)) {   // a new tab every 33 changes
      tabAt = changes / 33 | 0;
      const m = lines.length;
      const h = hitsMap();
      if (h[tabs[tabAt % tabs.length]]) { await click(h[tabs[tabAt % tabs.length]]); await wait(/^WT_HITS screen=avatar /, 4000, m); await sleep(500); }
    }
    const h = hitsMap();
    const keys = Object.keys(h).filter(k => /^(Color_|Pattern_|Item_|Skin_)/.test(k));
    if (!keys.length) { await sleep(300); continue; }
    const k = keys[(changes * 7 + (changes >> 2)) % keys.length];
    await click(h[k]);
    changes++;
    await sleep(380);   // the editor rebuilds and logs its new positions after 0.4 s
    if (changes % 50 === 0) await sample(results.editor, `${changes} changes`);
  }
  const picks = lines.filter(l => l.startsWith('WT_AVATAR_PICK')).length - picks0;
  console.log(`editor: ${picks} picks logged for ${changes} clicks`);
  if (picks < CHANGES * 0.9) fail(`only ${picks} of ${CHANGES} editor clicks changed anything`);
  judge(results.editor, 'editor', 1);
  // close it with Done
  { const m = lines.length; const h = hitsMap(); if (h.Back) await click(h.Back); if (await wait(/^WT_WARDROBE_CLOSE/, 8000, m) < 0) fail('Done did not close the editor'); await wait(/^WT_WORLD_MODE mode=Free/, 8000, m); }

  // ---------------------------------------------------------------- 2. boutique visits
  console.log(`boutique: ${VISITS} visits`);
  await sleep(1500);
  await sample(results.boutique, 'start');
  const hook = async (cmd, re, ms) => {
    for (let tries = 0; tries < 8; tries++) {
      const m = lines.length;
      await page.evaluate(c => window.__wtInstance.SendMessage('WorldGame', 'TestBoutique', c), cmd);
      const i = await wait(re, ms, m);
      if (i >= 0) return i;
      if (crashed) return -1;
    }
    return -1;
  };
  for (let v = 1; v <= VISITS && !crashed; v++) {
    let m = lines.length;
    if (await hook('in', /^WT_WORLD_ARRIVED place=coffee interior=boutique/, 2500) < 0) { fail(`visit ${v}: never got inside`); break; }
    if (await wait(/^WT_WORLD_MODE mode=Free/, 15000, m) < 0) { fail(`visit ${v}: not free inside`); break; }
    await sleep(600);
    if (v <= 2) await sample(results.stages, `visit ${v} inside`);   // where a visit's memory goes: inside, after the mirror, back out
    m = lines.length;
    await hook('browse', /^WT_BOUTIQUE_CLOSE/, 9000);
    await sleep(900);
    if (v <= 2) await sample(results.stages, `visit ${v} browsed`);
    m = lines.length;
    if (await hook('out', /^WT_WORLD_ARRIVED place=coffee interior=none/, 2500) < 0) { fail(`visit ${v}: never got back out`); break; }
    if (await wait(/^WT_WORLD_MODE mode=Free/, 15000, m) < 0) { fail(`visit ${v}: not free outside`); break; }
    if (v <= 2) await sample(results.stages, `visit ${v} out`);
    if (v % 5 === 0) await sample(results.boutique, `${v} visits`);
  }
  judge(results.boutique, 'boutique', 1);
  if (errors.length) fail(`${errors.length} console error(s): ${errors.slice(0, 2).join(' | ').slice(0, 300)}`);
  if (crashed) fail('the renderer crashed during the run');
  await ctx.close();
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
