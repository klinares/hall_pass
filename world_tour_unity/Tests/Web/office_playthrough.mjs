// Task 5.2 playthrough of the web build in tour/: the manager's office, from the map's footer link, by mouse or by keyboard,
// in tour pink or peach + navy. The save's numbers on the page, the reduce-motion box, copying the save code, a bad pasted
// code refused, a good one previewed and cancelled, the reset question cancelled and then confirmed (back to the map, a
// backup kept). Plain Node plus the Playwright install in world_tour/tests/e2e (read only).
//
//   node Tests/Web/office_playthrough.mjs --mode <mouse|keyboard> [--vw 1366] [--vh 768] [--theme tour-pink|peach-navy]
//        [--shots <dir>] [--out <json>] [--site <folder>]
//
//   mouse     the office logs where its controls are (WT_HITS, fractions of the window; again after each scroll), the
//             wheel scrolls them into view; the map's footer link is reached by scrolling the map to the bottom.
//   keyboard  Tab, Enter, Space, Esc and ctrl+v only (WT_FOCUS names where focus went).
//
// The results file is the evidence: { pass, failures, errors, steps, shots }. Exits 1 on failure.
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
const mode = opt('--mode', 'mouse');
const theme = opt('--theme', 'tour-pink');
const vw = +opt('--vw', '1366'), vh = +opt('--vh', '768');
const shots = path.resolve(opt('--shots', path.join(project, 'Temp', 'office_playthrough')));
const tag = `${theme === 'peach-navy' ? 'peach' : 'pink'}_${vw}x${vh}_${mode}`;
const outFile = path.resolve(opt('--out', path.join(shots, `office_${tag}.json`)));
const site = path.resolve(opt('--site', path.join(repo, 'tour')));
const S = Math.sqrt((vw / 1366) * (vh / 768));        // CanvasScaler 1366x768, match 0.5
const cw = vw / S, ch = vh / S;
const out = { mode, theme, vw, vh, pass: false, failures: [], errors: [], warnings: [], steps: [], shots: [] };
const fail = m => { out.failures.push(m); console.log('FAIL ' + m); };
const step = m => { out.steps.push(m); console.log('  ' + m); };
fs.mkdirSync(shots, { recursive: true });

const today = (() => { const d = new Date(), p = n => String(n).padStart(2, '0'); return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`; })();
const day = k => { const d = new Date(Date.parse(today) - k * 864e5); return d.toISOString().slice(0, 10); };
const owned = ['theme:tour-pink', 'guitar:classic', 'dog:none'].concat(theme === 'peach-navy' ? ['theme:peach-navy'] : []);
const SEED = JSON.stringify({
  v: 1, created: day(20), fans: { balance: 12400, lifetime: 64000 }, streak: { days: 3, best: 5, last: day(1) },
  venues: { unlocked: ['coffee', 'records', 'rec'], seen: ['coffee', 'records', 'rec'] },
  skills: {
    'spell:remix': { level: 2, run: 1, missRun: 0, a: 40, c: 31, h: 6, byLevel: { 1: [10, 9], 2: [30, 22], 3: [0, 0] }, mistakes: {} },
    'spell:slots': { level: 1, run: 0, missRun: 0, a: 12, c: 9, h: 3, byLevel: { 1: [12, 9], 2: [0, 0], 3: [0, 0] }, mistakes: {} },
    dec: { level: 2, run: 0, missRun: 1, a: 30, c: 21, h: 9, byLevel: { 1: [10, 9], 2: [20, 12], 3: [0, 0] }, mistakes: { 'no-borrow': 4, 'gave-total': 2 } },
    frac: { level: 1, run: 2, missRun: 0, a: 18, c: 11, h: 4, byLevel: { 1: [18, 11], 2: [0, 0], 3: [0, 0] }, mistakes: { 'added-denominators': 5 } },
  },
  words: {
    illegible: { a: 5, c: 2, first: 1, h: 2, miss: 3, last: day(1) },
    irregular: { a: 4, c: 3, first: 2, h: 1, miss: 1, last: day(2) },
    believe: { a: 3, c: 3, first: 3, h: 0, miss: 0, last: day(2) },
  },
  shows: [0, 1, 1, 2, 4, 6].map((k, i) => ({ d: day(k), mode: ['remix', 'merch', 'beat', 'slots', 'remix', 'merch'][i], venue: 'coffee', n: 10, first: 7, hinted: 1, fans: 900, secs: 300, best: 5 })),
  time: { [day(0)]: 600, [day(1)]: 1500, [day(2)]: 420, [day(4)]: 45, [day(6)]: 900 },
  review: { words: ['illegible'], math: [] },
  inventory: { owned, equipped: { theme, guitar: 'classic', dog: 'none' } },
});

let server, browser;
try {
  server = await startServer(site);
  browser = await chromium.launch({ args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
  const ctx = await browser.newContext({ viewport: { width: vw, height: vh }, acceptDownloads: true, permissions: ['clipboard-read', 'clipboard-write'] });
  const page = await ctx.newPage();
  let lines = [];
  page.on('console', m => {
    const t = m.text().trim(); lines.push(t);
    if (m.type() === 'error') out.errors.push(t);
    if (m.type() === 'warning') out.warnings.push(t);
  });
  page.on('pageerror', e => out.errors.push('pageerror: ' + e.message));
  const mark = () => lines.length;
  const wait = async (re, ms = 15000, from = 0) => {
    const end = Date.now() + ms;
    for (;;) { const l = lines.slice(from).find(x => re.test(x)); if (l) return l; if (Date.now() > end) { fail('missing ' + re); return null; } await page.waitForTimeout(40); }
  };
  const shot = async name => { const p = path.join(shots, `${name}_${tag}.png`); await page.screenshot({ path: p }); out.shots.push(p); step('shot ' + path.basename(p)); };
  const click = async (x, y) => { await page.mouse.move(Math.round(x), Math.round(y)); await page.waitForTimeout(60); await page.mouse.down(); await page.waitForTimeout(40); await page.mouse.up(); await page.waitForTimeout(400); };
  const key = async k => { await page.keyboard.press(k); await page.waitForTimeout(150); };
  const wheel = async notches => { await page.mouse.move(vw / 2, vh / 2); for (let n = 0; n < Math.abs(notches); n++) { await page.mouse.wheel(0, Math.sign(notches) * 100); await page.waitForTimeout(25); } await page.waitForTimeout(900); };
  const hits = () => {
    const all = lines.filter(x => x.startsWith('WT_HITS screen=office '));
    const o = {};
    if (!all.length) return o;
    for (const part of all[all.length - 1].split(' ').slice(2)) { const i = part.lastIndexOf('='); const [x, y] = part.slice(i + 1).split(',').map(Number); o[part.slice(0, i)] = [x * vw, y * vh]; }
    return o;
  };
  /** mouse: scroll the office until a control is on screen, then click it */
  const press = async name => {
    for (let i = 0; i < 12; i++) {
      const h = hits()[name];
      if (!h) { await page.waitForTimeout(500); continue; }
      if (h[1] > 70 && h[1] < vh - 70) { await click(h[0], h[1]); return true; }
      await wheel(h[1] >= vh - 70 ? 3 : -3);
    }
    fail('could not reach ' + name);
    return false;
  };
  const tabTo = async (name, max = 90) => {
    for (let i = 0; i < max; i++) {
      const m0 = mark();
      await key('Tab');
      const end = Date.now() + 2000;
      let l = null;
      while (!l && Date.now() < end) { l = lines.slice(m0).find(t => t.startsWith('WT_FOCUS ')); if (!l) await page.waitForTimeout(30); }
      if (l === 'WT_FOCUS name=' + name) return true;
    }
    fail(`Tab never reached ${name}`);
    return false;
  };
  /** one control, by the mode's own means: a click, or Tab + Enter (Space for the checkbox) */
  const use = async (hit, focusName, k = 'Enter') => {
    if (mode === 'mouse') return press(hit);
    if (await tabTo(focusName)) { await key(k); return true; }
    return false;
  };

  // ---- seed the save, then start
  await page.goto(server.url, { waitUntil: 'load' });
  await wait(/^WT_SAVE_READY/, 60000);
  await page.evaluate(([k, v]) => { localStorage.setItem(k, v); localStorage.removeItem(k + '.backup'); localStorage.removeItem(k + '.backup.2'); }, ['worldtour.v1', SEED]);
  lines = [];
  await page.reload({ waitUntil: 'load' });
  await wait(/^WT_START_SHOWN/, 60000);
  await page.waitForTimeout(500);
  if (mode === 'keyboard') { await page.evaluate(() => document.getElementById('unity-canvas').focus()); await key('Enter'); }
  else await click(vw / 2, vh / 2 + 85 * S);
  const main = await wait(/^WT_MAIN_READY/, 30000);
  if (main && !main.includes('theme=' + theme)) fail(`expected theme=${theme}: ${main}`);
  await wait(/^WT_MAP_READY/, 15000);
  await page.waitForTimeout(800);

  // ---- the map's footer link
  let m0 = mark();
  if (mode === 'keyboard') { if (await tabTo('OfficeLink')) { await page.waitForTimeout(400); await shot('map_footer'); await key('Enter'); } }
  else { await wheel(40); await shot('map_footer'); await click(vw / 2, (ch - 40 - 24) * S); }
  const open = await wait(/^WT_OFFICE_OPEN /, 8000, m0);
  step(open);
  if (open && !/ shows=6 fans=64000 words=2 mistakes=3 /.test(open)) fail('the office should show the seeded save: ' + open);
  await wait(/^WT_HITS screen=office /, 4000, m0);
  await page.waitForTimeout(400);
  await shot('office_top');

  // ---- the tables, then the copy and load cards
  if (mode === 'mouse') { await wheel(5); await shot('office_tables'); await wheel(6); await shot('office_lower'); await wheel(-40); }
  else { await tabTo('ReduceMotion'); await page.waitForTimeout(300); await shot('office_tables'); }

  // ---- reduce motion: on, then off again
  m0 = mark();
  await use('motion', 'ReduceMotion', 'Space');
  const stored1 = await page.evaluate(() => JSON.parse(localStorage.getItem('worldtour.v1')).settings.motion);
  if (stored1 !== 'reduced') fail('reduce motion did not save: motion=' + stored1);
  await page.waitForTimeout(400);                                                // past the 300 ms repeat guard
  if (mode === 'mouse') await press('motion'); else await key('Space');
  await page.waitForTimeout(300);
  const stored2 = await page.evaluate(() => JSON.parse(localStorage.getItem('worldtour.v1')).settings.motion);
  if (stored2 !== 'auto') fail('reduce motion did not turn off: motion=' + stored2);

  // ---- copy the save code
  m0 = mark();
  await use('copy', 'CopyCode');
  await wait(/^WT_OFFICE_EXPORT kind=code ok=true/, 4000, m0);
  await page.waitForTimeout(300);
  const code = await page.evaluate(() => navigator.clipboard.readText()).catch(() => '');
  if (!code.startsWith('WT1-')) fail('the clipboard holds no code');
  await shot('office_copied');

  // ---- a bad code: refused
  const before = await page.evaluate(() => localStorage.getItem('worldtour.v1'));
  m0 = mark();
  if (await use('paste', 'PasteCode', 'Enter')) {
    await page.evaluate(t => navigator.clipboard.writeText(t), code.slice(0, 40));
    await key('Control+V');
  }
  step(await wait(/^WT_OFFICE_CHECK from=paste ok=false/, 4000, m0));
  await page.waitForTimeout(500);
  await shot('office_bad_code');
  if (await page.evaluate(() => localStorage.getItem('worldtour.v1')) !== before) fail('a bad code changed the save');

  // ---- a good code: the preview, then cancel (Esc on the keyboard)
  m0 = mark();
  await page.evaluate(t => navigator.clipboard.writeText(t), code);
  if (mode === 'mouse') await press('paste');
  await key('Control+V');
  step(await wait(/^WT_OFFICE_CHECK from=paste ok=true/, 4000, m0));
  await page.waitForTimeout(600);
  await shot('office_import_preview');
  if (mode === 'mouse') await press('importCancel'); else await key('Escape');
  await page.waitForTimeout(400);
  if (await page.evaluate(() => localStorage.getItem('worldtour.v1.backup')) !== null) fail('cancel still replaced the save');

  // ---- the reset: ask, cancel, ask, erase
  m0 = mark();
  await use('reset', 'Reset');
  await page.waitForTimeout(500);
  await shot('office_reset_question');
  if (mode === 'mouse') await press('resetCancel'); else await key('Enter');      // focus is on cancel
  await page.waitForTimeout(400);
  const still = await page.evaluate(() => JSON.parse(localStorage.getItem('worldtour.v1')).fans.lifetime);
  if (still !== 64000) fail('cancel erased the save');
  m0 = mark();
  await use('reset', 'Reset');
  await page.waitForTimeout(500);
  if (mode === 'mouse') await press('erase'); else { if (await tabTo('Erase')) await key('Enter'); }
  step(await wait(/^WT_OFFICE_RESET changed=true backup=Kept/, 4000, m0));
  await wait(/^WT_MAP_READY/, 5000, m0);
  await page.waitForTimeout(800);
  await shot('map_after_reset');
  const after = await page.evaluate(() => [JSON.parse(localStorage.getItem('worldtour.v1')).fans.lifetime, JSON.parse(localStorage.getItem('worldtour.v1.backup') || '{"fans":{}}').fans.lifetime]);
  if (after[0] !== 0 || after[1] !== 64000) fail('after the reset: fans ' + after[0] + ', backup ' + after[1]);
  step('theme after reset: ' + (lines.filter(l => /theme=/.test(l)).pop() || ''));
} catch (e) {
  fail('playthrough broke: ' + (e && e.stack || e));
} finally {
  if (browser) await browser.close().catch(() => {});
  if (server) await server.close().catch(() => {});
  if (out.errors.length) out.failures.push(`${out.errors.length} console error(s): ${out.errors.slice(0, 3).join(' | ')}`);
  out.pass = out.failures.length === 0;
  fs.writeFileSync(outFile, JSON.stringify(out, null, 2));
  console.log(`${out.pass ? 'PASS' : 'FAIL'} office ${tag} -> ${outFile}`);
  process.exitCode = out.pass ? 0 : 1;
}
