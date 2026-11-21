/* MODE: lyric writer. A line of lyrics with a blank; pick (level 1) or type (levels 2-3)
   the week's word that fits. Only words with an `x` sentence are used, and the mode is
   hidden for weeks where fewer than 4 words have one. */
import { esc } from '../../js/util.js';
import { chunks } from '../../js/show.js';
import { choiceButtons, crossOut, optionsFrom, typedField, wireTyped, pat } from './parts.js';
import { pop, slap } from '../../js/motion.js';

const withX = week => week.words.filter(v => typeof v.x === 'string' && v.x.includes('___'));

function sheet(v) {
  const [a, b = ''] = v.x.split('___');
  return `
    <div class="scrap lyricwrap" data-wobble>
      <div class="torn lined lyric">
        <span class="tape tr c"></span>
        <span class="cluelbl">verse 2, line 1</span>
        <p class="lyric-line" data-before="${esc(a)}">${esc(a)}<span class="blank" id="blank" aria-label="blank"></span>${esc(b)}</p>
      </div>
    </div>`;
}

export default {
  id: 'lyric',
  name: 'lyric writer',
  sub: 'finish the lyric with this week’s word',
  sticker: 'setlist',
  available: week => withX(week).length >= 4,
  pool: week => withX(week),
  render(ctx) {
    const v = ctx.item, w = v.w;
    const typing = ctx.level >= 2;
    const blank = () => ctx.el.querySelector('#blank');
    const fill = (word, cls) => { const b = blank(); b.textContent = word; b.className = 'blank ' + cls; slap(b); };

    if (typing) {
      ctx.el.innerHTML = `${sheet(v)}
        <div class="typebox">${typedField('type the missing word')}</div>
        <div class="controls">${ctx.hintBtn()}<button class="btn" id="go">check it</button></div>`;
      const t = wireTyped(ctx, { onReveal: () => fill(w, 'shown') });
      ctx.focus = t.focus;
      const inp = ctx.el.querySelector('#ans');
      inp.addEventListener('keydown', e => { if (e.key === 'Enter' && inp.value.trim().toLowerCase() === w) fill(w, 'ok'); });
      ctx.el.querySelector('#go').addEventListener('click', () => { if (inp.value.trim().toLowerCase() === w) fill(w, 'ok'); });
      const steps = ['def', 'start', 'chunks', 'ends'];
      ctx.setupHints(steps.length, level => {
        const s = steps[level - 1];
        if (s === 'def') ctx.say(`the word means: <b>${esc(v.h.toLowerCase())}</b>`, 'think');
        if (s === 'start') ctx.say(`it starts with <b>“${w[0]}”</b> and has <b>${w.length}</b> letters.`, 'think');
        if (s === 'chunks') ctx.say(`sounds like: <b>${chunks(v)}</b>`, 'think');
        if (s === 'ends') ctx.say(`<span class="pat">${pat(w, (c, i) => i === 0 || i === w.length - 1)}</span>`, 'think');
        t.focus();
      });
      return;
    }

    const opts = optionsFrom(w, withX(ctx.week).map(x => x.w));
    ctx.el.innerHTML = `${sheet(v)}
      ${choiceButtons(opts, 'grid2')}
      <div class="controls">${ctx.hintBtn()}<button class="btn" id="nx" hidden>next song →</button></div>`;
    const btns = [...ctx.el.querySelectorAll('.choice')];
    btns.forEach(b => b.onclick = () => {
      if (b.dataset.v === w) {
        ctx.lockHints(); btns.forEach(x => x.disabled = true);
        b.classList.add('good'); pop(b); fill(w, 'ok');
        ctx.win(); ctx.next(1500);
        return;
      }
      b.classList.add('bad', 'gone'); b.disabled = true;
      const tries = ctx.miss();
      if (tries >= 2) {
        ctx.lockHints(); btns.forEach(x => x.disabled = true);
        const right = btns.find(x => x.dataset.v === w); right.classList.add('good'); fill(w, 'shown');
        ctx.reveal();
        const nx = ctx.el.querySelector('#nx'); nx.hidden = false; nx.onclick = () => ctx.next(0);
      } else {
        ctx.say(`not that one. read the whole line again: what would <i>${esc(v.h.toLowerCase())}</i> sound like here?`, 'oops');
      }
    });
    ctx.setupHints(3, level => {
      if (level <= 2) {
        if (!crossOut(btns, w)) return false;
        ctx.say(level === 1 ? 'crossed one out.' : 'down to two. read it out loud with each.', 'think');
      } else ctx.say(`the word means: <b>${esc(v.h.toLowerCase())}</b>`, 'think');
    });
  }
};
