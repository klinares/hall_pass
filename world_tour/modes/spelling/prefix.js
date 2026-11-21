/* MODE: prefix mixer. Drag a prefix onto the root (or tap it, then tap the blank) to build
   the week's word, then pick what the prefix does to the meaning.
   Only offered for weeks where 4+ words carry a `prefix` field. */
import { esc, shuffle } from '../../js/util.js';
import { makeDnD } from '../../js/dnd.js';
import { pop, slap, wobble } from '../../js/motion.js';
import * as snd from '../../js/sound.js';
import { crossOut } from './parts.js';

const withPrefix = week => week.words.filter(v => v.prefix && v.prefix.p && v.prefix.root && v.prefix.means);
/* extra prefixes only when a week has fewer than 3 of its own. These could form real words
   with some roots, so keep the week's own prefixes as the main board. */
const EXTRA_P = ['un', 're', 'dis', 'pre'];
const EXTRA_M = ['again', 'before', 'against', 'too much', 'under'];
const NOT_FAMILY = ['in', 'im', 'il', 'ir'];

function rule(pf) {
  if (NOT_FAMILY.includes(pf.p)) return `the “not” prefix changes to match the root’s first sound: <b>il-</b> before l, <b>ir-</b> before r, <b>im-</b> before m or p, <b>in-</b> for most others. <b>${esc(pf.root)}</b> starts with “${esc(pf.root[0])}”.`;
  return `<b>${esc(pf.p)}-</b> means “${esc(pf.means)}.” does “${esc(pf.means)} ${esc(pf.root)}” fit this word?`;
}

export default {
  id: 'prefix',
  name: 'prefix mixer',
  sub: 'build the word, then crack the prefix',
  sticker: 'pick',
  available: week => withPrefix(week).length >= 4,
  pool: week => withPrefix(week),
  render(ctx) {
    const v = ctx.item, pf = v.prefix, w = v.w;
    const own = [...new Set(withPrefix(ctx.week).map(x => x.prefix.p))];
    const board = shuffle(own.length >= 3 ? own : [...own, ...shuffle(EXTRA_P.filter(p => !own.includes(p))).slice(0, 3 - own.length)]);
    const meanings = [...new Set(withPrefix(ctx.week).map(x => x.prefix.means))];
    const mOpts = shuffle([pf.means, ...shuffle([...meanings, ...EXTRA_M].filter((m, i, a) => m !== pf.means && a.indexOf(m) === i)).slice(0, 3)]);

    ctx.el.innerHTML = `
      <div class="desk mixer" data-wobble>
        <span class="desk-tag">the mixer</span>
        <div class="build" aria-live="polite">
          <button class="drop" id="drop" aria-label="prefix slot. tap after choosing a prefix">
            <span class="drop-in" id="dropin">?</span>
          </button><span class="root" id="root">${esc(pf.root)}</span>
        </div>
        <div class="pchips" role="group" aria-label="prefixes">
          ${board.map((p, i) => `<button class="pchip" data-p="${esc(p)}" style="--r:${[-4, 3, -2, 5][i % 4]}deg">${esc(p)}-</button>`).join('')}
        </div>
        <p class="tap">drag a prefix onto the blank, or tap one and then tap the blank</p>
      </div>
      <div class="step2" id="step2" hidden>
        <p class="q">so what does <b>${esc(pf.p)}-</b> do to <b>${esc(pf.root)}</b>?</p>
        <div class="choices grid2">${mOpts.map((m, i) => `<button class="choice" data-v="${esc(m)}" style="--r:${[-1, 1.2, -.6, .8][i]}deg">it means “${esc(m)}”</button>`).join('')}</div>
      </div>
      <div class="controls">${ctx.hintBtn()}<button class="btn" id="nx" hidden>next song →</button></div>`;

    const $ = s => ctx.el.querySelector(s);
    const drop = $('#drop'), dropin = $('#dropin'), chips = [...ctx.el.querySelectorAll('.pchip')];
    chips.forEach(c => { c.dataset.v = c.dataset.p; });   /* crossOut() reads data-v */
    let step = 1, h1 = 0, h2 = 0, over = false;
    ctx.say('build this week’s word: which prefix goes on the front?', 'idle');

    const finishWord = () => {
      dropin.textContent = pf.p; drop.classList.add('ok'); $('#root').classList.add('ok');
      chips.forEach(c => c.disabled = true);
      slap(drop);
    };
    const toStep2 = () => {
      step = 2;
      const s2 = $('#step2'); s2.hidden = false; slap(s2);
      ctx.say(`<b>${esc(w)}</b>. nice build. now the meaning.`, 'cheer');
      s2.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
    };
    const endWithReveal = msg => {
      over = true;
      ctx.lockHints();
      ctx.reveal();
      ctx.say(msg, 'oops');
      const nx = $('#nx'); nx.hidden = false; nx.onclick = () => ctx.next(0);
    };

    const dnd = makeDnD({
      items: chips, zones: [drop],
      onDrop(chip) {
        if (over || step !== 1) return;
        const p = chip.dataset.p;
        snd.tap();
        if (p === pf.p) { finishWord(); toStep2(); return; }
        dropin.textContent = p; drop.classList.add('bad'); wobble(drop);
        chip.classList.add('gone'); chip.disabled = true;
        const tries = ctx.miss();
        setTimeout(() => { if (!drop.classList.contains('ok')) { dropin.textContent = '?'; drop.classList.remove('bad'); } }, 700);
        if (tries >= 3) { finishWord(); endWithReveal(`it’s <b>${esc(pf.p)}-${esc(pf.root)}</b>: ${esc(w)}. <b>${esc(pf.p)}-</b> means “${esc(pf.means)}.”`); }
        else if (tries === 1) ctx.say(`say it out loud: “${esc(p + pf.root)}.” is that a word you know?`, 'oops');
        else ctx.say(rule(pf), 'oops');
      }
    });

    const mbtns = [...ctx.el.querySelectorAll('#step2 .choice')];
    mbtns.forEach(b => b.onclick = () => {
      if (over) return;
      if (b.dataset.v === pf.means) {
        over = true; ctx.lockHints(); mbtns.forEach(x => x.disabled = true);
        b.classList.add('good'); pop(b);
        ctx.win();
        ctx.say(`${esc(pf.p)}- + ${esc(pf.root)} = ${esc(pf.means)} ${esc(pf.root)}. that’s <b>${esc(w)}</b>.`, 'cheer');
        ctx.next(2000);
        return;
      }
      b.classList.add('bad', 'gone'); b.disabled = true;
      const tries = ctx.miss();
      if (tries >= 3) {
        mbtns.forEach(x => x.disabled = true);
        const r = mbtns.find(x => x.dataset.v === pf.means); r.classList.add('good');
        endWithReveal(`<b>${esc(pf.p)}-</b> means “${esc(pf.means)}.” ${esc(w)}: ${esc(v.h.toLowerCase())}.`);
      } else ctx.say(`not quite. here’s what ${esc(w)} means: <i>${esc(v.h.toLowerCase())}</i>. which word fits in front of “${esc(pf.root)}”?`, 'oops');
    });

    ctx.setupHints(5, () => {
      if (over) return false;
      if (step === 1) {
        if (h1 >= 3) return false;
        h1++;
        dnd.clear();
        if (h1 === 1) ctx.say('try each one out loud in front of the root. only one makes a real word.', 'think');
        else if (h1 === 2) { if (!crossOut(chips, pf.p)) return false; ctx.say('crossed one out.', 'think'); }
        else ctx.say(rule(pf), 'think');
        return;
      }
      if (h2 >= 2) return false;
      h2++;
      if (h2 === 1) ctx.say(`here’s what ${esc(w)} means: <i>${esc(v.h.toLowerCase())}</i>.`, 'think');
      else { if (!crossOut(mbtns, pf.means)) return false; ctx.say('one meaning crossed out.', 'think'); }
    });

  }
};
