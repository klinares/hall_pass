// Phase 9.2: a save made before the 2D game was removed loads in world/ unchanged.
//   node Tests/Web/world_old_save.mjs [--state <localStorage json>] [--gpu d3d11]
// The default save is fixtures/old_save_v1.json: the real "worldtour.v1" blob written by the build that shipped before 9.2 (a tour of
// coffee and records with a gig and a show played, every place unlocked, 2,023,600 fans, the wardrobe tried on). It is put into
// localStorage before the page loads. Checks: the game reaches WT_WORLD_GAME_READY with the same fan count, resumes in the saved place,
// the save keeps its version and key, and after the game has re-saved, every earned thing (fans, venues, skills, words,
// review queue, shows, inventory, journal, world) is identical to the fixture. A second run with storage blocked still starts and moves.
import { createRequire } from 'node:module';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { startServer } from './server.mjs';
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..', '..');
const { chromium } = createRequire(path.join(here, 'package.json'))('playwright');
const argv = process.argv.slice(2);
const opt = (n, d) => { const i = argv.indexOf(n); return i >= 0 ? argv[i + 1] : d; };
const stateFile = opt('--state', path.join(here, 'fixtures', 'old_save_v1.json'));
const state = fs.readFileSync(stateFile, 'utf8');
const sleep = ms => new Promise(r => setTimeout(r, ms));
const world = await startServer(path.join(repo, 'world'));
const gpu = (opt('--gpu', process.env.WT_GPU || 'swiftshader') === 'd3d11');
const browser = await chromium.launch({ headless: true, args: (gpu ? ['--use-gl=angle', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl'] : ['--use-angle=swiftshader', '--enable-unsafe-swiftshader']) });
const fails = [];
const check = (ok, msg) => { if (!ok) fails.push(msg); console.log((ok ? '  ok   ' : '  FAIL ') + msg); };
async function open(init, blocked) {
  const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 } });
  if (blocked) await ctx.addInitScript(() => { const bad = () => { throw new DOMException('blocked', 'SecurityError'); }; for (const k of ['getItem', 'setItem', 'removeItem', 'clear']) Storage.prototype[k] = bad; indexedDB.open = bad; });
  else if (init) await ctx.addInitScript(s => { try { if (!localStorage.getItem('worldtour.v1')) localStorage.setItem('worldtour.v1', s); } catch (e) {} }, init);
  const page = await ctx.newPage(); const lines = []; const errs = [];
  page.on('console', m => { lines.push(m.text().trim()); if (m.type() === 'error') errs.push(m.text()); });
  page.on('pageerror', e => errs.push(String(e)));
  await page.goto(world.url + '?wt_test=1', { waitUntil: 'load' });
  return { ctx, page, lines, errs };
}
const wait = async (h, re, ms = 60000) => { const end = Date.now() + ms; while (Date.now() < end) { const l = h.lines.find(x => re.test(x)); if (l) return l; await sleep(100); } return null; };
const save = h => h.page.evaluate(() => { try { return JSON.parse(localStorage.getItem('worldtour.v1')); } catch (e) { return null; } });
const seed = JSON.parse(state);

// the old save loads
let h = await open(state);
const ready = await wait(h, /^WT_WORLD_GAME_READY/);
check(!!ready, 'world/ reaches WT_WORLD_GAME_READY with the old save: ' + ready);
const fans = ready ? +/fans=(\d+)/.exec(ready)[1] : -1;
check(fans === seed.fans.lifetime, `the game shows ${fans} fans, the save had ${seed.fans.lifetime}`);
const resume = await wait(h, /^WT_WORLD_RESUME/, 5000), arrived = await wait(h, new RegExp('^WT_WORLD_ARRIVED place=' + seed.world.place), 30000);
check(!!resume && resume.includes('place=' + seed.world.place) && !!arrived, 'the game resumes in the saved place ' + seed.world.place + ': ' + resume + ' / ' + arrived);
const sr = await wait(h, /^WT_SAVE_READY/, 5000);
check(!!sr && new RegExp('v=' + seed.v + ' ').test(sr), 'the save loads as version ' + seed.v + ': ' + sr);
await sleep(4000);
const after = await save(h);
check(!!after && after.v === seed.v, 'the save is still version ' + seed.v + ' under worldtour.v1');
for (const k of ['fans', 'venues', 'skills', 'words', 'review', 'shows', 'inventory', 'journal', 'streak'])
  check(after && JSON.stringify(after[k]) === JSON.stringify(seed[k]), `${k} is unchanged after the game re-saved`);
check(after && after.world && after.world.place === seed.world.place && (after.world.metCrew || []).join() === (seed.world.metCrew || []).join(), 'world place and met crew are unchanged');
check(h.errs.length === 0, 'no console errors: ' + h.errs.slice(0, 2).join(' | '));
await h.ctx.close();

// storage blocked still plays
h = await open(null, true);
check(!!(await wait(h, /^WT_WORLD_GAME_READY/)), 'with storage blocked the game still starts');
await h.page.evaluate(() => { const c = document.getElementById('unity-canvas'); if (c) c.focus(); }); await sleep(1500);
const p0 = h.lines.filter(l => l.startsWith('WT_WORLD_POS')).pop(); await h.page.keyboard.down('KeyD'); await sleep(1500); await h.page.keyboard.up('KeyD'); await sleep(1500);
const p1 = h.lines.filter(l => l.startsWith('WT_WORLD_POS')).pop();
check(!!p1 && p1 !== p0, 'with storage blocked she still moves: ' + p0 + ' -> ' + p1);
await h.ctx.close();
await browser.close(); await world.close();
console.log(fails.length ? 'FAILED: ' + fails.length : 'PASSED');
process.exit(fails.length ? 1 : 0);
