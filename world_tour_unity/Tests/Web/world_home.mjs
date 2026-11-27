// Phase 10B.2: her home and the decorating, by keyboard alone and by mouse alone, in the real browser.
//   node Tests/Web/world_home.mjs [--gpu d3d11] [--shots <dir>] [--only keyboard|mouse|routes|rooms|buy]
// Needs a build of world/ made after the home went in. Test hooks (page URL with wt_test): wt_goto=home opens the page inside her home; SendMessage('WorldGame','TestHome', cmd)
// with cmd = grant (every piece and 1000 coins), layout, state, room:n, cell:x:z (logs WT_HOME_CELL x z sx sy), map (the tour map's go home) or in/out.
// The game logs WT_HOME_DECO / DROP / AWAY / COLOR / STATE (what she holds and whether it fits), WT_HOME_LAYOUT (the home key of the save) and, with wt_test, where every button,
// piece and door is on the screen (WT_HOME_TARGETS name=x:y ...).
// Keyboard: E opens decorating, Tab selects a piece, an arrow lifts and moves it, Enter drops it (a red spot refuses and says why), R turns it, Delete puts it away, B opens the box and
//   Up picks the group, Enter takes a piece out, C paints the room, Escape backs out; then a reload shows the same layout.
// Mouse: the decorate button, a click on a piece lifts it, the pointer slides its ghost, a click drops it; turn, put away, box, group and piece buttons, the color swatches, done.
// Routes: the tour map's "go home" button and the door on the coffee street; rooms: a locked door asks the price, buying opens it, the room keeps its own pieces.
// Buy: with ?wt_coins=300, B opens the box, Enter on the sofa buys it (90 coins) and holds it, Enter places it; a reload shows it standing and the coins lower.
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
const shots = path.resolve(opt('--shots', path.join(project, 'Temp', 'home-shots')));
fs.mkdirSync(shots, { recursive: true });
const sleep = ms => new Promise(r => setTimeout(r, ms));
const world = await startServer(path.join(repo, 'world'));
const browser = await chromium.launch({ headless: true, args: gpu ? ['--use-gl=angle', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl'] : ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const fails = [];
const check = (ok, msg) => { if (!ok) fails.push(msg); console.log((ok ? '  ok   ' : '  FAIL ') + msg); };
const W = 1366, H = 768;
const only = opt('--only', '');

async function open(label, query, init) {
  const ctx = await browser.newContext({ viewport: { width: W, height: H } });
  if (init) await ctx.addInitScript(init);
  const page = await ctx.newPage();
  const s = { ctx, page, lines: [], errs: [], label };
  page.on('console', m => { s.lines.push(m.text().trim()); if (m.type() === 'error') s.errs.push(m.text()); });
  page.on('pageerror', e => s.errs.push(String(e)));
  s.wait = async (re, ms, from = 0) => { const end = Date.now() + ms; while (Date.now() < end) { for (let i = from; i < s.lines.length; i++) if (re.test(s.lines[i])) return i; await sleep(60); } return -1; };
  s.last = re => { for (let i = s.lines.length - 1; i >= 0; i--) if (re.test(s.lines[i])) return s.lines[i]; return null; };
  s.save = () => page.evaluate(() => { try { return JSON.parse(localStorage.getItem('worldtour.v1')); } catch (e) { return null; } });
  s.shot = name => page.screenshot({ path: path.join(shots, `10B2_home_${label}_${name}.png`) });
  s.target = key => { const l = s.last(/^WT_HOME_TARGETS/); const m = l && new RegExp(` ${key}=(-?\\d+):(-?\\d+)`).exec(l); return m ? { x: +m[1], y: +m[2] } : null; };
  s.item = i => { const l = s.last(/^WT_HOME_TARGETS/); const m = l && new RegExp(` item${i}=(-?\\d+):(-?\\d+):(\\S+)`).exec(l); return m ? { x: +m[1], y: +m[2], id: m[3] } : null; };
  s.state = () => { const l = s.last(/^WT_HOME_STATE/); if (!l) return null; const g = k => { const m = new RegExp(` ${k}=(\\S+)`).exec(l); return m ? m[1] : null; }; return { room: +g('room'), deco: g('deco') === 'True', held: g('held'), x: +g('x'), z: +g('z'), rot: +g('rot'), check: g('check'), cursor: +g('cursor'), placed: +g('placed'), boxed: +g('boxed'), coins: +g('coins'), box: g('box') === 'True', colors: g('colors') === 'True' }; };
  s.button = label2 => { const l = s.last(/^WT_WORLD_BUTTONS/); const m = l && new RegExp(`${label2}:(-?\\d+):(-?\\d+)`).exec(l); return m ? { x: +m[1], y: +m[2] } : null; };
  s.click = async p => { await page.mouse.move(p.x, p.y); await sleep(140); await page.mouse.down(); await sleep(90); await page.mouse.up(); };
  s.cmd = c => page.evaluate(v => window.__wtInstance.SendMessage('WorldGame', 'TestHome', v), c);
  s.layout = async () => { const mark = s.lines.length; await s.cmd('layout'); const i = await s.wait(/^WT_HOME_LAYOUT /, 4000, mark); return i >= 0 ? JSON.parse(s.lines[i].slice('WT_HOME_LAYOUT '.length)) : null; };
  s.cell = async (x, z) => { const mark = s.lines.length; await s.cmd(`cell:${x}:${z}`); const i = await s.wait(/^WT_HOME_CELL /, 4000, mark); if (i < 0) return null; const p = s.lines[i].split(' '); return { x: +p[3], y: +p[4] }; };
  s.key = async k => { await page.keyboard.press(k); await sleep(130); };
  s.go = async () => {
    await page.goto(world.url + '?wt_test=1&' + query, { waitUntil: 'load' });
    const ok = await s.wait(/^WT_WORLD_GAME_READY/, 150000);
    if (ok >= 0) await page.evaluate(() => { const c = document.getElementById('unity-canvas'); if (c) c.focus(); });
    await sleep(800);
    return ok >= 0;
  };
  return s;
}

const put = home => (home && home.put) || [];
const find = (home, id) => put(home).find(p => p[0] === id);

async function inside(s) {
  check(await s.go(), 'the game is ready');
  check((await s.wait(/^WT_WORLD_ARRIVED place=coffee interior=home/, 60000)) >= 0, 'she arrives inside her home');
  await sleep(1200);
}

/** Holds a lifted piece to a cell with the arrow keys, reading where it is from the game's own log. */
async function steer(s, tx, tz) {
  for (let n = 0; n < 80; n++) {
    const st = s.state();
    if (st.x === tx && st.z === tz) return true;
    const mark = s.lines.length;
    await s.key(st.x < tx ? 'ArrowRight' : st.x > tx ? 'ArrowLeft' : st.z < tz ? 'ArrowUp' : 'ArrowDown');
    await s.wait(/^WT_HOME_STATE/, 1500, mark);
  }
  return false;
}

// ------------------------------------------------------------------------------------------------------ keyboard
if (only === '' || only === 'keyboard') {
  console.log('keyboard alone');
  const s = await open('keyboard', 'wt_goto=home');
  await inside(s);
  const home0 = (await s.save()).home;
  check(put(home0).length === 4, 'an old save starts with the four free pieces standing in her room: ' + JSON.stringify(put(home0).map(p => p[0])));
  check(!!find(home0, 'furn:bed_single') && !!find(home0, 'furn:rug_round') && !!find(home0, 'furn:lamp_floor') && !!find(home0, 'furn:riff_bed'), 'a bed, a rug, a lamp and riff\'s bed');
  await s.shot('start');
  await s.cmd('grant');
  await sleep(500);

  let mark = s.lines.length;
  await s.key('e');
  check((await s.wait(/^WT_HOME_DECO open/, 4000, mark)) >= 0, 'E opens decorating');
  await sleep(500);
  await s.shot('decorate_open');

  // Tab selects the rug, Tab again the bed
  await s.key('Tab'); await s.key('Tab');
  let st = s.state();
  check(st.cursor === 1, 'Tab twice selects the second piece, the bed (cursor ' + st.cursor + ')');
  const bed0 = find(await s.layout(), 'furn:bed_single');
  // the arrow lifts and moves it, Enter drops it
  mark = s.lines.length;
  await s.key('ArrowRight');
  st = s.state();
  check(st.held === 'furn:bed_single' && st.x === bed0[1] + 1, 'an arrow lifts the selected piece and moves it one cell: x ' + st.x);
  await s.key('ArrowUp'); await s.key('ArrowUp');
  await s.shot('moving_ghost');
  mark = s.lines.length;
  await s.key('Enter');
  check((await s.wait(/^WT_HOME_DROP result=Ok id=furn:bed_single/, 4000, mark)) >= 0, 'Enter drops it where the ghost is');
  await sleep(300);
  let bed1 = find(await s.layout(), 'furn:bed_single');
  check(bed1[1] === bed0[1] + 1 && bed1[2] === bed0[2] + 2, 'the bed is saved two cells back and one right: ' + JSON.stringify(bed1));

  // R turns it
  await s.key('r');
  st = s.state();
  check(st.held === 'furn:bed_single' && st.rot === 1, 'R turns the piece a quarter');
  mark = s.lines.length;
  await s.key('Enter');
  await s.wait(/^WT_HOME_DROP/, 4000, mark);
  bed1 = find(await s.layout(), 'furn:bed_single');
  check(bed1[3] === 1, 'the quarter turn is saved: rot ' + bed1[3]);

  // a red spot refuses: lift the lamp and drag it over the bed
  await s.key('Tab'); await s.key('Tab'); await s.key('Tab');   // rug, bed, lamp ... wraps from the bed (cursor 1) to the lamp
  st = s.state();
  const lampI = put(await s.layout()).findIndex(p => p[0] === 'furn:lamp_floor');
  while (s.state().cursor !== lampI) await s.key('Tab');
  await s.key('ArrowRight');
  check(s.state().held === 'furn:lamp_floor', 'the lamp is lifted');
  check(await steer(s, bed1[1] + 1, bed1[2] + 1), 'the lamp ghost is moved over the bed');
  check(s.state().check === 'Overlap', 'over the bed the spot is red (Overlap)');
  await s.shot('ghost_red');
  mark = s.lines.length;
  await s.key('Enter');
  check((await s.wait(/^WT_HOME_DROP result=Overlap/, 4000, mark)) >= 0, 'Enter on a red spot refuses and keeps holding');
  check(s.state().held === 'furn:lamp_floor', 'the lamp is still in her hand');
  check(await steer(s, 14, 20), 'the ghost moves to a free cell');
  check(s.state().check === 'Ok', 'a free cell is green (Ok)');
  await s.shot('ghost_green');
  mark = s.lines.length;
  await s.key('Enter');
  check((await s.wait(/^WT_HOME_DROP result=Ok id=furn:lamp_floor/, 4000, mark)) >= 0, 'Enter on a green spot drops the lamp');

  // Delete puts it away, the box gives it back
  mark = s.lines.length;
  await s.key('Delete');
  check((await s.wait(/^WT_HOME_AWAY id=furn:lamp_floor/, 4000, mark)) >= 0, 'Delete puts the selected piece away');
  check(put(await s.layout()).length === 3, 'it is out of the room (3 standing)');
  mark = s.lines.length;
  await s.key('b');
  check((await s.wait(/^WT_HOME_DECO box/, 4000, mark)) >= 0, 'B opens the box');
  await sleep(400);
  await s.key('ArrowUp'); await s.key('ArrowUp'); await s.key('ArrowUp');   // seating, tables, beds, lamps
  await sleep(300);
  await s.shot('box_open');
  mark = s.lines.length;
  await s.key('Enter');
  check((await s.wait(/^WT_HOME_DECO take id=furn:lamp_floor/, 4000, mark)) >= 0, 'Up three times then Enter takes the lamp out of the box');
  await s.key('Enter');
  check((await s.wait(/^WT_HOME_DROP result=Ok id=furn:lamp_floor/, 4000, mark)) >= 0, 'and Enter drops it at the free spot nearest the middle');

  // Backspace works as Delete, on a piece held
  await s.key('ArrowLeft');
  mark = s.lines.length;
  await s.key('Backspace');
  check((await s.wait(/^WT_HOME_AWAY id=furn:lamp_floor/, 4000, mark)) >= 0, 'Backspace puts away a piece in her hand');

  // colors
  mark = s.lines.length;
  await s.key('c');
  check((await s.wait(/^WT_HOME_DECO colors/, 4000, mark)) >= 0, 'C opens the colors');
  await s.key('ArrowRight'); await s.key('ArrowRight');
  await s.key('ArrowUp'); await s.key('ArrowUp'); await s.key('ArrowUp');
  await sleep(400);
  await s.shot('colors');
  await s.key('Escape');
  const lay = await s.layout();
  check(lay.wall === 1 && lay.floor === 2, 'right twice paints the wall (peach), up three times the floor (navy): wall ' + lay.wall + ' floor ' + lay.floor);

  // esc is done
  mark = s.lines.length;
  await s.key('Escape');
  check((await s.wait(/^WT_HOME_DECO close/, 4000, mark)) >= 0, 'Escape leaves decorating');
  const before = await s.save();
  await sleep(700);
  await s.shot('after');
  check(s.errs.length === 0, 'no console errors: ' + s.errs.slice(0, 2).join(' | '));
  const homeBefore = JSON.stringify(before.home);
  await s.ctx.close();

  // reload: the same layout
  const t = await open('keyboard2', 'wt_goto=home', `try { localStorage.setItem('worldtour.v1', ${JSON.stringify(JSON.stringify(before))}); } catch (e) {}`);
  await inside(t);
  const lay2 = await t.layout();
  const live = JSON.stringify(lay2);
  check(live === JSON.stringify({ ...before.home, box: before.home.box }) || (JSON.stringify(put(lay2)) === JSON.stringify(put(before.home)) && lay2.wall === 1 && lay2.floor === 2), 'after a reload the layout and colors are the same: ' + put(lay2).length + ' pieces');
  await t.shot('reloaded');
  check(t.errs.length === 0, 'no console errors after the reload: ' + t.errs.slice(0, 2).join(' | '));
  await t.ctx.close();
}

// ------------------------------------------------------------------------------------------------------ mouse
if (only === '' || only === 'mouse') {
  console.log('mouse alone');
  const s = await open('mouse', 'wt_goto=home');
  await inside(s);
  await s.cmd('grant');
  await sleep(500);
  const home0 = await s.layout();
  let mark = s.lines.length;
  await sleep(500);
  const dec = s.target('decorate');
  check(!!dec, 'the decorate button is on screen at ' + (dec ? dec.x + ',' + dec.y : '?'));
  await s.click(dec);
  check((await s.wait(/^WT_HOME_DECO open/, 4000, mark)) >= 0, 'a click on the decorate button opens decorating');
  await sleep(700);
  await s.shot('decorate_open');

  // click the riff bed, slide it, drop it
  const riffI = put(home0).findIndex(p => p[0] === 'furn:riff_bed');
  let it = s.item(riffI);
  check(!!it && it.id === 'furn:riff_bed', 'riff\'s bed shows on screen at ' + (it ? it.x + ',' + it.y : '?'));
  mark = s.lines.length;
  await s.click(it);
  check((await s.wait(/^WT_HOME_DECO lift id=furn:riff_bed/, 4000, mark)) >= 0, 'a click on a piece picks it up');
  const spot = await s.cell(8, 3);
  await s.page.mouse.move(spot.x, spot.y, { steps: 6 });
  await sleep(400);
  check(s.state().held === 'furn:riff_bed' && s.state().check === 'Ok', 'moving the pointer slides the ghost, green where it fits (x ' + s.state().x + ', z ' + s.state().z + ')');
  await s.shot('ghost_follows_pointer');
  mark = s.lines.length;
  await s.click(spot);
  check((await s.wait(/^WT_HOME_DROP result=Ok id=furn:riff_bed/, 4000, mark)) >= 0, 'a click on a cell drops it there');
  let riff = find(await s.layout(), 'furn:riff_bed');
  check(Math.abs(riff[1] - 7) <= 1 && Math.abs(riff[2] - 2) <= 1, 'it is saved where she dropped it: ' + JSON.stringify(riff));

  // a red spot: pick it up again and click on the bed
  const bedI = put(await s.layout()).findIndex(p => p[0] === 'furn:bed_single');
  it = s.item(bedI);
  mark = s.lines.length;
  await s.click(s.item(put(await s.layout()).findIndex(p => p[0] === 'furn:riff_bed')));
  await s.wait(/^WT_HOME_DECO lift id=furn:riff_bed/, 4000, mark);
  const onBed = await s.cell(4, 18);
  await s.page.mouse.move(onBed.x, onBed.y, { steps: 6 });
  await sleep(400);
  check(s.state().check === 'Overlap', 'over the bed the ghost is red');
  await s.shot('ghost_red');
  mark = s.lines.length;
  await s.click(onBed);
  check((await s.wait(/^WT_HOME_DROP result=Overlap/, 4000, mark)) >= 0, 'a click on a red spot refuses');
  const t = s.target('bar_turn');
  mark = s.lines.length;
  await s.click(t);
  check((await s.wait(/^WT_HOME_DECO turn rot=1/, 4000, mark)) >= 0, 'the turn button turns the piece in her hand');
  // put it back somewhere free with a click and then take it away with the button
  const free = await s.cell(18, 9);
  await s.page.mouse.move(free.x, free.y, { steps: 6 });
  await sleep(300);
  mark = s.lines.length;
  await s.click(free);
  check((await s.wait(/^WT_HOME_DROP result=Ok id=furn:riff_bed/, 4000, mark)) >= 0, 'a click on a free cell drops it, turned');
  check(find(await s.layout(), 'furn:riff_bed')[3] === 1, 'the turn is saved');
  mark = s.lines.length;
  await s.click(s.item(put(await s.layout()).findIndex(p => p[0] === 'furn:riff_bed')));
  await s.wait(/^WT_HOME_DECO lift id=furn:riff_bed/, 4000, mark);
  mark = s.lines.length;
  await s.click(s.target('bar_away'));
  check((await s.wait(/^WT_HOME_AWAY id=furn:riff_bed/, 4000, mark)) >= 0, 'the put away button puts it in the box');
  check(!find(await s.layout(), 'furn:riff_bed'), 'it is out of the room');

  // the box: lamps group, a piece, drop
  mark = s.lines.length;
  await s.click(s.target('bar_box'));
  check((await s.wait(/^WT_HOME_DECO box/, 4000, mark)) >= 0, 'the box button opens the box');
  await sleep(600);
  mark = s.lines.length;
  await s.click(s.target('cat_riff'));
  await sleep(600);
  await s.shot('box_open');
  mark = s.lines.length;
  await s.click(s.target('piece_0'));
  check((await s.wait(/^WT_HOME_DECO take id=furn:riff_bed/, 4000, mark)) >= 0, 'a click on the riff group, then on riff\'s bed, takes it out');
  const spot2 = await s.cell(16, 6);
  await s.page.mouse.move(spot2.x, spot2.y, { steps: 6 });
  await sleep(300);
  mark = s.lines.length;
  await s.click(spot2);
  check((await s.wait(/^WT_HOME_DROP result=Ok id=furn:riff_bed/, 4000, mark)) >= 0, 'and a click drops it');

  // colors by swatch
  mark = s.lines.length;
  await s.click(s.target('bar_colors'));
  check((await s.wait(/^WT_HOME_DECO colors/, 4000, mark)) >= 0, 'the colors button opens the palette');
  await sleep(500);
  mark = s.lines.length;
  await s.click(s.target('wall_11'));
  await s.wait(/^WT_HOME_COLOR wall=11/, 4000, mark);
  await sleep(300);
  mark = s.lines.length;
  await s.click(s.target('floor_20'));
  await s.wait(/^WT_HOME_COLOR wall=11 floor=20/, 4000, mark);
  await sleep(500);
  await s.shot('colors');
  const lay = await s.layout();
  check(lay.wall === 11 && lay.floor === 20, 'a click on a swatch paints the wall (mint) and the floor (white): ' + lay.wall + '/' + lay.floor);
  mark = s.lines.length;
  await s.click(s.target('bar_done'));
  await s.wait(/^WT_HOME_DECO close/, 4000, mark);
  await sleep(600);
  await s.shot('after');
  check(s.errs.length === 0, 'no console errors: ' + s.errs.slice(0, 2).join(' | '));
  const before = await s.save();
  await s.ctx.close();

  const r = await open('mouse2', 'wt_goto=home', `try { localStorage.setItem('worldtour.v1', ${JSON.stringify(JSON.stringify(before))}); } catch (e) {}`);
  await inside(r);
  const lay2 = await r.layout();
  check(JSON.stringify(put(lay2)) === JSON.stringify(put(before.home)) && lay2.wall === 11 && lay2.floor === 20, 'after a reload the layout and colors are the same');
  await r.ctx.close();
}

// ------------------------------------------------------------------------------------------------------ the two routes in
if (only === '' || only === 'routes') {
  console.log('the door on the street, by mouse');
  const s = await open('door', '');
  check(await s.go(), 'the game is ready');
  await sleep(1500);
  let mark = s.lines.length;
  const door = s.target('home_door');
  check(!!door, 'the home door is on screen on the coffee street at ' + (door ? door.x + ',' + door.y : '?'));
  await s.shot('street_door');
  await s.click(door);
  check((await s.wait(/^WT_HOME_DOOR ask=enter/, 25000, mark)) >= 0, 'a click on the door walks her up and asks "go in"');
  await sleep(900);
  await s.shot('door_card');
  const b = s.button('go_in');
  mark = s.lines.length;
  await s.click(b);
  check((await s.wait(/^WT_WORLD_ARRIVED place=coffee interior=home/, 30000, mark)) >= 0, 'a click on "go in" takes her home');
  await sleep(1500);
  await s.shot('inside');
  // out by the mat and back on the street by the door
  mark = s.lines.length;
  await s.page.keyboard.down('ArrowDown');
  const ask = await s.wait(/^WT_HOME_DOOR ask=leave/, 20000, mark);
  await s.page.keyboard.up('ArrowDown');
  check(ask >= 0, 'walking onto the mat asks "go out"');
  await sleep(700);
  mark = s.lines.length;
  await s.key('Enter');
  check((await s.wait(/^WT_WORLD_ARRIVED place=coffee interior=none/, 30000, mark)) >= 0, 'Enter on "go out" puts her back on the street');
  check(s.errs.length === 0, 'no console errors: ' + s.errs.slice(0, 2).join(' | '));
  await s.ctx.close();

  console.log('the tour map, by keyboard');
  const m = await open('map', '');
  check(await m.go(), 'the game is ready');
  await sleep(1500);
  mark = m.lines.length;
  // from the spawn down to the road's far side, then east to the exit spot
  await m.page.keyboard.down('ArrowDown'); await sleep(450); await m.page.keyboard.up('ArrowDown');
  await m.page.keyboard.down('ArrowRight');
  const card = await m.wait(/^WT_WORLD_MAP/, 12000, mark);
  await m.page.keyboard.up('ArrowRight');
  check(card >= 0, 'walking to the exit opens the tour map');
  await sleep(900);
  await m.shot('map_card');
  const home = m.button('go_home');
  check(!!home, 'the tour map has a "go home" button');
  mark = m.lines.length;
  if (home) await m.click(home);
  check((await m.wait(/^WT_WORLD_ARRIVED place=coffee interior=home/, 30000, mark)) >= 0, 'a click on "go home" takes her home');
  check(m.errs.length === 0, 'no console errors: ' + m.errs.slice(0, 2).join(' | '));
  await m.ctx.close();
}

// ------------------------------------------------------------------------------------------------------ more rooms
if (only === '' || only === 'rooms') {
  console.log('the other rooms');
  const s = await open('rooms', 'wt_goto=home&wt_coins=300');
  await inside(s);
  let mark = s.lines.length;
  // walk east to the den's door (it is locked: 200 coins)
  await s.page.keyboard.down('ArrowRight');
  const ask = await s.wait(/^WT_HOME_DOOR ask=buy room=1 price=200/, 15000, mark);
  await s.page.keyboard.up('ArrowRight');
  check(ask >= 0, 'the east door is locked and asks 200 coins for the den');
  await sleep(900);
  await s.shot('den_door_card');
  mark = s.lines.length;
  await s.key('Enter');
  check((await s.wait(/^WT_HOME_BUY room=1/, 5000, mark)) >= 0, 'Enter buys the den');
  check((await s.wait(/^WT_HOME_DOOR through room=1/, 8000, mark)) >= 0, 'and she walks through into it');
  await sleep(900);
  await s.shot('den');
  const save = await s.save();
  check(save.fans.coins <= 150 && save.home.rooms && save.home.rooms.includes(1), 'the coins were taken and the den is in the save: coins ' + save.fans.coins);
  check(save.fans.lifetime === 0, 'fans are never spent');
  await s.cmd('grant');
  await sleep(300);
  await s.key('e'); await sleep(400);
  await s.key('b'); await sleep(300);
  mark = s.lines.length;
  await s.key('Enter');
  check((await s.wait(/^WT_HOME_DECO take/, 4000, mark)) >= 0, 'the den has the box too');
  await s.key('Enter');
  await s.wait(/^WT_HOME_DROP result=Ok/, 4000, mark);
  const lay = await s.layout();
  check(put(lay).some(p => p[4] === 1), 'a piece dropped in the den is saved with room 1: ' + JSON.stringify(put(lay).filter(p => p[4] === 1)));
  await s.shot('den_decorated');
  check(s.errs.length === 0, 'no console errors: ' + s.errs.slice(0, 2).join(' | '));
  await s.ctx.close();
}

// ------------------------------------------------------------------------------------------------------ buying in the box (10B.2 wrap-up)
if (only === '' || only === 'buy') {
  console.log('buying a piece in the box');
  const s = await open('buy', 'wt_goto=home&wt_coins=300');
  await inside(s);
  const save0 = await s.save();
  const coins0 = save0.fans.coins, fans0 = save0.fans.lifetime;
  check(coins0 >= 300, 'the coins hook gave her coins: ' + coins0);
  check(!find(save0.home, 'furn:sofa') && !((save0.home.box || []).some(b => b.startsWith('furn:sofa'))), 'she does not own a sofa yet');
  let mark = s.lines.length;
  await s.key('e');
  check((await s.wait(/^WT_HOME_DECO open/, 4000, mark)) >= 0, 'E opens decorating');
  await sleep(400);
  mark = s.lines.length;
  await s.key('b');
  check((await s.wait(/^WT_HOME_DECO box/, 4000, mark)) >= 0, 'B opens the box on the seating group');
  await sleep(500);
  await s.shot('box_for_sale');
  mark = s.lines.length;
  await s.key('Enter');   // the first seating piece, the sofa (90 coins): nothing of the group is in the box yet, so it is for sale
  check((await s.wait(/^WT_HOME_BUY id=furn:sofa result=Ok price=90/, 4000, mark)) >= 0, 'Enter on the sofa buys it');
  check((await s.wait(/^WT_HOME_DECO take id=furn:sofa/, 4000, mark)) >= 0, 'and she holds it, ready to place');
  await sleep(300);
  await s.shot('bought_held');
  mark = s.lines.length;
  await s.key('Enter');
  check((await s.wait(/^WT_HOME_DROP result=Ok id=furn:sofa/, 4000, mark)) >= 0, 'Enter places the sofa');
  await s.key('Escape');
  await sleep(500);
  const before = await s.save();
  check(before.fans.coins === coins0 - 90, 'the price came off her coins: ' + coins0 + ' -> ' + before.fans.coins);
  check(before.fans.lifetime === fans0, 'fans are never spent');
  check(!!find(before.home, 'furn:sofa'), 'the sofa is standing in the save');
  check(s.errs.length === 0, 'no console errors: ' + s.errs.slice(0, 2).join(' | '));
  await s.ctx.close();

  const r = await open('buy2', 'wt_goto=home', `try { localStorage.setItem('worldtour.v1', ${JSON.stringify(JSON.stringify(before))}); } catch (e) {}`);
  await inside(r);
  const after = await r.save();
  const sofa0 = find(before.home, 'furn:sofa'), sofa1 = find(after.home, 'furn:sofa');
  check(!!sofa1 && JSON.stringify(sofa1) === JSON.stringify(sofa0), 'after a reload the sofa still stands where she put it: ' + JSON.stringify(sofa1));
  check(after.fans.coins === coins0 - 90, 'and her coins are still lower: ' + after.fans.coins);
  await r.shot('bought_reloaded');
  check(r.errs.length === 0, 'no console errors after the reload: ' + r.errs.slice(0, 2).join(' | '));
  await r.ctx.close();
}

await browser.close(); await world.close();
console.log(fails.length ? 'FAILED: ' + fails.length : 'PASSED');
process.exit(fails.length ? 1 : 0);
