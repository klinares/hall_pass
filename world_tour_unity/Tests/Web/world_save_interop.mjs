// Phase 8.4: the save carries between world/ and tour/ in both directions, a reload keeps the place (reports where it lands), and storage blocked still loads.
//   node Tests/Web/world_save_interop.mjs --state <localStorage json from a world run>
import { createRequire } from 'node:module';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { startServer } from './server.mjs';
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..', '..');
const { chromium } = createRequire(path.join(repo, 'world_tour', 'tests', 'e2e', 'package.json'))('playwright');
const argv = process.argv.slice(2);
const state = fs.readFileSync(argv[argv.indexOf('--state') + 1], 'utf8');
const sleep = ms => new Promise(r => setTimeout(r, ms));
const world = await startServer(path.join(repo, 'world')), tour = await startServer(path.join(repo, 'tour'));
const browser = await chromium.launch({ headless: true, args: (process.env.WT_GPU === 'd3d11' ? ['--use-gl=angle', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl'] : ['--use-angle=swiftshader', '--enable-unsafe-swiftshader']) });
const out = {};
async function open(srv, q, init, blocked) {
  const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 } });
  if (blocked) await ctx.addInitScript(() => { const bad = () => { throw new DOMException('blocked', 'SecurityError'); }; for (const k of ['getItem', 'setItem', 'removeItem', 'clear']) Storage.prototype[k] = bad; indexedDB.open = bad; });
  else if (init) await ctx.addInitScript(s => { try { if (!localStorage.getItem('worldtour.v1')) localStorage.setItem('worldtour.v1', s); } catch (e) {} }, init);
  const page = await ctx.newPage(); const lines = []; const errs = [];
  page.on('console', m => { lines.push(m.text().trim()); if (m.type() === 'error') errs.push(m.text()); }); page.on('pageerror', e => errs.push(String(e)));
  await page.goto(srv.url + q, { waitUntil: 'load' });
  return { ctx, page, lines, errs };
}
const wait = async (h, re, ms = 60000) => { const end = Date.now() + ms; while (Date.now() < end) { const l = h.lines.find(x => re.test(x)); if (l) return l; await sleep(100); } return null; };
const save = h => h.page.evaluate(() => { try { return JSON.parse(localStorage.getItem('worldtour.v1')); } catch (e) { return null; } });
const j0 = JSON.parse(state);
// world -> tour
let h = await open(tour, '', state);
out.tourReady = await wait(h, /^WT_SAVE_READY/); await sleep(4000);
const s1 = await save(h);
out.tour_fans = s1 && s1.fans; out.tour_worldKeyKept = !!(s1 && s1.world); out.tour_errors = h.errs.length; await h.ctx.close();
// tour -> world: the save the 2D game just wrote (seeded then re-saved by tour/) goes into world/
h = await open(world, '?wt_test=1', JSON.stringify(s1));
out.worldReady = await wait(h, /^WT_WORLD_GAME_READY/); out.world_errors = h.errs.length; await h.ctx.close();
// blocked storage in world
h = await open(world, '?wt_test=1', null, true);
out.blockedReady = await wait(h, /^WT_WORLD_GAME_READY/); out.blockedSave = await wait(h, /^WT_SAVE_READY/, 3000); out.blocked_errors = h.errs.slice(0, 3);
await h.page.evaluate(() => { const c = document.getElementById('unity-canvas'); if (c) c.focus(); }); await sleep(1500);
const p0 = h.lines.filter(l => l.startsWith('WT_WORLD_POS')).pop(); await h.page.keyboard.down('KeyD'); await sleep(1500); await h.page.keyboard.up('KeyD'); await sleep(1500);
out.blocked_moved = [p0, h.lines.filter(l => l.startsWith('WT_WORLD_POS')).pop()]; await h.ctx.close();
out.fansInSeed = j0.fans;
console.log(JSON.stringify(out, null, 1));
await browser.close(); await world.close(); await tour.close();
