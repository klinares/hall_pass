/* Venue card: the poster for tonight, the week picker, and the setlist of shows. */
import * as P from '../js/progress.js';
import * as store from '../js/store.js';
import { venueById, VENUES } from '../data/venues.js';
import { WEEKS } from '../data/weeks.js';
import { MODES, isAvailable } from '../modes/index.js';
import { venueArt, sticker, icon } from '../art/sticker.js';
import { fmt, esc, localISO } from '../js/util.js';

export const sortedWeeks = () => [...WEEKS].sort((a, b) => b.id.localeCompare(a.id));
export const defaultWeek = () => { const s = sortedWeeks(); return s.find(w => w.id <= localISO()) || s[s.length - 1]; };

let chosenWeek = null;   /* remembered for the session */

export function mount(root, { id }) {
  const v = venueById(id), open = P.unlocked(v.id), idx = VENUES.indexOf(v);
  const lifetime = store.get().fans.lifetime;
  const weeks = sortedWeeks();
  const wk = chosenWeek && weeks.find(w => w.id === chosenWeek) || defaultWeek();
  const spellingAll = MODES.filter(m => m.kind === 'spelling');
  const spelling = spellingAll.filter(m => isAvailable(m, wk));
  const hidden = spellingAll.filter(m => !isAvailable(m, wk));

  root.innerHTML = `
    <section class="venuepage">
      <a class="backlink" href="#/map">${icon('i-back')}the map</a>
      <div class="scrap poster ${open ? '' : 'locked'}">
        <div class="torn">
          <span class="tape tc"></span>
          <div class="poster-art">${venueArt(v.art)}</div>
          <div class="poster-copy">
            <p class="eyebrow">stop ${idx + 1} of ${VENUES.length} ✶ ${open ? 'tonight' : 'locked'}</p>
            <h1 class="shout">${esc(v.name)}</h1>
            <p>${esc(v.blurb)}</p>
            ${open ? '' : `<p class="need">${icon('i-lock')} unlocks at <b>${fmt(v.fans)}</b> fans. ${fmt(v.fans - lifetime)} to go.</p>`}
          </div>
        </div>
      </div>

      ${open ? `
      <div class="sethead">
        <h2 class="hand-h">spelling sets</h2>
        <label class="weekpick">
          <span>tour date</span>
          <select id="wk">${weeks.map(w => `<option value="${w.id}" ${w.id === wk.id ? 'selected' : ''}>${esc(w.label)}</option>`).join('')}</select>
        </label>
      </div>
      <ol class="modelist">
        ${spelling.map((m, i) => `
          <li><a class="moderow" href="#/show/${m.id}?v=${v.id}&w=${wk.id}" data-mode="${m.id}" style="--r:${i % 2 ? 1 : -1}deg">
            <span class="mno">${String(i + 1).padStart(2, '0')}</span>
            ${sticker(m.sticker, { small: true })}
            <span class="mtxt"><b>${esc(m.name)}</b><small>${esc(m.sub)}</small></span>
            <span class="marr" aria-hidden="true">→</span>
          </a></li>`).join('')}
      </ol>
      ${hidden.length ? `<p class="pencil">not on this week’s bill: ${hidden.map(m => esc(m.name)).join(' and ')}. ${hidden.some(m => m.id === 'prefix') ? 'prefix mixer plays on weeks built around prefixes. ' : ''}${hidden.some(m => m.id === 'lyric') ? 'lyric writer needs sentences for the words. ' : ''}</p>` : ''}
      <div class="sethead"><h2 class="hand-h">math sets</h2><span class="pencil small">levels move with you</span></div>
      <ol class="modelist">
        ${MODES.filter(m => m.kind === 'math').map((m, i) => `
          <li><a class="moderow math" href="#/show/${m.id}?v=${v.id}" data-mode="${m.id}" style="--r:${i % 2 ? -1 : 1}deg">
            <span class="mno">${String(i + 1).padStart(2, '0')}</span>
            ${sticker(m.sticker, { small: true })}
            <span class="mtxt"><b>${esc(m.name)}</b><small>${esc(m.sub)}</small><span class="mlvl" aria-label="level ${P.level(m.skill)} of 3">${'★'.repeat(P.level(m.skill))}${'☆'.repeat(3 - P.level(m.skill))} <small>level ${P.level(m.skill)}</small></span></span>
            <span class="marr" aria-hidden="true">→</span>
          </a></li>`).join('')}
      </ol>
      ` : `<a class="btn" href="#/venue/${P.newestVenue().id}">play at ${esc(P.newestVenue().short)} instead</a>`}
    </section>`;

  const sel = root.querySelector('#wk');
  if (sel) sel.onchange = () => { chosenWeek = sel.value; mount(root, { id }); };
}
