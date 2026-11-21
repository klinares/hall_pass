/* MODE: remix. Tap the letter stickers in order to spell the word.
   Level 2 adds one decoy letter, level 3 adds two.
   Hint: clears any wrong letters, then glows the next right tile (pressing again
   while one is glowing is free). Misses follow the spelling ladder. */
import { shuffle, tilt, esc } from '../../js/util.js';
import { icon, sticker } from '../../art/sticker.js';
import * as snd from '../../js/sound.js';
import { slap, shrink, pop } from '../../js/motion.js';

const DECOYS = 'aeioulnrstcdmp';

export default {
  id: 'remix',
  name: 'remix',
  sub: 'tap the letter stickers in order',
  sticker: 'cassette',
  render(ctx) {
    const v = ctx.item, word = v.w;
    const extra = Math.max(0, ctx.level - 1);
    const decoys = shuffle([...DECOYS].filter(c => !word.includes(c))).slice(0, extra);
    let mix;
    do { mix = shuffle([...word, ...decoys]); } while (mix.join('') === word);

    ctx.el.innerHTML = `
      <div class="scrap cluewrap">
        <div class="torn clue">
          <span class="tape tl"></span>
          <span class="cluelbl">clue</span>
          <p>${esc(v.h)}</p>
        </div>
      </div>
      <div class="desk">
        <span class="desk-lights l">${sticker('lights', { small: true })}</span>
        <span class="desk-lights r">${sticker('lights', { small: true })}</span>
        <span class="desk-tag">the mix</span>
      <div class="slots" id="built" data-wobble style="--n:${word.length}" aria-label="your word so far">
        ${[...word].map(() => `<div class="slot"></div>`).join('')}
      </div>
      <div class="tiles" id="rack" role="group" aria-label="letter tiles">
        ${mix.map((c, i) => `<button class="tile" data-i="${i}" style="--r:${tilt(4)}deg" aria-label="letter ${c}">${c}</button>`).join('')}
      </div>
      ${decoys.length ? `<p class="decoynote">${decoys.length === 1 ? 'one letter doesn’t belong' : 'two letters don’t belong'}. leave ${decoys.length === 1 ? 'it' : 'them'} on the rack.</p>` : ''}
      </div>
      <div class="controls">
        <button class="btn ghost sm" id="undo">${icon('i-undo')}undo</button>
        <button class="btn ghost sm" id="clear">clear</button>
        ${ctx.hintBtn()}
      </div>`;

    const root = ctx.el;
    const slots = [...root.querySelectorAll('.slot')];
    const tiles = [...root.querySelectorAll('.tile')];
    const picked = [];
    let busy = false;

    const paint = (landed = -1) => {
      slots.forEach((s, i) => {
        const has = picked[i] != null;
        s.textContent = has ? mix[picked[i]] : '';
        s.classList.toggle('fill', has);
        if (i === landed) slap(s);
      });
      tiles.forEach((t, i) => { t.classList.toggle('used', picked.includes(i)); t.classList.toggle('picked', picked.includes(i)); t.classList.remove('glow'); });
    };

    tiles.forEach(t => t.onclick = () => {
      if (busy || picked.includes(+t.dataset.i) || picked.length >= word.length) return;
      snd.tap();
      const a = shrink(t);
      picked.push(+t.dataset.i);
      t.classList.add('picked');
      const landed = picked.length - 1;
      const done = () => paint(landed);
      if (a) a.onfinish = done; else done();
      if (picked.length === word.length) check();
    });

    function check() {
      const built = picked.map(i => mix[i]).join('');
      if (built === word) {
        busy = true;
        tiles.forEach(x => x.disabled = true);
        setTimeout(() => slots.forEach((s, i) => setTimeout(() => { s.classList.add('ok'); pop(s); }, i * 35)), 170);
        ctx.win();
        ctx.next(1400);
        return;
      }
      const tries = ctx.miss();
      if (tries >= 3) {
        busy = true;
        tiles.forEach(x => x.disabled = true);
        setTimeout(() => { picked.length = 0; [...word].forEach((c, i) => { slots[i].textContent = c; slots[i].classList.add('fill', 'given'); slap(slots[i]); }); }, 360);
        ctx.reveal();
        ctx.next(2800);
      } else {
        busy = true;
        const hb = root.querySelector('#hint'); if (hb) hb.disabled = true;
        ctx.say(ctx.ladder(), 'oops');
        ctx.later(() => { picked.length = 0; paint(); busy = false; const b = root.querySelector('#hint'); if (b) b.disabled = false; repaintHint(); }, 750);
      }
    }

    function repaintHint() {
      const b = root.querySelector('#hint');
      if (b && ctx.hints >= hintMax) b.disabled = true;
    }

    root.querySelector('#undo').onclick = () => { if (!busy && picked.length) { snd.tap(); picked.pop(); paint(); } };
    root.querySelector('#clear').onclick = () => { if (!busy && picked.length) { snd.tap(); picked.length = 0; paint(); } };

    const hintMax = Math.max(2, Math.ceil(word.length / 3));
    ctx.setupHints(hintMax, () => {
      if (busy) return false;
      if (tiles.some(t => t.classList.contains('glow'))) { ctx.say('that glowing tile is still your next letter. tap it.', 'think'); return false; }
      let ok = 0;
      while (ok < picked.length && mix[picked[ok]] === word[ok]) ok++;
      const hadWrong = ok < picked.length;
      picked.length = ok; paint();
      const t = tiles.find((x, i) => !picked.includes(i) && mix[i] === word[ok]);
      if (t) { t.classList.add('glow'); pop(t); }
      ctx.say(hadWrong ? 'i cleared the wrong letters. tap the glowing tile next.' : 'tap the glowing tile. that’s your next letter.', 'think');
    });
  }
};
