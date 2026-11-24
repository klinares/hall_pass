// Task 5.1 playthrough of the web build in tour/: the tour journal and the backstage shop in tour pink, trying on,
// buying (two taps) and wearing peach + navy, the map and the journal in the new theme, a math show screen in it, then
// a reload that must bring everything back. Plain Node plus the Playwright install in world_tour/tests/e2e (read only).
//
//   node Tests/Web/meta_playthrough.mjs --mode <mouse|keyboard> [--vw 1366] [--vh 768] [--shots <dir>] [--out <json>] [--site <folder>]
//
//   mouse     the shop and the journal by mouse: the screens log where their controls are (WT_HITS, fractions of the
//             window), the map's hub cards are reached by scrolling the wheel to the bottom; the journal's first sticker is
//             dragged across the page and then clicked and placed with a second click.
//   keyboard  Tab, Enter, Esc, arrow keys and Page Down only (WT_FOCUS names where focus went).
//
// A seeded save (70,000 fans, one finished show) is written before the game starts. The results file is the
// evidence: { pass, failures, errors, steps, shots }. Exits 1 on failure.
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
const vw = +opt('--vw', '1366'), vh = +opt('--vh', '768');
const shots = path.resolve(opt('--shots', path.join(project, 'Temp', 'meta_playthrough')));
const outFile = path.resolve(opt('--out', path.join(shots, `meta_${mode}_${vw}x${vh}.json`)));
const site = path.resolve(opt('--site', path.join(repo, 'tour')));
const S = Math.sqrt((vw / 1366) * (vh / 768));        // CanvasScaler 1366x768, match 0.5
const cw = vw / S, ch = vh / S;                       // the canvas in its own units
const out = { mode, vw, vh, pass: false, failures: [], errors: [], warnings: [], steps: [], shots: [], lines: [] };
const fail = m => { out.failures.push(m); console.log('FAIL ' + m); };
const step = m => { out.steps.push(m); console.log('  ' + m); };
fs.mkdirSync(shots, { recursive: true });

const today = (() => { const d = new Date(), p = n => String(n).padStart(2, '0'); return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`; })();
const SEED = JSON.stringify({
  v: 1, created: today, fans: { balance: 70000, lifetime: 70000 },
  venues: { unlocked: ['coffee', 'records', 'rec'], seen: ['coffee', 'records', 'rec'] },
  shows: [{ d: today, mode: 'remix', venue: 'coffee', n: 10, first: 7, hinted: 1, fans: 20000, secs: 300, best: 5 }],
});

let server, browser;
try {
  server = await startServer(site);
  browser = await chromium.launch({ args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
  const ctx = await browser.newContext({ viewport: { width: vw, height: vh } });
  let page = await ctx.newPage();
  let lines = [];
  const attach = p => {
    p.on('console', m => {
      const t = m.text().trim(); lines.push(t);
      if (m.type() === 'error') out.errors.push(t);
      if (m.type() === 'warning') out.warnings.push(t);
    });
    p.on('pageerror', e => out.errors.push('pageerror: ' + e.message));
  };
  attach(page);
  const mark = () => lines.length;
  const wait = async (re, ms = 15000, from = 0) => {
    const end = Date.now() + ms;
    for (;;) { const l = lines.slice(from).find(x => re.test(x)); if (l) return l; if (Date.now() > end) { fail('missing ' + re); return null; } await page.waitForTimeout(40); }
  };
  const shot = async name => { const p = path.join(shots, `${name}_${vw}x${vh}_${mode}.png`); await page.screenshot({ path: p }); out.shots.push(p); step('shot ' + path.basename(p)); };
  const click = async (x, y) => { await page.mouse.move(Math.round(x), Math.round(y)); await page.waitForTimeout(60); await page.mouse.down(); await page.waitForTimeout(40); await page.mouse.up(); await page.waitForTimeout(60); };
  const key = async k => { await page.keyboard.press(k); await page.waitForTimeout(120); };
  const wheel = async notches => { await page.mouse.move(vw / 2, vh / 2); for (let n = 0; n < Math.abs(notches); n++) { await page.mouse.wheel(0, Math.sign(notches) * 100); await page.waitForTimeout(25); } await page.waitForTimeout(600); };
  /** the latest WT_HITS line for a screen, as { name: [x, y] } in window pixels */
  const hits = async (screen, from = 0) => {
    const l = await wait(new RegExp('^WT_HITS screen=' + screen + ' '), 8000, from);
    const o = {};
    if (!l) return o;
    const all = lines.slice(from).filter(x => x.startsWith('WT_HITS screen=' + screen + ' '));
    for (const part of all[all.length - 1].split(' ').slice(2)) { const i = part.lastIndexOf('='); const [x, y] = part.slice(i + 1).split(',').map(Number); o[part.slice(0, i)] = [x * vw, y * vh]; }
    return o;
  };
  /** keyboard: Tab until focus lands on `name` (FocusNavigator logs WT_FOCUS name=...) */
  const tabTo = async (name, max = 60) => {
    for (let i = 0; i < max; i++) {
      const m0 = mark();
      await key('Tab');
      const l = await (async () => { const end = Date.now() + 2000; for (;;) { const x = lines.slice(m0).find(t => t.startsWith('WT_FOCUS ')); if (x) return x; if (Date.now() > end) return null; await page.waitForTimeout(30); } })();
      if (l === 'WT_FOCUS name=' + name) return true;
    }
    fail(`Tab never reached ${name}`);
    return false;
  };
  const start = async () => {
    await wait(/^WT_START_SHOWN/, 60000);
    await page.waitForTimeout(500);
    if (mode === 'keyboard') { await page.evaluate(() => document.getElementById('unity-canvas').focus()); await key('Enter'); }
    else await click(vw / 2, vh / 2 + 85 * S);
    const main = await wait(/^WT_MAIN_READY/, 30000);
    await wait(/^WT_MAP_READY/, 15000);
    await page.waitForTimeout(800);
    return main;
  };
  /** the map's hub cards, under the map: scroll to the bottom (mouse) or Tab to them (keyboard) */
  const openHub = async which => {
    const m0 = mark();
    if (mode === 'keyboard') { if (await tabTo('Hub_' + which)) await key('Enter'); }
    else {
      await wheel(40);
      const mw = Math.min(cw, 1180) - 64, x0 = (cw - mw) / 2, cardW = (mw - 14) / 2;
      const cx = which === 'backstage' ? x0 + cardW / 2 : x0 + cardW + 14 + cardW / 2;
      await click(cx * S, (ch - 40 - 48 - 24 - 38) * S);   // the hub cards sit above the footer link (task 5.2: 24 gap + 48)
    }
    return m0;
  };
  const back = async (screen, from) => {
    if (mode === 'keyboard') { await key('Escape'); return; }
    const h = await hits(screen, from);
    if (h.back) await click(...h.back); else fail('no back control on ' + screen);
  };

  // ---- seed the save, then start
  await page.goto(server.url, { waitUntil: 'load' });
  await wait(/^WT_SAVE_READY/, 60000);
  await page.evaluate(([k, v]) => localStorage.setItem(k, v), ['worldtour.v1', SEED]);
  lines = [];
  await page.reload({ waitUntil: 'load' });
  const main1 = await start();
  if (!/theme=tour-pink/.test(main1 || '')) fail('should start in tour pink: ' + main1);
  await shot('map_pink');

  // ---- the journal in tour pink: the new stickers peel on
  let m0 = await openHub('journal');
  const jr = await wait(/^WT_JOURNAL_READY/, 6000, m0);
  step(jr);
  if (jr && !/fresh=[1-9]/.test(jr)) fail('no fresh stickers on the first journal visit: ' + jr);
  await wait(/^WT_JOURNAL_PEEL key=first-show/, 4000, m0);
  await page.waitForTimeout(900);
  await shot('journal_pink_p1');
  let jh = await hits('journal', m0);
  if (mode === 'mouse') {
    // drag the opening night sticker across the page, then click it and click a spot to place it
    const a = jh['stk:first-show'], pg = jh.page;
    if (a && pg) {
      let m1 = mark();
      await page.mouse.move(a[0], a[1]); await page.mouse.down();
      for (let i = 1; i <= 12; i++) { await page.mouse.move(a[0] + (pg[0] - a[0]) * i / 12, a[1] + (pg[1] + 40 * S - a[1]) * i / 12); await page.waitForTimeout(30); }
      await page.mouse.up();
      await wait(/^WT_JOURNAL_PLACE key=first-show/, 3000, m1);
      await page.waitForTimeout(500);
      jh = await hits('journal', m0);
      m1 = mark();
      const now = [pg[0], pg[1] + 40 * S];
      await click(now[0], now[1]);                     // pick it up
      await page.waitForTimeout(350);
      await click(pg[0] - 260 * S, pg[1] - 120 * S);   // and put it down on an empty spot
      await wait(/^WT_JOURNAL_PLACE key=first-show/, 3000, m1);
      await shot('journal_pink_moved');
    } else fail('journal hits missing the sticker or the page');
    if (jh.next) { const m1 = mark(); await click(...jh.next); await wait(/^WT_JOURNAL_PAGE page=1/, 3000, m1); await page.waitForTimeout(1200); await shot('journal_pink_p2'); }
  } else {
    if (await tabTo('Stk_first-show')) {
      const m1 = mark();
      await key('ArrowRight'); await key('ArrowRight'); await key('ArrowDown');
      await wait(/^WT_JOURNAL_PLACE key=first-show/, 3000, m1);
      await key('Enter');                                // pick it up: the caption names it
      await page.waitForTimeout(300);
      await shot('journal_pink_moved');
    }
    const m1 = mark();
    await key('PageDown');
    await wait(/^WT_JOURNAL_PAGE page=1/, 3000, m1);
    await page.waitForTimeout(1200);
    await shot('journal_pink_p2');
  }
  m0 = mark();
  await back('journal', 0);
  await wait(/^WT_MAP_READY/, 5000, m0);
  await page.waitForTimeout(600);

  // ---- backstage in tour pink: try on peach + navy, buy it with two taps, wear it
  m0 = await openHub('backstage');
  step(await wait(/^WT_BACKSTAGE_READY/, 6000, m0));
  await page.waitForTimeout(500);
  await shot('backstage_pink');
  let bh = await hits('backstage', m0);
  const pressItem = async id => {
    const m1 = mark();
    if (mode === 'keyboard') { if (await tabTo('Item_' + id)) await key('Enter'); }
    else if (bh[id]) await click(...bh[id]); else fail('no hit for ' + id);
    await wait(new RegExp('^WT_SHOP_SELECT id=' + id.replace(/[+:]/g, m => '\\' + m)), 3000, m1);
    await page.waitForTimeout(400);
  };
  const pressAct = async outcome => {
    const m1 = mark();
    if (mode === 'keyboard') {
      const focused = lines.filter(l => l.startsWith('WT_FOCUS ')).pop();
      if (focused !== 'WT_FOCUS name=Act') { if (!(await tabTo('Act'))) return; }
      await key('Enter');
    } else { bh = await hits('backstage', m0); if (bh.act) await click(...bh.act); else fail('no act control'); }
    const l = await wait(/^WT_SHOP_PRESS /, 3000, m1);
    if (l && !l.includes('outcome=' + outcome)) fail(`expected ${outcome}: ${l}`);
    step(l);
    await page.waitForTimeout(450);
  };
  await pressItem('theme:peach-navy');
  await wait(/^WT_THEME theme=peach-navy/, 3000);
  await page.waitForTimeout(500);
  await shot('backstage_tryon_peach');
  await pressAct('armed');
  await shot('backstage_armed');
  await pressAct('bought');
  await page.waitForTimeout(500);
  await shot('backstage_bought_peach');
  bh = await hits('backstage', mark() - 1);
  await pressItem('guitar:lilac');                      // 12,000 with 10,000 left: "2,000 more fans needed", the theme stays on
  await shot('backstage_short_peach');
  if (lines.filter(l => /^WT_THEME theme=tour-pink/.test(l)).length > 0 && !lines.slice(-30).some(l => /theme=peach-navy/.test(l))) fail('picking a guitar took the bought theme off');

  // ---- the map and the journal in peach + navy
  m0 = mark();
  await back('backstage', 0);
  await wait(/^WT_MAP_READY/, 5000, m0);
  await page.waitForTimeout(700);
  if (mode === 'mouse') await wheel(-60);
  await page.waitForTimeout(300);
  await shot('map_peach');
  m0 = await openHub('journal');
  await wait(/^WT_JOURNAL_READY/, 6000, m0);
  await page.waitForTimeout(1500);
  await shot('journal_peach_p1');
  {
    const m1 = mark();
    if (mode === 'keyboard') await key('PageDown');
    else { jh = await hits('journal', m0); if (jh.next) await click(...jh.next); }
    await wait(/^WT_JOURNAL_PAGE page=1/, 3000, m1);
    await page.waitForTimeout(800);
    await shot('journal_peach_p2');
  }

  // ---- reload: everything comes back, in peach + navy; then a math show screen in it
  lines = [];
  await page.reload({ waitUntil: 'load' });
  const main2 = await start();
  if (!/theme=peach-navy/.test(main2 || '')) fail('the theme did not survive the reload: ' + main2);
  const saved = JSON.parse(await page.evaluate(() => localStorage.getItem('worldtour.v1')) || 'null');
  if (!saved) fail('no save after the reload');
  else {
    if (saved.v !== 2) fail('save v=' + saved.v);
    if (saved.inventory.equipped.theme !== 'peach-navy' || !saved.inventory.owned.includes('theme:peach-navy')) fail('the theme purchase was lost');
    if (saved.fans.lifetime !== 70000 || saved.fans.balance !== 10000) fail('fans: ' + JSON.stringify(saved.fans));
    if (!saved.journal.stickers['first-show'] || !saved.journal.seen.includes('first-show')) fail('the sticker or its seen mark was lost');
    if (!saved.journal.layout['first-show']) fail('the moved sticker spot was lost');
    step('save after reload: v=' + saved.v + ' theme=' + saved.inventory.equipped.theme + ' fans=' + JSON.stringify(saved.fans) + ' seen=' + saved.journal.seen.join(','));
  }
  await shot('map_peach_reloaded');
  // a math screen in peach + navy: tonight (focused) -> the venue card -> the first math mode the served math_config.json has switched on
  // (phase 7 switched merch off, so the mode is read from the config the build is serving, not assumed). Skill keys and mode ids as ModeCatalog.cs, in its order.
  const MATH_MODES = [['merch', 'dec'], ['beat', 'frac'], ['tickets', 'md'], ['stage', 'xy'], ['truck', 'vol'], ['budget', 'ops'], ['soundcheck', 'flu'], ['snack', 'fm']];
  const cfg = await page.evaluate(async () => { try { return await (await fetch('StreamingAssets/math_config.json', { cache: 'no-store' })).json(); } catch (e) { return null; } });
  const switches = cfg && cfg.ENABLED_SKILLS;
  const enabled = MATH_MODES.filter(([, skill]) => !switches || switches[skill] !== false);   // a missing switch means on, as MathConfig does
  step('math modes on in the served config: ' + (enabled.map(e => e[0]).join(',') || 'none'));
  if (!enabled.length) fail('the served math_config.json has every math mode off');
  m0 = mark();
  await page.evaluate(() => document.getElementById('unity-canvas').focus());
  await key('Enter');
  await wait(/^WT_VENUE_OPEN/, 5000, m0);
  await page.waitForTimeout(700);
  await shot('venue_peach');
  if (enabled.length) {
    const modeId = enabled[0][0];
    if (await tabTo('Mode_' + modeId)) {
      const m1 = mark();
      await key('Enter');
      await wait(new RegExp('^WT_SHOW_REQUEST .* mode=' + modeId), 5000, m1);
      await page.waitForTimeout(2500);
      await shot('math_' + modeId + '_peach');
    }
  }

  out.pass = out.failures.length === 0 && out.errors.length === 0;
  if (out.errors.length) fail(`${out.errors.length} console error(s): ${out.errors.slice(0, 3).join(' | ')}`);
} catch (e) {
  fail('runner: ' + (e && e.stack || e));
} finally {
  out.lines = out.lines.concat([]);
  try { if (browser) await browser.close(); } catch (e) { /* closing */ }
  try { if (server) await server.close(); } catch (e) { /* closing */ }
  fs.writeFileSync(outFile, JSON.stringify(out, null, 2));
  console.log(`${out.pass ? 'PASS' : 'FAIL'} meta ${mode} ${vw}x${vh}: ${out.failures.length} failure(s), ${out.errors.length} console error(s); ${outFile}`);
  process.exit(out.pass ? 0 : 1);
}
