// Browser smoke test for the 3D world build in world/ (phase 8.0 spike). Plain Node plus the Playwright install
// from Tests/Web/node_modules (npm ci in Tests/Web).
//
//   node Tests/Web/world_smoke.mjs [--site <folder>] [--gpu auto|swiftshader|d3d11|chrome] [--sizes 1366x768,1920x1080]
//                                  [--out <results.json>] [--shots <dir>] [--seconds 5]
//
// For each size: serves world/ with the plain static server, loads it in headless Chrome, waits for WT_WORLD_READY
// (log line from the game), checks that keyboard movement and click-to-move both change the character's position,
// measures frames per second over --seconds with the character moving (Unity's own per-second frame count; the rAF count is
// recorded too but reads 2x because Unity registers two callbacks per frame), saves a screenshot, and records the WebGL renderer string Chrome used.
// Fails on console errors, no ready line, no movement, or fps under 30 (the advisor's headless floor).
//   --places records,rec,...  which places the tour visits (default records,rec,rooftop,club); --no-places / --places-only / --no-travel / --no-tour
// --gpu: swiftshader = software GL (what run.mjs uses); d3d11 = ANGLE on the machine's GPU in the headless shell;
//        chrome = installed Chrome (new headless) on its default GPU path; auto = swiftshader.
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { startServer } from './server.mjs';
import { placeTests } from './world_places.mjs';
import { wardrobeTests } from './world_wardrobe.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const project = path.resolve(here, '..', '..');
const repo = path.resolve(project, '..');
const { chromium, firefox } = createRequire(path.join(here, 'package.json'))('playwright');

const argv = process.argv.slice(2);
const opt = (n, d) => { const i = argv.indexOf(n); return i >= 0 && i + 1 < argv.length ? argv[i + 1] : d; };
const site = path.resolve(opt('--site', path.join(repo, 'world')));
const gpuMode = opt('--gpu', 'auto');
const browserName = opt('--browser', 'chromium'); // chromium | firefox
const sizes = opt('--sizes', '1366x768,1920x1080').split(',').map(s => s.split('x').map(Number));
const outFile = path.resolve(opt('--out', path.join(project, 'Temp', 'world-results.json')));
const shotsDir = path.resolve(opt('--shots', path.join(project, 'Temp', 'world-shots')));
const seconds = Number(opt('--seconds', '5'));
const READY_TIMEOUT_MS = 90000, MIN_FPS = 30;

const results = { site, gpuMode, startedAt: new Date().toISOString(), sizes: {}, failures: [] };
const fail = (m) => { results.failures.push(m); console.log('FAIL', m); };

function launchOptions() {
  if (gpuMode === 'd3d11') return { headless: true, args: ['--use-gl=angle', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl'] };
  if (gpuMode === 'chrome') return { headless: true, channel: 'chrome', args: ['--ignore-gpu-blocklist', '--enable-gpu-rasterization'] };
  return { headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] };
}

const RAF_COUNTER = `(() => {
  window.__raf = 0;
  const orig = window.requestAnimationFrame.bind(window);
  window.requestAnimationFrame = cb => orig(t => { window.__raf++; cb(t); });
})();`;

const sleep = ms => new Promise(r => setTimeout(r, ms));

let browser = null, server = null;

// ---------------------------------------------------------------------------------------------------------------
// Phase 8.1 gameplay: walk to a crew member (click on them), talk, play the gig to the end (3 to 5 questions, answered
// from the answer the game logs in WT_MATH, typed on the keyboard as the 2D tests do), walk to the stage, start the show,
// play all ten songs to the ticket, go back to the world, then reload and check the fans persisted. The page is opened
// with ?wt_test, which makes the game log the screen position of each crew member, the stage and the exit.
// ---------------------------------------------------------------------------------------------------------------
async function gameplay() {
  const [vw, vh] = sizes[0];
  const g = results.gameplay = { size: `${vw}x${vh}`, steps: [], errors: [], numbers: {} };
  const step = m => { g.steps.push(m); console.log('  gameplay: ' + m); };
  const gfail = m => { g.steps.push('FAIL ' + m); fail('gameplay: ' + m); };
  const ctx = await browser.newContext({ viewport: { width: vw, height: vh } });
  const page = await ctx.newPage();
  const lines = [];
  page.on('console', m => {
    const text = m.text();
    if (m.type() === 'error') g.errors.push(text);
    lines.push({ t: Date.now(), text });
  });
  page.on('pageerror', e => g.errors.push('pageerror: ' + e.message));
  const mark = () => lines.length;
  const waitAny = async (res, ms, from = 0) => {
    const end = Date.now() + ms;
    for (;;) {
      for (let i = from; i < lines.length; i++) if (res.some(r => r.test(lines[i].text))) return { i, text: lines[i].text };
      if (Date.now() > end) return null;
      await sleep(40);
    }
  };
  const waitLine = async (re, ms, from = 0) => (await waitAny([re], ms, from));
  const lastTargets = () => {
    for (let i = lines.length - 1; i >= 0; i--) if (lines[i].text.startsWith('WT_WORLD_TARGETS')) return lines[i].text;
    return null;
  };
  const target = (name) => {
    const t = lastTargets();
    const m = t && new RegExp(`${name}=(-?\\d+):(-?\\d+)(?:;(\\w*))?`).exec(t);
    return m ? { x: +m[1], y: +m[2], mode: m[3] || '' } : null;
  };
  const shot = async name => { await page.screenshot({ path: path.join(shotsDir, `world_${name}_${vw}x${vh}.png`) }); };
  const storedFans = async () => page.evaluate(() => { try { const j = JSON.parse(localStorage.getItem('worldtour.v1')); return j ? j.fans.lifetime : null; } catch (e) { return null; } });

  // plays every question of a gig or show by typing the logged answer; returns the number of songs answered
  async function playSongs(doneRe, label, from) {
    let m = from, songs = 0;
    for (let guard = 0; guard < 40; guard++) {
      const hit = await waitAny([/^WT_MATH mode=/, doneRe], 25000, m);
      if (!hit) { gfail(`${label}: neither a question nor the end after ${songs} songs`); return songs; }
      if (doneRe.test(hit.text)) return songs;
      const raw = /answer=(\S+)/.exec(hit.text);
      if (!raw) { gfail(`${label}: no answer in "${hit.text}"`); return songs; }
      const ans = raw[1].replace(/[^0-9.\/\-]/g, '');   // keeps the slash: the show's default set is now snack table (fractions)
      await sleep(900);                                  // let the mode finish drawing and the keypad listen
      await page.keyboard.type(ans, { delay: 70 });
      await page.keyboard.press('Enter');
      const res = await waitLine(/^WT_SONG_RESULT/, 12000, hit.i + 1);
      if (!res) { gfail(`${label}: no result for "${hit.text}" typing ${ans}`); return songs; }
      if (!/kind=clean/.test(res.text)) gfail(`${label}: answer ${ans} was not clean: ${res.text}`);
      songs++;
      m = res.i + 1;
    }
    return songs;
  }

  await page.goto(server.url + '?wt_test=1', { waitUntil: 'load' });
  const ready = await waitLine(/^WT_WORLD_GAME_READY/, READY_TIMEOUT_MS);
  if (!ready) { gfail('no WT_WORLD_GAME_READY'); await ctx.close(); return; }
  step(ready.text);
  await page.evaluate(() => { const c = document.getElementById('unity-canvas'); if (c) c.focus(); });
  await sleep(1500);

  // ---- walk to the first crew member by clicking them, then talk
  const crewT = target('crew1');
  if (!crewT) { gfail('no crew1 position logged'); await ctx.close(); return; }
  g.crewMode = crewT.mode;
  if (crewT.mode !== 'tickets') gfail(`crew1 offers "${crewT.mode}", the test types ticket sales answers`);
  const speechEarly = waitLine ? await waitLine(/^WT_SPEECH_UNLOCKED/, 50) : null;
  if (speechEarly) gfail('read-aloud unlocked before any click or key: ' + speechEarly.text);
  await page.mouse.click(crewT.x, crewT.y);
  const speechUnlock = await waitLine(/^WT_SPEECH_UNLOCKED/, 5000);
  if (!speechUnlock) gfail('no WT_SPEECH_UNLOCKED after the first click (read-aloud stays locked)'); else step('read-aloud unlocked: ' + speechUnlock.text);
  const talk = await waitLine(/^WT_WORLD_TALK crew=Crew_1/, 25000);
  if (!talk) { gfail('clicking crew 1 never started a talk'); await shot('talk_missing'); await ctx.close(); return; }
  step('talk: ' + talk.text);
  await sleep(700);
  await shot('talk');

  // ---- accept the gig (Enter) and play it to the end
  let m0 = mark();
  await page.keyboard.press('Enter');
  const start = await waitLine(/^WT_GIG_START/, 8000, m0);
  if (!start) { gfail('Enter on the offer did not start a gig'); await shot('gig_missing'); await ctx.close(); return; }
  const gigN = +/n=(\d+)/.exec(start.text)[1];
  g.gigLength = gigN;
  if (gigN < 3 || gigN > 5) gfail(`gig length ${gigN} is outside 3 to 5`);
  await sleep(1800);
  await shot('gig');
  const songs = await playSongs(/^WT_GIG_DONE/, "gig", m0);
  g.gigSongs = songs;
  if (songs !== gigN) gfail(`the gig had ${gigN} songs but ${songs} were answered`);
  await sleep(700);
  await shot('gig_done');
  m0 = mark();
  await page.keyboard.press('Enter');                    // "keep exploring"
  if (!await waitLine(/^WT_WORLD_MODE mode=Free/, 8000, m0)) gfail('the world did not come back after the gig card');
  const fansGig = await storedFans();
  g.numbers.fansAfterGig = fansGig;
  if (!(fansGig > 0)) gfail(`no fans saved after the gig (${fansGig})`);
  // 3 clean songs earn 1000 + 2000 + 3000; the saved count must have moved by the gig's own total
  step(`gig of ${gigN}: fans saved ${fansGig}`);

  // ---- the stage: walk onto it, play the show
  await sleep(1200);
  const stageT = target('stage');
  if (!stageT) { gfail('no stage position logged'); await ctx.close(); return; }
  m0 = mark();
  await page.mouse.click(stageT.x, stageT.y);
  if (!await waitLine(/^WT_WORLD_STAGE_OFFER/, 30000, m0)) { gfail('walking onto the stage never offered the show'); await shot('stage_missing'); await ctx.close(); return; }
  await sleep(500);
  await shot('stage_offer');
  m0 = mark();
  await page.keyboard.press('Enter');
  if (!await waitLine(/^WT_WORLD_SHOW_START/, 6000, m0)) { gfail('Enter on the stage offer did not start the show'); await ctx.close(); return; }
  const showStart = await waitLine(/^WT_SHOW_START/, 12000, m0);
  if (!showStart) { gfail('the show never started'); await shot('show_missing'); await ctx.close(); return; }
  step(showStart.text);
  await sleep(1500);
  await shot('show');
  const showSongs = await playSongs(/^WT_SHOW_DONE/, "show", m0);
  g.showSongs = showSongs;
  if (showSongs !== 10) gfail(`the show had ${showSongs} songs, want 10`);
  const ticket = await waitLine(/^WT_TICKET_SHOWN/, 12000, m0);
  if (!ticket) gfail('no ticket after the show'); else step(ticket.text);
  await sleep(1200);
  await shot('ticket');

  // ---- back to the world from the ticket (Tab, ArrowRight or ArrowDown then Enter, as the 2D test does)
  m0 = mark();
  let back = false;
  for (const k of ['Tab', 'ArrowRight', 'ArrowDown']) {
    const mm = mark();
    await page.keyboard.press(k); await sleep(120); await page.keyboard.press('Enter');
    if (await waitLine(/^WT_TICKET_ACTION action=map/, 1500, mm)) { back = true; break; }
    if (lines.slice(mm).some(l => /^WT_TICKET_ACTION action=again/.test(l.text))) { gfail('reached play again, not back to the map'); break; }
  }
  if (!back) gfail('could not reach "back to the map" on the ticket');
  if (!await waitLine(/^WT_WORLD_MODE mode=(Free|Dialog)/, 8000, m0)) gfail('the world did not come back after the ticket');
  await sleep(1500);
  await shot('after_show');
  const fansShow = await storedFans();
  g.numbers.fansAfterShow = fansShow;
  if (!(fansShow > fansGig)) gfail(`the show added no fans (${fansGig} to ${fansShow})`);

  // ---- reload: the fans (and where she is) persist
  await sleep(500);
  const json = await page.evaluate(() => localStorage.getItem('worldtour.v1'));
  const saved = JSON.parse(json);
  g.world = saved.world;
  if (!saved.world || saved.world.place !== 'coffee' || !saved.world.position || !(saved.world.metCrew || []).includes('coffee/Crew_1'))
    gfail('the save has no usable world key: ' + JSON.stringify(saved.world));
  const keys = Object.keys(saved);
  for (const k of ['v', 'fans', 'venues', 'skills', 'words', 'streak', 'inventory', 'journal']) if (!keys.includes(k)) gfail('the save lost key ' + k);
  m0 = mark();
  await page.reload({ waitUntil: 'load' });
  const ready2 = await waitLine(/^WT_WORLD_GAME_READY/, READY_TIMEOUT_MS, m0);
  if (!ready2) { gfail('no ready line after reload'); await ctx.close(); return; }
  const fansAfterReload = +/fans=(\d+)/.exec(ready2.text)[1];
  g.numbers.fansAfterReload = fansAfterReload;
  if (fansAfterReload !== fansShow) gfail(`reload shows ${fansAfterReload} fans, the save had ${fansShow}`);
  const sr = await waitLine(/^WT_SAVE_READY/, 5000, m0);
  if (!sr || !new RegExp(`fans=${fansShow}\\b`).test(sr.text)) gfail('WT_SAVE_READY after reload: ' + (sr && sr.text));
  step(`reload: ${ready2.text}`);
  await sleep(1500);
  await shot('after_reload');

  if (g.errors.length) gfail(`${g.errors.length} console error(s): ${g.errors.slice(0, 3).join(' | ').slice(0, 300)}`);
  console.log(`gameplay: gig ${gigN} songs, show ${showSongs} songs, fans ${fansGig} -> ${fansShow} -> reload ${fansAfterReload}`);
  await ctx.close();
}

try {
  if (!fs.existsSync(path.join(site, 'index.html'))) throw new Error(`no index.html in ${site}; run BuildWeb.BuildWorld first`);
  server = await startServer(site);
  browser = browserName === 'firefox'
    ? await firefox.launch({ headless: true })
    : await chromium.launch(launchOptions());
  results.browserVersion = browser.version();

  for (const [vw, vh] of (argv.includes('--places-only') || argv.includes('--wardrobe-only') ? [] : sizes)) {
    const key = `${vw}x${vh}`;
    const r = results.sizes[key] = { errors: [], warnings: [], positions: [] };
    const ctx = await browser.newContext({ viewport: { width: vw, height: vh } });
    await ctx.addInitScript(RAF_COUNTER);
    const page = await ctx.newPage();
    const t0 = Date.now();
    let readyMs = null;
    page.on('console', m => {
      const text = m.text();
      if (m.type() === 'error') r.errors.push(text);
      else if (m.type() === 'warning') r.warnings.push(text);
      if (text.startsWith('WT_WORLD_READY') && readyMs === null) readyMs = Date.now() - t0;
      const mm = /^WT_WORLD_POS x=(-?[\d.]+) z=(-?[\d.]+) dogx=(-?[\d.]+) dogz=(-?[\d.]+) fps=([\d.]+)/.exec(text);
      if (mm) r.positions.push({ t: Date.now() - t0, x: +mm[1], z: +mm[2], dogx: +mm[3], dogz: +mm[4], fps: +mm[5] });
    });
    page.on('pageerror', e => r.errors.push('pageerror: ' + e.message));
    server.resetCounters();
    await page.goto(server.url, { waitUntil: 'load' });
    while (readyMs === null && Date.now() - t0 < READY_TIMEOUT_MS) await sleep(50);
    r.readyMs = readyMs;
    r.bytes = server.bytesServed;
    r.files = server.files;
    r.largest = r.files.reduce((a, f) => (!a || f.bytes > a.bytes ? f : a), null);
    if (readyMs === null) { fail(`${key}: no WT_WORLD_READY within ${READY_TIMEOUT_MS} ms`); await ctx.close(); continue; }

    r.renderer = await page.evaluate(() => {
      const c = document.createElement('canvas');
      const gl = c.getContext('webgl2');
      if (!gl) return 'no webgl2';
      const e = gl.getExtension('WEBGL_debug_renderer_info');
      return e ? gl.getParameter(e.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER);
    });

    await sleep(1500);
    await page.screenshot({ path: path.join(shotsDir, `world_idle_${key}.png`) });
    const lastPos = () => r.positions[r.positions.length - 1];
    await page.waitForFunction(() => true);
    await sleep(1200); // make sure at least one position line exists
    const start = lastPos();
    if (!start) { fail(`${key}: no WT_WORLD_POS lines`); await ctx.close(); continue; }

    // Keyboard: hold D (+x on screen) for 1.6 s.
    await page.keyboard.down('KeyD');
    await sleep(1600);
    await page.keyboard.up('KeyD');
    await sleep(1200);
    const afterKey = lastPos();
    r.keyboard = { from: [start.x, start.z], to: [afterKey.x, afterKey.z] };
    if (!(afterKey.x - start.x > 2)) fail(`${key}: keyboard D moved x by ${(afterKey.x - start.x).toFixed(2)} (want > 2)`);

    // Click to move: click to the left and lower on screen; the target is left of her (x decreases).
    await page.mouse.click(Math.round(vw * 0.28), Math.round(vh * 0.8));
    await sleep(2800);
    const afterClick = lastPos();
    r.click = { from: [afterKey.x, afterKey.z], to: [afterClick.x, afterClick.z] };
    if (!(afterKey.x - afterClick.x > 2)) fail(`${key}: click-to-move changed x by ${(afterClick.x - afterKey.x).toFixed(2)} (want < -2)`);
    r.dogFollowed = Math.hypot(afterClick.dogx - afterClick.x, afterClick.dogz - afterClick.z) < 4;
    if (!r.dogFollowed) fail(`${key}: the dog is not near the character`);

    // Frame rate over `seconds` with the character moving: alternate A and D every second.
    const raf0 = await page.evaluate(() => window.__raf);
    const tStart = Date.now();
    let dir = 'KeyD';
    await page.keyboard.down(dir);
    let shot = false;
    while (Date.now() - tStart < seconds * 1000) {
      await sleep(1000);
      if (!shot && Date.now() - tStart > 2000) { shot = true; await page.screenshot({ path: path.join(shotsDir, `world_moving_${key}.png`) }); }
      await page.keyboard.up(dir);
      dir = dir === 'KeyD' ? 'KeyA' : 'KeyD';
      await page.keyboard.down(dir);
    }
    await page.keyboard.up(dir);
    const elapsed = (Date.now() - tStart) / 1000;
    const raf1 = await page.evaluate(() => window.__raf);
    r.rafPerSecond = +((raf1 - raf0) / elapsed).toFixed(1); // Unity's loop registers two callbacks a frame, so this reads about 2x
    const unityFps = r.positions.filter(p => p.t > tStart - t0).map(p => p.fps);
    r.unityFps = unityFps.length ? +(unityFps.reduce((a, b) => a + b, 0) / unityFps.length).toFixed(1) : null;
    r.fps = r.unityFps; // frames Unity itself rendered per second, from its own once-a-second log
    if (r.fps === null || r.fps < MIN_FPS) fail(`${key}: ${r.fps} fps over ${elapsed.toFixed(1)} s (floor ${MIN_FPS})`);
    if (r.errors.length) fail(`${key}: ${r.errors.length} console error(s): ${r.errors.slice(0, 3).join(' | ').slice(0, 300)}`);
    console.log(`${key}: ready ${r.readyMs} ms, ${r.fps} fps (unity ${r.unityFps}), renderer "${r.renderer}", ${(r.bytes / 1e6).toFixed(1)} MB over the wire`);
    await ctx.close();
  }
  if (!argv.includes('--no-gameplay') && !argv.includes('--places-only') && !argv.includes('--wardrobe-only')) await gameplay();
  // phase 8.3: the wardrobe (world_wardrobe.mjs); --no-wardrobe skips it, --wardrobe-only runs only it
  if (!argv.includes('--no-wardrobe') && !argv.includes('--places-only')) await wardrobeTests({ browser, server, sizes, results, fail, shotsDir, argv, READY_TIMEOUT_MS });
  // phase 8.2: the places and the travel between them (world_places.mjs); --no-places skips it, --places-only runs only it
  if (!argv.includes('--no-places') && !argv.includes('--wardrobe-only')) await placeTests({ browser, server, sizes, results, fail, shotsDir, opt, argv, READY_TIMEOUT_MS });
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
