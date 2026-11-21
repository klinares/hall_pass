/* MODE: soundcheck. Quick facts and powers of ten, multiple choice.
   The meter is the only timer in the game, and it only decides bonus fans:
   answer right before it runs out for +300. Running out costs nothing. */
import { generate, SKILL } from '../../math/gen/fluency.js';
import { show } from '../../math/answer.js';
import { ladder, controls, rich, showNext } from './parts.js';
import { pop, wobble } from '../../js/motion.js';
import * as snd from '../../js/sound.js';
import { pick } from '../../js/util.js';

const BONUS = 300;

export default {
  id: 'soundcheck',
  name: 'soundcheck',
  sub: 'fast facts and powers of ten',
  sticker: 'amp',
  kind: 'math',
  skill: SKILL,
  generate,
  render(ctx) {
    const p = ctx.prob, secs = p.params.seconds;
    const label = c => rich(show({ type: p.answer.type, value: c.value }));
    ctx.el.innerHTML = `
      <div class="desk sounddesk" data-wobble>
        <span class="desk-tag">soundcheck ✶ one, two</span>
        <div class="meter" aria-hidden="true"><i id="lvl"></i></div>
        <div class="meterlbl"><span id="secs">${secs}</span><small> sec for +${BONUS} bonus fans</small></div>
        <div class="fact">${rich(p.prompt)}</div>
      </div>
      <div class="choices grid2 facts" id="ch">${p.choices.map((c, i) => `<button class="choice" data-i="${i}" style="--r:${[-1, 1.2, -.6, .8][i]}deg">${label(c)}</button>`).join('')}</div>
      ${controls(ctx)}`;
    const $ = s => ctx.el.querySelector(s);
    const btns = [...ctx.el.querySelectorAll('#ch .choice')];
    const t0 = performance.now();
    let left = secs, beat = true;

    const lvl = $('#lvl'), num = $('#secs'), lbl = num.parentElement;
    const timer = setInterval(() => {
      if (!lvl.isConnected) { clearInterval(timer); return; }      /* this song is over */
      left = Math.max(0, secs - (performance.now() - t0) / 1000);
      lvl.style.width = (left / secs * 100) + '%';
      num.textContent = Math.ceil(left);
      if (left <= 0) { beat = false; clearInterval(timer); lbl.classList.add('out'); lbl.textContent = 'no bonus this time. no rush.'; }
    }, 100);
    const stop = () => clearInterval(timer);

    const L = ladder(ctx, p, {
      onStep(n) {
        if (n === 2) {
          const live = btns.filter(b => !p.choices[+b.dataset.i].correct && !b.disabled);
          if (live.length > 1) { const b = pick(live); b.classList.add('gone'); b.disabled = true; wobble(b); }
        }
        if (n >= 3) btns.forEach(b => { b.disabled = true; if (p.choices[+b.dataset.i].correct) b.classList.add('good'); });
      }
    });

    btns.forEach(b => b.onclick = () => {
      if (L.over) return;
      const c = p.choices[+b.dataset.i];
      if (c.correct) {
        stop();
        btns.forEach(x => x.disabled = true);
        b.classList.add('good'); pop(b);
        const bonus = beat && left > 0 ? BONUS : 0;
        L.right(bonus);
        if (bonus) { snd.tick(); ctx.say(`beat the meter. <b>+${BONUS}</b> bonus fans.`, 'cheer'); }
        ctx.next(1100);
        return;
      }
      b.classList.add('bad', 'gone'); b.disabled = true; wobble(b);
      if (L.wrong(null, c.why)) { stop(); showNext(ctx); }
    });

    ctx.say('quick as you can. the meter only changes bonus fans.', 'idle');
  }
};
