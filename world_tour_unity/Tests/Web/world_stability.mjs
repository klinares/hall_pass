// Phase 8.4 P0b and P0d: nothing may get stuck, and nothing may hitch. Plain Node plus the Playwright install already in world_tour/tests/e2e/node_modules (read only).
//
//   node Tests/Web/world_stability.mjs [--site <folder>] [--gpu d3d11|swiftshader] [--sizes 1366x768] [--places coffee,records,...] [--modes keyboard,mouse]
//                                      [--hitch-ms 100] [--still] [--abuse] [--shots <dir>] [--out <results.json>]
//
// For each place and each input mode (keyboard only, mouse only) it opens the place on a save with the fans that place needs and does the whole visit:
//   walk to each of the three crew members and talk (keyboard: the route the path finder logs as WT_WORLD_ROUTE, walked with the arrow keys, then E; mouse: click the
//   crew member, or click the ground toward them when they are off screen); play one typed gig to the end (keyboard: typed answers; mouse: the on-screen keypad);
//   walk to the stage, start the show, answer one song, leave mid-show; walk to the exit and travel on through the tour card.
// While it does, a page script records every animation frame time. After the first second nothing may take longer than --hitch-ms (default 100), travel itself
// (from the go button until a second after arrival) is counted separately. It also checks that while she stands still nothing hides or shows and the camera does not rise or sink.
//   --still   also walks a square pattern in each place and stands still at eight spots (the standing-still toggle check on its own, the P0b reproduction)
//   --abuse   (keyboard run only) holds each arrow key into each edge, opens a gig with Riff in the way, spams clicks and keys during the travel
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { startServer } from './server.mjs';
import { makePage, loadReady } from './world_places.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const project = path.resolve(here, '..', '..');
const repo = path.resolve(project, '..');
const { chromium } = createRequire(path.join(repo, 'world_tour', 'tests', 'e2e', 'package.json'))('playwright');
const argv = process.argv.slice(2);
const opt = (n, d) => { const i = argv.indexOf(n); return i >= 0 && i + 1 < argv.length ? argv[i + 1] : d; };
const ORDER = ['coffee', 'records', 'rec', 'rooftop', 'club', 'theater', 'festival', 'arena'];
const FANS = { coffee: 0, records: 20000, rec: 60000, rooftop: 125000, club: 225000, theater: 375000, festival: 600000, arena: 900000 };
const TYPED = ['tickets', 'snack', 'merch'];
const site = path.resolve(opt('--site', path.join(repo, 'world')));
const gpuMode = opt('--gpu', 'd3d11');
const sizes = opt('--sizes', '1366x768').split(',').map(s => s.split('x').map(Number));
const places = opt('--places', ORDER.join(',')).split(',').filter(Boolean);
const modes = opt('--modes', 'keyboard,mouse').split(',').filter(Boolean);
const hitchMs = Number(opt('--hitch-ms', '100'));
const wantStill = argv.includes('--still'), wantAbuse = argv.includes('--abuse');
const shotsDir = path.resolve(opt('--shots', path.join(project, 'Temp', 'stability-shots')));
const outFile = path.resolve(opt('--out', path.join(project, 'Temp', 'stability-results.json')));
fs.mkdirSync(shotsDir, { recursive: true });
fs.mkdirSync(path.dirname(outFile), { recursive: true });
const sleep = ms => new Promise(r => setTimeout(r, ms));
const results = { site, gpuMode, startedAt: new Date().toISOString(), runs: {}, failures: [] };
const fail = m => { results.failures.push(m); console.log('FAIL', m); };

const FRAME_RECORDER = `(() => {
  window.__ft = []; window.__ev = [];
  let last = performance.now();
  const f = t => { window.__ft.push([t, t - last]); last = t; requestAnimationFrame(f); };
  requestAnimationFrame(f);
  window.__mark = label => window.__ev.push([performance.now(), label]);
  const log = console.log.bind(console);
  console.log = (...a) => { try { const t = String(a[0]); if (t.startsWith('WT_') && !t.startsWith('WT_WORLD_TARGETS') && !t.startsWith('WT_WORLD_ROUTE') && !t.startsWith('WT_WORLD_BUTTONS')) window.__ev.push([performance.now(), t.slice(0, 48)]); } catch (e) {} log(...a); };
})();`;

function launchOptions() {
  if (gpuMode === 'd3d11') return { headless: true, args: ['--use-gl=angle', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl'] };
  return { headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] };
}

// ---------------------------------------------------------------------------------------------------------------
async function visit(browser, server, id, size, mode) {
  const [vw, vh] = size;
  const label = `${id}_${mode}_${vw}x${vh}`;
  const h = await makePage(browser, server, size, label, results, fail, shotsDir, `stab_${id}_${mode}`);
  const { page, g, step, gfail, mark, waitAny, waitLine, shot } = h;
  const run = results.runs[label] = { place: id, mode, size: `${vw}x${vh}`, ok: false, hitches: [], steps: g.steps };
  const keyboard = mode === 'keyboard';
  const lastLine = re => { for (let i = h.lines.length - 1; i >= 0; i--) if (re.test(h.lines[i].text)) return h.lines[i]; return null; };
  const targets = () => { const l = lastLine(/^WT_WORLD_TARGETS/); return l ? l.text : null; };
  const star = () => { const t = targets(); const m = t && /star=(-?[\d.]+):(-?[\d.]+)/.exec(t); return m ? { x: +m[1], z: +m[2] } : null; };
  const modeNow = () => { const t = targets(); const m = t && / mode=(\w+)/.exec(t); return m ? m[1] : ''; };
  const tgt = name => { const t = targets(); const m = t && new RegExp(`(?:^| )${name}=(-?\\d+):(-?\\d+)`).exec(t); return m ? { x: +m[1], y: +m[2] } : null; };
  const evMark = async l => { try { await page.evaluate(x => window.__mark(x), l); } catch (e) {} };
  const onScreen = t => t && t.x > 40 && t.x < vw - 40 && t.y > 70 && t.y < vh - 50;
  const aim = t => {
    if (onScreen(t)) return [t.x, t.y, true];
    const dx = t.x - vw / 2, dy = t.y - vh / 2;
    const k = Math.min(0.42 * vw / Math.max(Math.abs(dx), 1), 0.36 * vh / Math.max(Math.abs(dy), 1), 1);
    return [Math.round(vw / 2 + dx * k), Math.round(vh / 2 + dy * k), false];
  };
  // like a hand: the pointer arrives, rests a moment, presses for about a tenth of a second, lets go
  const click = async (x, y) => { await evMark('click'); await page.mouse.move(Math.round(x), Math.round(y)); await sleep(250); await page.mouse.down(); await sleep(110); await page.mouse.up(); };
  // a key is held for 80 ms like a finger: a press whose down and up land in one frame can be missed by the game's per-frame key check
  const key = async (k, d = 120) => { await page.keyboard.press(k, { delay: 80 }); await sleep(d); };
  const down = new Set();
  const hold = async (code, on) => { if (on && !down.has(code)) { down.add(code); await page.keyboard.down(code); } else if (!on && down.has(code)) { down.delete(code); await page.keyboard.up(code); } };
  const releaseAll = async () => { for (const c of [...down]) await hold(c, false); };

  // the logged route from where she stands to a goal ("crew1", "stage", "exit"): [{x,z}], plus whether the path finder reached it
  const route = name => {
    const l = lastLine(/^WT_WORLD_ROUTE/);
    if (!l || Date.now() - l.t > 2500) return null;
    const m = new RegExp(`(?:^| )${name}=(ok|short);(\\S*)`).exec(l.text);
    if (!m) return null;
    return { reached: m[1] === 'ok', pts: m[2].split(',').filter(Boolean).map(s => { const [x, z] = s.split(':').map(Number); return { x, z }; }) };
  };
  // walks with the arrow keys along the logged route until `done()` is true or the time is up
  async function keyWalk(name, done, ms = 40000) {
    const end = Date.now() + ms;
    let lastPos = null, lastMove = Date.now(), wiggleUntil = 0, wig = [0, 0];
    try {
      while (Date.now() < end) {
        if (done()) return true;
        // a card she walked into on the way (the stage or the exit offer) is closed like a child would, then the walk goes on
        if (name.startsWith('crew') && modeNow() === 'Dialog') { await releaseAll(); await closeCards('on the way to ' + name); continue; }
        const sp = star(), r = route(name);
        if (!sp || !r || !r.pts.length) { await releaseAll(); await sleep(150); continue; }
        // the first waypoint that is not already reached (0.45 m), the goal when it is the last
        let wp = r.pts[r.pts.length - 1];
        for (const p of r.pts) if (Math.hypot(p.x - sp.x, p.z - sp.z) > 0.45) { wp = p; break; }
        let dx = wp.x - sp.x, dz = wp.z - sp.z;
        if (!lastPos || Math.hypot(sp.x - lastPos.x, sp.z - lastPos.z) > 0.2) { lastPos = sp; lastMove = Date.now(); }
        if (Date.now() - lastMove > 1800) { wiggleUntil = Date.now() + 700; wig = [[1, 0], [-1, 0], [0, 1], [0, -1]][Math.floor(Math.random() * 4)]; lastMove = Date.now() + 700; lastPos = sp; }
        if (Date.now() < wiggleUntil) { dx = wig[0] * 5; dz = wig[1] * 5; }
        if (process.env.TRACE && (process.env.TRACE === "2" || Date.now() % 7 < 2)) console.log(`    trace ${name} star ${sp.x.toFixed(2)},${sp.z.toFixed(2)} -> ${wp.x},${wp.z} keys ${[...down].join('')}`);
        await hold('KeyD', dx > 0.3); await hold('KeyA', dx < -0.3); await hold('KeyW', dz > 0.3); await hold('KeyS', dz < -0.3);
        await sleep(100);
      }
      return done();
    } finally { await releaseAll(); }
  }
  // mouse only: click the crew member when she is close enough for them to be on the screen, else click the ground point the game logs a little way along her route
  // (the screen position of a point about 7 m ahead on the path), until `done()`
  const clickPoint = name => {
    const l = lastLine(/^WT_WORLD_ROUTE/);
    if (!l || Date.now() - l.t > 2500) return null;
    const m = new RegExp(`(?:^| )click_${name}=(-?\\d+):(-?\\d+)`).exec(l.text);
    return m ? { x: +m[1], y: +m[2] } : null;
  };
  async function mouseWalk(name, done, ms = 60000) {
    const end = Date.now() + ms;
    let lastClick = 0, lastPos = null, lastMove = Date.now();
    while (Date.now() < end) {
      if (done()) return true;
      // a click on the wardrobe trunk on the way opens the backstage panel: close it (Escape; the panel's own back button works too, world_wardrobe.mjs covers that)
      if (modeNow() === 'Panel') { await key('Escape', 900); continue; }
      const sp = star(), r = route(name);
      if (sp && r && r.pts.length) {
        if (!lastPos || Math.hypot(sp.x - lastPos.x, sp.z - lastPos.z) > 0.15) { lastPos = sp; lastMove = Date.now(); }
        const idle = Date.now() - lastMove > 1200;
        if (idle || Date.now() - lastClick > 6000) {
          const goal = r.pts[r.pts.length - 1];
          const near = Math.hypot(goal.x - sp.x, goal.z - sp.z) < 5;
          const t = tgt(name);
          let x, y;
          if (name.startsWith('crew') && near && onScreen(t)) { x = t.x; y = t.y; }
          else { const c = clickPoint(name); if (!c) { await sleep(150); continue; } x = c.x; y = c.y; }
          lastClick = Date.now(); lastMove = Date.now();
          await click(x, y);
        }
      }
      await sleep(120);
    }
    return done();
  }

  // finds the newest line of a kind after `from` and returns its text
  const buttonCentres = from => { for (let i = h.lines.length - 1; i >= from; i--) if (h.lines[i].text.startsWith('WT_WORLD_BUTTONS')) return h.lines[i].text.slice('WT_WORLD_BUTTONS '.length).trim(); return null; };
  // presses the first button of the open card (keyboard: Enter; mouse: a click on it, found from the logged button positions)
  async function pressFirst(from, labelStart = '') {
    if (keyboard) { await key('Enter', 150); return true; }
    for (let n = 0; n < 40; n++) {
      const b = buttonCentres(from);
      if (b) {
        const parts = b.split('|').map(s => { const m = /^(.*):(-?\d+):(-?\d+)$/.exec(s); return m ? { label: m[1], x: +m[2], y: +m[3] } : null; }).filter(Boolean);
        const hit = parts.find(p => p.label.startsWith(labelStart)) || parts[0];
        if (hit) { await click(hit.x, hit.y); run.clicks = (run.clicks || 0) + 1; return true; }
      }
      await sleep(150);
    }
    return false;
  }

  // plays one typed song: the logged answer, typed (keyboard) or pressed on the on-screen keypad (mouse)
  async function answer(hit, fromIdx) {
    const raw = /answer=(\S+)/.exec(hit.text);
    if (!raw) { gfail(`no answer in "${hit.text}"`); return false; }
    const ans = raw[1].replace(/[^0-9.\/\-]/g, '');
    await sleep(900);
    if (keyboard) {
      await page.keyboard.type(ans, { delay: 70 });
      await key('Enter', 100);
    } else {
      let pad = null;
      for (let n = 0; n < 40 && !pad; n++) { const l = h.lines.slice(hit.i).find(x => x.text.startsWith('WT_KEYPAD pos=')); if (l) pad = l.text; else await sleep(100); }
      if (!pad) { gfail('no keypad positions logged for the mouse'); return false; }
      const pos = {};
      for (const s of pad.slice('WT_KEYPAD pos='.length).split(',')) { const m = /^([^:]+):(-?\d+):(-?\d+)\s*$/.exec(s.trim()); if (m) pos[m[1]] = { x: +m[2], y: +m[3] }; }
      for (const ch of ans) {
        const k = ch === '/' ? 'frac' : ch === '.' ? 'dot' : ch === '-' ? 'minus' : ch;
        if (!pos[k]) { gfail(`keypad has no ${k} key (${Object.keys(pos).join(' ')})`); return false; }
        await click(pos[k].x, pos[k].y); await sleep(120);
      }
      if (!pos.go) { gfail('keypad has no go key: ' + pad.slice(0, 200)); return false; }
      await click(pos.go.x, pos.go.y);
    }
    return true;
  }
  async function playGig(m0) {
    let m = m0, songs = 0;
    for (let guard = 0; guard < 30; guard++) {
      const hit = await waitAny([/^WT_MATH mode=/, /^WT_GIG_DONE/], 25000, m);
      if (!hit) { gfail(`gig: neither a question nor the end after ${songs} songs`); return songs; }
      if (/^WT_GIG_DONE/.test(hit.text)) return songs;
      if (!await answer(hit)) return songs;
      const res = await waitLine(/^WT_SONG_RESULT/, 12000, hit.i + 1);
      if (!res) { gfail(`gig: no result for "${hit.text}"`); return songs; }
      if (!/kind=clean/.test(res.text)) gfail(`gig: the typed answer was not clean: ${res.text}`);
      songs++; m = res.i + 1;
    }
    return songs;
  }

  // while she stands still nothing may hide or show, and the camera must not rise or sink: a few seconds of WT_WORLD_TARGETS
  async function stillCheck(where, seconds = 5, settle = 6.5) {
    await sleep(settle * 1000);
    const m1 = mark();
    await sleep(seconds * 1000);
    const seq = h.lines.slice(m1).filter(l => /^WT_WORLD_TARGETS/.test(l.text)).map(l => ({ hid: /hid=(\d+)/.exec(l.text)?.[1], lift: /lift=([\d.-]+)/.exec(l.text)?.[1], names: /hidn=(\S+)/.exec(l.text)?.[1] || '', pos: /star=(\S+)/.exec(l.text)?.[1] }));
    const hs = new Set(seq.map(s => s.hid)), ls = new Set(seq.map(s => s.lift));
    run.still = run.still || [];
    run.still.push(`${where} @${seq[0]?.pos}: hid ${[...hs].join('/')} lift ${[...ls].join('/')}`);
    // the camera is still easing in, so one change right at the edge of a test is allowed; going back and forth is not
    const changes = arr => { let n = 0; for (let i = 1; i < arr.length; i++) if (arr[i] !== arr[i - 1]) n++; return n; };
    const liftVals = seq.map(x => +x.lift);
    let reversed = false, dir = 0;
    for (let i = 1; i < liftVals.length; i++) { const d = Math.sign(liftVals[i] - liftVals[i - 1]); if (d !== 0) { if (dir !== 0 && d !== dir) reversed = true; dir = d; } }
    if (changes(seq.map(x => x.hid)) > 1 || reversed) gfail(`standing still at ${where} (${seq[0]?.pos}) things changed: hid ${[...hs].join('/')} lift ${[...ls].join('/')} (${[...new Set(seq.map(s => s.names))].join(' | ')})`);
  }

  let crewModes = [];
  try {
    await page.addInitScript(FRAME_RECORDER);
    if (!await loadReady(h, server, '?wt_test=1', 90000)) return;
    const idx = ORDER.indexOf(id);
    const nextId = ORDER[Math.min(idx + 1, ORDER.length - 1)];   // the save opens the next place too, so the exit card offers the way on (the last place offers the way back)
    await h.patchSave(FANS[nextId], ORDER.slice(0, Math.min(idx + 2, ORDER.length)));
    let m0 = mark();
    await page.goto(server.url + '?wt_test=1&wt_goto=' + id, { waitUntil: 'load' });
    const arrived = await waitLine(new RegExp('^WT_WORLD_(ARRIVED|GAME_READY) place=' + id), 90000, m0);
    if (!arrived) { gfail('never arrived'); return; }
    await h.focus();
    await waitLine(/^WT_WORLD_MODE mode=Free/, 20000, arrived.i);
    crewModes = (/crew=(\S*)/.exec(arrived.text)?.[1] || '').split(',');
    step(`arrived: ${arrived.text}`);
    await sleep(1500);
    await page.evaluate(() => { window.__ft.length = 0; window.__ev.length = 0; });   // the frame clock starts here: the first second after the arrival is allowed
    await evMark('arrived');
    await shot('spawn');
    if (!await waitLine(/^WT_WORLD_ROUTE/, 5000, m0) && keyboard) { gfail('the game logs no WT_WORLD_ROUTE (needs the test build)'); return; }
    await stillCheck('spawn', 5, 3);

    // ---- walk to each crew member and talk ----
    let gigDone = false, firstTyped = crewModes.findIndex(m => TYPED.includes(m)) + 1;
    for (let c = 1; c <= 3; c++) {
      const name = 'crew' + c;
      await evMark('walk ' + name);
      m0 = mark();
      const talked = () => h.lines.slice(m0).some(l => new RegExp('^WT_WORLD_TALK crew=Crew_' + c).test(l.text));
      let ok;
      if (keyboard) {
        // up to three tries: walk the route, then press E (or Space) a few times, the way a child tries again when nothing happens
        for (let attempt = 0; attempt < 3 && !talked(); attempt++) {
          if (attempt > 0) step(`crew ${c}: no talk on try ${attempt}, walking up again`);
        ok = await keyWalk(name, () => { if (talked()) return true; const r = route(name), sp = star(); return !!(r && sp && r.pts.length && Math.hypot(r.pts[r.pts.length - 1].x - sp.x, r.pts[r.pts.length - 1].z - sp.z) < 0.7); }, 25000);
        if (c === 1 && attempt === 0 && !talked()) await stillCheck('crew 1 spot');
        for (let n = 0; n < 4 && !talked(); n++) { await key('KeyE', 100); await sleep(400); if (!talked()) { await key('Space', 100); await sleep(400); } }
        }
        ok = talked();
      } else {
        ok = await mouseWalk(name, talked, 70000);
      }
      if (!ok) { gfail(`${keyboard ? 'walking and pressing E' : 'clicking'} crew ${c} never started a talk (mode ${modeNow()}, route age ${Date.now() - (lastLine(/^WT_WORLD_ROUTE/)?.t || 0)} ms, star ${JSON.stringify(star())}, ${lastLine(/^WT_WORLD_ROUTE/)?.text.slice(0, 260)})`); await shot('talk_missing_' + c); return; }
      await sleep(700);
      if (c === 1) await shot('talk');
      const modeId = crewModes[c - 1];
      if (!gigDone && c === firstTyped) {
        // play the offer: the first button is "play"
        await evMark('gig open');
        m0 = mark();
        await pressFirst(m0, 'play');
        const start = await waitLine(/^WT_GIG_START/, 8000, m0);
        if (!start) { gfail(`the offer from crew ${c} did not start a gig`); return; }
        await sleep(1500);
        await shot('gig');
        const n = +/n=(\d+)/.exec(start.text)[1];
        const songs = await playGig(m0);
        if (songs !== n) gfail(`the gig had ${n} songs but ${songs} were answered`);
        const done = await waitLine(/^WT_GIG_DONE/, 8000, m0);
        await sleep(700);
        await evMark('gig close');
        const mc = mark();
        await pressFirst(mc, 'keep');
        for (let n = 0; n < 3 && !keyboard && !await waitLine(/^WT_WORLD_MODE mode=Free/, 1500, mc); n++) { step(`click ${n + 1} on keep exploring did nothing`); await pressFirst(mc, 'keep'); }
        if (!await waitLine(/^WT_WORLD_MODE mode=Free/, 8000, mc)) { gfail('the gig card did not hand the world back (mode ' + modeNow() + ')'); await shot('gig_done_stuck'); return; }
        gigDone = !!done;
        step(`gig with crew ${c} (${modeId}): ${songs} songs`);
        await sleep(1200);
        await evMark('gig closed, free');
      } else {
        // not a gig: leave the talk (keyboard: Escape; mouse: the "not now" button, then ok)
        m0 = mark();
        if (keyboard) { await key('Escape', 400); }
        else {
          await pressFirst(m0, 'not');   // buttons: play, [another gig], not now
          for (let n = 0; n < 2; n++) { await sleep(400); const mc = mark(); if (await waitLine(/^WT_WORLD_MODE mode=Free/, 300, m0)) break; await pressFirst(mc, 'ok'); }
        }
        if (modeNow() !== 'Free') { await sleep(500); const mc = mark(); if (modeNow() === 'Dialog') await pressFirst(mc, 'ok'); }
        if (!await waitLine(/^WT_WORLD_MODE mode=Free/, 6000, m0) && modeNow() !== 'Free') { gfail(`could not close the talk with crew ${c}`); return; }
        await sleep(300);
        step(`talked with crew ${c} (${modeId || 'no gig'})`);
      }
    }
    if (!gigDone) { gfail('no typed gig was offered at this place'); return; }

    // ---- abuse: edges, Riff in the way (keyboard run only) ----
    if (wantAbuse && keyboard) await abuse();

    // ---- the stage show ----
    await evMark('walk stage');
    m0 = mark();
    const offered = () => h.lines.slice(m0).some(l => /^WT_WORLD_STAGE_OFFER/.test(l.text));
    let ok = keyboard ? await keyWalk('stage', offered, 50000) : await mouseWalk('stage', offered, 80000);
    if (!ok) { gfail(`${keyboard ? 'walking' : 'clicking'} onto the stage never offered the show`); await shot('stage_missing'); return; }
    await sleep(500);
    await shot('stage_offer');
    await evMark('show start');
    m0 = mark();
    await pressFirst(m0, 'play the show');
    if (!await waitLine(/^WT_SHOW_START|^WT_WORLD_SHOW_START/, 15000, m0)) { gfail('the show never started'); return; }
    // one song, then leave part way
    const q = await waitLine(/^WT_MATH mode=|^WT_SONG_START/, 25000, m0);
    if (q && /^WT_MATH/.test(q.text)) {
      await answer(q);
      await waitLine(/^WT_SONG_RESULT/, 12000, q.i + 1);
    }
    await sleep(1200);
    await shot('show');
    await evMark('show leave');
    const mq = mark();
    if (keyboard) {
      await key('Escape', 700);
      let out = await waitLine(/^WT_WORLD_MODE mode=(Free|Dialog)/, 2500, mq);
      for (let n = 0; n < 12 && !out; n++) { const mt = mark(); await key('Tab', 100); if (h.lines.slice(mt).some(l => /^WT_FOCUS name=Quit/.test(l.text))) { await key('Enter', 100); out = await waitLine(/^WT_WORLD_MODE mode=(Free|Dialog)/, 4000, mt); break; } }
      if (!out) { gfail('could not leave the show part way (Escape, then Tab to the quit button)'); await shot('noquit'); return; }
    } else {
      let out = null;
      for (let n = 0; n < 5 && !out; n++) {
        const l = lastLine(/^WT_WORLD_TARGETS/);
        const qm = l && /quit=(-?\d+):(-?\d+)/.exec(l.text);
        if (qm) await click(+qm[1], +qm[2]);
        out = await waitLine(/^WT_WORLD_MODE mode=(Free|Dialog)/, 2500, mq);
      }
      if (!out) { gfail('could not leave the show part way with the mouse (the quit button)'); await shot('noquit'); return; }
    }
    await sleep(1500);
    await evMark('show closed');
    if (modeNow() === 'Dialog') { const mc = mark(); await pressFirst(mc, 'nice'); await sleep(600); }
    if (modeNow() !== 'Free') { gfail('after leaving the show the world is not hers: mode ' + modeNow()); return; }
    await shot('after_show');
    // she can walk again and the camera is back on her: a short step with the keys or a click
    const s0 = star();
    if (keyboard) { await hold('KeyA', true); await sleep(700); await hold('KeyA', false); }
    else { const c = clickPoint('exit'); await click(c ? c.x : vw / 2, c ? c.y : vh * 0.7); await sleep(900); }
    const s1 = star();
    if (!s0 || !s1 || Math.hypot(s1.x - s0.x, s1.z - s0.z) < 0.3) gfail('after leaving the show she does not move');

    // ---- the exit and the travel ----
    await evMark('walk exit');
    m0 = mark();
    const exitCard = () => h.lines.slice(m0).some(l => /^WT_WORLD_MAP/.test(l.text));
    ok = keyboard ? await keyWalk('exit', exitCard, 60000) : await mouseWalk('exit', exitCard, 90000);
    if (!ok) { gfail(`${keyboard ? 'walking' : 'clicking'} to the exit never opened the tour card`); await shot('exit_missing'); return; }
    await sleep(500);
    await shot('exit_card');
    const mapLine = h.lines[h.lines.slice(m0).findIndex(l => /^WT_WORLD_MAP/.test(l.text)) + m0].text;
    step(mapLine);
    await evMark('travel start');
    const mt = mark();
    await pressFirst(mt, 'go');
    const travel = await waitLine(/^WT_WORLD_TRAVEL next=/, 6000, mt);
    if (!travel) { gfail('the card button did not start the travel'); return; }
    const tStart = Date.now();
    if (wantAbuse) {
      // a child hammering everything while the bus drives: clicks anywhere, Enter, Space, E, Escape
      for (let n = 0; n < 40 && Date.now() - tStart < 3000; n++) {
        await page.mouse.click(60 + Math.random() * (vw - 120), 80 + Math.random() * (vh - 160));
        await page.keyboard.press(['Enter', 'Space', 'KeyE', 'Escape', 'ArrowUp'][n % 5], { delay: 30 });
        await sleep(40);
      }
    }
    const next = /next=(\w+)/.exec(travel.text)[1];
    const there = await waitLine(new RegExp('^WT_WORLD_ARRIVED place=' + next), 40000, mt);
    if (!there) { gfail('the next place never loaded'); await shot('travel_missing'); return; }
    const travelEnd = Date.now();
    await evMark('arrived next');
    if (!await waitLine(/^WT_WORLD_MODE mode=Free/, 10000, there.i)) { gfail('she did not get control back after the travel'); return; }
    await sleep(1800);
    if (modeNow() !== 'Free') gfail('after the travel the world is not hers: mode ' + modeNow());
    await shot('next_place');
    step(`traveled to ${next} in ${travelEnd - tStart} ms`);
    run.travelMs = travelEnd - tStart;

    if (wantStill) await still8();
    run.ok = true;
  } catch (e) {
    gfail('exception: ' + (e && e.message ? e.message : e));
  } finally {
    await releaseAll();
    // frame times: everything after the first second, the travel window counted apart
    try {
      const ft = await page.evaluate(() => ({ ft: window.__ft, ev: window.__ev }));
      const ev = ft.ev;
      const travelStart = ev.find(e => e[1] === 'travel start'), travelBack = ev.find(e => e[1] === 'arrived next');
      const inTravel = t => travelStart && t >= travelStart[0] && (!travelBack || t <= travelBack[0] + 1000);
      const frames = ft.ft.filter(f => f[0] > (ev[0] ? ev[0][0] : 0) + 1000);
      const normal = frames.filter(f => !inTravel(f[0])), trav = frames.filter(f => inTravel(f[0]));
      const worst = normal.slice().sort((a, b) => b[1] - a[1]).slice(0, 6).map(f => { const e = ev.filter(x => x[0] <= f[0]).pop(); return `${Math.round(f[1])} ms, ${e ? Math.round(f[0] - e[0]) : '?'} ms after "${e ? e[1] : 'start'}"`; });
      run.frames = normal.length;
      run.maxFrameMs = normal.length ? Math.round(Math.max(...normal.map(f => f[1]))) : null;
      run.maxTravelFrameMs = trav.length ? Math.round(Math.max(...trav.map(f => f[1]))) : null;
      run.worst = worst;
      if (run.maxFrameMs !== null && run.maxFrameMs > hitchMs) gfail(`longest frame ${run.maxFrameMs} ms (limit ${hitchMs}): ${worst.join('; ')}`);
    } catch (e) { /* page already gone */ }
    if (g.errors.length) gfail(`${g.errors.length} console error(s): ${g.errors.slice(0, 3).join(' | ').slice(0, 300)}`);
    console.log(`${label}: ${run.ok ? 'ok' : 'FAILED'} longest frame ${run.maxFrameMs} ms, travel ${run.maxTravelFrameMs} ms`);
    await h.ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------------------
  // walking along an edge can cross the stage or the exit, which offer their card: that is right, and it must close with Escape (the last button) or Enter on an "ok" card
  async function closeCards(where) {
    for (let n = 0; n < 4 && modeNow() !== 'Free'; n++) {
      if (modeNow() !== 'Dialog') { await sleep(500); continue; }
      const m = mark();
      await key('Escape', 500);
      if (!await waitLine(/^WT_WORLD_MODE mode=Free/, 1200, m) && modeNow() === 'Dialog') await key('Enter', 500);
      await sleep(300);
    }
    if (modeNow() !== 'Free') gfail(`${where} a card stays open: mode ${modeNow()}`);
  }

  async function abuse() {
    await evMark('abuse');
    // every edge: hold each arrow key for six seconds (she must stay inside the place and still answer to the keys), then each corner
    const dirs = [['KeyW'], ['KeyS'], ['KeyA'], ['KeyD'], ['KeyW', 'KeyA'], ['KeyW', 'KeyD'], ['KeyS', 'KeyA'], ['KeyS', 'KeyD']];
    for (const d of dirs) {
      for (const k of d) await hold(k, true);
      await sleep(4500);
      for (const k of d) await hold(k, false);
      const sp = star();
      if (!sp || !(Math.abs(sp.x) <= 14.5 && Math.abs(sp.z) <= 14.5)) gfail(`walking ${d.join('+')} into the edge put her at ${JSON.stringify(sp)}`);
      await closeCards(`after walking ${d.join('+')} into the edge`);
    }
    // from the last corner she can still be walked out with the opposite keys
    const c0 = star();
    await hold('KeyW', true); await hold('KeyD', true); await sleep(1500); await hold('KeyW', false); await hold('KeyD', false);
    await hold('KeyS', true); await hold('KeyA', true); await sleep(1500); await hold('KeyS', false); await hold('KeyA', false);
    const c1 = star();
    if (c0 && c1 && Math.hypot(c0.x - c1.x, c0.z - c1.z) < 0.3) { gfail(`she is stuck in a corner at ${JSON.stringify(c0)}: the keys do not move her (mode ${modeNow()})`); await shot('stuck_corner'); }
    step('walked into every edge and corner');
    // a gig with Riff in the way: walk to the crew member who offers a typed gig, put a click on Riff, press E: the talk must still open
    const ci = Math.max(0, crewModes.findIndex(m => TYPED.includes(m))) + 1;
    const m1 = mark();
    const talked = () => h.lines.slice(m1).some(l => new RegExp('^WT_WORLD_TALK crew=Crew_' + ci).test(l.text));
    await keyWalk('crew' + ci, () => { const r = route('crew' + ci), sp = star(); return !!(r && sp && r.pts.length && Math.hypot(r.pts[r.pts.length - 1].x - sp.x, r.pts[r.pts.length - 1].z - sp.z) < 0.7); });
    const dg = tgt('riff');
    if (dg && onScreen(dg)) {
      await click(dg.x, dg.y); await sleep(200);   // a click on Riff is a click on the ground under him: it walks her there; so walk back to the crew member
      await keyWalk('crew' + ci, () => { const r = route('crew' + ci), sp = star(); return !!(r && sp && r.pts.length && Math.hypot(r.pts[r.pts.length - 1].x - sp.x, r.pts[r.pts.length - 1].z - sp.z) < 0.7); });
    }
    for (let n = 0; n < 4 && !talked(); n++) { await key('KeyE', 100); await sleep(500); }
    if (!talked()) { gfail(`with Riff in the way crew ${ci} did not talk`); return; }
    await sleep(500);
    const m2 = mark();
    await key('Enter', 150);
    if (!await waitLine(/^WT_GIG_START/, 8000, m2)) { gfail('with Riff in the way the gig did not start'); return; }
    await sleep(1200);
    // leave the gig half way with the quit button (Escape) and check the world comes back
    const m3 = mark();
    await key('Escape', 700);
    let out = await waitLine(/^WT_WORLD_MODE mode=(Free|Dialog)/, 2500, m3);
    for (let n = 0; n < 12 && !out; n++) { const mt = mark(); await key('Tab', 100); if (h.lines.slice(mt).some(l => /^WT_FOCUS name=Quit/.test(l.text))) { await key('Enter', 100); out = await waitLine(/^WT_WORLD_MODE mode=(Free|Dialog)/, 4000, mt); break; } }
    if (!out) { gfail('could not leave the gig part way'); return; }
    await sleep(1000);
    if (modeNow() !== 'Free') gfail('after leaving the gig the world is not hers: mode ' + modeNow());
    step('a gig with Riff in the way started, and leaving it half way gave the world back');
  }

  async function still8() {
    await evMark('still8');
    const dirs = [['KeyW'], ['KeyD'], ['KeyD', 'KeyW'], ['KeyA'], ['KeyS'], ['KeyA', 'KeyS'], ['KeyW'], ['KeyD']];
    let n = 0;
    for (const d of dirs) {
      for (const k of d) await hold(k, true);
      await sleep(1400);
      for (const k of d) await hold(k, false);
      await stillCheck('pattern stop ' + (++n));
    }
  }
}

// ---------------------------------------------------------------------------------------------------------------
const server = await startServer(site);
const browser = await chromium.launch(launchOptions());
try {
  for (const id of places) for (const size of sizes) for (const mode of modes) {
    try { await visit(browser, server, id, size, mode); } catch (e) { fail(`${id} ${mode}: ${e.message}`); }
    fs.writeFileSync(outFile, JSON.stringify(results, null, 2));
  }
} finally {
  results.finishedAt = new Date().toISOString();
  fs.writeFileSync(outFile, JSON.stringify(results, null, 2));
  await browser.close();
  server.close();
}
const longest = Object.values(results.runs).map(r => `${r.place}/${r.mode} ${r.maxFrameMs}`).join(', ');
console.log(`world_stability: ${Object.values(results.runs).filter(r => r.ok).length}/${Object.keys(results.runs).length} runs ok, ${results.failures.length} failure(s)\n  longest frame (ms): ${longest}`);
process.exit(results.failures.length ? 1 : 0);
