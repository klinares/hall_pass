// Phase 10A: the page's WebGL context request and its blank-canvas guard (Assets/WebGLTemplates/WorldTourWorld/index.html), in Chrome and in Firefox.
//   node Tests/Web/world_blank_guard.mjs [--browser chromium|firefox] [--gpu d3d11]
// Checks:
//   1. the context the game asks for: Chrome and Edge ask for powerPreference "high-performance"; Firefox asks for the default (no powerPreference, or "default");
//   2. a normal load reaches WT_WORLD_GAME_READY and the guard note stays hidden;
//   3. the guard: with no ready line it shows the reload note and logs WT_BLANK_CANVAS, and the note goes away when the ready line arrives (the test resets the page variables to simulate it);
//   4. the page title is "Natalia's World Tour".
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { startServer } from './server.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..', '..');
const { chromium, firefox } = createRequire(path.join(here, 'package.json'))('playwright');
const argv = process.argv.slice(2);
const opt = (n, d) => { const i = argv.indexOf(n); return i >= 0 && i + 1 < argv.length ? argv[i + 1] : d; };
const name = opt('--browser', 'chromium');
const gpu = opt('--gpu', 'swiftshader') === 'd3d11';
const sleep = ms => new Promise(r => setTimeout(r, ms));
const world = await startServer(path.join(repo, 'world'));
const browser = name === 'firefox'
  ? await firefox.launch({ headless: true })
  : await chromium.launch({ headless: true, args: gpu ? ['--use-gl=angle', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl'] : ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const fails = [];
const check = (ok, msg) => { if (!ok) fails.push(msg); console.log((ok ? '  ok   ' : '  FAIL ') + msg); };

const SPY = `(() => {
  window.__wtCtx = [];
  const get = HTMLCanvasElement.prototype.getContext;
  HTMLCanvasElement.prototype.getContext = function (type, attrs) {
    try { if (/webgl/.test(String(type))) window.__wtCtx.push({ type: String(type), power: attrs ? attrs.powerPreference : undefined }); } catch (e) {}
    return get.apply(this, arguments);
  };
  window.__wtMessageSeen = false;
  new MutationObserver(() => { const m = document.getElementById('message'); if (m && m.style.display === 'block') window.__wtMessageSeen = true; }).observe(document, { subtree: true, attributes: true, childList: true });
})();`;

async function open(query) {
  const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 } });
  await ctx.addInitScript(SPY);
  const page = await ctx.newPage();
  const lines = []; const errs = [];
  page.on('console', m => { lines.push(m.text().trim()); if (m.type() === 'error') errs.push(m.text()); });
  page.on('pageerror', e => errs.push(String(e)));
  await page.goto(world.url + query, { waitUntil: 'load' });
  return { ctx, page, lines, errs };
}
const wait = async (h, re, ms) => { const end = Date.now() + ms; while (Date.now() < end) { if (h.lines.some(l => re.test(l))) return true; await sleep(100); } return false; };
const visible = h => h.page.evaluate(() => { const m = document.getElementById('message'); return !!m && getComputedStyle(m).display !== 'none'; });

console.log(`browser: ${name}`);
let h = await open('?wt_test=1');
check(await wait(h, /^WT_WORLD_GAME_READY/, 120000), 'a normal load reaches WT_WORLD_GAME_READY');
const ctxs = await h.page.evaluate(() => window.__wtCtx);
console.log('  context requests: ' + JSON.stringify(ctxs));
const gl = ctxs.find(c => c.power !== undefined) || ctxs.find(c => c.type === 'webgl2') || ctxs[0];   // the loader also opens a plain probe context first; the game's own request is the one with attributes
check(!!gl, 'the page asked for a WebGL context');
if (name === 'firefox') check(gl && (gl.power === undefined || gl.power === 'default' || gl.power === 0), 'Firefox asks for the default power preference, not high-performance (saw ' + (gl && gl.power) + ')');
else check(gl && (gl.power === 'high-performance' || gl.power === 2), 'Chrome still asks for high-performance (saw ' + (gl && gl.power) + ')');
await sleep(4000);
check(await h.page.evaluate(() => gameReady === true), "the page saw the game's own ready line (so the 75 s guard can never fire on a healthy load)");
check(!(await visible(h)), 'the guard note is hidden after a normal load');
check(!h.lines.some(l => l.startsWith('WT_BLANK_CANVAS')), 'no WT_BLANK_CANVAS warning on a normal load');
check((await h.page.title()) === "Natalia's World Tour", 'the page title is Natalia\'s World Tour: ' + (await h.page.title()));
check(h.errs.length === 0, 'no console errors: ' + h.errs.slice(0, 2).join(' | '));
await h.ctx.close();

// the guard itself: pretend the ready line never came (the page's own variables), let the short timer run out, then let the ready line arrive
h = await open('?wt_test=1');
check(await wait(h, /^WT_WORLD_GAME_READY/, 120000), 'ready again for the guard check');
await h.page.evaluate(() => { gameReady = false; blankMs = 600; watchForBlank(); });
await sleep(1500);
check(await visible(h), 'with no ready line the guard shows its reload note');
check(h.lines.some(l => l.startsWith('WT_BLANK_CANVAS')), 'and logs WT_BLANK_CANVAS');
await h.page.evaluate(() => console.log('WT_WORLD_GAME_READY simulated'));
await sleep(300);
check(!(await visible(h)), 'the note goes away when the ready line arrives');
const p0 = h.lines.filter(l => l.startsWith('WT_WORLD_POS')).length; await sleep(2500);
check(h.lines.filter(l => l.startsWith('WT_WORLD_POS')).length > p0, 'the game is still running');
await h.ctx.close();

await browser.close(); await world.close();
console.log(fails.length ? 'FAILED: ' + fails.length : 'PASSED');
process.exit(fails.length ? 1 : 0);
