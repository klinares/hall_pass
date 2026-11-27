// Phase 10B.1: the clothing boutique, by keyboard alone and by mouse alone, in the real browser.
//   node Tests/Web/world_boutique.mjs [--gpu d3d11] [--shots <dir>]
// Needs a build of world/ made after the boutique went in. Test hooks (page URL with wt_test): wt_goto=boutique opens the page inside the boutique, wt_coins=N adds spending
// money (the save keeps her fans: a purchase must never touch them). The game logs WT_BOUTIQUE_* lines and, with wt_test, where each spot is on the screen
// (WT_BOUTIQUE_TARGETS spot=x:y;d=metres) and the open card's buttons (WT_WORLD_BUTTONS label:x:y|...).
// Keyboard: walk up to the mirror with the arrow keys, E opens the store, Tab and Enter step through the card (look, next, buy), Escape backs out, walk down onto the mat, Enter on "go out".
// Mouse: click the shoes rack (she walks there), click look, next, buy, all done, click the mat, click go out, click the door back on the street, click go in.
// Mirror: a click on the mirror opens the store like the racks do. Both: coins go down by the price and her fans do not move, the bought item is in the save, no console errors.
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
const shots = path.resolve(opt('--shots', path.join(project, 'Temp', 'boutique-shots')));
fs.mkdirSync(shots, { recursive: true });
const sleep = ms => new Promise(r => setTimeout(r, ms));
const world = await startServer(path.join(repo, 'world'));
const browser = await chromium.launch({ headless: true, args: gpu ? ['--use-gl=angle', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl'] : ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const fails = [];
const check = (ok, msg) => { if (!ok) fails.push(msg); console.log((ok ? '  ok   ' : '  FAIL ') + msg); };
const W = 1366, H = 768;
const only = opt('--only', '');   // keyboard | mouse

async function open(label, query) {
  const ctx = await browser.newContext({ viewport: { width: W, height: H } });
  const page = await ctx.newPage();
  const s = { ctx, page, lines: [], errs: [], label };
  page.on('console', m => { s.lines.push(m.text().trim()); if (m.type() === 'error') s.errs.push(m.text()); });
  page.on('pageerror', e => s.errs.push(String(e)));
  s.wait = async (re, ms, from = 0) => { const end = Date.now() + ms; while (Date.now() < end) { for (let i = from; i < s.lines.length; i++) if (re.test(s.lines[i])) return i; await sleep(60); } return -1; };
  s.last = re => { for (let i = s.lines.length - 1; i >= 0; i--) if (re.test(s.lines[i])) return s.lines[i]; return null; };
  s.save = () => page.evaluate(() => { try { return JSON.parse(localStorage.getItem('worldtour.v1')); } catch (e) { return null; } });
  s.shot = name => page.screenshot({ path: path.join(shots, `boutique_${label}_${name}.png`) });
  s.spot = key => { const l = s.last(/^WT_BOUTIQUE_TARGETS/); const m = l && new RegExp(` ${key}=(-?\\d+):(-?\\d+);d=([\\d.]+)`).exec(l); return m ? { x: +m[1], y: +m[2], d: +m[3] } : null; };
  s.button = label2 => { const l = s.last(/^WT_WORLD_BUTTONS/); const m = l && new RegExp(`${label2}:(-?\\d+):(-?\\d+)`).exec(l); return m ? { x: +m[1], y: +m[2] } : null; };
  s.click = async (p) => { await page.mouse.move(p.x, p.y); await sleep(120); await page.mouse.down(); await sleep(90); await page.mouse.up(); };
  s.go = async () => {
    await page.goto(world.url + '?wt_test=1&' + query, { waitUntil: 'load' });
    const ok = await s.wait(/^WT_WORLD_GAME_READY/, 150000);
    if (ok >= 0) await page.evaluate(() => { const c = document.getElementById('unity-canvas'); if (c) c.focus(); });
    await sleep(800);
    return ok >= 0;
  };
  return s;
}

const coinsOf = save => (save && save.fans && save.fans.coins) || 0;

// ------------------------------------------------------------------------------------------------------ keyboard
if (only === '' || only === 'keyboard') {
  console.log('keyboard alone');
  const s = await open('keyboard', 'wt_goto=boutique&wt_coins=300');
  check(await s.go(), 'the game is ready');
  check((await s.wait(/^WT_WORLD_ARRIVED place=coffee interior=boutique/, 60000)) >= 0, 'she arrives inside the boutique');
  const spots = await s.wait(/^WT_BOUTIQUE_SPOTS .*interior=boutique spots=(\d+)/, 5000);
  check(spots >= 0 && +/spots=(\d+)/.exec(s.lines[spots])[1] >= 10, 'the boutique has its door, shopkeeper, mirror and seven racks: ' + (spots >= 0 ? s.lines[spots] : 'no log'));
  await sleep(1500);
  const save0 = await s.save();
  const fans0 = save0.fans.lifetime, bal0 = save0.fans.balance, coins0 = coinsOf(save0);
  check(coins0 >= 300, 'the test coins are in the save: ' + coins0);
  await s.shot('inside');

  // walk up to the mirror with the up arrow
  await s.page.keyboard.down('ArrowUp');
  const end = Date.now() + 15000;
  while (Date.now() < end) { const m = s.spot('mirror'); if (m && m.d <= 2.4) break; await sleep(100); }
  await s.page.keyboard.up('ArrowUp');
  const m1 = s.spot('mirror');
  check(!!m1 && m1.d <= 2.6, 'the arrow keys brought her to the mirror (distance ' + (m1 ? m1.d : '?') + ' m)');
  let mark = s.lines.length;
  await s.page.keyboard.press('e');
  check((await s.wait(/^WT_BOUTIQUE_OPEN/, 5000, mark)) >= 0, 'E at the mirror opens the store');
  check((await s.wait(/^WT_BOUTIQUE_RACK slot=top/, 5000, mark)) >= 0, 'it starts on the tops rack');
  await sleep(900);
  await s.shot('rack_card');

  mark = s.lines.length;
  await s.page.keyboard.press('Enter');                       // "look" is focused first
  let i = await s.wait(/^WT_BOUTIQUE_ITEM id=top:/, 5000, mark);
  check(i >= 0, 'Enter on "look" shows the first item: ' + (i >= 0 ? s.lines[i] : 'no log'));
  // Tab twice to "next", Enter: the next top is for sale
  mark = s.lines.length;
  await s.page.keyboard.press('Tab'); await sleep(150); await s.page.keyboard.press('Tab'); await sleep(150);
  await s.page.keyboard.press('Enter');
  i = await s.wait(/^WT_BOUTIQUE_ITEM id=top:tank/, 5000, mark);
  check(i >= 0 && /state=CanBuy/.test(s.lines[i]), 'Tab, Tab, Enter steps to the tank, which she can buy: ' + (i >= 0 ? s.lines[i] : 'no log'));
  await sleep(700);
  await s.shot('item_card');
  // Tab back round to the first button (buy) and press it
  mark = s.lines.length;
  for (let k = 0; k < 4; k++) { await s.page.keyboard.press('Tab'); await sleep(150); }
  await s.page.keyboard.press('Enter');
  i = await s.wait(/^WT_BOUTIQUE_BUY id=top:tank result=ok/, 5000, mark);
  check(i >= 0, 'Enter on the buy button buys it: ' + (i >= 0 ? s.lines[i] : 'no log'));
  await sleep(700);
  await s.shot('bought');
  // Escape: back to the rack card, Escape: all done
  mark = s.lines.length;
  await s.page.keyboard.press('Escape');
  check((await s.wait(/^WT_BOUTIQUE_RACK slot=top/, 4000, mark)) >= 0, 'Escape goes back to the rack card');
  await sleep(300);
  mark = s.lines.length;
  await s.page.keyboard.press('Escape');
  check((await s.wait(/^WT_BOUTIQUE_CLOSE/, 4000, mark)) >= 0, 'Escape again leaves the store');
  await sleep(1500);
  const save1 = await s.save();
  check(coinsOf(save1) === coins0 - 25, 'coins went down by the price, 25: ' + coins0 + ' to ' + coinsOf(save1));
  check(save1.fans.lifetime === fans0 && save1.fans.balance === bal0, 'her fans did not move: ' + save1.fans.lifetime + '/' + save1.fans.balance);
  check(JSON.stringify(save1).includes('top:tank'), 'the tank is in the save');

  // walk down onto the mat and go out
  mark = s.lines.length;
  await s.page.keyboard.down('ArrowDown');
  const ask = await s.wait(/^WT_BOUTIQUE_DOOR ask=leave/, 20000, mark);
  await s.page.keyboard.up('ArrowDown');
  check(ask >= 0, 'walking onto the mat asks "go out"');
  await sleep(900);
  await s.shot('go_out_card');
  mark = s.lines.length;
  await s.page.keyboard.press('Enter');
  check((await s.wait(/^WT_WORLD_ARRIVED place=coffee interior=none/, 30000, mark)) >= 0, 'Enter on "go out" puts her back on the coffee street');
  await sleep(1500);
  await s.shot('street');
  check(s.errs.length === 0, 'no console errors: ' + s.errs.slice(0, 2).join(' | '));
  await s.ctx.close();
}

// ------------------------------------------------------------------------------------------------------ mouse
if (only === '' || only === 'mouse') {
  console.log('mouse alone');
  const s = await open('mouse', 'wt_goto=boutique&wt_coins=300');
  check(await s.go(), 'the game is ready');
  check((await s.wait(/^WT_WORLD_ARRIVED place=coffee interior=boutique/, 60000)) >= 0, 'she arrives inside the boutique');
  await sleep(1500);
  const save0 = await s.save();
  const fans0 = save0.fans.lifetime, coins0 = coinsOf(save0);

  let mark = s.lines.length;
  const shoes = s.spot('rack_shoes');
  check(!!shoes, 'the shoes rack is on screen at ' + (shoes ? shoes.x + ',' + shoes.y : '?'));
  await s.click(shoes);
  check((await s.wait(/^WT_BOUTIQUE_RACK slot=shoes/, 25000, mark)) >= 0, 'a click on the shoes rack walks her there and opens it');
  await sleep(1200);
  await s.shot('rack_card');
  let b = s.button('look');
  check(!!b, 'the card has a look button');
  mark = s.lines.length;
  await s.click(b);
  check((await s.wait(/^WT_BOUTIQUE_ITEM id=shoes:/, 5000, mark)) >= 0, 'a click on look shows the first pair');
  // step to the sandals (boots, sneakers, flats, sandals): three clicks on next
  for (let k = 0; k < 3; k++) {
    await sleep(700);
    b = s.button('next');
    mark = s.lines.length;
    await s.click(b);
    await s.wait(/^WT_BOUTIQUE_ITEM id=shoes:/, 5000, mark);
  }
  check(/WT_BOUTIQUE_ITEM id=shoes:sandals state=CanBuy/.test(s.last(/^WT_BOUTIQUE_ITEM/) || ''), 'three clicks on next reach the sandals: ' + s.last(/^WT_BOUTIQUE_ITEM/));
  await sleep(900);
  await s.shot('item_card');
  b = s.button('buy_25_coins');
  check(!!b, 'the buy button says the price');
  mark = s.lines.length;
  await s.click(b);
  check((await s.wait(/^WT_BOUTIQUE_BUY id=shoes:sandals result=ok/, 5000, mark)) >= 0, 'a click on buy buys them');
  await sleep(900);
  await s.shot('bought');
  b = s.button('back');
  mark = s.lines.length;
  await s.click(b);
  await s.wait(/^WT_BOUTIQUE_RACK/, 4000, mark);
  await sleep(700);
  b = s.button('all_done');
  mark = s.lines.length;
  await s.click(b);
  check((await s.wait(/^WT_BOUTIQUE_CLOSE/, 5000, mark)) >= 0, 'back then all done leaves the store');
  await sleep(1500);
  const save1 = await s.save();
  check(coinsOf(save1) === coins0 - 25 && save1.fans.lifetime === fans0, 'coins down by 25, fans unchanged');
  check(JSON.stringify(save1).includes('shoes:sandals'), 'the sandals are in the save');

  // the mat, by clicking it
  mark = s.lines.length;
  // the mat is off the bottom of the screen from the mirror: click the ground toward it until it shows, like a hand would
  for (let k = 0; k < 6; k++) { const t = s.spot('door_out'); if (t && t.y < 700) break; await s.click({ x: 683, y: 690 }); await sleep(2500); }
  const mat = s.spot('door_out');
  console.log('  mat target ' + JSON.stringify(mat) + ' ' + s.last(/^WT_BOUTIQUE_TARGETS/));
  await s.click(mat || { x: W / 2, y: H - 60 });
  check((await s.wait(/^WT_BOUTIQUE_DOOR ask=leave/, 25000, mark)) >= 0, 'a click on the mat walks her onto it and asks "go out"');
  await sleep(900);
  b = s.button('go_out');
  mark = s.lines.length;
  await s.click(b);
  check((await s.wait(/^WT_WORLD_ARRIVED place=coffee interior=none/, 30000, mark)) >= 0, 'a click on "go out" returns her to the street');
  await sleep(1800);

  // and back in through the door
  const door = s.spot('boutique_door');
  check(!!door, 'the boutique door is on screen on the street');
  mark = s.lines.length;
  await s.click(door);
  check((await s.wait(/^WT_BOUTIQUE_DOOR ask=enter/, 25000, mark)) >= 0, 'a click on the door walks her up and asks "go in"');
  await sleep(900);
  await s.shot('door_card');
  b = s.button('go_in');
  mark = s.lines.length;
  await s.click(b);
  check((await s.wait(/^WT_WORLD_ARRIVED place=coffee interior=boutique/, 30000, mark)) >= 0, 'a click on "go in" takes her inside');
  check(s.errs.length === 0, 'no console errors: ' + s.errs.slice(0, 2).join(' | '));
  await s.ctx.close();
}

// ------------------------------------------------------------------------------------------------------ the mirror by mouse (it once stopped 3 m short and never opened)
if (only === '' || only === 'mirror') {
  console.log('the mirror by mouse');
  const s = await open('mirror', 'wt_goto=boutique&wt_coins=300');
  check(await s.go(), 'the game is ready');
  check((await s.wait(/^WT_WORLD_ARRIVED place=coffee interior=boutique/, 60000)) >= 0, 'she arrives inside the boutique');
  await sleep(1500);
  await s.shot('name_tags');
  const mark = s.lines.length;
  const mirror = s.spot('mirror');
  check(!!mirror, 'the mirror is on screen at ' + (mirror ? mirror.x + ',' + mirror.y : '?'));
  await s.click(mirror);
  check((await s.wait(/^WT_BOUTIQUE_OPEN/, 25000, mark)) >= 0, 'a click on the mirror walks her there and opens the store');
  check(s.errs.length === 0, 'no console errors: ' + s.errs.slice(0, 2).join(' | '));
  await s.ctx.close();
}

await browser.close(); await world.close();
console.log(fails.length ? 'FAILED: ' + fails.length : 'PASSED');
process.exit(fails.length ? 1 : 0);
