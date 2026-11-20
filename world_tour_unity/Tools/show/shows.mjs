// Pinned values for Assets/Tests/EditMode/ShowEngineJsTests.cs: runs the real world_tour/js/show.js mountShow (read only;
// sound, fx, dog, sticker, motion and the ticket screen are stubbed by hooks.mjs) on scripted spelling shows.
// Run from world_tour_unity/Tools/show:  node --import ./register.mjs shows.mjs > "$TEMP/shows_out.txt" && node compact.mjs "$TEMP/shows_out.txt"
// (prints the Pinned(...) entries; keep the output outside the repo). picks.mjs and chunks.mjs pin ShowPickerTests the same way.
const WT = new URL('../../../world_tour/', import.meta.url).href;   // read only
const { rng } = await import(WT + 'math/rng.js');
const store = await import(WT + 'js/store.js');
const P = await import(WT + 'js/progress.js');
const { mountShow } = await import(WT + 'js/show.js');

function fakeRoot() {
  const els = {};
  const mk = id => ({ id, innerHTML: '', textContent: '', dataset: {}, disabled: false, offsetWidth: 0,
    classList: { add() {}, remove() {} }, closest() { return mk('chip'); }, querySelector() { return null; } });
  return { els, set innerHTML(v) {}, querySelector(sel) { const id = sel.replace(/^#/, ''); return els[id] || (els[id] = mk(id)); } };
}
const tick = () => new Promise(r => setTimeout(r, 1));

async function run(name, { words, script, encoreScript = null, encoreSeed = 5, startLevel = null }) {
  store.reset();
  if (startLevel) { P.level('spell:remix'); store.get().skills['spell:remix'].level = startLevel; }
  Math.random = rng(1234).next;
  const root = fakeRoot();
  const log = [];
  let ctx = null, scripts = [script], tickets = [];
  const mode = { id: 'remix', kind: 'spelling', name: 'remix',
    render(c) { ctx = c; c.setupHints(3, () => { if (ctx.__free) { ctx.__free = false; return false; } }); } };
  globalThis.__ticket = a => tickets.push(a);
  const playAll = async (acts) => {
    let j = 0;
    while (true) {
      await tick();
      if (tickets.length >= scripts.length) break;
      const a = acts[j++] ?? 'w';
      for (const ch of a) {
        if (ch === 'h') root.els.hint.onclick();
        else if (ch === 'f') { ctx.__free = true; root.els.hint.onclick(); }
        else if (ch === 'm') ctx.miss();
        else if (ch === 'w') ctx.win();
        else if (ch === 'x') ctx.win(250);
        else if (ch === 'r') ctx.reveal();
      }
      log.push([ctx.item.w, ctx.bonus ? 1 : 0, a, Number(root.els.sfans.textContent.replace(/,/g, '')), Number(root.els.sstreak.textContent), P.level('spell:remix')]);
      ctx.next(0);
    }
  };
  mountShow(root, { mode, venue: 'coffee', week: { words }, items: words });
  await playAll(script);
  const sum = t => ({ q: t.G.q.map(x => x.v.w + (x.bonus ? '*' : '') + ':' + x.state), fans: t.G.fans, first: t.G.firstTry, hintOnly: t.G.hintOnly,
    best: t.G.best, hintTotal: t.G.hintTotal, hinted: [...t.G.hinted].sort(), missed: [...t.G.missed].sort(), pct: t.pct, n: t.n,
    show: store.get().shows[store.get().shows.length - 1] });
  const out = { log, t1: sum(tickets[0]), skill: { ...store.get().skills['spell:remix'] } };
  if (encoreScript) {
    Math.random = rng(encoreSeed).next;
    scripts.push(encoreScript);
    log.push('encore');
    tickets[0].onEncore();
    // first render already happened synchronously inside onEncore
    await playAll(encoreScript);
    out.t2 = sum(tickets[1]);
    out.skill = { ...store.get().skills['spell:remix'] };
  }
  delete out.t1.show.d; if (out.t2) delete out.t2.show.d;
  delete out.t1.show.secs; if (out.t2) delete out.t2.show.secs;
  console.log('### ' + name + '\n' + JSON.stringify(out));
}

const W = n => Array.from({ length: n }, (_, i) => ({ w: 'w' + i }));
await run('A_one_miss', { words: W(10), script: ['w', 'w', 'mw', 'w', 'w', 'w', 'w', 'w', 'w', 'w', 'w'], encoreScript: ['w'] });
await run('B_hints', { words: W(10), script: ['w', 'w', 'hw', 'w', 'hhw', 'fw', 'w', 'w', 'w', 'w'] });
await run('C_three_misses', { words: W(10), script: ['mmmr', 'mw', 'w', 'mw', 'w', 'w', 'w', 'w', 'w', 'w', 'w', 'w'], encoreScript: ['w', 'mw', 'hw'] });
await run('D_late_misses', { words: W(10), script: ['w', 'w', 'w', 'w', 'w', 'w', 'w', 'w', 'mw', 'mmmr', 'w', 'w'], encoreScript: ['w', 'w'] });
await run('E_levels', { words: W(10), script: ['w', 'w', 'w', 'w', 'w', 'w', 'w', 'w', 'mmmr', 'mmmr', 'mmmr', 'w'] });
await run('F_down_from_3', { words: W(10), startLevel: 3, script: ['mmmr', 'mmmr', 'mmmr', 'mmmr', 'w', 'w', 'w', 'w', 'w', 'w', 'w', 'w'] });
await run('G_clean', { words: W(10), script: [] });
await run('H_bonus_miss_soundcheck', { words: W(6), script: ['mw', 'x', 'w', 'w', 'mmmr', 'w', 'w'], encoreScript: ['x'] });
process.exit(0);
