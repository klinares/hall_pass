// Phase 8.4 tester: the whole tour in legs. Plain Node plus the Playwright install already in world_tour/tests/e2e/node_modules (read only).
//
//   node Tests/Web/world_tour.mjs --from coffee --to rec [--dir forward|back] [--state <file>] [--fresh] [--size 1366x768] [--gpu d3d11]
//                                 [--shots <dir>] [--out <results.json>] [--fullshow coffee] [--fans 2000000]
//
// Starts in --from (a fresh save with ?wt_fans=N, or the localStorage saved in --state by the previous leg), visits each place up to and including --to, and travels
// to the next through the exit card (the tour map). In each place (forward): talks to all three crew, plays one typed gig to the end, opens the wardrobe and tries an item
// on, walks to the stage and starts the show: song 1 right, song 2 a deliberate miss then right, song 3 with a hint then right, then quits (or plays all ten with --fullshow).
// With --dir back it only walks place to place through the exits, towards coffee. All WT_AUDIO lines are collected. After each leg the save is written to --state.
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
const ORDER = ['coffee', 'records', 'rec', 'rooftop', 'club', 'theater', 'festival', 'arena'];
const from = opt('--from', 'coffee'), to = opt('--to', 'arena'), dir = opt('--dir', 'forward');
const stateFile = opt('--state', null);
const [vw, vh] = opt('--size', '1366x768').split('x').map(Number);
const shotsDir = path.resolve(opt('--shots', path.join(project, 'Temp', 'tour-shots')));
const outFile = path.resolve(opt('--out', path.join(project, 'Temp', 'tour-results.json')));
const gpu = opt('--gpu', 'd3d11');
const fullShow = (opt('--fullshow', '') || '').split(',').filter(Boolean);
const fans = opt('--fans', '2000000');
const site = path.resolve(opt('--site', path.join(repo, 'world')));
fs.mkdirSync(shotsDir, { recursive: true });
const sleep = ms => new Promise(r => setTimeout(r, ms));
const res = { from, to, dir, size: `${vw}x${vh}`, places: {}, audio: [], failures: [], errors: [] };
const fail = m => { res.failures.push(m); console.log('FAIL ' + m); };
const log = m => console.log(m);

const launch = gpu === 'd3d11'
  ? { headless: true, args: ['--use-gl=angle', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl'] }
  : { headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] };

const server = await startServer(site);
const browser = await chromium.launch(launch);
const ctx = await browser.newContext({ viewport: { width: vw, height: vh } });
const saved = stateFile && fs.existsSync(stateFile) && !argv.includes('--fresh') ? fs.readFileSync(stateFile, 'utf8') : null;
if (saved) await ctx.addInitScript(s => { try { if (!localStorage.getItem('worldtour.v1')) localStorage.setItem('worldtour.v1', s); } catch (e) {} }, saved);
const page = await ctx.newPage();
const lines = [];
page.on('console', m => { const text = m.text(); lines.push({ t: Date.now(), text }); if (m.type() === 'error') res.errors.push(text); if (text.startsWith('WT_AUDIO')) res.audio.push(text); });
page.on('pageerror', e => res.errors.push('pageerror: ' + e.message));
const mark = () => lines.length;
const waitAny = async (rs, ms, f = 0) => { const end = Date.now() + ms; for (;;) { for (let i = f; i < lines.length; i++) if (rs.some(r => r.test(lines[i].text))) return { i, text: lines[i].text }; if (Date.now() > end) return null; await sleep(40); } };
const waitLine = (re, ms, f = 0) => waitAny([re], ms, f);
const lastLine = re => { for (let i = lines.length - 1; i >= 0; i--) if (re.test(lines[i].text)) return lines[i].text; return null; };
const targets = () => lastLine(/^WT_WORLD_TARGETS/);
const target = name => {
  if (name === 'trunk') { const l = lastLine(/^WT_SHOP_TARGET trunk=/); const m = l && /trunk=(-?[0-9]+):(-?[0-9]+)/.exec(l); return m ? { x: +m[1], y: +m[2], mode: '' } : null; }
  const t = targets(); const m = t && new RegExp(`${name}=(-?\\d+):(-?\\d+)(?:;(\\w*))?`).exec(t); return m ? { x: +m[1], y: +m[2], mode: m[3] || '' } : null; };
const modeNow = () => { const t = targets(); const m = t && / mode=(\w+)/.exec(t); return m ? m[1] : ''; };
const shot = async (name) => { try { await page.screenshot({ path: path.join(shotsDir, `${name}_${vw}x${vh}.png`) }); } catch (e) {} };
const focus = async () => { await page.evaluate(() => { const c = document.getElementById('unity-canvas'); if (c) c.focus(); }); };
const saveJson = async () => page.evaluate(() => { try { return JSON.parse(localStorage.getItem('worldtour.v1')); } catch (e) { return null; } });
const key = async (k, d = 120) => { await page.keyboard.press(k); await sleep(d); };
const onScreen = t => t.x > 30 && t.x < vw - 30 && t.y > 70 && t.y < vh - 40;
function aim(t) {
  if (onScreen(t)) return [t.x, t.y, true];
  const dx = t.x - vw / 2, dy = t.y - vh / 2;
  const k = Math.min(0.42 * vw / Math.max(Math.abs(dx), 1), 0.36 * vh / Math.max(Math.abs(dy), 1), 1);
  return [Math.round(vw / 2 + dx * k), Math.round(vh / 2 + dy * k), false];
}


// World positions (x, z) of each place's empties, read from Assets/World/Scenes/Place_*.unity (the game logs only screen positions of these).
const WORLD = {
  coffee:   { crew1: [-5.0, 1.6], crew2: [3.0, 3.4], crew3: [-9.2, 5.9], stage: [9.0, 9.2], exit: [11.5, -9.5] },
  records:  { crew1: [-5.6, 1.5], crew2: [3.4, 0.6], crew3: [1.5, 8.2], stage: [8.0, 10.4], exit: [-11.5, -9.6] },
  rec:      { crew1: [-5.4, -3.4], crew2: [5.2, -0.4], crew3: [6.0, 3.9], stage: [0.0, 10.4], exit: [11.2, -9.6] },
  rooftop:  { crew1: [-5.8, -2.6], crew2: [-7.6, 5.6], crew3: [-1.2, 2.6], stage: [5.5, 10.2], exit: [10.4, -8.0] },
  club:     { crew1: [-5.6, -0.6], crew2: [3.4, -0.2], crew3: [2.2, 6.8], stage: [7.8, 10.2], exit: [11.4, -10.2] },
  theater:  { crew1: [-4.2, -8.6], crew2: [7.0, -3.2], crew3: [-6.6, 2.0], stage: [0.0, 11.8], exit: [11.4, -11.2] },
  festival: { crew1: [-4.6, -6.2], crew2: [7.4, -5.4], crew3: [1.8, 6.6], stage: [-8.6, 9.6], exit: [11.5, -10.5] },
  arena:    { crew1: [-5.0, -5.8], crew2: [8.8, 1.4], crew3: [-9.6, 5.4], stage: [0.0, 10.5], exit: [11.5, -10.5] },
};
let curPlace = from;
// Waypoints from the stage back to open ground in front of the exit (the crowd and the side alleys stop a straight walk).
// Round 8.4: the path finder's own stage-to-exit legs per place (the WT_ROUTE lines of PathGridPlaceTests, last point = the exit, left out here), so the key walk goes round the crowd box and the side pockets.
const ROUTE = {
  coffee: [[13.7, 5.7], [13.7, 0.5], [11.3, -0.3], [12.9, -5.5]],
  records: [[3.3, 7.7], [-1.1, 0.9]],
  rec: [[4.5, 8.1], [4.5, 2.1], [2.1, -1.1], [2.5, -2.3], [5.3, -4.3]],
  rooftop: [[10.1, 7.7]],
  club: [[12.1, 7.7], [12.1, 3.3], [11.3, -3.5], [12.9, -7.9]],
  theater: [[5.3, 9.3], [4.5, -3.9], [1.3, -7.5], [1.7, -9.5], [6.5, -11.1]],
  festival: [[-3.9, 6.9], [-1.5, 5.7], [1.7, -3.1], [3.3, -5.9], [6.1, -5.9]],
  arena: [[6.9, 7.7], [8.1, -1.1]],
};
const starNow = () => { const t = targets(); const m = t && /star=(-?[0-9.]+):(-?[0-9.]+)/.exec(t); return m ? { x: +m[1], z: +m[2] } : null; };


// Greedy walk with the keys to a world point (x, z), with a wiggle when she stalls; used for waypoints back to open ground after the show.
async function walkWorld(wx, wz, tol = 1.2, ms = 25000) {
  const down = new Set();
  const set = async (code, on) => { if (on && !down.has(code)) { down.add(code); await page.keyboard.down(code); } else if (!on && down.has(code)) { down.delete(code); await page.keyboard.up(code); } };
  const end = Date.now() + ms; let lastPos = null, lastMove = Date.now(), flip = 0;
  await focus();
  try {
    while (Date.now() < end) {
      const sp = starNow(); if (!sp) { await sleep(150); continue; }
      let dx = wx - sp.x, dz = wz - sp.z;
      if (Math.hypot(dx, dz) < tol) return true;
      if (!lastPos || Math.hypot(sp.x - lastPos.x, sp.z - lastPos.z) > 0.25) { lastPos = sp; lastMove = Date.now(); }
      if (Date.now() - lastMove > 1500) { flip++; lastMove = Date.now(); lastPos = sp; }
      if (Date.now() - lastMove < 1200 && flip % 3 === 1) dx = 0;
      if (Date.now() - lastMove < 1200 && flip % 3 === 2) dz = 0;
      await set('KeyD', dx > 0.5); await set('KeyA', dx < -0.5); await set('KeyW', dz > 0.5); await set('KeyS', dz < -0.5);
      await sleep(120);
    }
  } finally { for (const c of [...down]) await set(c, false); }
  return false;
}

// Walk toward a named target until doneRe shows: click it when it is on screen (path finding), and hold the arrow keys toward it by its screen offset otherwise.
async function approach(name, doneRe, label, tries = 150) {   // 8.4: 90 tries (about 40 s) ran out on a loaded machine; the walk itself is unchanged
  const down = new Set();
  const set = async (code, on) => { if (on && !down.has(code)) { down.add(code); await page.keyboard.down(code); } else if (!on && down.has(code)) { down.delete(code); await page.keyboard.up(code); } };
  let lastDismiss = 0, lastLook = null, lastClick = 0, lastPos = null, lastMove = Date.now(), escapeUntil = 0, escapeDir = [0, 0];
  await focus();
  try {
    for (let n = 0; n < tries; n++) {
      const m0 = mark();
      const t = target(name);
      if (process.env.WT_DEBUG && n % 5 === 0) log(`    [approach ${name}] n=${n} t=${t ? t.x + ':' + t.y : 'none'} star=${JSON.stringify(starNow())} mode=${modeNow()} last=${lines.slice(-40).map(l => l.text).filter(x => /^WT_/.test(x)).slice(-3).join(' | ').slice(0, 300)}`);
      if (!t) { await sleep(300); continue; }
      // 8.4: walking past a crew member opens their talk card; a walk to somewhere else must close it ("not now") or it waits there for ever
      if (modeNow() === 'Dialog' && name.startsWith('crew')) {
        const tl = lastLine(/^WT_WORLD_TALK /);   // the card of the crew member we wanted: done (it may have opened between two checks)
        if (tl && doneRe.test(tl)) return { i: lines.length - 1, text: tl };
      }
      if (modeNow() === 'Dialog' && Date.now() - lastDismiss > 1500) {
        lastDismiss = Date.now();
        for (const c of [...down]) await set(c, false);
        await key('Escape', 400);
        if (modeNow() === 'Dialog') { lastLook = null; }
        continue;
      }
      const w = WORLD[curPlace] && WORLD[curPlace][name], sp = starNow();
      const far = w && sp ? Math.hypot(w[0] - sp.x, w[1] - sp.z) : 0;
      const sane = !(w && sp) || Math.abs(w[1] - sp.z) < 3 || ((t.y - vh * 0.58) * -(w[1] - sp.z) > 0);   // a target behind the camera comes out mirrored
      if (onScreen(t) && sane && far < 30 && Date.now() - lastClick > 6000) {
        for (const c of [...down]) await set(c, false);
        lastClick = Date.now();
        await page.mouse.click(t.x, t.y);
        const hit = await waitLine(doneRe, 6500, m0);
        if (hit) return hit;
        continue;
      }
      if (w && sp) {
        if (!lastPos || Math.hypot(sp.x - lastPos.x, sp.z - lastPos.z) > 0.3) { lastPos = sp; lastMove = Date.now(); }
        if (Date.now() - lastMove > 1500) { escapeUntil = Date.now() + 1600; escapeDir = [[1, 0], [-1, 0], [0, 1], [0, -1]][Math.floor(Math.random() * 4)]; lastMove = Date.now() + 1600; lastPos = sp; }
        let dx = w[0] - sp.x, dz = w[1] - sp.z;
        if (Date.now() < escapeUntil) { dx = escapeDir[0] * 5; dz = escapeDir[1] * 5; }
        await set('KeyD', dx > 0.6); await set('KeyA', dx < -0.6); await set('KeyW', dz > 0.6); await set('KeyS', dz < -0.6);
      }
      const hit = await waitLine(doneRe, 300, m0);
      if (hit) return hit;
    }
  } finally { for (const c of [...down]) await set(c, false); }
  fail(`${label}: never got ${doneRe} by walking toward ${name}`);
  await shot('tour_stuck_' + label.replace(/\W+/g, '_'));
  return null;
}

async function playSongs(doneRe, from0, label, plan) {
  let m = from0, songs = 0; const kinds = [];
  for (let guard = 0; guard < 40; guard++) {
    const hit = await waitAny([/^WT_MATH mode=/, doneRe], 25000, m);
    if (!hit) { fail(`${label}: neither a question nor the end after ${songs} songs`); return { songs, kinds }; }
    if (doneRe.test(hit.text)) return { songs, kinds };
    const raw = /answer=([0-9./ -]+?)(?: right=| builder=| kind=|\s*$)/.exec(hit.text.trim());
    if (!raw) { fail(`${label}: no answer in "${hit.text}"`); return { songs, kinds }; }
    const ans = raw[1].replace(/[^0-9./ -]/g, "").trim();
    const how = plan ? plan(songs) : 'right';
    await sleep(1000);
    const rightIdx = (/ right=([0-9]+)/.exec(hit.text) || [])[1];
    let choices = null;
    if (rightIdx !== undefined) {
      const ch = await waitLine(/^WT_CHOICES pos=/, 2500, hit.i);
      if (ch) choices = [...ch.text.matchAll(/([0-9]+):([0-9]+)/g)].map(m => [+m[1], +m[2]]);
    }
    const pick = async (idx) => { const c = choices[idx % choices.length]; await page.mouse.click(c[0], c[1]); };
    if (how === 'hint') {
      let ok = false;
      for (let n = 0; n < 30; n++) { const mm = mark(); await key('Tab', 100); if (lines.slice(mm).some(l => /^WT_FOCUS name=Hint/.test(l.text))) { ok = true; break; } }
      if (!ok) fail(`${label}: Tab never reached the hint button`);
      else { const mh = mark(); await key('Enter', 700); if (!await waitLine(/^WT_HINT/, 2500, mh)) fail(`${label}: Enter on the hint did not log WT_HINT`); await shot(`tour_${label}_hint`); }
      await focus();
    }
    if (how === 'miss') {
      const wrong = ans.includes('/') ? '97/101' : '98765';
      if (choices) await pick(+rightIdx + 1); else { await page.keyboard.type(wrong, { delay: 70 }); await key('Enter', 900); }
      await sleep(900);
      await shot(`tour_${label}_miss`);
      await sleep(600);
    }
    if (choices) await pick(+rightIdx); else { await page.keyboard.type(ans, { delay: 70 }); await key('Enter', 100); }
    const r = await waitLine(/^WT_SONG_RESULT/, 15000, hit.i + 1);
    if (!r) { fail(`${label}: no result typing ${ans} for "${hit.text}"`); await shot(`tour_${label}_stuck`); return { songs, kinds }; }
    kinds.push((/kind=(\w+)/.exec(r.text) || [])[1]);
    songs++; m = r.i + 1;
    if (plan && plan.stopAfter && songs >= plan.stopAfter) return { songs, kinds, stopped: true };
  }
  return { songs, kinds };
}

async function visit(id) {
  curPlace = id;
  const P = res.places[id] = { steps: [], crew: [], audio: [], kinds: [] };
  const step = m => { P.steps.push(m); log(`  ${id}: ${m}`); };
  const pf = m => { P.steps.push('FAIL ' + m); fail(`${id}: ${m}`); };
  const a0 = res.audio.length;
  await waitLine(/^WT_WORLD_MODE mode=Free/, 15000, 0);
  await sleep(1800);
  await shot(`tour_${id}_spawn`);
  const src = lastLine(new RegExp('^WT_WORLD_(ARRIVED|GAME_READY) place=' + id)) || '';
  const crewModes = ((/crew=(\S*)/.exec(src) || [, ''])[1]).split(',');
  P.crewModes = crewModes;
  step('crew modes ' + crewModes.join(','));
  let gigDone = false;
  for (let i = 1; i <= 3; i++) {
    const talk = await approach('crew' + i, new RegExp('^WT_WORLD_TALK crew=Crew_' + i), `${id} crew${i}`);
    if (!talk) { await key('Escape'); continue; }
    const mode = /mode=(\S*)/.exec(talk.text)[1];
    P.crew.push(mode);
    await sleep(700);
    await shot(`tour_${id}_talk${i}`);
    const typed = ['tickets', 'snack', 'merch'].includes(mode);
    if (typed && !gigDone) {
      const mg = mark();
      await key('Enter', 100);
      const start = await waitLine(/^WT_GIG_START/, 8000, mg);
      if (!start) { pf('Enter did not start the gig'); continue; }
      const n = +/n=(\d+)/.exec(start.text)[1];
      await sleep(1500); await shot(`tour_${id}_gig`);
      const r = await playSongs(/^WT_GIG_DONE/, mg, `${id}_gig`, null);
      if (r.songs !== n) pf(`gig had ${n} songs, ${r.songs} answered`);
      P.kinds.push(...r.kinds);
      await sleep(900); await shot(`tour_${id}_gigdone`);
      const mm = mark();
      await key('Enter', 100);
      await waitLine(/^WT_WORLD_MODE mode=(Free|Dialog)/, 8000, mm);
      await sleep(900);
      if (lines.slice(mm).some(l => /^WT_WORLD_UNLOCK/.test(l.text)) && modeNow() === 'Dialog') { step('unlock card shown'); await shot(`tour_${id}_unlock`); await key('Enter', 600); }
      step(`gig ${mode}: ${r.songs} songs ${r.kinds.join(',')}`);
      gigDone = true;
    } else {
      await key('Escape', 700);
    }
  }
  if (!gigDone) pf('no typed gig was offered (' + crewModes.join(',') + ')');

  // wardrobe
  await sleep(500);
  const trunk = () => { const l = lastLine(/^WT_SHOP_TARGET trunk=/); const m = l && /trunk=(-?\d+):(-?\d+)/.exec(l); return m ? { x: +m[1], y: +m[2] } : null; };
  const tl = lines.map(l => l.text).reverse().find(t => t.startsWith('WT_WARDROBE_TRUNK place=' + id));
  const tm = tl && /pos=(-?[0-9.]+):(-?[0-9.]+)/.exec(tl);
  if (tm) WORLD[id].trunk = [+tm[1], +tm[2]];
  // festival: the food trucks between the crew and the trunk trap the greedy key walk, so go round them by the path finder's waypoints first
  const TRUNK_ROUTE = { festival: [[1.7, -3.1], [3.3, -5.9], [6.1, -5.9]] };
  if (TRUNK_ROUTE[id]) for (const [x, z] of TRUNK_ROUTE[id]) await walkWorld(x, z);
  const opened = await approach('trunk', /^WT_WARDROBE_OPEN/, id + ' trunk');
  if (!opened) { pf('the wardrobe trunk never opened'); await shot(`tour_${id}_trunk_missing`); }
  else {
    const m0 = opened.i;
    if (!await waitLine(/^WT_WARDROBE_READY/, 8000, m0)) pf('shop not ready');
    await sleep(1400);
    await shot(`tour_${id}_shop`);
    await waitLine(/^WT_HITS screen=backstage /, 6000, m0);
    const all = lines.slice(m0).filter(l => l.text.startsWith('WT_HITS screen=backstage '));
    const last = all.length ? all[all.length - 1].text : '';
    const hits = {};
    for (const mm of last.matchAll(/ (\S+?)=([\d.]+),([\d.]+)/g)) hits[mm[1]] = [Math.round(+mm[2] * vw), Math.round(+mm[3] * vh)];
    const outfit = Object.keys(hits).find(k => k.startsWith('outfit:') && k !== 'outfit:tour') || Object.keys(hits).find(k => k.startsWith('outfit:'));
    P.hitKeys = Object.keys(hits);
    if (outfit) {
      const mt = mark();
      await page.mouse.click(hits[outfit][0], hits[outfit][1]);
      const l = await waitLine(/^WT_WARDROBE look/, 4000, mt);
      step(`try on ${outfit}: ${l ? l.text : 'no look line'}`);
      if (!l) pf('trying an outfit on logged no look');
      await sleep(900); await shot(`tour_${id}_tryon`);
    } else pf('no outfit buttons in the shop hits: ' + P.hitKeys.join(','));
    if (hits.back) {
      const mc = mark();
      await page.mouse.click(hits.back[0], hits.back[1]);
      if (!await waitLine(/^WT_WARDROBE_CLOSE/, 6000, mc)) pf('Back did not close the shop');
      await waitLine(/^WT_WORLD_MODE mode=Free/, 6000, mc);
    } else { pf('no back button hit'); await key('Escape', 500); }
    await sleep(800);
  }

  // the show
  const offer = await approach('stage', /^WT_WORLD_STAGE_OFFER/, `${id} stage`);
  if (offer) {
    P.stageMode = /mode=(\S*)/.exec(offer.text)[1];
    await sleep(500); await shot(`tour_${id}_stage_offer`);
    const m0 = mark();
    await key('Enter', 100);
    const started = await waitLine(/^WT_SHOW_START/, 15000, m0);
    if (!started) pf('the show never started');
    else {
      await sleep(2000); await shot(`tour_${id}_show`);
      const full = fullShow.includes(id);
      const plan = s => (s === 1 ? 'miss' : s === 2 ? 'hint' : 'right');
      if (!full) plan.stopAfter = 3;
      const r = await playSongs(/^WT_SHOW_DONE/, m0, `${id}_show`, plan);
      P.show = r;
      step(`show ${P.stageMode}: ${r.songs} songs kinds ${r.kinds.join(',')}`);
      if (full) {
        if (r.songs < 10 || r.songs > 12) pf(`full show had ${r.songs} songs`);
        await waitLine(/^WT_TICKET_SHOWN/, 12000, m0);
        await sleep(1500); await shot(`tour_${id}_ticket`);
        const mm = mark();
        let back = false;
        for (const k of ['Tab', 'ArrowRight', 'ArrowDown']) {
          const m2 = mark(); await key(k, 120); await key('Enter', 100);
          if (await waitLine(/^WT_TICKET_ACTION action=map/, 1500, m2)) { back = true; break; }
        }
        if (!back) pf('could not reach back to the map on the ticket');
        await waitLine(/^WT_WORLD_MODE mode=(Free|Dialog)/, 8000, mm);
        await sleep(1200);
        if (lines.slice(mm).some(l => /^WT_WORLD_UNLOCK/.test(l.text)) && modeNow() === 'Dialog') { await shot(`tour_${id}_unlock_show`); await key('Enter', 600); }
        await shot(`tour_${id}_after_show`);
      } else {
        const mq = mark();
        await key('Escape', 700);
        let out = await waitLine(/^WT_WORLD_MODE mode=(Free|Dialog)/, 2500, mq);
        if (!out) {
          for (let n = 0; n < 12 && !out; n++) { const mt = mark(); await key('Tab', 100); if (lines.slice(mt).some(l => /^WT_FOCUS name=Quit/.test(l.text))) { await key('Enter', 100); out = await waitLine(/^WT_WORLD_MODE mode=(Free|Dialog)/, 4000, mt); break; } }
          if (!out) { pf('could not leave the show part way (Escape and Tab to quit)'); await shot(`tour_${id}_noquit`); }
          else step('left the show with the quit button');
        } else step('left the show with Escape');
        await sleep(900); await shot(`tour_${id}_after_quit`);
      }
    }
  }
  P.audio = res.audio.slice(a0);
  await sleep(1500);
  await shot(`tour_${id}_end`);
}

async function go() {
  const gotoPlace = opt('--goto', null);   // start the leg in this place directly (the wt_goto test hook), for a single-place repro
  const q = '?wt_test=1' + (saved ? '' : `&wt_fans=${fans}`) + (gotoPlace ? `&wt_goto=${gotoPlace}` : '');   // round 8.4: a reload resumes in the saved place on its own (WT_WORLD_RESUME), so later legs no longer need the wt_goto hook
  const t0 = Date.now();
  await page.goto(server.url + q, { waitUntil: 'load' });
  const ready = await waitLine(/^WT_WORLD_GAME_READY/, 90000);
  res.loadMs = Date.now() - t0; res.bytes = server.bytesServed;
  if (!ready) { fail('no ready'); return; }
  let ready2 = null;
  if ((saved || gotoPlace) && from !== 'coffee') { ready2 = await waitLine(new RegExp('^WT_WORLD_ARRIVED place=' + from), 40000); if (!ready2) { fail('the reload never resumed in ' + from); return; } }
  await focus(); await sleep(1500);
  const here0 = (/place=(\w+)/.exec((ready2 || ready).text) || [])[1];
  log(`ready ${res.loadMs} ms in ${here0}: ${ready.text}`);
  if (here0 !== from) fail(`started in ${here0}, wanted ${from}`);
  let i = ORDER.indexOf(here0);
  const end = ORDER.indexOf(to);
  for (;;) {
    if (dir === 'forward') await visit(ORDER[i]);
    else {
      res.places[ORDER[i]] = res.places[ORDER[i]] || { steps: [], audio: [] };
      await waitLine(/^WT_WORLD_MODE mode=Free/, 15000, 0); await sleep(1500); await shot(`tour_back_${ORDER[i]}`);
    }
    if (i === end) break;
    curPlace = ORDER[i];
    const nextI = dir === 'forward' ? i + 1 : i - 1;
    if (dir === 'forward' && ROUTE[ORDER[i]]) for (const [x, z] of ROUTE[ORDER[i]]) await walkWorld(x, z);
    const map = await approach('exit', /^WT_WORLD_MAP/, `${ORDER[i]} exit`, 20);
    if (!map) {
      res.exitFallbacks = (res.exitFallbacks || []).concat(ORDER[i]);
      const mg = mark();
      await page.goto(server.url + '?wt_test=1&wt_goto=' + ORDER[nextI], { waitUntil: 'load' });
      const arr0 = await waitLine(new RegExp('^WT_WORLD_ARRIVED place=' + ORDER[nextI]), 40000, mg);
      if (!arr0) { fail('fallback goto never arrived'); break; }
      log('  (exit not reachable by the harness; used wt_goto to ' + ORDER[nextI] + ')');
      await focus(); await sleep(1000);
      i = nextI; continue;
    }
    log('  ' + map.text);
    await sleep(600); await shot(`tour_${ORDER[i]}_exitcard_${dir}`);
    const wantNext = ORDER[nextI];
    const mm = /next=(\w+) open=(\w+)/.exec(map.text);
    const backFirst = dir === 'back' && mm && mm[2] === 'True';
    if (backFirst) await key('Tab', 150);
    const mt = mark();
    await key('Enter', 100);
    const arr = await waitLine(new RegExp('^WT_WORLD_ARRIVED place=' + wantNext), 40000, mt);
    if (!arr) { fail(`never arrived at ${wantNext} after the card ${map.text}`); await shot(`tour_${wantNext}_arrive_missing`); break; }
    log(`  arrived ${wantNext}; whoosh logged: ${res.audio.slice(-15).some(a => /Whoosh/.test(a))}`);
    await sleep(500);
    i = nextI;
  }
  const j = await saveJson();
  if (j && stateFile) { fs.mkdirSync(path.dirname(stateFile), { recursive: true }); fs.writeFileSync(stateFile, JSON.stringify(j)); }
  res.save = j ? { fans: j.fans, world: j.world, unlocked: j.venues && j.venues.unlocked, inventory: j.inventory } : null;
}

try { await go(); } catch (e) { fail('runner: ' + (e && e.stack || e)); }
finally {
  res.lines = lines.map(l => l.text).filter(t => /^WT_|^\[/.test(t)).slice(-4000);
  await browser.close().catch(() => {}); await server.close().catch(() => {});
  if (res.errors.length) fail(`${res.errors.length} console error(s): ${res.errors.slice(0, 3).join(' | ').slice(0, 300)}`);
  fs.mkdirSync(path.dirname(outFile), { recursive: true });
  fs.writeFileSync(outFile, JSON.stringify(res, null, 2));
  console.log(res.failures.length ? `FAILED (${res.failures.length}); results in ${outFile}` : `PASSED; results in ${outFile}`);
  process.exitCode = res.failures.length ? 1 : 0;
}
