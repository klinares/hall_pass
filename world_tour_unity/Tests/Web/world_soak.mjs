// Phase 10B perf: a long walk through all eight places in ONE session (the advisor's acceptance for the load work), and the per-place frame numbers.
//
//   node Tests/Web/world_soak.mjs [--site <folder>] [--gpu d3d11|swiftshader] [--minutes 30] [--dwell 45] [--places coffee,records,...] [--size 1366x768]
//                                 [--limit-ms 100] [--dpr 1] [--label soak] [--out <results.json>] [--shots <dir>]
//
// Loads the game on a rich save (?wt_test&wt_fans=2000000), then travels place to place with the ?wt_test hook WorldGame.TestTravel (the same travel the tour map does), in the
// tour order and round again, for --minutes (a pass of one visit each place with --minutes 0). In each place she walks about with the keys for --dwell seconds (a new direction
// every 1.2 s, a few clicks). A page script records every animation frame; the game's WT_WORLD_MODE mode=Free line marks "she can move". Per visit it keeps
//   travel      the worst frame between the go and Free (the fade and the load; not counted against the limit)
//   arrival     the worst frame in the first 1.5 s after Free (the "arrival frame")
//   steady      the worst frame after that, and the fps (mean, and the slowest 1 percent)
// Pass: no crash, no console error, no frame over --limit-ms (default 100) from Free on, in any visit. Prints a per-place table; the results go to the --out file.
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
const ORDER = ['coffee', 'records', 'rec', 'rooftop', 'club', 'theater', 'festival', 'arena'];
const site = path.resolve(opt('--site', path.join(repo, 'world')));
const gpuMode = opt('--gpu', 'd3d11');
const minutes = +opt('--minutes', '30'), dwell = +opt('--dwell', '45');
const places = opt('--places', ORDER.join(',')).split(',').filter(Boolean);
const [vw, vh] = opt('--size', '1366x768').split('x').map(Number);
const limit = +opt('--limit-ms', '100');
const dpr = +opt('--dpr', '1');   // device pixel ratio of the emulated screen (2 draws 4 times the pixels at the same window size)
const label = opt('--label', 'soak');
const outFile = path.resolve(opt('--out', path.join(project, 'Temp', `world-${label}.json`)));
const shotsDir = path.resolve(opt('--shots', path.join(project, 'Temp', 'soak-shots')));
fs.mkdirSync(shotsDir, { recursive: true });
fs.mkdirSync(path.dirname(outFile), { recursive: true });
const sleep = ms => new Promise(r => setTimeout(r, ms));
const res = { site, gpuMode, label, dpr, size: `${vw}x${vh}`, startedAt: new Date().toISOString(), visits: [], failures: [], errors: [], crashed: false };
const fail = m => { res.failures.push(m); console.log('FAIL', m); };

const RECORDER = `(() => {
  window.__ft = [];
  let last = performance.now();
  const f = t => { window.__ft.push([t, t - last]); last = t; requestAnimationFrame(f); };
  requestAnimationFrame(f);
  window.__marks = [];
  const log = console.log.bind(console);
  console.log = (...a) => { try { const t = String(a[0]); if (/^WT_WORLD_(MODE|ARRIVED|TRAVEL)|^WT_HITCH/.test(t)) window.__marks.push([performance.now(), t.slice(0, 90)]); } catch (e) {} log(...a); };
})();`;

const launch = gpuMode === 'd3d11'
  ? { headless: true, args: ['--use-gl=angle', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl', '--enable-precise-memory-info'] }
  : { headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--enable-precise-memory-info'] };

let server = null, browser = null;
const pct = (arr, p) => { if (!arr.length) return 0; const s = [...arr].sort((a, b) => a - b); return s[Math.min(s.length - 1, Math.floor(s.length * p))]; };
try {
  server = await startServer(site);
  browser = await chromium.launch(launch);
  const ctx = await browser.newContext({ viewport: { width: vw, height: vh }, deviceScaleFactor: dpr });
  await ctx.addInitScript(RECORDER);
  const page = await ctx.newPage();
  const lines = [];
  page.on('console', m => { const t = m.text().trim(); lines.push(t); if (m.type() === 'error') res.errors.push(t); });
  page.on('pageerror', e => res.errors.push('pageerror: ' + e.message));
  page.on('crash', () => { res.crashed = true; fail('the page crashed (Target crashed)'); });
  page.on('close', () => { if (!res.done) { res.crashed = true; fail('the page closed'); } });
  const wait = async (re, ms, from = 0) => { const end = Date.now() + ms; for (;;) { for (let i = from; i < lines.length; i++) if (re.test(lines[i])) return i; if (Date.now() > end || res.crashed) return -1; await sleep(40); } };

  await page.goto(server.url + '?wt_test=1&wt_fans=2000000', { waitUntil: 'load' });
  if (await wait(/^WT_WORLD_GAME_READY/, 120000) < 0) throw new Error('no WT_WORLD_GAME_READY');
  await page.evaluate(() => { const c = document.getElementById('unity-canvas'); if (c) c.focus(); });
  await sleep(2500);

  const t0 = Date.now();
  let cur = 'coffee', visit = 0;
  const walk = async seconds => {
    const keys = ['KeyW', 'KeyA', 'KeyS', 'KeyD'];
    const end = Date.now() + seconds * 1000;
    let held = [];
    try {
      while (Date.now() < end && !res.crashed) {
        for (const k of held) await page.keyboard.up(k);
        const a = keys[Math.floor(Math.random() * 4)], b = Math.random() < 0.4 ? keys[Math.floor(Math.random() * 4)] : null;
        held = b && b !== a ? [a, b] : [a];
        for (const k of held) await page.keyboard.down(k);
        if (Math.random() < 0.15) await page.mouse.click(vw * (0.25 + Math.random() * 0.5), vh * (0.35 + Math.random() * 0.4));
        await sleep(1200);
      }
    } finally { for (const k of held) await page.keyboard.up(k).catch(() => {}); }
  };

  for (;;) {
    for (const id of places) {
      if (res.crashed) break;
      if (minutes > 0 && Date.now() - t0 > minutes * 60000) break;
      let v;
      if (id === cur && visit === 0) {
        v = { place: id, visit: visit++, first: true };
        const mFree = await wait(/^WT_WORLD_MODE mode=Free/, 20000, 0);
        v.freeAt = await page.evaluate(() => { const m = window.__marks.find(x => /mode=Free/.test(x[1])); return m ? m[0] : performance.now(); });
        v.goAt = v.freeAt - 3000;
      } else {
        const from = lines.length;
        v = { place: id, visit: visit++ };
        v.goAt = await page.evaluate(() => performance.now());
        await page.evaluate(p => window.__wtInstance.SendMessage('WorldGame', 'TestTravel', p), id);
        const arr = await wait(new RegExp('^WT_WORLD_ARRIVED place=' + id), 60000, from);
        if (arr < 0) { fail(`never arrived at ${id}`); break; }
        await wait(/^WT_WORLD_MODE mode=Free/, 20000, arr);
        v.freeAt = await page.evaluate(() => { const ms = window.__marks.filter(x => /mode=Free/.test(x[1])); return ms.length ? ms[ms.length - 1][0] : performance.now(); });
      }
      cur = id;
      await walk(dwell);
      const data = await page.evaluate(([a, b]) => {
        const ft = window.__ft.filter(x => x[0] >= a && x[0] <= b);
        const marks = window.__marks.filter(x => x[0] >= a && x[0] <= b && /WT_HITCH/.test(x[1])).map(x => x[1]);
        const mem = performance.memory ? Math.round(performance.memory.usedJSHeapSize / 1048576) : 0;
        return { ft: ft.map(x => [Math.round(x[0]), +x[1].toFixed(1)]), hitches: marks, jsMB: mem };
      }, [v.goAt, performance_now_end()]);
      const rel = data.ft;
      const travel = rel.filter(x => x[0] < v.freeAt).map(x => x[1]);
      const arrival = rel.filter(x => x[0] >= v.freeAt && x[0] < v.freeAt + 1500).map(x => x[1]);
      const steady = rel.filter(x => x[0] >= v.freeAt + 1500).map(x => x[1]);
      const all = arrival.concat(steady);
      v.travelWorst = Math.round(Math.max(0, ...travel));
      v.arrivalWorst = Math.round(Math.max(0, ...arrival));
      v.steadyWorst = Math.round(Math.max(0, ...steady));
      v.fps = +(1000 / (all.reduce((s, x) => s + x, 0) / Math.max(1, all.length))).toFixed(1);
      v.p99 = Math.round(pct(all, 0.99));
      v.over = all.filter(x => x > limit).length;
      v.hitches = data.hitches.slice(0, 6);
      v.jsMB = data.jsMB;
      v.atMin = +((Date.now() - t0) / 60000).toFixed(1);
      res.visits.push(v);
      console.log(`  [${String(v.atMin).padStart(5)} min] ${id.padEnd(9)} travel ${String(v.travelWorst).padStart(3)} ms  arrival ${String(v.arrivalWorst).padStart(3)} ms  steady ${String(v.steadyWorst).padStart(3)} ms  fps ${v.fps}  p99 ${v.p99} ms  over${limit} ${v.over}  js ${v.jsMB} MB`);
      if (v.over) fail(`${id} (visit ${v.visit}): ${v.over} frame(s) over ${limit} ms after she could move (arrival ${v.arrivalWorst}, steady ${v.steadyWorst}) ${v.hitches.join(' | ')}`);
      if (res.visits.length % 8 === 0) { try { await page.screenshot({ path: path.join(shotsDir, `${label}_${id}_${v.atMin}min.png`) }); } catch (e) {} }
    }
    if (res.crashed || minutes <= 0 || Date.now() - t0 > minutes * 60000) break;
  }
  res.durationMin = +((Date.now() - t0) / 60000).toFixed(1);
  res.done = true;
  if (res.errors.length) fail(`${res.errors.length} console error(s): ${res.errors.slice(0, 3).join(' | ').slice(0, 300)}`);
} catch (e) {
  fail('runner: ' + (e && e.stack || e));
} finally {
  res.done = true;
  // per-place worst across visits
  const by = {};
  for (const v of res.visits) { const b = by[v.place] = by[v.place] || { visits: 0, arrivalWorst: 0, steadyWorst: 0, travelWorst: 0, fps: [], over: 0 }; b.visits++; b.arrivalWorst = Math.max(b.arrivalWorst, v.arrivalWorst); b.steadyWorst = Math.max(b.steadyWorst, v.steadyWorst); b.travelWorst = Math.max(b.travelWorst, v.travelWorst); b.fps.push(v.fps); b.over += v.over; }
  res.byPlace = {};
  for (const [k, b] of Object.entries(by)) res.byPlace[k] = { visits: b.visits, travelWorst: b.travelWorst, arrivalWorst: b.arrivalWorst, steadyWorst: b.steadyWorst, fpsMean: +(b.fps.reduce((s, x) => s + x, 0) / b.fps.length).toFixed(1), over: b.over };
  console.log('\nper place (worst over all visits):');
  for (const [k, b] of Object.entries(res.byPlace)) console.log(`  ${k.padEnd(9)} visits ${b.visits}  travel ${b.travelWorst} ms  arrival ${b.arrivalWorst} ms  steady ${b.steadyWorst} ms  fps ${b.fpsMean}  frames over ${limit}: ${b.over}`);
  await browser?.close().catch(() => {}); await server?.close().catch(() => {});
  fs.writeFileSync(outFile, JSON.stringify(res, null, 2));
  console.log(res.failures.length ? `FAILED (${res.failures.length}); results in ${outFile}` : `PASSED (${res.durationMin} min, ${res.visits.length} visits); results in ${outFile}`);
  process.exitCode = res.failures.length ? 1 : 0;
}
function performance_now_end() { return 1e12; }
