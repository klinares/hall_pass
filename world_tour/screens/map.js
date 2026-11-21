/* Home: the tour map. */
import * as store from '../js/store.js';
import * as P from '../js/progress.js';
import { VENUES } from '../data/venues.js';
import { LAYOUTS, mapSVG } from '../art/map.js';
import { sticker, venueArt, scribble } from '../art/sticker.js';
import { dog } from '../art/dog.js';
import { fmt, short, esc } from '../js/util.js';
import * as copy from '../js/copy.js';
import { peelOn } from '../js/motion.js';
import * as snd from '../js/sound.js';
import { MODES } from '../modes/index.js';
import { MILESTONES } from '../data/milestones.js';

export function mount(root) {
  P.setModes(MODES);
  P.awardStickers();     /* catches anything earned outside a show (time played, venues) */
  const s = store.get();
  const stk = Object.keys(s.journal.stickers).length;
  const newest = P.newestVenue(), next = P.nextVenue();
  const streak = P.dayStreak(), away = P.daysAway();
  const reachedIdx = VENUES.findIndex(v => v.id === newest.id);
  const fresh = P.unseenUnlocks();
  const sleepy = away != null && away >= 3;

  root.innerHTML = `
    <section class="home">
      <div class="hero">
        <div class="titleblock">
          <p class="eyebrow">spelling + math ✶ on the road</p>
          <h1 class="shout title">${esc(copy.NAME)}’s<br>world tour</h1>
          <button class="btn play-cta" id="tonight">tonight: ${esc(newest.short)} →</button>
        </div>
        <div class="statstack">
          <div class="statcard tilt-2">
            ${sticker('star', { small: true })}
            <div><b>${fmt(s.fans.lifetime)}</b><span>total fans</span></div>
          </div>
          <div class="statcard tilt-1">
            ${sticker('bolt', { small: true })}
            <div><b>${streak}</b><span>day streak${s.streak.best > streak ? ` ✶ best ${s.streak.best}` : ''}</span></div>
          </div>
        </div>
        <div class="coach home-coach">
          <div class="dog-host">${dog(sleepy ? 'sleepy' : 'idle')}</div>
          <div class="bubble">${copy.dogHome({ next: next?.short, toGo: next ? fmt(next.fans - s.fans.lifetime) : 0, streak, away })}</div>
        </div>
      </div>

      <div class="maphead">
        <h2 class="hand-h">the route</h2>
        <p>${next ? `${scribble(short(next.fans) + ' fans')} unlocks ${esc(next.short)}` : 'every venue unlocked. legend.'}</p>
      </div>
      <div class="scrap mapwrap">
        <div class="torn map" id="map"></div>
        <span class="tape tl"></span><span class="tape tr c"></span>
      </div>

      <nav class="hubnav" aria-label="more">
        <a class="hubcard tilt-3" href="#/backstage">${sticker('amp', { small: true })}<span><b>backstage</b><small>${fmt(s.fans.balance)} fans to spend</small></span></a>
        <a class="hubcard tilt-4" href="#/journal">${sticker('setlist', { small: true })}<span><b>tour journal</b><small>${stk} of ${MILESTONES.length} stickers</small></span></a>
      </nav>
    </section>`;

  root.querySelector('#tonight').onclick = () => { location.hash = '#/venue/' + newest.id; };

  const mapEl = root.querySelector('#map');
  let layout = '';
  function drawMap() {
    const want = mapEl.clientWidth >= 640 ? 'landscape' : 'portrait';
    if (want === layout) return;
    layout = want;
    const L = LAYOUTS[want];
    mapEl.dataset.layout = want;
    mapEl.style.aspectRatio = `${L.w} / ${L.h}`;
    const pct = ([x, y]) => `left:${(x / L.w * 100).toFixed(2)}%;top:${(y / L.h * 100).toFixed(2)}%`;
    mapEl.innerHTML = `
      ${mapSVG(L, reachedIdx)}
      ${L.decor.map(([id, x, y, r]) => `<span class="decor" style="${pct([x, y])};--r:${r}deg">${sticker(id, { small: true })}</span>`).join('')}
      ${VENUES.map((v, i) => {
        const open = P.unlocked(v.id), here = v.id === newest.id;
        return `<a class="venue ${open ? 'open' : 'locked'} ${here ? 'here' : ''}" href="#/venue/${v.id}" style="${pct(L.pts[i])};--r:${[-2, 2, -1.5, 2.5, -2.5, 1.5, -1, 2][i]}deg"
                   aria-label="${esc(v.name)}${open ? '' : `, locked, needs ${fmt(v.fans)} fans`}" data-id="${v.id}">
          <span class="vnum">${String(i + 1).padStart(2, '0')}</span>
          ${venueArt(v.art)}
          <span class="vlabel">${esc(v.short)}</span>
          ${open ? '' : `<span class="sticky">${short(v.fans)}<small>fans</small></span>`}
          ${here ? `<span class="here-tag">you’re here</span><span class="bus">${sticker('bus', { small: true, label: 'tour bus' })}</span>` : ''}
        </a>`;
      }).join('')}`;
    if (fresh.length) {
      fresh.forEach(id => {
        const el = mapEl.querySelector(`[data-id="${id}"]`);
        if (el) { el.classList.add('fresh'); setTimeout(() => { peelOn(el); snd.peel(); }, 300); }
        P.markSeen(id);
      });
      fresh.length = 0;
    }
  }
  drawMap();
  const here = mapEl.querySelector('.venue.here');
  if (here && layout === 'portrait' && reachedIdx > 1) setTimeout(() => here.scrollIntoView({ block: 'center', behavior: 'smooth' }), 400);
  const ro = new ResizeObserver(() => drawMap());
  ro.observe(mapEl);
  return () => ro.disconnect();
}
