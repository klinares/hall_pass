// 8.4 tester probe: walk the logged route to a goal (crewN|stage|exit) by keyboard, press E and print what the game says. Not part of the suites.
//   node Tests/Web/probe_crew.mjs <place> <goal> [fans] [size]
import path from 'node:path'; import fs from 'node:fs';
import { createRequire } from 'node:module'; import { fileURLToPath } from 'node:url';
import { startServer } from './server.mjs'; import { makePage, loadReady } from './world_places.mjs';
const here = path.dirname(fileURLToPath(import.meta.url)); const repo = path.resolve(here, '..', '..', '..');
const { chromium } = createRequire(path.join(repo, 'world_tour', 'tests', 'e2e', 'package.json'))('playwright');
const [place, goal = 'crew3', fans = '20000000', size = '1366x768'] = process.argv.slice(2);
const [vw, vh] = size.split('x').map(Number);
const ORDER = ['coffee', 'records', 'rec', 'rooftop', 'club', 'theater', 'festival', 'arena'];
const sleep = ms => new Promise(r => setTimeout(r, ms));
const results = { failures: [] };
const server = await startServer(path.join(repo, 'world'));
const browser = await chromium.launch({ headless: true, args: ['--use-gl=angle', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl'] });
const out = process.env.OUT || '.';
const h = await makePage(browser, server, [vw, vh], 'probe', results, m => console.log('FAIL', m), out, 'probe_' + place);
await loadReady(h, server, '?wt_test=1', 90000);
await h.patchSave(+fans, ORDER);
const m0 = h.mark();
await h.page.goto(server.url + '?wt_test=1&wt_goto=' + place, { waitUntil: 'load' });
await h.waitLine(new RegExp('^WT_WORLD_(ARRIVED|GAME_READY) place=' + place), 90000, m0);
await h.focus(); await sleep(3000);
const last = re => { for (let i = h.lines.length - 1; i >= 0; i--) if (re.test(h.lines[i].text)) return h.lines[i].text; return ''; };
const star = () => { const m = /star=(-?[\d.]+):(-?[\d.]+)/.exec(last(/^WT_WORLD_TARGETS/)); return m ? { x: +m[1], z: +m[2] } : null; };
const down = new Set(); const hold = async (c, on) => { if (on && !down.has(c)) { down.add(c); await h.page.keyboard.down(c); } else if (!on && down.has(c)) { down.delete(c); await h.page.keyboard.up(c); } };
const end = Date.now() + 40000;
while (Date.now() < end) {
  const sp = star(); const r = new RegExp('(?:^| )' + goal + '=(ok|short);([^ ]*)').exec(last(/^WT_WORLD_ROUTE/));
  if (!sp || !r) { await sleep(150); continue; }
  const pts = r[2].split(',').filter(Boolean).map(s => { const [x, z] = s.split(':').map(Number); return { x, z }; });
  if (!pts.length) break;
  const g = pts[pts.length - 1];
  if (process.env.TRACE) console.log("trace", JSON.stringify(sp), JSON.stringify(g), [...down].join());
  if (Math.hypot(g.x - sp.x, g.z - sp.z) < 0.7) break;
  let wp = g; for (const p of pts) if (Math.hypot(p.x - sp.x, p.z - sp.z) > 0.45) { wp = p; break; }
  const dx = wp.x - sp.x, dz = wp.z - sp.z;
  await hold('KeyD', dx > 0.3); await hold('KeyA', dx < -0.3); await hold('KeyW', dz > 0.3); await hold('KeyS', dz < -0.3);
  await sleep(100);
}
for (const c of [...down]) await hold(c, false);
await sleep(600);
console.log('route line:', last(/^WT_WORLD_ROUTE/).slice(0, 400));
console.log('at', JSON.stringify(star()), 'targets:', last(/^WT_WORLD_TARGETS/).slice(0, 300));
for (let n = 0; n < 3; n++) {
  const mk = h.mark(); await h.page.keyboard.press('KeyE', { delay: 80 }); await sleep(900);
  console.log('E ->', h.lines.slice(mk).map(l => l.text).filter(t => /^WT_(WORLD_TALK|WORLD_MODE|WARDROBE|GIG)/.test(t)).join(' || ') || '(nothing)');
  await h.shot('E' + n);
  await h.page.keyboard.press('Escape'); await sleep(700);
}

// extra: crew positions over time (are they wandering?) and a push toward the crew
const cr = []; for (let i = 0; i < 6; i++) { cr.push(/crew[123]=[^ ]*/.exec(last(/^WT_WORLD_ROUTE/))?.[0] + ' ' + (new RegExp(goal + '=(?:ok|short);([^ ]*)').exec(last(/^WT_WORLD_ROUTE/))?.[1])); await sleep(2000); }
console.log('goal over time', cr.join(' | '));
await browser.close(); server.close();
