// Tester playthrough of the web build in tour/ (task 2.7): start card, map, coffee shop card, a full remix show
// with a deliberate miss, a hint and the encore, to both tickets and back to the map. Plain Node plus the Playwright
// install in world_tour/tests/e2e (read only).
//
//   node Tests/Web/playthrough.mjs --mode <mouse|keyboard|reduced|blocked> [--vw 1366] [--vh 768] [--shots <dir>] [--out <json>] [--site <folder>] [--theme peach-navy]
//
//   mouse     mouse only: tiles are found in a screenshot and read by matching them against Courier Prime Bold
//             (the bundled font), clicked one by one, one tile dragged onto the slots, the hint clicked; screenshots
//             of every screen.
//   keyboard  keyboard only: Enter, Tab, typing, Backspace and Delete; then reload to check the save, the unlock
//             moment shown once, and the mute toggle remembered.
//   reduced   keyboard run with prefers-reduced-motion: reduce.
//   blocked   keyboard run with localStorage and indexedDB throwing.
//
// The results file is the evidence: { pass, failures, errors, steps, numbers, shots }. Exits 1 on failure.
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
// --theme peach-navy (task 5.1): start from a save that owns and wears peach + navy (written only when storage is empty),
// and find the navy ink buttons (#1c2747) where the default theme's are black.
const theme = opt('--theme', 'tour-pink');
const vw = +opt('--vw', '1366'), vh = +opt('--vh', '768');
const shots = path.resolve(opt('--shots', path.join(project, 'Temp', 'playthrough')));
const outFile = path.resolve(opt('--out', path.join(shots, `play_${mode}_${vw}x${vh}.json`)));
const site = path.resolve(opt('--site', path.join(repo, 'tour')));
const keyboardMode = mode !== 'mouse';
const S = Math.sqrt((vw / 1366) * (vh / 768));        // CanvasScaler 1366x768, match 0.5
const fontB64 = fs.readFileSync(path.join(project, 'Assets', 'Fonts', 'CourierPrime', 'CourierPrime-Bold.ttf')).toString('base64');

const out = { mode, vw, vh, pass: false, failures: [], errors: [], warnings: [], steps: [], numbers: {}, shots: [], lines: [] };
const fail = m => { out.failures.push(m); console.log('FAIL ' + m); };
const step = m => { out.steps.push(m); console.log('  ' + m); };
fs.mkdirSync(shots, { recursive: true });

const STORAGE_BLOCKED = `(() => {
  const blocked = () => { throw new DOMException('storage blocked by test', 'SecurityError'); };
  try { IDBFactory.prototype.open = blocked; } catch (e) {}
  try { Object.defineProperty(window, 'localStorage', { configurable: true, get: blocked }); } catch (e) {}
})();`;

// ---------------------------------------------------------------- image analysis (in a separate blank page)

const ANALYSIS = `
window.loadFont = async (b64) => {
  const bytes = Uint8Array.from(atob(b64), c => c.charCodeAt(0));
  const f = new FontFace('CPB', bytes.buffer); await f.load(); document.fonts.add(f);
};
window.decode = async (b64) => {
  const img = new Image(); img.src = 'data:image/png;base64,' + b64; await img.decode();
  const c = document.createElement('canvas'); c.width = img.width; c.height = img.height;
  const g = c.getContext('2d'); g.drawImage(img, 0, 0);
  return g.getImageData(0, 0, c.width, c.height);
};
// connected components (4-neighbour) of pixels where pred(r,g,b) is true, inside region [x0,y0,x1,y1)
window.comps = (im, pred, region) => {
  const W = im.width, H = im.height, d = im.data;
  const [x0, y0, x1, y1] = region || [0, 0, W, H];
  const lab = new Int32Array(W * H), res = [];
  const st = [];
  for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) {
    const p = y * W + x;
    if (lab[p] || !pred(d[p * 4], d[p * 4 + 1], d[p * 4 + 2])) continue;
    let n = 0, minx = x, maxx = x, miny = y, maxy = y; const id = res.length + 1;
    lab[p] = id; st.push(p);
    while (st.length) {
      const q = st.pop(), qx = q % W, qy = (q / W) | 0; n++;
      if (qx < minx) minx = qx; if (qx > maxx) maxx = qx; if (qy < miny) miny = qy; if (qy > maxy) maxy = qy;
      for (const [nx, ny] of [[qx + 1, qy], [qx - 1, qy], [qx, qy + 1], [qx, qy - 1]]) {
        if (nx < x0 || ny < y0 || nx >= x1 || ny >= y1) continue;
        const r = ny * W + nx;
        if (!lab[r] && pred(d[r * 4], d[r * 4 + 1], d[r * 4 + 2])) { lab[r] = id; st.push(r); }
      }
    }
    res.push({ x: minx, y: miny, w: maxx - minx + 1, h: maxy - miny + 1, n });
  }
  return res;
};
const white = (r, g, b) => r > 225 && g > 225 && b > 225;
const dark = (r, g, b) => r + g + b < 330;
function maskOf(im, box, inset) {
  const W = im.width, d = im.data, pts = [];
  for (let y = box.y + inset; y < box.y + box.h - inset; y++) for (let x = box.x + inset; x < box.x + box.w - inset; x++) {
    const p = (y * W + x) * 4; if (dark(d[p], d[p + 1], d[p + 2])) pts.push([x, y]);
  }
  return pts;
}
function glyphMask(ch, px, angle) {
  const size = Math.ceil(px * 2.2), c = document.createElement('canvas'); c.width = c.height = size;
  const g = c.getContext('2d'); g.translate(size / 2, size / 2); g.rotate(angle * Math.PI / 180);
  g.font = 'bold ' + px + 'px CPB'; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillStyle = '#000'; g.fillText(ch, 0, 0);
  const d = g.getImageData(0, 0, size, size).data, pts = [];
  for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) if (d[(y * size + x) * 4 + 3] > 110) pts.push([x, y]);
  return pts;
}
function centred(pts) {
  let sx = 0, sy = 0; for (const [x, y] of pts) { sx += x; sy += y; }
  const cx = sx / pts.length, cy = sy / pts.length;
  return new Set(pts.map(([x, y]) => Math.round(x - cx) + ',' + Math.round(y - cy)));
}
const cache = {};
function score(tileSet, ch, px) {
  let best = 0;
  for (let a = -5; a <= 5; a += 1) {
    const k = ch + px + ':' + a;
    const gs = cache[k] || (cache[k] = centred(glyphMask(ch, px, a)));
    let inter = 0;
    for (const p of tileSet) {
      if (gs.has(p)) { inter++; continue; }
      const [x, y] = p.split(',').map(Number);   // one pixel of slack
      if (gs.has((x + 1) + ',' + y) || gs.has((x - 1) + ',' + y) || gs.has(x + ',' + (y + 1)) || gs.has(x + ',' + (y - 1))) inter += .7;
    }
    const iou = inter / (tileSet.size + gs.size - inter);
    if (iou > best) best = iou;
  }
  return best;
}
// the remix rack: white tile faces inside the play area, each read as a letter
window.findTiles = async (b64, S, vw, vh) => {
  const im = await decode(b64);
  const region = [Math.round(32 * S), Math.round(118 * S), Math.round(vw - (32 + 300) * S), vh];
  const cs = comps(im, white, region).filter(c => c.w > 36 * S && c.w < 58 * S && c.h > 42 * S && c.h < 64 * S && c.n / (c.w * c.h) > .55);
  // the slots, once filled, are white too; tiles are below them (the rack is the lowest rows)
  const letters = 'abcdefghijklmnopqrstuvwxyz';
  return cs.map(c => {
    const pts = maskOf(im, c, Math.round(3 * S));
    const set = pts.length ? centred(pts) : new Set();
    const scores = {};
    if (set.size) for (const ch of letters) scores[ch] = score(set, ch, Math.round(27 * S));
    return { x: c.x + c.w / 2, y: c.y + c.h / 2, w: c.w, h: c.h, top: c.y, bottom: c.y + c.h, scores };
  });
};
// sticker buttons: white faces of about 150x48 canvas units (ghost buttons) in a region
window.findBoxes = async (b64, region, minW, maxW, minH, maxH, colour) => {
  const im = await decode(b64);
  const pred = colour === 'black' ? ((r, g, b) => r + g + b < (window.INK_SUM || 60)) : white;
  return comps(im, pred, region).filter(c => c.w >= minW && c.w <= maxW && c.h >= minH && c.h <= maxH && c.n / (c.w * c.h) > .5)
    .map(c => ({ x: c.x + c.w / 2, y: c.y + c.h / 2, w: c.w, h: c.h, top: c.y, bottom: c.y + c.h }));
};
// how many pixels of a colour (within tol) are in a box
window.countColour = async (b64, box, rgb, tol) => {
  const im = await decode(b64), W = im.width, d = im.data; let n = 0;
  for (let y = Math.max(0, box[1]); y < Math.min(im.height, box[3]); y++) for (let x = Math.max(0, box[0]); x < Math.min(W, box[2]); x++) {
    const p = (y * W + x) * 4;
    if (Math.abs(d[p] - rgb[0]) <= tol && Math.abs(d[p + 1] - rgb[1]) <= tol && Math.abs(d[p + 2] - rgb[2]) <= tol) n++;
  }
  return n;
};`;

/** Assigns letters to tiles: the word's letters first (by best score), the rest are decoys. */
function assign(tiles, word) {
  const need = {}; for (const c of word) need[c] = (need[c] || 0) + 1;
  const cand = [];
  tiles.forEach((t, i) => { for (const [c, s] of Object.entries(t.scores)) cand.push({ i, c, s }); });
  cand.sort((a, b) => b.s - a.s);
  const letter = new Array(tiles.length).fill(null);
  for (const k of cand) if (letter[k.i] === null && need[k.c] > 0) { letter[k.i] = k.c; need[k.c]--; }
  return letter;   // null = a decoy (or unread)
}
function order(letters, word) {
  const used = new Set(), seq = [];
  for (const c of word) { const i = letters.findIndex((l, k) => l === c && !used.has(k)); if (i < 0) return null; used.add(i); seq.push(i); }
  return seq;
}

// ---------------------------------------------------------------- run

let browser, server;
const t0 = Date.now();
try {
  server = await startServer(site);
  browser = await chromium.launch({ headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
  const ctx = await browser.newContext({ viewport: { width: vw, height: vh }, reducedMotion: mode === 'reduced' ? 'reduce' : 'no-preference' });
  const page = await ctx.newPage();
  if (mode === 'blocked') await page.addInitScript(STORAGE_BLOCKED);
  if (theme === 'peach-navy') await page.addInitScript(() => {
    try {
      if (!localStorage.getItem('worldtour.v1')) localStorage.setItem('worldtour.v1', JSON.stringify({ v: 1, inventory: { owned: ['theme:tour-pink', 'guitar:classic', 'dog:none', 'theme:peach-navy'], equipped: { theme: 'peach-navy', guitar: 'classic', dog: 'none' } } }));
    } catch (e) { /* storage blocked */ }
  });
  const tool = await (await browser.newContext()).newPage();
  await tool.setContent('<html><body></body></html>');
  await tool.addScriptTag({ content: ANALYSIS });
  await tool.evaluate(b => window.loadFont(b), fontB64);
  if (theme === 'peach-navy') await tool.evaluate(() => { window.INK_SUM = 170; });

  let lines = [];
  page.on('console', m => {
    const t = m.text().trim(); lines.push({ t, at: Date.now() - t0, type: m.type() });
    if (m.type() === 'error') out.errors.push(t);
    if (m.type() === 'warning') out.warnings.push(t);
  });
  page.on('pageerror', e => out.errors.push('pageerror: ' + e.message));
  const has = re => lines.find(l => re.test(l.t));
  const count = re => lines.filter(l => re.test(l.t)).length;
  const wait = async (re, ms = 20000, from = 0) => {
    const end = Date.now() + ms;
    for (;;) { const l = lines.slice(from).find(x => re.test(x.t)); if (l) return l.t; if (Date.now() > end) { step('MISSING ' + re); return null; } await page.waitForTimeout(40); }
  };
  const mark = () => lines.length;
  const png = async () => (await page.screenshot()).toString('base64');
  const shot = async name => { const p = path.join(shots, `${name}_${vw}x${vh}${mode === 'mouse' ? '' : '_' + mode}.png`); await page.screenshot({ path: p }); out.shots.push(p); };
  // like a person: arrive, settle for a frame or two, then press (SwiftShader frames are slow at 1920)
  const click = async (x, y) => { await page.mouse.move(Math.round(x), Math.round(y)); await page.waitForTimeout(40); await page.mouse.down(); await page.waitForTimeout(+(process.env.WT_HOLD_MS || 30)); await page.mouse.up(); };
  const key = async k => { await page.keyboard.press(k); await page.waitForTimeout(90); };
  // a real mouse wheel: one notch (100 px) at a time, like a person scrolling
  const wheel = async (notches) => { await page.mouse.move(vw / 2, vh / 2); for (let n = 0; n < Math.abs(notches); n++) { await page.mouse.wheel(0, Math.sign(notches) * 100); await page.waitForTimeout(25); } await page.waitForTimeout(700); };

  // ---- load
  const nav = Date.now();
  await page.goto(server.url, { waitUntil: 'load' });
  if (!await wait(/^WT_START_SHOWN/, 60000)) throw new Error('no start card');
  out.numbers.startShownMs = Date.now() - nav;
  out.numbers.bytes = server.bytesServed;
  const largest = server.files.reduce((a, f) => (!a || f.bytes > a.bytes ? f : a), null);
  out.numbers.largest = largest ? { file: largest.path || largest.url || largest.name, bytes: largest.bytes } : null;
  const sound0 = has(/^WT_SOUND_UNLOCKED/);
  if (sound0) fail('sound unlocked before any click');
  await page.waitForTimeout(500);
  await shot('start');

  // ---- start
  if (keyboardMode) { await page.focus('#unity-canvas').catch(() => {}); await page.evaluate(() => document.getElementById('unity-canvas').focus()); await key('Enter'); }
  else {
    const b = await tool.evaluate(([p, vw, vh]) => findBoxes(p, [0, 0, vw, vh], 60, 600, 30, 200, 'black'), [await png(), vw, vh]);
    b.sort((a, c) => c.w * c.h - a.w * a.h);
    if (!b.length) throw new Error('no start button found');
    step(`start button at ${Math.round(b[0].x)},${Math.round(b[0].y)}`);
    await click(b[0].x, b[0].y);
  }
  if (!await wait(/^WT_STARTED/, 5000)) throw new Error('start did nothing');
  if (!has(/^WT_SOUND_UNLOCKED/)) fail('sound not unlocked by the start click');
  const mainLine = await wait(/^WT_MAIN_READY/, 20000);
  out.numbers.mainReady = mainLine;
  if (mode === 'reduced' && !/reduced=true/.test(mainLine || '')) fail('reduce motion not picked up: ' + mainLine);
  if (theme === 'peach-navy' && !/theme=peach-navy/.test(mainLine || '')) fail('not in peach + navy: ' + mainLine);
  const saveLine = has(/^WT_SAVE_READY/);
  out.numbers.saveReady = saveLine && saveLine.t;
  if (mode === 'blocked' && !/persistent=false/.test(saveLine ? saveLine.t : '')) fail('storage blocked but persistent: ' + (saveLine && saveLine.t));
  if (!await wait(/^WT_MAP_READY/)) throw new Error('no map');
  await page.waitForTimeout(700);
  await shot('map');
  if (mode === 'mouse') {
    await wheel(4); await shot('map_scrolled');
    await wheel(40); await shot('map_bottom');
    await wheel(-60);
  }

  // ---- venue card
  let m0 = mark();
  if (keyboardMode) await key('Enter');                               // "tonight" has focus
  else {
    // the "tonight" button: the black sticker in the hero (left half, top half)
    const b = await tool.evaluate(([p, r, S]) => findBoxes(p, r, 150 * S, 420 * S, 40 * S, 90 * S, 'black'), [await png(), [0, 0, Math.round(vw / 2), Math.round(vh * .6)], S]);
    if (!b.length) throw new Error('no tonight button found');
    await click(b[0].x, b[0].y);
  }
  if (!await wait(/^WT_VENUE_OPEN id=coffee/, 5000, m0)) throw new Error('no venue card');
  await page.waitForTimeout(600);
  await shot('venue');

  // ---- start remix
  m0 = mark();
  // Since task 3.1 the first row is missing letters, not remix: pick Mode_remix by name.
  if (keyboardMode) {
    // Tab until the focus log names Mode_remix (third of seven rows), then Enter
    let ok = false;
    for (let t = 0; t < 14 && !ok; t++) { const mt = mark(); await key('Tab'); const f = await wait(/^WT_FOCUS name=/, 800, mt); if (f && f.endsWith('name=Mode_remix')) ok = true; }
    if (!ok) throw new Error('Tab never reached Mode_remix');
    await key('Enter');
  } else {
    // pixels carry no names: Mode_remix is the third white row (rows sorted top to bottom); the show request
    // below must then name mode=remix, so a click on any other row fails here instead of playing the wrong mode
    const b = await tool.evaluate(([p, r, S]) => findBoxes(p, r, 500 * S, 800 * S, 60 * S, 100 * S, 'white'), [await png(), [0, Math.round(vh * .3), vw, vh], S]);
    b.sort((a, c) => a.y - c.y);
    if (b.length < 3) throw new Error('no remix row found (' + b.length + ' rows)');
    await click(b[2].x - b[2].w * .3, b[2].y);
  }
  const request = await wait(/^WT_SHOW_REQUEST /, 5000, m0);
  if (!request) throw new Error('no show request');
  if (!/ mode=remix$/.test(request)) throw new Error('the row picked was not Mode_remix: ' + request);
  if (!await wait(/^WT_SHOW_START/, 5000, m0)) throw new Error('no show');

  // ---- the show
  let ticketLine = null;
  const plays = [];
  for (let k = 0; k < 16; k++) {
    const m0song = mark();
    const songLine = await wait(new RegExp(`^WT_SONG i=${k + 1} word=`), 20000);
    if (!songLine) break;
    const word = songLine.split('word=')[1];
    const resultRe = new RegExp(`^WT_SONG_RESULT i=${k + 1} `);
    await page.waitForTimeout(450);
    if (k === 0) await shot('show_song1');
    let tiles = null, letters = null, seq = null;
    if (!keyboardMode) {
      tiles = await tool.evaluate(([p, S, vw, vh]) => findTiles(p, S, vw, vh), [await png(), S, vw, vh]);
      // rack order is row by row, left to right; WT_RACK gives the letters in that order
      tiles.sort((a, b) => (Math.abs(a.y - b.y) > 15 * S ? a.y - b.y : a.x - b.x));
      const rackLine = await wait(/^WT_RACK letters=/, 3000, m0song);
      const rack = rackLine ? rackLine.split('letters=')[1].split(' ')[0] : null;
      const ocr = assign(tiles, word);
      if (rack && rack.length === tiles.length) {
        letters = [...rack];
        const agree = ocr.filter((l, q) => l === letters[q]).length;
        out.numbers.ocrAgree = (out.numbers.ocrAgree || 0) + agree; out.numbers.ocrTiles = (out.numbers.ocrTiles || 0) + tiles.length;
      } else { step(`song ${k + 1}: WT_RACK ${rack} vs ${tiles.length} tiles found; using OCR`); letters = ocr; }
      seq = order(letters, word);
      if (!seq) { fail(`song ${k + 1}: could not read the rack for "${word}" (${tiles.length} tiles)`); break; }
    }
    const spell = async (s, from = 0, to = undefined) => {
      if (keyboardMode) { await page.keyboard.type(s.slice(from, to), { delay: 70 }); return; }
      for (const i of seq.slice(from, to)) { await click(tiles[i].x, tiles[i].y); await page.waitForTimeout(60); }
    };
    const buttons = async () => {
      const top = tiles ? Math.max(...tiles.map(t => t.bottom)) : Math.round(vh * .5);
      const b = await tool.evaluate(([p, r, S]) => findBoxes(p, r, 120 * S, 175 * S, 34 * S, 60 * S, 'white'), [await png(), [0, top + 10, Math.round(vw - 330 * S), vh], S]);
      b.sort((a, c) => a.x - c.x);
      return b;   // undo, clear, hint
    };
    const m = mark();
    if (k === 0) {
      // deliberate miss, then the right word (mouse: first tile dragged onto the slots)
      if (keyboardMode) await page.keyboard.type([...word].reverse().join('') === word ? word.slice(1) + word[0] : [...word].reverse().join(''), { delay: 70 });
      else {
        const wrong = seq.slice(); const j = wrong.findIndex((t, q) => q > 0 && letters[t] !== letters[wrong[0]]); [wrong[0], wrong[j]] = [wrong[j], wrong[0]];
        for (const i of wrong) { await click(tiles[i].x, tiles[i].y); await page.waitForTimeout(60); }
        // clicks during the wobble (must be ignored, not break anything)
        await click(tiles[seq[0]].x, tiles[seq[0]].y);
      }
      await page.waitForTimeout(250); await shot('show_after_miss_wobble');
      await page.waitForTimeout(700); await shot('show_after_miss');
      if (keyboardMode) await spell(word);
      else {
        const t = tiles[seq[0]], slotY = Math.min(...tiles.map(q => q.top)) - 14 * S - 30 * S;
        await page.mouse.move(t.x, t.y); await page.mouse.down(); await page.waitForTimeout(120); await page.mouse.move(vw * .4, slotY, { steps: 6 }); await page.mouse.move(t.x, slotY, { steps: 6 }); await page.mouse.up();
        await page.waitForTimeout(300);
        await shot('show_after_drag');
        await spell(word, 1);
      }
    } else if (k === 1) {
      // undo and clear
      if (keyboardMode) { await page.keyboard.type(word.slice(0, 3), { delay: 70 }); await key('Backspace'); await key('Delete'); await page.waitForTimeout(200); await shot('show_after_undo_clear'); await spell(word); }
      else {
        await click(tiles[seq[0]].x, tiles[seq[0]].y); await click(tiles[seq[1]].x, tiles[seq[1]].y); await click(tiles[seq[2]].x, tiles[seq[2]].y);
        await page.waitForTimeout(250);
        const b = await buttons();
        if (b.length < 3) fail(`song 2: found ${b.length} control buttons, expected 3`);
        else { await click(b[0].x, b[0].y); await page.waitForTimeout(350); await click(b[1].x, b[1].y); await page.waitForTimeout(350); }
        await shot('show_after_undo_clear');
        await spell(word);
      }
    } else if (k === 2) {
      // a hint (spam-clicked); keyboard: Tab to the hint button and press Enter
      if (keyboardMode) {
        const b = await buttons();
        const hint = b[2];
        let focused = false;
        for (let n = 0; n < 30 && hint; n++) {
          await key('Tab'); await page.waitForTimeout(80);
          // the focus ring is accent-2; in peach + navy that is the navy of every outline, so there the focus log decides
          const ring = theme === 'peach-navy'
            ? ((lines.filter(l => l.t.startsWith('WT_FOCUS ')).pop() || {}).t === 'WT_FOCUS name=Hint' ? 999 : 0)
            : await tool.evaluate(([p, bx]) => countColour(p, bx, [47, 47, 193], 30), [await png(), [Math.round(hint.x - hint.w / 2 - 12), Math.round(hint.y - hint.h / 2 - 12), Math.round(hint.x + hint.w / 2 + 12), Math.round(hint.y + hint.h / 2 + 12)]]);
          if (ring > 60) { focused = true; step(`hint focused after ${n + 1} Tab presses`); break; }
        }
        if (!focused) fail('keyboard: Tab never reached the hint button (no focus ring seen)');
        await key('Enter'); await key('Enter');
      } else {
        const b = await buttons();
        if (b.length < 3) fail(`song 3: found ${b.length} control buttons, expected 3`);
        else { const h = b[2]; await click(h.x, h.y); await click(h.x, h.y); await page.mouse.dblclick(h.x, h.y); }
      }
      await page.waitForTimeout(400); await shot('show_after_hint');
      if (keyboardMode) await page.evaluate(() => document.getElementById('unity-canvas').focus());
      await spell(word);
    } else if (k === 3) {
      // mid-show shot; spam the last tile
      await spell(word, 0, word.length - 1);
      await page.waitForTimeout(200); await shot('show_mid');
      if (keyboardMode) { await page.keyboard.type(word.slice(-1)); await page.keyboard.type(word.slice(-1)); await page.keyboard.type(word.slice(-1)); }
      else { const t = tiles[seq[seq.length - 1]]; await page.mouse.dblclick(t.x, t.y); await click(t.x, t.y); }
    } else {
      await spell(word);
    }
    const res = await wait(resultRe, 6000, m);
    if (!res) { await shot(`stuck_song${k + 1}`); out.stuckTail = lines.slice(m).map(l => l.at + 'ms ' + l.t).filter(t => /WT_|\[/.test(t)); out.stuckSeq = seq && seq.map(i => [i, letters[i], Math.round(tiles[i].x), Math.round(tiles[i].y)]); fail(`song ${k + 1} ("${word}") never resolved`); break; }
    plays.push({ i: k + 1, word, result: res, bonus: /bonus/.test(word) });
    await page.waitForTimeout(80);
    if (count(resultRe) > 1) fail(`song ${k + 1}: ${count(resultRe)} results for one song`);
    ticketLine = has(/^WT_TICKET_SHOWN/);
    if (has(/^WT_SHOW_DONE/)) break;
  }
  out.plays = plays;
  const kinds = plays.map(p => (p.result || '').split('kind=')[1]);
  step('results: ' + kinds.join(','));
  if (kinds[0] !== 'wrong') fail('song 1 should be a win after a miss, got ' + kinds[0]);
  if (kinds[2] !== 'hinted') fail('song 3 should be hinted, got ' + kinds[2]);
  if (kinds.filter((x, q) => q !== 0 && q !== 2 && x !== 'clean').length) fail('unexpected results: ' + kinds.join(','));
  if (plays[4] && plays[4].word !== plays[0].word) fail(`song 5 should be the missed word again (${plays[0].word}), got ${plays[4].word}`);

  // ---- ticket
  if (!await wait(/^WT_TICKET_SHOWN/, 10000)) throw new Error('no ticket');
  ticketLine = has(/^WT_TICKET_SHOWN/).t;
  out.numbers.ticket = ticketLine;
  if (!/encore=true/.test(ticketLine)) fail('the first ticket should offer the encore: ' + ticketLine);
  await page.waitForTimeout(1100);
  await shot('ticket_top');
  await wheel(60);
  await shot('ticket_bottom');

  m0 = mark();
  if (keyboardMode) { await page.evaluate(() => document.getElementById('unity-canvas').focus()); await key('Enter'); }   // the encore button has focus
  else {
    const b = await tool.evaluate(([p, r, S]) => findBoxes(p, r, 150 * S, 500 * S, 40 * S, 90 * S, 'black'), [await png(), [0, Math.round(vh * .3), vw, vh], S]);
    if (!b.length) throw new Error('no encore button found');
    b.sort((a, c) => c.y - a.y);
    await page.mouse.dblclick(b[0].x, b[0].y);                        // a double click must open one encore
  }
  if (!await wait(/^WT_TICKET_ACTION action=encore/, 5000, m0)) throw new Error('encore did nothing');
  const enc = await wait(/^WT_SHOW_START/, 5000, m0);
  if (count(/^WT_SHOW_START/) !== 2) fail(`${count(/^WT_SHOW_START/)} shows started, expected 2 (a double click opened two encores?)`);
  const encSong = await wait(/^WT_SONG i=1 word=/, 8000, m0);
  const encWord = encSong ? encSong.split('word=')[1] : null;
  if (encWord !== plays[0].word) fail(`the encore song is ${encWord}, expected ${plays[0].word}`);
  await page.waitForTimeout(450);
  await shot('encore_song');
  if (keyboardMode) await page.keyboard.type(encWord, { delay: 70 });
  else {
    const tiles = await tool.evaluate(([p, S, vw, vh]) => findTiles(p, S, vw, vh), [await png(), S, vw, vh]);
    tiles.sort((a, b) => (Math.abs(a.y - b.y) > 15 * S ? a.y - b.y : a.x - b.x));
    const rl = lines.slice(m0).find(l => /^WT_RACK letters=/.test(l.t));
    const rk = rl ? rl.t.split('letters=')[1].split(' ')[0] : null;
    const seq = order(rk && rk.length === tiles.length ? [...rk] : assign(tiles, encWord), encWord);
    for (const i of seq) { await click(tiles[i].x, tiles[i].y); await page.waitForTimeout(60); }
  }
  const t2 = await wait(/^WT_TICKET_SHOWN/, 10000, m0);
  if (!t2) throw new Error('no encore ticket');
  out.numbers.encoreTicket = t2;
  if (!/encore=false/.test(t2)) fail('the encore ticket should offer play again: ' + t2);
  await page.waitForTimeout(1100);
  await shot('encore_ticket_top');
  await wheel(60);
  await shot('encore_ticket_bottom');

  // ---- back to the map
  m0 = mark();
  if (keyboardMode) {
    await page.evaluate(() => document.getElementById('unity-canvas').focus());
    let ok = false;
    for (const k of ['Tab', 'ArrowRight', 'ArrowDown']) {
      const mm = mark(); await key(k); await key('Enter');
      if (await wait(/^WT_TICKET_ACTION action=map/, 1200, mm)) { ok = true; step(`back to the map reached with ${k}`); break; }
      if (lines.slice(mm).some(l => /^WT_TICKET_ACTION action=again/.test(l.t))) { fail(`keyboard: ${k} then Enter pressed play again, not back to the map`); break; }
    }
    if (!ok) fail('keyboard: could not reach "back to the map"');
  } else {
    const b = await tool.evaluate(([p, r, S]) => findBoxes(p, r, 150 * S, 500 * S, 40 * S, 90 * S, 'white'), [await png(), [0, Math.round(vh * .3), vw, vh], S]);
    b.sort((a, c) => c.y - a.y || c.x - a.x);
    if (!b.length) fail('no back-to-the-map button found'); else await click(b[0].x, b[0].y);
  }
  if (await wait(/^WT_MAP_READY/, 5000, m0)) {
    await wait(/^WT_UNLOCK_SHOWN/, 3000, m0);
    await page.waitForTimeout(900);
    await shot('map_after_show');
    out.numbers.unlockShown = count(/^WT_UNLOCK_SHOWN/);
    if (count(/^WT_UNLOCK_SHOWN/) !== 1) fail(`unlock moment shown ${count(/^WT_UNLOCK_SHOWN/)} times`);
  } else fail('never got back to the map');

  // ---- keyboard: the mute toggle, then reload: save, unlock once, mute remembered
  if (mode === 'keyboard' || mode === 'reduced') {
    const mapLine = lines.filter(l => /^WT_MAP_READY/.test(l.t)).pop().t;
    out.numbers.mapAfter = mapLine;
    const before = lines.length;
    const toggle = has(/^WT_SOUND_TOGGLE/);
    step('mute toggle line before reload: ' + (toggle ? toggle.t : 'none'));
    lines = [];
    await page.reload({ waitUntil: 'load' });
    if (!await wait(/^WT_START_SHOWN/, 60000)) fail('no start card after reload');
    const sv = await wait(/^WT_SAVE_READY/, 1000);
    out.numbers.saveAfterReload = sv;
    const fans = +((mapLine.match(/fans=(\d+)/) || [])[1] || -1);
    if (!sv || !new RegExp(`fans=${fans}\\b`).test(sv)) fail(`after reload the save says ${sv}, the map had fans=${fans}`);
    if (!/unlocked=2/.test(sv || '')) fail('after reload the record store is not unlocked: ' + sv);
    if (mode === 'keyboard') {
      await page.evaluate(() => document.getElementById('unity-canvas').focus()); await key('Enter');
      await wait(/^WT_MAP_READY/, 20000); await page.waitForTimeout(1200);
      if (out.numbers.unlockShown === 1 && count(/^WT_UNLOCK_SHOWN/)) fail('the unlock moment played again after the reload');
      await shot('map_after_reload');
    }
  }
  out.pass = out.failures.length === 0 && out.errors.length === 0;
} catch (e) {
  fail('driver: ' + e.message);
} finally {
  if (browser) await browser.close();
  if (server) await server.close();
  out.lines = undefined;
  out.pass = out.failures.length === 0 && out.errors.length === 0;
  if (out.errors.length) console.log('console errors: ' + out.errors.slice(0, 5).join(' | '));
  fs.writeFileSync(outFile, JSON.stringify(out, null, 2));
  console.log(`${out.pass ? 'PASS' : 'FAIL'} ${mode} ${vw}x${vh} -> ${outFile}`);
  process.exitCode = out.pass ? 0 : 1;
}
