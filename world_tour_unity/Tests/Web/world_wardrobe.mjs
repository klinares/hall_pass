// Phase 8.3 browser tests for the wardrobe in the world build (imported by world_smoke.mjs; `--no-wardrobe` skips it, `--wardrobe-only` runs only it):
//   1. the shop through the UI: walk to the trunk by clicking it, open the shop, try an item on (the look changes, the save does not), buy it with two taps (balance
//      and equipped item saved), try another item and leave without buying (the look goes back), reload and see the bought outfit still on her;
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
      const l = await s.wait(/^WT_HITS screen=backstage /, 8000, from);
      if (!l) return null;
      const all = s.lines.slice(from).filter(x => x.startsWith('WT_HITS screen=backstage '));
      const last = all[all.length - 1];
      const out = {};
      for (const m of last.matchAll(/ (\S+?)=([\d.]+),([\d.]+)/g)) out[m[1]] = [Math.round(+m[2] * vw), Math.round(+m[3] * vh)];
      return out;
    };
    s.save = async () => page.evaluate(() => { try { return JSON.parse(localStorage.getItem('worldtour.v1')); } catch (e) { return null; } });
    s.click = async (xy) => { await page.mouse.click(xy[0], xy[1]); };
    return s;
  }

  const lookRe = (a) => new RegExp('^WT_WARDROBE look ' + a);

  // ---------------------------------------------------------------- 1. the shop through the UI
  for (const [vw, vh] of sizes) {
    const tag = `${vw}x${vh}`;
    const s = await open(vw, vh, '', 'ui ' + tag);
    const { ready, from } = await s.go('wt_fans=100000');
    if (!ready) { wfail(`${tag}: no ready line`); await s.ctx.close(); continue; }
    const trunkLine = await s.wait(/^WT_WARDROBE_TRUNK place=coffee pos=/, 3000, from);
    if (!trunkLine) wfail(`${tag}: the coffee place has no wardrobe trunk`); else step(`${tag}: ${trunkLine.text}`);
    if (!s.lastLine(lookRe('outfit=tour hair=chestnut guitar=classic dog=none lights=tour'))) wfail(`${tag}: a new save did not start in the default look: ` + s.lastLine(/^WT_WARDROBE look/));
    await sleep(1500);
    await s.shot('world_trunk');
    const tl = s.lastLine(/^WT_SHOP_TARGET trunk=/);
    const tm = tl && /trunk=(-?\d+):(-?\d+)/.exec(tl);
    if (!tm) { wfail(`${tag}: no trunk position logged`); await s.ctx.close(); continue; }
    let m0 = s.mark();
    await s.click([+tm[1], +tm[2]]);
    const opened = await s.wait(/^WT_WARDROBE_OPEN/, 25000, m0);
    if (!opened) { wfail(`${tag}: clicking the trunk never opened the shop`); await s.shot('open_missing'); await s.ctx.close(); continue; }
    step(`${tag}: ${opened.text}`);
    const rdy = await s.wait(/^WT_WARDROBE_READY/, 8000, m0);
    if (!rdy) { wfail(`${tag}: the shop screen never got ready`); await s.ctx.close(); continue; }
    await sleep(1400);
    let hits = await s.hits(m0);
    if (!hits || !hits['outfit:peach-navy'] || !hits.act || !hits.back || !hits.tab_hair) { wfail(`${tag}: hits missing: ` + JSON.stringify(hits)); await s.shot('hits_missing'); await s.ctx.close(); continue; }
    await s.shot('shop_outfits');

    // try peach + navy on: the look changes, the save does not
    m0 = s.mark();
    await s.click(hits['outfit:peach-navy']);
    if (!await s.wait(lookRe('outfit=peach-navy'), 5000, m0)) wfail(`${tag}: selecting peach + navy did not put it on the Star`);
    await sleep(900);
    await s.shot('shop_tryon_peach');
    let save = await s.save();
    if (save.inventory.equipped.outfit) wfail(`${tag}: a try-on was saved as worn (${save.inventory.equipped.outfit})`);
    const bal0 = save.fans.balance;
    hits = await s.hits(m0);

    // two-tap buy
    m0 = s.mark();
    await s.click(hits.act);
    let press = await s.wait(/^WT_SHOP_PRESS id=outfit:peach-navy outcome=armed/, 5000, m0);
    if (!press) wfail(`${tag}: the first tap on buy did not arm it`);
    save = await s.save();
    if (save.fans.balance !== bal0) wfail(`${tag}: the first tap spent fans`);
    await sleep(700);
    hits = await s.hits(m0);
    await s.click(hits.act);
    press = await s.wait(/^WT_SHOP_PRESS id=outfit:peach-navy outcome=bought/, 5000, m0);
    if (!press) wfail(`${tag}: the second tap did not buy it`);
    await sleep(900);
    save = await s.save();
    w.numbers[tag + ' balance'] = [bal0, save.fans.balance];
    if (save.fans.balance !== bal0 - 5000) wfail(`${tag}: buying peach + navy left balance ${save.fans.balance} (want ${bal0 - 5000})`);
    if (save.inventory.equipped.outfit !== 'peach-navy' || !save.inventory.owned.includes('outfit:peach-navy')) wfail(`${tag}: the purchase is not in the save: ` + JSON.stringify(save.inventory));
    if (save.fans.lifetime < 100000) wfail(`${tag}: lifetime fans went down`);
    await s.shot('shop_bought');

    // another shelf: try a hair colour, leave without buying
    hits = await s.hits(m0);
    m0 = s.mark();
    await s.click(hits.tab_hair);
    hits = await s.hits(m0);
    if (!hits || !hits['hair:blonde']) { wfail(`${tag}: no hair shelf`); await s.ctx.close(); continue; }
    m0 = s.mark();
    await s.click(hits['hair:blonde']);
    if (!await s.wait(lookRe('outfit=peach-navy hair=blonde'), 5000, m0)) wfail(`${tag}: trying blonde hair on did not show: ` + s.lastLine(/^WT_WARDROBE look/));
    await sleep(900);
    await s.shot('shop_tryon_hair');
    hits = await s.hits(m0);
    m0 = s.mark();
    await s.click(hits.back);
    const closed = await s.wait(/^WT_WARDROBE_CLOSE look outfit=peach-navy hair=chestnut/, 6000, m0);
    if (!closed) wfail(`${tag}: leaving without buying did not put the saved look back: ` + s.lastLine(/^WT_WARDROBE_CLOSE/));
    save = await s.save();
    if (save.inventory.equipped.hair) wfail(`${tag}: the hair she only tried on was saved (${save.inventory.equipped.hair})`);
    if (!await s.wait(/^WT_WORLD_MODE mode=Free/, 6000, m0)) wfail(`${tag}: the world did not come back after the shop`);
    await sleep(1200);
    await s.shot('after_shop');

    // reload: the outfit is on her again
    m0 = s.mark();
    await s.page.reload({ waitUntil: 'load' });
    const r2 = await s.wait(/^WT_WORLD_GAME_READY/, READY_TIMEOUT_MS, m0);
    if (!r2) { wfail(`${tag}: no ready line after reload`); await s.ctx.close(); continue; }
    if (!await s.wait(lookRe('outfit=peach-navy hair=chestnut guitar=classic dog=none lights=tour'), 3000, m0)) wfail(`${tag}: after a reload she is not wearing peach + navy: ` + s.lastLine(/^WT_WARDROBE look/));
    else step(`${tag}: after reload ${s.lastLine(/^WT_WARDROBE look/)}`);
    await sleep(1500);
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
      const op = await s.wait(/^WT_WARDROBE_READY/, 20000, from);
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
