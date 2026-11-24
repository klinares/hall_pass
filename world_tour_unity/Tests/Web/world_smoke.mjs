// Browser smoke test for the 3D world build in world/ (phase 8.0 spike). Plain Node plus the Playwright install
// already in world_tour/tests/e2e/node_modules (read only).
//
//   node Tests/Web/world_smoke.mjs [--site <folder>] [--gpu auto|swiftshader|d3d11|chrome] [--sizes 1366x768,1920x1080]
//                                  [--out <results.json>] [--shots <dir>] [--seconds 5]
//
// For each size: serves world/ with the plain static server, loads it in headless Chrome, waits for WT_WORLD_READY
// (log line from the game), checks that keyboard movement and click-to-move both change the character's position,
// measures frames per second over --seconds with the character moving (Unity's own per-second frame count; the rAF count is
// recorded too but reads 2x because Unity registers two callbacks per frame), saves a screenshot, and records the WebGL renderer string Chrome used.
// Fails on console errors, no ready line, no movement, or fps under 30 (the advisor's headless floor).
// --gpu: swiftshader = software GL (what run.mjs uses); d3d11 = ANGLE on the machine's GPU in the headless shell;
//        chrome = installed Chrome (new headless) on its default GPU path; auto = swiftshader.
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { startServer } from './server.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const project = path.resolve(here, '..', '..');
const repo = path.resolve(project, '..');
const { chromium } = createRequire(path.join(repo, 'world_tour', 'tests', 'e2e', 'package.json'))('playwright');

const argv = process.argv.slice(2);
const opt = (n, d) => { const i = argv.indexOf(n); return i >= 0 && i + 1 < argv.length ? argv[i + 1] : d; };
const site = path.resolve(opt('--site', path.join(repo, 'world')));
const gpuMode = opt('--gpu', 'auto');
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
try {
  if (!fs.existsSync(path.join(site, 'index.html'))) throw new Error(`no index.html in ${site}; run BuildWeb.BuildWorld first`);
  server = await startServer(site);
  browser = await chromium.launch(launchOptions());
  results.browserVersion = browser.version();

  for (const [vw, vh] of sizes) {
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
