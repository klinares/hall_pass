// Phase 8.3 browser tests for the wardrobe in the world build (imported by world_smoke.mjs; `--no-wardrobe` skips it, `--wardrobe-only` runs only it):
//   1. the avatar editor through the UI (phase 10B.1): walk to the trunk by clicking it, open the editor, change a top color, a hair style, the skin tone and save an outfit
//      (every change is worn and saved at once), close it with Done, reload and see the look still on her. Buying is the boutique's job (world_boutique.mjs);
//   2. looks: the Star in three outfit and hair colour sets, riff in two outfits, a stage with another light colour, the guitar skin close up (shots at both sizes);
//   3. the guitar in her hands during a stage show;
//   4. every place has its trunk (a log line each) and a shot of it.
// Test hooks (page URL with wt_test): wt_fans=N adds fans, wt_buy=id,id buys and wears items, wt_shop=id (or 1) opens the shop on that item at start.
import path from 'node:path';

const sleep = ms => new Promise(r => setTimeout(r, ms));
const PLACES = ['coffee', 'records', 'rec', 'rooftop', 'club', 'theater', 'festival', 'arena'];

export async function wardrobeTests(o) {
  const { browser, server, sizes, results, fail, shotsDir, argv, READY_TIMEOUT_MS } = o;
  const w = results.wardrobe = { steps: [], errors: [], numbers: {} };
  const step = m => { w.steps.push(m); console.log('  wardrobe: ' + m); };
  const wfail = m => { w.steps.push('FAIL ' + m); fail('wardrobe: ' + m); };

  async function open(vw, vh, query, label) {
    const ctx = await browser.newContext({ viewport: { width: vw, height: vh } });
    const page = await ctx.newPage();
    const s = { ctx, page, lines: [], vw, vh, label };
    page.on('console', m => {
      const text = m.text();
      if (m.type() === 'error') w.errors.push(`${label}: ${text}`);
      s.lines.push(text);
    });
    page.on('pageerror', e => w.errors.push(`${label}: pageerror ${e.message}`));
    s.mark = () => s.lines.length;
    s.wait = async (re, ms, from = 0) => {
      const end = Date.now() + ms;
      for (;;) {
        for (let i = from; i < s.lines.length; i++) if (re.test(s.lines[i])) return { i, text: s.lines[i] };
        if (Date.now() > end) return null;
        await sleep(40);
      }
    };
    s.shot = async name => { await page.screenshot({ path: path.join(shotsDir, `wardrobe_${name}_${vw}x${vh}.png`) }); };
    s.go = async (q, from = 0) => {
      const m = s.mark();
      await page.goto(server.url + '?wt_test=1' + (q ? '&' + q : ''), { waitUntil: 'load' });
      const ready = await s.wait(/^WT_WORLD_GAME_READY/, READY_TIMEOUT_MS, m);
      if (ready) { await page.evaluate(() => { const c = document.getElementById('unity-canvas'); if (c) c.focus(); }); await sleep(800); }
      return { ready, from: m };
    };
    s.lastLine = re => { for (let i = s.lines.length - 1; i >= 0; i--) if (re.test(s.lines[i])) return s.lines[i]; return null; };
    s.hits = async (from) => {
      const l = await s.wait(/^WT_HITS screen=avatar /, 8000, from);
      if (!l) return null;
      const all = s.lines.slice(from).filter(x => x.startsWith('WT_HITS screen=avatar '));
      const last = all[all.length - 1];
      const out = {};
      for (const m of last.matchAll(/ (\S+?)=([\d.]+),([\d.]+)/g)) out[m[1]] = [Math.round(+m[2] * vw), Math.round(+m[3] * vh)];
      return out;
    };
    s.save = async () => page.evaluate(() => { try { return JSON.parse(localStorage.getItem('worldtour.v1')); } catch (e) { return null; } });
    s.click = async (xy) => { await page.mouse.click(xy[0], xy[1]); };
    return s;
  }

  const lookRe = (a) => new RegExp('^WT_WARDROBE look .*' + a);

  // ---------------------------------------------------------------- 1. the editor through the UI (mouse only)
  for (const [vw, vh] of sizes) {
    const tag = `${vw}x${vh}`;
    const s = await open(vw, vh, '', 'ui ' + tag);
    const { ready, from } = await s.go('wt_fans=100000&wt_buy=' + encodeURIComponent('outfit:peach-navy,hair:blonde'));
    if (!ready) { wfail(`${tag}: no ready line`); await s.ctx.close(); continue; }
    const trunkLine = await s.wait(/^WT_WARDROBE_TRUNK place=coffee pos=/, 3000, from);
    if (!trunkLine) wfail(`${tag}: the coffee place has no wardrobe trunk`); else step(`${tag}: ${trunkLine.text}`);
    await sleep(1500);
    await s.shot('world_trunk');
    const tl = s.lastLine(/^WT_SHOP_TARGET trunk=/);
    const tm = tl && /trunk=(-?\d+):(-?\d+)/.exec(tl);
    if (!tm) { wfail(`${tag}: no trunk position logged`); await s.ctx.close(); continue; }
    let m0 = s.mark();
    await s.click([+tm[1], +tm[2]]);
    const opened = await s.wait(/^WT_WARDROBE_OPEN/, 25000, m0);
    if (!opened) { wfail(`${tag}: clicking the trunk never opened the editor`); await s.shot('open_missing'); await s.ctx.close(); continue; }
    step(`${tag}: ${opened.text}`);
    const rdy = await s.wait(/^WT_AVATAR_READY/, 8000, m0);
    if (!rdy) { wfail(`${tag}: the editor never got ready`); await s.ctx.close(); continue; }
    await sleep(1000);
    let hits = await s.hits(m0);
    if (!hits || !hits.Back || !hits.Tab_hair || !hits.Tab_top || !hits.Tab_outfits || !Object.keys(hits).some(k => k.startsWith('Item_'))) { wfail(`${tag}: hits missing: ` + JSON.stringify(hits && Object.keys(hits))); await s.shot('hits_missing'); await s.ctx.close(); continue; }
    await s.shot('editor_tops');
    const before = s.lastLine(/^WT_WARDROBE look/);

    // a top in another color: the look changes and is saved at once
    m0 = s.mark();
    await s.click(hits.Color_5 || hits.Color_3);
    const pick = await s.wait(/^WT_AVATAR_PICK slot=top .*color=(5|3) /, 5000, m0);
    if (!pick) wfail(`${tag}: choosing a color did not change her top: ` + s.lastLine(/^WT_AVATAR_PICK/));
    await sleep(700);
    let save = await s.save();
    if (!save.avatar || !JSON.stringify(save.avatar).includes('|' + (/color=(\d+)/.exec(pick ? pick.text : '') || [0, 5])[1] + '|')) wfail(`${tag}: the new color is not in the save: ` + JSON.stringify(save.avatar).slice(0, 200));
    if (s.lastLine(/^WT_WARDROBE look/) === before) wfail(`${tag}: the Star's look did not change on screen`);
    await s.shot('editor_color');

    // another item, on the hair tab
    m0 = s.mark();
    await s.click(hits.Tab_hair);
    hits = await s.hits(m0);
    const hairItems = Object.keys(hits || {}).filter(k => k.startsWith('Item_hair:'));
    if (hairItems.length < 2) { wfail(`${tag}: the hair tab lists ${hairItems.length} owned styles (want 2 or more)`); await s.ctx.close(); continue; }
    m0 = s.mark();
    await s.click(hits[hairItems[1]]);
    if (!await s.wait(/^WT_AVATAR_PICK slot=hair id=hair:/, 5000, m0)) wfail(`${tag}: picking a hair style logged nothing`);
    await sleep(700);
    await s.shot('editor_hair');

    // skin tone and the outfit slots
    hits = await s.hits(m0);
    m0 = s.mark();
    await s.click(hits.Tab_skin);
    hits = await s.hits(m0);
    m0 = s.mark();
    await s.click(hits.Skin_3);
    if (!await s.wait(/^WT_AVATAR_PICK slot=hair .*skin=3/, 5000, m0)) wfail(`${tag}: picking a skin tone logged nothing`);
    hits = await s.hits(m0);
    m0 = s.mark();
    await s.click(hits.Tab_outfits);
    hits = await s.hits(m0);
    m0 = s.mark();
    await s.click(hits.Save_1);
    if (!await s.wait(/^WT_AVATAR_PICK outfit=1 word=/, 5000, m0)) wfail(`${tag}: saving an outfit logged nothing`);
    await sleep(500);
    save = await s.save();
    if (!save.avatar || !/"outfits"/.test(JSON.stringify(save.avatar))) wfail(`${tag}: the saved outfit is not in the save`);
    await s.shot('editor_outfits');
    hits = await s.hits(m0);

    // done closes it with the mouse, the world comes back
    m0 = s.mark();
    await s.click(hits.Back);
    if (!await s.wait(/^WT_WARDROBE_CLOSE/, 6000, m0)) wfail(`${tag}: Done did not close the editor`);
    if (!await s.wait(/^WT_WORLD_MODE mode=Free/, 6000, m0)) wfail(`${tag}: the world did not come back after the editor`);
    await sleep(1000);
    await s.shot('after_editor');
    const worn = s.lastLine(/^WT_WARDROBE look/);

    // reload: the same look is on her again
    m0 = s.mark();
    await s.page.reload({ waitUntil: 'load' });
    const r2 = await s.wait(/^WT_WORLD_GAME_READY/, READY_TIMEOUT_MS, m0);
    if (!r2) { wfail(`${tag}: no ready line after reload`); await s.ctx.close(); continue; }
    await sleep(1500);
    const again = s.lastLine(/^WT_WARDROBE look/);
    if (!again || !/skin=3 /.test(again + ' ') ) wfail(`${tag}: after a reload her look is not the saved one: ${again}`);
    else step(`${tag}: after reload ${again}`);
    await s.shot('after_reload');
    await s.ctx.close();
  }

  // ---------------------------------------------------------------- 2. looks, close up in the shop (both sizes)
  const looks = [
    ['star_peach_blonde', 'outfit:peach-navy,hair:blonde', '1'],
    ['star_cherry_lilac', 'outfit:cherry,hair:lilac', '1'],
    ['star_mint_black', 'outfit:mint,hair:black', '1'],
    ['riff_beanie', 'dog:beanie', 'dog:beanie'],
    ['riff_shades', 'dog:shades,outfit:peach-navy', 'dog:shades'],
    ['riff_collar', 'dog:collar', 'dog:collar'],
    ['guitar_cherry', 'guitar:cherry,outfit:peach-navy', 'guitar:cherry'],
    ['stage_cyan', 'lights:cyan', 'lights:cyan'],
    ['stage_gold', 'lights:gold', 'lights:gold'],
  ];
  for (const [vw, vh] of sizes) {
    const tag = `${vw}x${vh}`;
    for (const [name, buy, sel] of looks) {
      const s = await open(vw, vh, '', `${name} ${tag}`);
      const { ready, from } = await s.go(`wt_fans=300000&wt_buy=${encodeURIComponent(buy)}&wt_shop=${encodeURIComponent(sel)}`);
      if (!ready) { wfail(`${tag} ${name}: no ready line`); await s.ctx.close(); continue; }
      const op = await s.wait(/^WT_AVATAR_READY/, 20000, from);
      if (!op) { wfail(`${tag} ${name}: the shop did not open`); await s.shot(name + '_missing'); await s.ctx.close(); continue; }
      await sleep(1800);
      await s.shot(name);
      await s.ctx.close();
    }
    step(`${tag}: ${looks.length} look shots`);
  }

  // ---------------------------------------------------------------- 3. the guitar in her hands on the stage
  {
    const [vw, vh] = sizes[0];
    const s = await open(vw, vh, '', 'guitar show');
    const { ready, from } = await s.go('wt_fans=100000&wt_buy=guitar:cherry,outfit:peach-navy');
    if (!ready) wfail('guitar show: no ready line');
    else {
      await sleep(1500);
      const tl = [...s.lines].reverse().find(l => l.startsWith('WT_WORLD_TARGETS'));
      const sm = tl && /stage=(-?\d+):(-?\d+)/.exec(tl);
      if (!sm) wfail('guitar show: no stage position');
      else {
        let m0 = s.mark();
        await s.click([+sm[1], +sm[2]]);
        if (!await s.wait(/^WT_WORLD_STAGE_OFFER/, 30000, m0)) wfail('guitar show: never reached the stage offer');
        else {
          await sleep(500);
          m0 = s.mark();
          await s.page.keyboard.press('Enter');
          const ss = await s.wait(/^WT_SHOW_START/, 14000, m0);
          if (!ss) wfail('guitar show: the show never started');
          else {
            await sleep(2500);
            const g = await s.wait(/^WT_WARDROBE_GUITAR on=true/i, 3000, m0);
            if (!g) wfail('guitar show: the guitar did not come out for the show'); else step('the guitar came out for the show: ' + g.text);
            await s.shot('stage_guitar');
          }
        }
      }
    }
    await s.ctx.close();
  }

  // ---------------------------------------------------------------- 4. every place has its trunk
  {
    const [vw, vh] = sizes[0];
    for (const id of PLACES) {
      const s = await open(vw, vh, '', 'place ' + id);
      const { ready, from } = await s.go(id === 'coffee' ? '' : 'wt_goto=' + id);
      if (!ready) { wfail(`${id}: no ready line`); await s.ctx.close(); continue; }
      const t = await s.wait(new RegExp('^WT_WARDROBE_TRUNK place=' + id + ' pos='), id === 'coffee' ? 3000 : 25000, from);
      if (!t) wfail(`${id}: no wardrobe trunk (${s.lastLine(/^WT_WARDROBE_TRUNK/)})`); else w.numbers['trunk ' + id] = t.text;
      await sleep(2200);
      await s.shot('place_' + id);
      await s.ctx.close();
    }
    step('trunks: ' + PLACES.map(id => w.numbers['trunk ' + id] ? id : id + '?').join(' '));
  }

  if (w.errors.length) wfail(`${w.errors.length} console error(s): ${w.errors.slice(0, 3).join(' | ').slice(0, 300)}`);
  console.log(`wardrobe: ${w.steps.filter(x => !x.startsWith('FAIL')).length} steps, ${w.steps.filter(x => x.startsWith('FAIL')).length} failures`);
}
