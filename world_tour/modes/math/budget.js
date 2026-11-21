/* MODE: tour budget. Order of operations on the band's budget sheet.
   Levels 1-2: work out the expression on the keypad.
   Level 3: pick the expression that matches the story, then work it out.
   Worked hint: the sheet rewrites one step at a time. */
import { generate, SKILL } from '../../math/gen/orderops.js';
import { promptCard, keypad, typedAnswer, controls, showNext } from './parts.js';
import { esc } from '../../js/util.js';
import { pop, slap, wobble } from '../../js/motion.js';

/* draw the expression; on step 2 the first operation to do gets the highlighter */
function sheet(p, n) {
  const st = p.params.states;
  if (n >= 3) return st.map((s, i) => `<div class="eline ${i === st.length - 1 ? 'final' : ''}">${i ? '= ' : ''}${esc(s)}</div>`).join('');
  if (n === 2 && st.length > 1) {
    const first = p.steps[1].match(/<b>(.*?) = /)?.[1] || '';
    const at = first ? st[0].indexOf(first) : -1;
    const body = at >= 0 ? `${esc(st[0].slice(0, at))}<mark>${esc(first)}</mark>${esc(st[0].slice(at + first.length))}` : esc(st[0]);
    return `<div class="eline">${body}</div><div class="eline">= ${esc(st[1])}</div>`;
  }
  return `<div class="eline">${esc(st[0])}</div>`;
}

export default {
  id: 'budget',
  name: 'tour budget',
  sub: 'order of operations on the books',
  sticker: 'setlist',
  kind: 'math',
  skill: SKILL,
  generate,
  render(ctx) {
    const p = ctx.prob, story = p.kind === 'story';
    let n = 0, picked = !story;

    ctx.el.innerHTML = `
      <div class="scrap budgetwrap" data-wobble>
        <div class="torn lined budget">
          <span class="tape tr b"></span>
          <div class="bhead"><span>tour budget</span><small>parentheses ✶ × ÷ ✶ + −</small></div>
          <div class="ebox" id="sheet">${story ? '<div class="eline q">which one matches?</div>' : sheet(p, 0)}</div>
        </div>
      </div>
      ${promptCard(story ? p.prompt : `what does it come to?`, story ? 'the books' : 'work it out')}
      ${story ? `<div class="choices grid2 exprs" id="ex">${p.choices.map((c, i) => `<button class="choice" data-i="${i}" style="--r:${[-1, 1.2, -.6, .8][i]}deg">${esc(c.value)}</button>`).join('')}</div>` : ''}
      <div id="padslot" ${story ? 'hidden' : ''}>${keypad('int', { checkLabel: 'balance it' })}</div>
      ${controls(ctx)}`;
    const $ = s => ctx.el.querySelector(s);
    const draw = () => { if (picked) $('#sheet').innerHTML = sheet(p, n); };

    const { L, pad } = typedAnswer(ctx, p, {
      onStep(k) {
        n = Math.max(n, k);
        if (!picked && k >= 2) {
          /* the partial step names the right expression, so move on to working it out */
          const right = p.choices.findIndex(c => c.correct);
          choose(right, true);
        }
        draw();
        slap($('#sheet'));
      }
    });

    function choose(i, given = false) {
      const btns = [...ctx.el.querySelectorAll('#ex .choice')];
      btns.forEach(b => b.disabled = true);
      btns[i].classList.add('good'); pop(btns[i]);
      picked = true; draw();
      $('#padslot').hidden = false; slap($('#padslot'));
      if (!given) ctx.say(`yes: <b>${esc(p.params.expr)}</b>. now work it out.`, 'cheer');
    }
    if (story) {
      const btns = [...ctx.el.querySelectorAll('#ex .choice')];
      btns.forEach((b, i) => b.onclick = () => {
        if (L.over || picked) return;
        const c = p.choices[i];
        if (c.correct) { choose(i); return; }
        b.classList.add('bad', 'gone'); b.disabled = true; wobble(b);
        if (L.wrong(null, c.why)) {
          const r = p.choices.findIndex(x => x.correct);
          btns.forEach(x => x.disabled = true); btns[r].classList.add('good');
          picked = true; n = 3; draw();
          $('#padslot').hidden = false; pad.set(String(p.answer.value)); pad.lock('shown');
          showNext(ctx);
        }
      });
    }
    ctx.say(story ? 'first, which expression tells this story?' : 'parentheses first. then × and ÷. then + and −.', 'idle');
  }
};
