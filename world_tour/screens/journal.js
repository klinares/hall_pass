/* #/journal: the tour journal. Earned and bought stickers sit on a notebook page.
   Drag one to move it, or tap it and then tap an empty spot. Arrow keys nudge the selected one. */
import * as store from '../js/store.js';
import * as P from '../js/progress.js';
import * as M from '../js/meta.js';
import { MODES } from '../modes/index.js';
import { journalArt, icon } from '../art/sticker.js';
import { dog } from '../art/dog.js';
import { esc } from '../js/util.js';
import { slap, peelOn } from '../js/motion.js';
import * as snd from '../js/sound.js';

export function mount(root) {
  P.setModes(MODES);
  const fresh = P.awardStickers();
  const s = store.get();
  const items = M.pageItems(s), left = M.toEarn(s);
  const earnedN = items.filter(i => !i.key.startsWith('sticker:')).length;
  let sel = null;

  root.innerHTML = `
    <section class="journal">
      <a class="backlink" href="#/map">${icon('i-back')}the map</a>
      <div class="bs-head">
        <div><p class="eyebrow">sticker book ✶ ${earnedN} of ${earnedN + left.length} earned</p><h1 class="shout">tour journal</h1></div>
      </div>
      <p class="bs-note" id="cap" aria-live="polite">${items.length ? 'drag a sticker to move it, or tap it and then tap where it should go.' : ''}</p>
      <div class="scrap pagewrap">
        <div class="torn lined jpage" id="page" role="group" aria-label="journal page">
          ${items.map((it, i) => {
            const p = M.spotOf(s, it.key, i);
            return `<button class="jstk" data-key="${esc(it.key)}" style="left:${p.x * 100}%;top:${p.y * 100}%;--r:${p.r}deg"
                      aria-label="${esc(it.name)}, ${esc(it.note)}">${journalArt(it.art)}</button>`;
          }).join('')}
          ${items.length ? '' : `<div class="jempty"><span class="dog-host">${dog('think')}</span><p>blank so far. finish a show and the first sticker lands here.</p></div>`}
        </div>
        <span class="tape tl"></span><span class="tape tr c"></span>
      </div>

      ${left.length ? `
      <h2 class="hand-h">still to earn</h2>
      <ul class="toearn">
        ${left.map(m => `<li>${journalArt(m.art, { small: true })}<span><b>${esc(m.name)}</b><small>${esc(m.how)}</small></span></li>`).join('')}
      </ul>` : `<p class="hand-h">every sticker earned. the whole book is full.</p>`}
      <p class="bs-note">more stickers to stick in: <a href="#/backstage">backstage</a>.</p>
    </section>`;

  const page = root.querySelector('#page'), cap = root.querySelector('#cap');
  const els = [...page.querySelectorAll('.jstk')];
  const info = key => items.find(i => i.key === key);

  function select(el) {
    els.forEach(e => e.classList.toggle('sel', e === el));
    sel = el;
    if (el) { const it = info(el.dataset.key); cap.innerHTML = `<b>${esc(it.name)}</b>: ${esc(it.note)}. tap an empty spot to move it there.`; }
  }
  function moveTo(el, x, y, save = true) {
    const r = page.getBoundingClientRect();
    const fx = Math.min(.95, Math.max(.05, x)), fy = Math.min(.95, Math.max(.05, y));
    el.style.left = fx * 100 + '%'; el.style.top = fy * 100 + '%';
    if (save) P.placeSticker(el.dataset.key, fx, fy, parseFloat(el.style.getPropertyValue('--r')) || 0);
    return r;
  }

  /* drag with pointer events; a press that barely moves is a tap (select) */
  els.forEach(el => {
    let start = null;
    el.addEventListener('pointerdown', e => {
      if (e.button) return;
      start = { x: e.clientX, y: e.clientY, moved: false };
      el.setPointerCapture(e.pointerId);
    });
    el.addEventListener('pointermove', e => {
      if (!start) return;
      if (!start.moved && Math.hypot(e.clientX - start.x, e.clientY - start.y) < 8) return;
      start.moved = true; el.classList.add('lift');
      const r = page.getBoundingClientRect();
      moveTo(el, (e.clientX - r.left) / r.width, (e.clientY - r.top) / r.height, false);
    });
    const end = e => {
      if (!start) return;
      const moved = start.moved; start = null; el.classList.remove('lift');
      if (moved) {
        const r = page.getBoundingClientRect();
        moveTo(el, (e.clientX - r.left) / r.width, (e.clientY - r.top) / r.height);
        slap(el); snd.pop(); select(null); cap.textContent = 'stuck.';
      } else { select(sel === el ? null : el); snd.tap(); if (!sel) cap.textContent = 'drag a sticker to move it, or tap it and then tap where it should go.'; }
    };
    el.addEventListener('pointerup', end);
    el.addEventListener('pointercancel', () => { start = null; el.classList.remove('lift'); });
    el.addEventListener('click', e => { if (e.detail === 0) { select(sel === el ? null : el); } });   /* keyboard Enter/Space */
    el.addEventListener('keydown', e => {
      const d = { ArrowLeft: [-.03, 0], ArrowRight: [.03, 0], ArrowUp: [0, -.03], ArrowDown: [0, .03] }[e.key];
      if (!d) return;
      e.preventDefault();
      moveTo(el, parseFloat(el.style.left) / 100 + d[0], parseFloat(el.style.top) / 100 + d[1]);
    });
  });

  /* tap-to-place: tapping the page itself moves the selected sticker there */
  page.addEventListener('click', e => {
    if (!sel || e.target.closest('.jstk')) return;
    const r = page.getBoundingClientRect();
    const el = sel;
    moveTo(el, (e.clientX - r.left) / r.width, (e.clientY - r.top) / r.height);
    slap(el); snd.pop(); select(null); cap.textContent = 'stuck.';
  });

  /* stickers earned since the last visit peel on */
  fresh.forEach((m, i) => {
    const el = page.querySelector(`[data-key="${m.id}"]`);
    if (el) setTimeout(() => { peelOn(el); snd.peel(); }, 300 + i * 220);
  });
}
