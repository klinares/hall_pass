/* MODE: fan mail. A fan DMs a definition; pick the word that means it.
   One pick per song, like the original. Hints: cross out a wrong answer (twice),
   then the first letter and length. */
import { esc, pick } from '../../js/util.js';
import { choiceButtons, crossOut, optionsFrom } from './parts.js';
import { pop } from '../../js/motion.js';

const HANDLES = ['@riffs_biggest_fan', '@front_row_forever', '@glitterbus_kid', '@encore_or_riot', '@setlist_collector', '@gymfloor_glitter'];

export default {
  id: 'fanmail',
  name: 'fan mail',
  sub: 'match the word to the fan’s message',
  sticker: 'heart',
  render(ctx) {
    const v = ctx.item, w = v.w;
    const opts = optionsFrom(w, ctx.week.words.map(x => x.w));
    ctx.el.innerHTML = `
      <div class="dm" data-wobble>
        <div class="dm-head"><span class="dm-av" aria-hidden="true">♥</span><span><b>${pick(HANDLES)}</b><small>sent you a message</small></span></div>
        <div class="dm-msg">ok quick question… which of your words means <i>${esc(v.h.toLowerCase())}</i>?</div>
      </div>
      ${choiceButtons(opts, 'grid2')}
      <div class="controls">${ctx.hintBtn()}<button class="btn" id="nx" hidden>next song →</button></div>`;
    const btns = [...ctx.el.querySelectorAll('.choice')];
    btns.forEach(b => b.onclick = () => {
      ctx.lockHints();
      btns.forEach(x => x.disabled = true);
      if (b.dataset.v === w) { b.classList.add('good'); pop(b); ctx.win(); ctx.next(1300); return; }
      b.classList.add('bad');
      ctx.miss();
      const right = btns.find(x => x.dataset.v === w);
      right.classList.add('good'); pop(right);
      ctx.reveal();
      ctx.say(`it was <b>${w}</b>. it’ll come back around.`, 'oops');
      const nx = ctx.el.querySelector('#nx'); nx.hidden = false; nx.onclick = () => ctx.next(0);
    });
    ctx.setupHints(3, level => {
      if (level <= 2) {
        if (!crossOut(btns, w)) return false;
        ctx.say(level === 1 ? 'crossed one out for you.' : 'one more gone. you’ve got this.', 'think');
      } else ctx.say(`it starts with <b>“${w[0]}”</b> and has <b>${w.length}</b> letters.`, 'think');
    });
  }
};
