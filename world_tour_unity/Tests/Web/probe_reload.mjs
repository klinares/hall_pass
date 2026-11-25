// 8.4 tester probe: reload mid-place resumes the same place (and with the travel card open, and mid-gig). Not part of the suites.
import path from 'node:path';
import { createRequire } from 'node:module'; import { fileURLToPath } from 'node:url';
import { startServer } from './server.mjs'; import { makePage, loadReady } from './world_places.mjs';
const here = path.dirname(fileURLToPath(import.meta.url)); const repo = path.resolve(here, '..', '..', '..');
const { chromium } = createRequire(path.join(repo, 'world_tour', 'tests', 'e2e', 'package.json'))('playwright');
const ORDER = ['coffee', 'records', 'rec', 'rooftop', 'club', 'theater', 'festival', 'arena'];
const sleep = ms => new Promise(r => setTimeout(r, ms));
const server = await startServer(path.join(repo, 'world'));
const browser = await chromium.launch({ headless: true, args: ['--use-gl=angle', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl'] });
const h = await makePage(browser, server, [1366, 768], 'reload', { failures: [] }, m => console.log('FAIL', m), process.env.OUT || '.', 'reload');
await loadReady(h, server, '?wt_test=1', 90000);
await h.patchSave(20000000, ORDER);
for (const place of ["festival", "rooftop"]) {
  let m0 = h.mark();
  await h.page.goto(server.url + '?wt_test=1&wt_goto=' + place, { waitUntil: 'load' });
  await h.waitLine(new RegExp('^WT_WORLD_(ARRIVED|GAME_READY) place=' + place), 90000, m0);
  await h.focus(); await sleep(2500);
  await h.page.keyboard.down('KeyD'); await sleep(900); await h.page.keyboard.up('KeyD'); await sleep(1500);
  m0 = h.mark();
  await h.page.goto(server.url, { waitUntil: 'load' });   // plain URL: what she gets when she re-opens the page
  await h.waitLine(/^WT_WORLD_GAME_READY/, 90000, m0); await sleep(16000);
  const r = { text: h.lines.slice(m0).map(l => l.text).filter(t => /^WT_WORLD_(RESUME|ARRIVED|GAME_READY)/.test(t)).map(t => t.slice(0, 90)).join(' => ') };
  console.log(place, '-> reload lands:', r ? r.text.slice(0, 80) : 'NOTHING');
  await h.shot('after_' + place);
}
await browser.close(); server.close();
