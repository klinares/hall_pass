// 8.4 tester probe: the "Riff in the way" step of world_stability --abuse, with everything logged. Not part of the suites.
//   node Tests/Web/probe_riff.mjs <place> <crewN>
import path from 'node:path';
import { createRequire } from 'node:module'; import { fileURLToPath } from 'node:url';
import { startServer } from './server.mjs'; import { makePage, loadReady } from './world_places.mjs';
const here = path.dirname(fileURLToPath(import.meta.url)); const repo = path.resolve(here, '..', '..', '..');
const { chromium } = createRequire(path.join(repo, 'world_tour', 'tests', 'e2e', 'package.json'))('playwright');
const [place, goal = 'crew3'] = process.argv.slice(2);
const [vw, vh] = [1366, 768];
const ORDER = ['coffee', 'records', 'rec', 'rooftop', 'club', 'theater', 'festival', 'arena'];
const sleep = ms => new Promise(r => setTimeout(r, ms));
const server = await startServer(path.join(repo, 'world'));
const browser = await chromium.launch({ headless: true, args: ['--use-gl=angle', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl'] });
const h = await makePage(browser, server, [vw, vh], 'probe', { failures: [] }, m => console.log('FAIL', m), process.env.OUT || '.', 'riff_' + place);
await loadReady(h, server, '?wt_test=1', 90000);
await h.patchSave(20000000, ORDER);
const m0 = h.mark();
await h.page.goto(server.url + '?wt_test=1&wt_goto=' + place, { waitUntil: 'load' });
await h.waitLine(new RegExp('^WT_WORLD_(ARRIVED|GAME_READY) place=' + place), 90000, m0);
await h.focus(); await sleep(3000);
const last = re => { for (let i = h.lines.length - 1; i >= 0; i--) if (re.test(h.lines[i].text)) return h.lines[i].text; return ''; };
const star = () => { const m = /star=(-?[\d.]+):(-?[\d.]+)/.exec(last(/^WT_WORLD_TARGETS/)); return m ? { x: +m[1], z: +m[2] } : null; };
const down = new Set(); const hold = async (c, on) => { if (on && !down.has(c)) { down.add(c); await h.page.keyboard.down(c); } else if (!on && down.has(c)) { down.delete(c); await h.page.keyboard.up(c); } };
async function walk() {
  const end = Date.now() + 30000;
  while (Date.now() < end) {
    const sp = star(); const r = new RegExp('(?:^| )' + goal + '=(ok|short);([^ ]*)').exec(last(/^WT_WORLD_ROUTE/));
    if (!sp || !r) { await sleep(150); continue; }
    const pts = r[2].split(',').filter(Boolean).map(s => { const [x, z] = s.split(':').map(Number); return { x, z }; });
    const g = pts[pts.length - 1]; if (!g) break;
    if (Math.hypot(g.x - sp.x, g.z - sp.z) < 0.7) break;
    let wp = g; for (const p of pts) if (Math.hypot(p.x - sp.x, p.z - sp.z) > 0.45) { wp = p; break; }
    await hold('KeyD', wp.x - sp.x > 0.3); await hold('KeyA', wp.x - sp.x < -0.3); await hold('KeyW', wp.z - sp.z > 0.3); await hold('KeyS', wp.z - sp.z < -0.3);
    await sleep(100);
  }
  for (const c of [...down]) await hold(c, false); await sleep(500);
}
if (process.env.EDGES) {
  const dirs = [['KeyW'], ['KeyS'], ['KeyA'], ['KeyD'], ['KeyW', 'KeyA'], ['KeyW', 'KeyD'], ['KeyS', 'KeyA'], ['KeyS', 'KeyD']];
  for (const d of dirs) {
    for (const k of d) await hold(k, true); await sleep(4500); for (const k of d) await hold(k, false);
    const md = () => /mode=(\w+)/.exec(last(/^WT_WORLD_TARGETS/))?.[1];
    for (let n = 0; n < 4 && md() !== 'Free'; n++) { await h.page.keyboard.press('Escape', { delay: 80 }); await sleep(600); }
    console.log('edge', d.join('+'), JSON.stringify(star()), md());
  }
}
if (process.env.EDGES) { await h.page.mouse.move(683, 384); await sleep(250); await h.page.mouse.down(); await sleep(110); await h.page.mouse.up(); await sleep(2500); console.log("cleared to", JSON.stringify(star())); }
await walk();
console.log('1 at', JSON.stringify(star()), last(/^WT_WORLD_TARGETS/).slice(0, 260));
const rf = /riff=(-?\d+):(-?\d+)/.exec(last(/^WT_WORLD_TARGETS/));
if (rf) { await h.page.mouse.move(+rf[1], +rf[2]); await sleep(250); await h.page.mouse.down(); await sleep(110); await h.page.mouse.up(); await sleep(1500); }
console.log('2 after riff click at', JSON.stringify(star()), 'mode', /mode=(\w+)/.exec(last(/^WT_WORLD_TARGETS/))?.[1]);
await walk();
console.log('3 back at', JSON.stringify(star()), last(/^WT_WORLD_ROUTE/).slice(0, 300), '|', last(/^WT_WORLD_TARGETS/).slice(0, 260));
await h.shot('before');
for (let n = 0; n < 4; n++) {
  const mk = h.mark(); await h.page.keyboard.press('KeyE', { delay: 80 }); await sleep(900);
  console.log('E ->', h.lines.slice(mk).map(l => l.text.replace(/\s+/g, ' ')).filter(t => /^WT_(WORLD_TALK|WORLD_MODE|WARDROBE|GIG)/.test(t)).join(' || ') || '(nothing)');
  if (n === 0) await h.shot('E0');
}

if (process.env.CLICK) {
  for (const [cx, cy] of [[683, 400], [683, 500], [400, 450], [900, 450]]) {
    const a = star(); await h.page.mouse.move(cx, cy); await sleep(250); await h.page.mouse.down(); await sleep(110); await h.page.mouse.up(); await sleep(2500);
    const b = star(); console.log('click', cx, cy, 'moved', Math.hypot(b.x - a.x, b.z - a.z).toFixed(2), JSON.stringify(a), '->', JSON.stringify(b));
  }
  await h.shot('afterclick');
}
await browser.close(); server.close();
