// Phase 8.2 browser checks for the places and the travel between them (imported by world_smoke.mjs; run it with --places-only to skip the rest).
//
//   travelUi:   coffee -> records through the tour map card at the exit (a test save with the fans for the record store), the "next stop" travel card, arrival at
//               records' PlayerSpawn, one gig there played to the end (the fans go up and are saved), then back to the coffee shop through the same card.
//   placeTour:  for each place and window size: load the page with ?wt_test&wt_goto=<place> (a save with the fans that place needs), wait for the arrival, take the
//               spawn view, measure frames per second with her moving, talk to a crew member (shot), start the stage show (shot during a song). Console errors fail.
//
// Shots go to the --shots folder as place_<id>_<what>_<size>.png.
import path from 'node:path';

const sleep = ms => new Promise(r => setTimeout(r, ms));
const FANS = { coffee: 0, records: 20000, rec: 60000, rooftop: 125000, club: 225000, theater: 375000, festival: 600000, arena: 900000 };
const ORDER = ['coffee', 'records', 'rec', 'rooftop', 'club', 'theater', 'festival', 'arena'];
const TYPED = ['tickets', 'snack', 'merch'];
const SIZES_FPS_FLOOR = 30;

export async function placeTests(env) {
  const { browser, server, sizes, results, fail, shotsDir, opt, argv, READY_TIMEOUT_MS } = env;
  const only = opt('--places', 'records,rec,rooftop,club').split(',').filter(Boolean);
  if (!argv.includes('--no-travel')) await travelUi(env);
  if (!argv.includes('--no-tour')) await placeTour(env, only);
}

// ---------------------------------------------------------------------------------------------------------------
export function makePage(browser, server, size, label, results, fail, shotsDir, shotPrefix) {
  const [vw, vh] = size;
  const info = results.places = results.places || {};
  const g = info[label] = { size: `${vw}x${vh}`, steps: [], errors: [], numbers: {} };
  const lines = [];
  const step = m => { g.steps.push(m); console.log(`  ${label}: ${m}`); };
  const gfail = m => { g.steps.push('FAIL ' + m); fail(`${label}: ${m}`); };
  return (async () => {
    const ctx = await browser.newContext({ viewport: { width: vw, height: vh } });
    const page = await ctx.newPage();
    page.on('console', m => {
      const text = m.text();
      if (m.type() === 'error') g.errors.push(text);
      lines.push({ t: Date.now(), text });
    });
    page.on('pageerror', e => g.errors.push('pageerror: ' + e.message));
    const mark = () => lines.length;
    const waitAny = async (res, ms, from = 0) => {
      const end = Date.now() + ms;
      for (;;) {
        for (let i = from; i < lines.length; i++) if (res.some(r => r.test(lines[i].text))) return { i, text: lines[i].text };
        if (Date.now() > end) return null;
        await sleep(40);
      }
    };
    const waitLine = (re, ms, from = 0) => waitAny([re], ms, from);
    const lastTargets = () => { for (let i = lines.length - 1; i >= 0; i--) if (lines[i].text.startsWith('WT_WORLD_TARGETS')) return lines[i].text; return null; };
    const target = name => {
      const t = lastTargets();
      const m = t && new RegExp(`${name}=(-?\\d+):(-?\\d+)(?:;(\\w*))?`).exec(t);
      return m ? { x: +m[1], y: +m[2], mode: m[3] || '' } : null;
    };
    const star = () => { const t = lastTargets(); const m = t && /star=(-?[\d.]+):(-?[\d.]+)/.exec(t); return m ? { x: +m[1], z: +m[2] } : null; };
    const shot = async name => { await page.screenshot({ path: path.join(shotsDir, `${shotPrefix}_${name}_${vw}x${vh}.png`) }); };
    const storedFans = async () => page.evaluate(() => { try { const j = JSON.parse(localStorage.getItem('worldtour.v1')); return j ? j.fans.lifetime : null; } catch (e) { return null; } });
    const patchSave = async (fans, unlocked) => page.evaluate(([f, u]) => {
      const j = JSON.parse(localStorage.getItem('worldtour.v1'));
      j.fans.lifetime = f; j.fans.balance = Math.max(j.fans.balance || 0, f);
      j.venues.unlocked = u; j.venues.seen = u;
      localStorage.setItem('worldtour.v1', JSON.stringify(j));
    }, [fans, unlocked]);
    const focus = async () => { await page.evaluate(() => { const c = document.getElementById('unity-canvas'); if (c) c.focus(); }); };
    // plays every question of a gig by typing the logged answer; returns the number of songs answered
    const playSongs = async (doneRe, from) => {
      let m = from, songs = 0;
      for (let guard = 0; guard < 40; guard++) {
        const hit = await waitAny([/^WT_MATH mode=/, doneRe], 25000, m);
        if (!hit) { gfail(`neither a question nor the end after ${songs} songs`); return songs; }
        if (doneRe.test(hit.text)) return songs;
        const raw = /answer=(\S+)/.exec(hit.text);
        if (!raw) { gfail(`no answer in "${hit.text}"`); return songs; }
        const ans = raw[1].replace(/[^0-9.\/\-]/g, '');
        await sleep(900);
        await page.keyboard.type(ans, { delay: 70 });
        await page.keyboard.press('Enter');
        const res = await waitLine(/^WT_SONG_RESULT/, 12000, hit.i + 1);
        if (!res) { gfail(`no result for "${hit.text}" typing ${ans}`); return songs; }
        if (!/kind=clean/.test(res.text)) gfail(`answer ${ans} was not clean: ${res.text}`);
        songs++;
        m = res.i + 1;
      }
      return songs;
    };
    return { ctx, page, lines, g, step, gfail, mark, waitAny, waitLine, target, star, shot, storedFans, patchSave, focus, playSongs, vw, vh };
  })();
}

export async function loadReady(h, server, query, READY_TIMEOUT_MS) {
  await h.page.goto(server.url + query, { waitUntil: 'load' });
  const r = await h.waitLine(/^WT_WORLD_GAME_READY/, READY_TIMEOUT_MS);
  if (!r) h.gfail('no WT_WORLD_GAME_READY');
  return r;
}

// Walks with the arrow keys toward a ground point (x, z), reading her position from the logged WT_WORLD_TARGETS star=x:z, until she is within `tol` metres or `ms` pass.
export async function walkKeys(h, tx, tz, tol = 1.0, ms = 14000) {
  const end = Date.now() + ms;
  const down = new Set();
  const set = async (code, on) => { if (on && !down.has(code)) { down.add(code); await h.page.keyboard.down(code); } else if (!on && down.has(code)) { down.delete(code); await h.page.keyboard.up(code); } };
  try {
    while (Date.now() < end) {
      const p = h.star();
      if (!p) { await sleep(100); continue; }
      const dx = tx - p.x, dz = tz - p.z;
      if (Math.hypot(dx, dz) < tol) break;
      await set('KeyD', dx > 0.4); await set('KeyA', dx < -0.4);
      await set('KeyW', dz > 0.4); await set('KeyS', dz < -0.4);
      await sleep(120);
    }
  } finally { for (const c of [...down]) await set(c, false); }
  await sleep(300);
}

async function measureFps(h, seconds) {
  const t0 = Date.now();
  let dir = 'KeyD';
  await h.page.keyboard.down(dir);
  while (Date.now() - t0 < seconds * 1000) {
    await sleep(1000);
    await h.page.keyboard.up(dir);
    dir = dir === 'KeyD' ? 'KeyA' : 'KeyD';
    await h.page.keyboard.down(dir);
  }
  await h.page.keyboard.up(dir);
  const fps = h.lines.filter(l => l.t >= t0 + 1200 && /^WT_WORLD_POS/.test(l.text)).map(l => +/fps=([\d.]+)/.exec(l.text)[1]);
  return fps.length ? +(fps.reduce((a, b) => a + b, 0) / fps.length).toFixed(1) : null;
}

// ---------------------------------------------------------------------------------------------------------------
async function travelUi(env) {
  const { browser, server, sizes, results, fail, shotsDir, READY_TIMEOUT_MS } = env;
  const size = sizes[0];
  const h = await makePage(browser, server, size, 'travel', results, fail, shotsDir, 'place_travel');
  const { g, step, gfail, waitLine, mark, shot, target, star, patchSave, storedFans, focus, page } = h;
  try {
    // a first load makes the save; then it gets the fans for the record store and the page loads again
    if (!await loadReady(h, server, '?wt_test=1', READY_TIMEOUT_MS)) return;
    await patchSave(FANS.records + 5000, ['coffee', 'records']);
    let m0 = mark();
    await page.reload({ waitUntil: 'load' });
    const ready = await waitLine(/^WT_WORLD_GAME_READY place=coffee/, READY_TIMEOUT_MS, m0);
    if (!ready) { gfail('no ready after the save patch'); return; }
    step(ready.text);
    await focus(); await sleep(1500);

    // walk to the exit ring in the south-east corner of the coffee street with the keys
    m0 = mark();
    await walkKeys(h, 11.4, -9.5, 0.8);
    let map = await waitLine(/^WT_WORLD_MAP/, 8000, m0);
    if (!map) { gfail('walking onto the exit never opened the tour map card'); await shot('map_missing'); return; }
    step(map.text);
    if (!/next=records open=True/.test(map.text)) gfail('the card should offer the record store as open: ' + map.text);
    await sleep(700);
    await shot('map_card');
    m0 = mark();
    await page.keyboard.press('Enter');                        // "go: record store" is the first button
    const travel = await waitLine(/^WT_WORLD_TRAVEL next=records/, 6000, m0);
    if (!travel) { gfail('Enter on the card did not start the travel'); return; }
    const tStart = travel.i;
    await sleep(450);
    await shot('travel_card');
    const arrived = await waitLine(/^WT_WORLD_ARRIVED place=records/, 30000, m0);
    if (!arrived) { gfail('the records scene never loaded'); await shot('travel_missing'); return; }
    step(arrived.text);
    const travelMs = h.lines[arrived.i].t - h.lines[tStart].t;
    g.numbers.travelMs = travelMs;
    if (travelMs > 2600) gfail(`travel took ${travelMs} ms (the card itself should be under 2 s, plus the load)`);
    if (!await waitLine(/^WT_WORLD_MODE mode=Free/, 8000, arrived.i)) gfail('she did not get control back after the travel');
    await sleep(2200);
    await shot('arrived');
    // she is at records' PlayerSpawn (0, -7.8)
    const sp = star();
    g.numbers.arrival = sp;
    if (!sp || Math.abs(sp.x) > 0.6 || Math.abs(sp.z + 7.8) > 0.6) gfail('she did not arrive at the spawn: ' + JSON.stringify(sp));
    const savedPlace = await page.evaluate(() => { try { return JSON.parse(localStorage.getItem('worldtour.v1')).world.place; } catch (e) { return null; } });
    if (savedPlace !== 'records') gfail('the save says place ' + savedPlace);

    // a gig at records: a crew member whose mode she can type (tickets, snack table)
    const crewModes = /crew=(\S*)/.exec(arrived.text)[1].split(',');
    let pick = -1;
    for (let i = 0; i < 3; i++) if (TYPED.includes(crewModes[i])) { pick = i + 1; break; }
    if (pick < 0) { gfail('no crew member offers a typed math mode: ' + crewModes.join(',')); return; }
    const fansBefore = await storedFans();
    const ct = target('crew' + pick);
    m0 = mark();
    await page.mouse.click(ct.x, ct.y);
    const talk = await waitLine(new RegExp('^WT_WORLD_TALK crew=Crew_' + pick), 30000, m0);
    if (!talk) { gfail('clicking crew ' + pick + ' never started a talk'); await shot('talk_missing'); return; }
    step(talk.text);
    await sleep(600);
    await shot('talk');
    m0 = mark();
    await page.keyboard.press('Enter');
    const start = await waitLine(/^WT_GIG_START/, 8000, m0);
    if (!start) { gfail('Enter on the offer did not start a gig'); return; }
    const n = +/n=(\d+)/.exec(start.text)[1];
    await sleep(1500);
    await shot('gig');
    const songs = await h.playSongs(/^WT_GIG_DONE/, m0);
    if (songs !== n) gfail(`the gig had ${n} songs but ${songs} were answered`);
    await sleep(600);
    m0 = mark();
    await page.keyboard.press('Enter');
    await waitLine(/^WT_WORLD_MODE mode=Free/, 8000, m0);
    const fansAfter = await storedFans();
    g.numbers.fans = [fansBefore, fansAfter];
    if (!(fansAfter > fansBefore)) gfail(`the gig at records added no fans (${fansBefore} to ${fansAfter})`);
    step(`gig at records: ${songs} songs, fans ${fansBefore} -> ${fansAfter}`);

    // back to the coffee shop through the exit card at records (the exit ring is at (-11.5, -9.6)): out of the cellar, over the crossing, along the south sidewalk
    await sleep(500);
    m0 = mark();
    await walkKeys(h, 0.2, 1.2, 0.7); await walkKeys(h, 0.2, -8.2, 0.7); await walkKeys(h, -11.2, -9.5, 0.8);
    let map2 = await waitLine(/^WT_WORLD_MAP place=records/, 8000, m0);
    if (!map2) { gfail('the exit at records never opened the card'); await shot('map2_missing'); return; }
    step(map2.text);
    if (!/back=coffee/.test(map2.text)) gfail('the card at records should offer the way back to the coffee shop: ' + map2.text);
    await sleep(500);
    await shot('map_card_back');
    m0 = mark();
    // buttons here: next stop (locked, so absent), "back to: coffee shop", "stay here": the first button is the way back
    await page.keyboard.press('Enter');
    const back = await waitLine(/^WT_WORLD_ARRIVED place=coffee/, 30000, m0);
    if (!back) { gfail('going back never reached the coffee shop'); await shot('back_missing'); return; }
    step(back.text);
    const placeNow = await page.evaluate(() => { try { return JSON.parse(localStorage.getItem('worldtour.v1')).world.place; } catch (e) { return null; } });
    if (placeNow !== 'coffee') gfail('the save says place ' + placeNow + ' after going back');
    await sleep(1500);
    await shot('back_in_coffee');
    if (g.errors.length) gfail(`${g.errors.length} console error(s): ${g.errors.slice(0, 3).join(' | ').slice(0, 300)}`);
    console.log(`travel: coffee -> records (${travelMs} ms) -> gig -> coffee, fans ${fansBefore} -> ${fansAfter}`);
  } finally {
    await h.ctx.close();
  }
}

// ---------------------------------------------------------------------------------------------------------------
async function placeTour(env, only) {
  const { browser, server, sizes, results, fail, shotsDir, READY_TIMEOUT_MS } = env;
  results.tour = results.tour || {};
  for (const id of only) {
    for (const size of sizes) {
      const key = `${id}_${size[0]}x${size[1]}`;
      const h = await makePage(browser, server, size, key, results, fail, shotsDir, 'place_' + id);
      const { g, step, gfail, waitLine, mark, shot, target, patchSave, focus, page } = h;
      const rec = results.tour[key] = { fps: null };
      try {
        if (!await loadReady(h, server, '?wt_test=1', READY_TIMEOUT_MS)) continue;
        const idx = ORDER.indexOf(id);
        await patchSave(FANS[id], ORDER.slice(0, idx + 1));
        let m0 = mark();
        await page.goto(server.url + '?wt_test=1&wt_goto=' + id, { waitUntil: 'load' });
        const arrived = await waitLine(new RegExp('^WT_WORLD_(ARRIVED|GAME_READY) place=' + id), READY_TIMEOUT_MS, m0);   // coffee is where the game opens: no travel, so the ready line is its arrival
        if (!arrived) { gfail('never arrived'); await shot('arrive_missing'); continue; }
        step(arrived.text);
        await focus();
        await waitLine(/^WT_WORLD_MODE mode=Free/, 15000, arrived.i);
        await sleep(2500);
        await shot('spawn');
        // frames per second with her moving
        rec.fps = await measureFps(h, 5);
        step(`fps ${rec.fps}`);
        if (rec.fps === null || rec.fps < SIZES_FPS_FLOOR) gfail(`${rec.fps} fps (floor ${SIZES_FPS_FLOOR})`);
        // back to the spawn side: she has been walking left and right; talk to the nearest crew member by clicking them
        await sleep(600);
        let ct = target('crew1');
        if (ct && ct.x > 30 && ct.x < h.vw - 30 && ct.y > 30 && ct.y < h.vh - 30) {
          m0 = mark();
          await page.mouse.click(ct.x, ct.y);
          const talk = await waitLine(/^WT_WORLD_TALK crew=Crew_1/, 25000, m0);
          if (!talk) { gfail('clicking crew 1 never started a talk'); await shot('talk_missing'); }
          else {
            await sleep(700);
            await shot('talk');
            m0 = mark();
            await page.keyboard.press('Escape');     // "not now" (a card says so, then "ok")
            await sleep(600);
            await page.keyboard.press('Enter');
            await waitLine(/^WT_WORLD_MODE mode=Free/, 6000, m0);
          }
        } else { step('crew 1 is not on screen from here; talk shot skipped'); }
        // the stage show: walk onto the stage spot, start the show, shoot a song
        const st = target('stage');
        if (!st) { gfail('no stage position'); continue; }
        m0 = mark();
        await page.mouse.click(Math.min(Math.max(st.x, 40), h.vw - 40), Math.min(Math.max(st.y, 40), h.vh - 40));
        let offer = await waitLine(/^WT_WORLD_STAGE_OFFER/, 35000, m0);
        if (!offer) {   // the stage may be off screen from here: walk toward it with the keys
          await page.keyboard.down('KeyW'); await sleep(3000); await page.keyboard.up('KeyW');
          const st2 = target('stage');
          if (st2) await page.mouse.click(Math.min(Math.max(st2.x, 40), h.vw - 40), Math.min(Math.max(st2.y, 40), h.vh - 40));
          offer = await waitLine(/^WT_WORLD_STAGE_OFFER/, 35000, m0);
        }
        if (!offer) { gfail('walking onto the stage never offered the show'); await shot('stage_missing'); continue; }
        await sleep(500);
        await shot('stage_offer');
        m0 = mark();
        await page.keyboard.press('Enter');
        const started = await waitLine(/^WT_SHOW_START/, 15000, m0);
        if (!started) { gfail('the show never started'); await shot('show_missing'); continue; }
        await sleep(2600);
        await shot('show');
        if (g.errors.length) gfail(`${g.errors.length} console error(s): ${g.errors.slice(0, 3).join(' | ').slice(0, 300)}`);
        console.log(`${key}: arrived, ${rec.fps} fps, talk and show shots taken`);
      } finally {
        if (g.errors.length && !g.steps.some(x => x.includes('console error'))) gfail(`${g.errors.length} console error(s): ${g.errors.slice(0, 3).join(' | ').slice(0, 300)}`);
        await h.ctx.close();
      }
    }
  }
}
