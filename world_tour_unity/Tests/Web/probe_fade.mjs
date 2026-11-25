// 8.4 fade probe: in a place (default theater) walk the logged route to the stage and report every run of WT_OCC_FADE lines (the occluder fade logs one per frame it moves):
// how many frames each fade took, over how many ms, and the biggest alpha step. A fade that jumped would be one frame.
//   node Tests/Web/probe_fade.mjs [place] [size] [seconds]     (OUT=<dir> for the shot)
import path from 'node:path';
import { createRequire } from 'node:module'; import { fileURLToPath } from 'node:url';
import { startServer } from './server.mjs'; import { makePage, loadReady } from './world_places.mjs';
const here = path.dirname(fileURLToPath(import.meta.url)); const repo = path.resolve(here, '..', '..', '..');
const { chromium } = createRequire(path.join(repo, 'world_tour', 'tests', 'e2e', 'package.json'))('playwright');
const [place = 'theater', size = '1366x768', secs = '45'] = process.argv.slice(2);
const [vw, vh] = size.split('x').map(Number);
const ORDER = ['coffee', 'records', 'rec', 'rooftop', 'club', 'theater', 'festival', 'arena'];
const sleep = ms => new Promise(r => setTimeout(r, ms));
const results = { failures: [] };
const server = await startServer(path.join(repo, 'world'));
const browser = await chromium.launch({ headless: true, args: ['--use-gl=angle', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl'] });
const h = await makePage(browser, server, [vw, vh], 'probe', results, m => console.log('FAIL', m), process.env.OUT || '.', 'fade_' + place);
await loadReady(h, server, '?wt_test=1', 90000);
await h.patchSave(20000000, ORDER);
const m0 = h.mark();
await h.page.goto(server.url + '?wt_test=1&wt_goto=' + place, { waitUntil: 'load' });
await h.waitLine(new RegExp('^WT_WORLD_(ARRIVED|GAME_READY) place=' + place), 90000, m0);
await h.focus(); await sleep(3000);
const last = re => { for (let i = h.lines.length - 1; i >= 0; i--) if (re.test(h.lines[i].text)) return h.lines[i].text; return ''; };
const star = () => { const m = /star=(-?[\d.]+):(-?[\d.]+)/.exec(last(/^WT_WORLD_TARGETS/)); return m ? { x: +m[1], z: +m[2] } : null; };
const down = new Set(); const hold = async (c, on) => { if (on && !down.has(c)) { down.add(c); await h.page.keyboard.down(c); } else if (!on && down.has(c)) { down.delete(c); await h.page.keyboard.up(c); } };
const goals = ['stage', 'crew1', 'crew2', 'crew3', 'exit', 'stage'];
const end = Date.now() + secs * 1000; let gi = 0, gStart = Date.now();
while (Date.now() < end) {
  const goal = goals[gi % goals.length];
  const sp = star(); const r = new RegExp('(?:^| )' + goal + '=(ok|short);([^ ]*)').exec(last(/^WT_WORLD_ROUTE/));
  if (!sp || !r) { await sleep(150); continue; }
  const pts = r[2].split(',').filter(Boolean).map(s => { const [x, z] = s.split(':').map(Number); return { x, z }; });
  if (!pts.length) { gi++; continue; }
  const g = pts[pts.length - 1];
  if (Math.hypot(g.x - sp.x, g.z - sp.z) < 0.7 || Date.now() - gStart > 12000) { gi++; gStart = Date.now(); for (const c of [...down]) await hold(c, false); await h.page.keyboard.press('Escape'); continue; }
  let wp = g; for (const p of pts) if (Math.hypot(p.x - sp.x, p.z - sp.z) > 0.45) { wp = p; break; }
  const dx = wp.x - sp.x, dz = wp.z - sp.z;
  await hold('KeyD', dx > 0.3); await hold('KeyA', dx < -0.3); await hold('KeyW', dz > 0.3); await hold('KeyS', dz < -0.3);
  await sleep(100);
}
for (const c of [...down]) await hold(c, false);
await sleep(800);
// group consecutive fade lines (frames within 3 of each other) into runs
const fl = h.lines.slice(m0).map(l => /^WT_OCC_FADE frame=(\d+) t=([\d.]+) busy=(\d+) min=([\d.]+) max=([\d.]+)/.exec(l.text)).filter(Boolean).map(m => ({ f: +m[1], t: +m[2], busy: +m[3], min: +m[4], max: +m[5] }));
const runs = []; let cur = null;
for (const x of fl) { if (cur && x.f - cur.at(-1).f <= 3) cur.push(x); else { cur = [x]; runs.push(cur); } }
console.log('fade lines', fl.length, 'runs', runs.length);
let smooth = 0;
for (const r of runs) {
  const frames = r.length, ms = Math.round((r.at(-1).t - r[0].t) * 1000);
  let big = 0; for (let i = 1; i < r.length; i++) big = Math.max(big, Math.abs(r[i].min - r[i - 1].min), Math.abs(r[i].max - r[i - 1].max));
  if (frames >= 6) smooth++;
  console.log(`run frames=${frames} span=${ms}ms alpha ${r[0].min.toFixed(2)}..${r.at(-1).min.toFixed(2)} biggest step ${big.toFixed(2)} busy=${Math.max(...r.map(z => z.busy))}`);
}
console.log('runs of 6+ frames:', smooth, 'of', runs.length);
await h.shot('end');
await browser.close(); server.close();
