/* #/backstage: the shop and locker. Tap anything to try it on; buy with fans to spend.
   Cosmetic only. Total fans never go down, and nothing bought is ever taken away. */
import * as store from '../js/store.js';
import * as P from '../js/progress.js';
import * as M from '../js/meta.js';
import { SHOP, SLOTS, itemById } from '../data/shop.js';
import { sticker, icon } from '../art/sticker.js';
import { dog } from '../art/dog.js';
import { applyLook } from '../js/look.js';
import { fmt, esc } from '../js/util.js';
import { burst, toast } from '../js/fx.js';
import { pop, wobble } from '../js/motion.js';
import * as snd from '../js/sound.js';

const swatch = it => `<span class="swatch" aria-hidden="true">${it.swatch.map(c => `<i style="background:${c}"></i>`).join('')}</span>`;
const guitar = (color, small = true) => `<span class="gtr-wrap" style="--gtr:${color}">${sticker('guitar', { small })}</span>`;

function thumb(it) {
  if (it.slot === 'theme') return swatch(it);
  if (it.slot === 'guitar') return guitar(it.color);
  if (it.slot === 'dog') return `<span class="mini-dog">${dog('idle', { outfit: it.id === 'dog:none' ? '' : it.id.slice(4) })}</span>`;
  return sticker(it.sticker, { small: true });
}

export function mount(root) {
  const s = store.get();
  let sel = M.equippedId(s, 'dog');
  let confirmT = 0, armed = null;

  root.innerHTML = `
    <section class="backstage">
      <a class="backlink" href="#/map">${icon('i-back')}the map</a>
      <div class="bs-head">
        <div><p class="eyebrow">merch + wardrobe ✶ cosmetic only</p><h1 class="shout">backstage</h1></div>
        <div class="wallet tilt-2">${sticker('star', { small: true })}<div><b id="bal">${fmt(s.fans.balance)}</b><span>fans to spend</span></div></div>
      </div>
      <p class="bs-note">spending never lowers your total fans (${fmt(s.fans.lifetime)}), so the map only goes forward.</p>

      <div class="bs-grid">
        <aside class="scrap mirror-wrap">
          <div class="torn mirror" id="mirror" aria-live="polite"></div>
          <span class="tape tl"></span>
        </aside>
        <div class="racks">
          ${SLOTS.map(sl => `
            <section class="rack">
              <h2 class="hand-h">${esc(sl.name)} <small>${esc(sl.note)}</small></h2>
              <div class="items">
                ${SHOP.filter(i => i.slot === sl.id).map(i => `
                  <button class="item" data-id="${i.id}" aria-pressed="false">
                    <span class="thumb">${thumb(i)}</span>
                    <span class="iname">${esc(i.name)}</span>
                    <span class="tag" data-tag></span>
                  </button>`).join('')}
              </div>
            </section>`).join('')}
        </div>
      </div>
    </section>`;

  const mirror = root.querySelector('#mirror');

  function tags() {
    root.querySelectorAll('.item').forEach(b => {
      const it = itemById(b.dataset.id), own = M.owns(s, it.id);
      const wearing = it.slot !== 'sticker' && M.equippedId(s, it.slot) === it.id;
      const t = b.querySelector('[data-tag]');
      t.textContent = wearing ? (it.slot === 'theme' ? 'on' : 'wearing') : own ? (it.slot === 'sticker' ? 'in journal' : 'yours') : fmt(it.price);
      b.classList.toggle('own', own); b.classList.toggle('wearing', wearing);
      b.classList.toggle('short', !own && it.price > s.fans.balance);
      b.setAttribute('aria-pressed', String(b.dataset.id === sel));
    });
    root.querySelector('#bal').textContent = fmt(s.fans.balance);
  }

  /* the mirror: riff and a guitar wearing the selected item (or what's equipped) */
  function paint() {
    const it = itemById(sel), own = M.owns(s, it.id);
    const L = M.look(s);
    const outfit = it.slot === 'dog' ? (it.id === 'dog:none' ? '' : it.id.slice(4)) : L.outfit;
    const gcol = it.slot === 'guitar' ? it.color : L.guitar;
    /* trying on a theme repaints the whole page until she leaves or picks something else */
    const root = document.documentElement;
    if (it.slot === 'theme') { if (it.id === 'theme:tour-pink') delete root.dataset.theme; else root.dataset.theme = it.id.slice(6); }
    else applyLook();
    const wearing = it.slot !== 'sticker' && M.equippedId(s, it.slot) === it.id;
    let action;
    if (wearing) action = `<button class="btn grow" disabled>${it.slot === 'theme' ? 'theme is on' : 'wearing it'}</button>`;
    else if (own && it.slot === 'sticker') action = `<a class="btn ghost grow" href="#/journal">see it in your journal</a>`;
    else if (own) action = `<button class="btn grow" id="act" data-do="equip">${it.slot === 'theme' ? 'switch to this theme' : 'wear it'}</button>`;
    else if (it.price > s.fans.balance) action = `<button class="btn grow" disabled>${fmt(it.price - s.fans.balance)} more fans needed</button>`;
    else action = `<button class="btn grow" id="act" data-do="buy">${armed === it.id ? 'tap again to buy' : `buy for ${fmt(it.price)}`}</button>`;
    mirror.innerHTML = `
      <p class="eyebrow">${own ? 'yours' : 'trying on'}</p>
      <div class="stage-look">
        <span class="mirror-dog">${dog('idle', { outfit })}</span>
        ${it.slot === 'sticker' ? `<span class="mirror-stk">${sticker(it.sticker)}</span>` : guitar(gcol, false)}
      </div>
      <h2 class="hand-h">${esc(it.name)}</h2>
      <p class="blurb">${esc(it.blurb)}</p>
      <div class="row">${action}</div>`;
    const act = mirror.querySelector('#act');
    if (act) act.onclick = () => act.dataset.do === 'equip' ? doEquip(it) : doBuy(it);
  }

  function doEquip(it) {
    P.equipItem(it.id); applyLook(); snd.pop();
    tags(); paint();
    pop(mirror.querySelector('.stage-look'));
  }

  function doBuy(it) {
    /* two taps, so a stray tap never spends fans */
    if (armed !== it.id) { armed = it.id; clearTimeout(confirmT); confirmT = setTimeout(() => { armed = null; paint(); }, 3500); paint(); snd.tap(); return; }
    armed = null; clearTimeout(confirmT);
    const r = P.buyItem(it.id);
    if (!r.ok) { wobble(mirror); snd.thud(); paint(); return; }
    if (it.slot !== 'sticker') P.equipItem(it.id);
    applyLook();
    snd.strum(); burst(40); toast(it.slot === 'sticker' ? 'stuck it in!' : 'yours!');
    tags(); paint();
    pop(mirror.querySelector('.stage-look'));
  }

  root.querySelectorAll('.item').forEach(b => b.onclick = () => {
    if (sel !== b.dataset.id) { armed = null; clearTimeout(confirmT); }
    sel = b.dataset.id; snd.tap(); tags(); paint();
    if (matchMedia('(max-width: 899px)').matches) mirror.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
  });

  tags(); paint();
  return () => { clearTimeout(confirmT); applyLook(); };
}
