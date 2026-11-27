// Phase 10A: the sound button opens a card with two sliders (music + ambience, effects), each down to off, saved with her progress.
//   node Tests/Web/world_sound_panel.mjs [--gpu d3d11] [--shots <dir>]
// Checks: the button opens and closes the card; the sliders start at 40 percent and 100 percent; moving them (by click on the bar and by keyboard) logs WT_SOUND_SLIDER, is
// saved under settings.ambience and settings.effects in worldtour.v1 (the rest of the save is untouched), survives a reload, and each goes down to off; with both off the
// button shows the muted icon; pressing elsewhere or Escape closes the card; a click on the card does not walk her.
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
const gpu = opt('--gpu', 'swiftshader') === 'd3d11';
const shots = path.resolve(opt('--shots', path.join(project, 'Temp', 'world-shots')));
fs.mkdirSync(shots, { recursive: true });
const sleep = ms => new Promise(r => setTimeout(r, ms));
const world = await startServer(path.join(repo, 'world'));
const browser = await chromium.launch({ headless: true, args: gpu ? ['--use-gl=angle', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl'] : ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const fails = [];
const check = (ok, msg) => { if (!ok) fails.push(msg); console.log((ok ? '  ok   ' : '  FAIL ') + msg); };
const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 } });
const page = await ctx.newPage();
const lines = []; const errs = [];
page.on('console', m => { lines.push(m.text().trim()); if (m.type() === 'error') errs.push(m.text()); });
page.on('pageerror', e => errs.push(String(e)));
const wait = async (re, ms, from = 0) => { const end = Date.now() + ms; while (Date.now() < end) { for (let i = from; i < lines.length; i++) if (re.test(lines[i])) return i; await sleep(100); } return -1; };
const save = () => page.evaluate(() => { try { return JSON.parse(localStorage.getItem('worldtour.v1')); } catch (e) { return null; } });
const BUTTON = [1366 - 16 - 24, 16 + 24];                         // the round sound button, top right
// the card hangs under the button: 280 wide, its right edge 16 px in; rows 68 px apart, the bar 34 px under the top of its row
const CARD_RIGHT = 1366 - 16, CARD_LEFT = CARD_RIGHT - 280, TOP = 16 + 48 + 10;
const bar = row => ({ x0: CARD_LEFT + 14 + 13, x1: CARD_RIGHT - 14 - 13, y: TOP + 14 + row * 68 + 34 + 15 });   // the track runs between the knob centres at both ends
const clickAt = async (x, y) => { await page.mouse.move(x, y); await page.mouse.down(); await page.mouse.up(); await sleep(250); };
const setStep = async (row, step) => { const b = bar(row); await clickAt(b.x0 + (b.x1 - b.x0) * step / 10, b.y); };

await page.goto(world.url + '?wt_test=1', { waitUntil: 'load' });
check((await wait(/^WT_WORLD_GAME_READY/, 120000)) >= 0, 'the game is ready');
await page.evaluate(() => { const c = document.getElementById('unity-canvas'); if (c) c.focus(); });
await sleep(1500);
let s0 = await save();
check(!!s0 && s0.settings.ambience === undefined && s0.settings.effects === undefined, 'a fresh save has no slider settings yet: ' + JSON.stringify(s0 && s0.settings));

let m = lines.length;
await clickAt(...BUTTON);
check((await wait(/^WT_SOUND_TOGGLE open/, 3000, m)) >= 0, 'the sound button opens the card');
await sleep(400);
await page.screenshot({ path: path.join(shots, 'sound_card_default.png') });

m = lines.length;
await setStep(0, 7);
let i = await wait(/^WT_SOUND_SLIDER/, 3000, m);
check(i >= 0 && /ambience=70 effects=100/.test(lines[i]), 'a click on the music bar sets it to 70: ' + (i >= 0 ? lines[i] : 'no log'));
m = lines.length;
await setStep(1, 3);
i = await wait(/^WT_SOUND_SLIDER/, 3000, m);
check(i >= 0 && /ambience=70 effects=30/.test(lines[i]), 'a click on the effects bar sets it to 30: ' + (i >= 0 ? lines[i] : 'no log'));
await sleep(500);
await page.screenshot({ path: path.join(shots, 'sound_card_70_30.png') });
let s1 = await save();
check(!!s1 && s1.settings.ambience === 70 && s1.settings.effects === 30 && s1.settings.sound === true, 'both are saved with her progress: ' + JSON.stringify(s1 && s1.settings));
check(!!s1 && JSON.stringify(s1.fans) === JSON.stringify(s0.fans) && s1.settings.theme === s0.settings.theme && s1.settings.motion === s0.settings.motion, 'nothing else in the save changed');

m = lines.length;
await setStep(0, 0);
await setStep(1, 0);
await sleep(500);
let s2 = await save();
check(!!s2 && s2.settings.ambience === 0 && s2.settings.effects === 0, 'each goes down to off: ' + JSON.stringify(s2 && s2.settings));
await page.screenshot({ path: path.join(shots, 'sound_card_off.png') });

// a click on the card is not a click on the world: she has not moved
const pos0 = lines.filter(l => l.startsWith('WT_WORLD_POS')).pop();
await sleep(800);
const pos1 = lines.filter(l => l.startsWith('WT_WORLD_POS')).pop();
const xy = l => (l || '').replace(/fps=\S+/, '').replace(/t=\S+/, '');
check(xy(pos0) === xy(pos1), 'clicking the sliders did not walk her');

// phase 10B.1: both at zero, then a reload: zero is a saved value, it does not fall back to the 40 and 100 defaults
await page.keyboard.press('Escape');
await sleep(400);
m = lines.length;
await page.reload({ waitUntil: 'load' });
check((await wait(/^WT_WORLD_GAME_READY/, 120000, m)) >= 0, 'ready after a reload with both sliders off');
await sleep(1000);
let s2z = await save();
check(!!s2z && s2z.settings.ambience === 0 && s2z.settings.effects === 0, 'both sliders are still off after a reload: ' + JSON.stringify(s2z && s2z.settings));
m = lines.length;
await clickAt(...BUTTON);
check((await wait(/^WT_SOUND_TOGGLE open/, 3000, m)) >= 0, 'the card opens after the reload with both off');
await sleep(400);
await page.screenshot({ path: path.join(shots, 'sound_card_off_reloaded.png') });

// back to something audible, then a reload keeps it
await setStep(0, 4);
await setStep(1, 10);
await sleep(600);
await page.keyboard.press('Escape');
m = lines.length;
await sleep(500);
await page.screenshot({ path: path.join(shots, 'sound_card_closed.png') });
await page.reload({ waitUntil: 'load' });
check((await wait(/^WT_WORLD_GAME_READY/, 120000, m)) >= 0, 'ready after a reload');
let s3 = await save();
check(!!s3 && s3.settings.ambience === 40 && s3.settings.effects === 100, 'the sliders are still 40 and 100 after a reload: ' + JSON.stringify(s3 && s3.settings));
await sleep(1000);
m = lines.length;
await clickAt(...BUTTON);
check((await wait(/^WT_SOUND_TOGGLE open/, 3000, m)) >= 0, 'the card opens again after the reload');
await clickAt(500, 500);                                            // anywhere else
m = lines.length;
await clickAt(...BUTTON);
const reopened = await wait(/^WT_SOUND_TOGGLE open/, 1500, m);
check(reopened >= 0, 'a press elsewhere closed the card (the next button press opens it again)');
check(errs.length === 0, 'no console errors: ' + errs.slice(0, 2).join(' | '));
await ctx.close(); await browser.close(); await world.close();
console.log(fails.length ? 'FAILED: ' + fails.length : 'PASSED');
process.exit(fails.length ? 1 : 0);
