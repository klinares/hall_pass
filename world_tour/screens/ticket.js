/* End of show: the ticket stub, the setlist, and the encore. */
import { fmt, esc, localISO } from '../js/util.js';
import { dog } from '../art/dog.js';
import { sticker, venueArt, journalArt as art } from '../art/sticker.js';
import * as copy from '../js/copy.js';
import { peelOn } from '../js/motion.js';
import { kid, labelOf } from '../js/items.js';
import { rich } from '../modes/math/parts.js';

function barcode(seed) {
  let x = 0; for (const c of seed) x = (x * 31 + c.charCodeAt(0)) >>> 0;
  let bars = '', pos = 0;
  while (pos < 100) { x = (x * 1103515245 + 12345) >>> 0; const w = 1 + (x >>> 16) % 4; if ((x >>> 8) & 1) bars += `<rect x="${pos}" y="0" width="${w}" height="30"/>`; pos += w + 1; }
  return `<svg class="barcode" viewBox="0 0 100 30" preserveAspectRatio="none" aria-hidden="true"><g style="fill:var(--ink)">${bars}</g></svg>`;
}

export function renderTicket(root, { G, pct, n, mode, venue, stickers = [], onEncore, onAgain }) {
  const vip = pct >= .9;
  const prim = G.q.filter(x => !x.bonus);
  const missedPrim = prim.filter(x => G.missed.has(kid(x.v)));
  const tag = v => G.missed.has(kid(v)) ? ['miss', 'rehearse again'] : G.hinted.has(kid(v)) ? ['hint', 'used a hint'] : ['ok', 'nailed it'];
  const date = localISO();
  const unlock = G.unlocks[G.unlocks.length - 1];

  root.innerHTML = `
    <section class="finish">
      <p class="eyebrow">${G.math ? 'math set' : esc(G.week.label)} ✶ ${esc(mode.name)}${G.encore ? ' encore' : ''}</p>
      <h1 class="shout">${copy.finishHead(pct)}</h1>
      <p class="lede">${copy.finishSub(pct)}</p>

      <div class="scrap ticketwrap">
        <div class="ticket">
          <div class="tk-main">
            <div class="tk-top">admit one ♥</div>
            <div class="tk-venue">live at ${esc(venue.name)}</div>
            <div class="tk-fans"><b>${fmt(G.fans)}</b> fans</div>
            <dl class="tk-rows">
              <div><dt>first try</dt><dd>${G.firstTry}/${n}</dd></div>
              <div><dt>best streak</dt><dd>${G.best}</dd></div>
              <div><dt>hints</dt><dd>${G.hintTotal}</dd></div>
            </dl>
          </div>
          <div class="tk-stub perf">
            <span class="tk-class">${vip ? 'vip' : 'ga'}</span>
            ${barcode(date + mode.id + G.fans)}
            <span class="tk-date">${date}</span>
          </div>
        </div>
        ${vip ? `<span class="stamp">sold<br>out</span>` : ''}
      </div>

      ${stickers.length ? `
      <a class="unlock newstk" href="#/journal">
        <span class="newstk-row">${stickers.slice(0, 3).map(m => art(m.art)).join('')}</span>
        <div><p class="eyebrow">new for your tour journal</p><p class="hand-h">${stickers.slice(0, 3).map(m => esc(m.name)).join(', ')}${stickers.length > 3 ? ` + ${stickers.length - 3} more` : ''}</p></div>
      </a>` : ''}

      ${unlock ? `
      <div class="unlock">
        ${venueArt(unlock.art)}
        <div><p class="eyebrow">new venue unlocked</p><p class="hand-h">${esc(unlock.name)}</p></div>
      </div>` : ''}

      <div class="scrap setwrap">
        <div class="torn lined setpaper">
          <span class="tape tr b"></span>
          <h2 class="hand-h">tonight’s setlist</h2>
          <ol class="setlist">
            ${prim.map(x => { const [c, t] = tag(x.v); return `<li class="${c}"><span class="w">${x.v.w ? esc(x.v.w) : rich(labelOf(x.v))}</span><span class="t">${t}</span></li>`; }).join('')}
          </ol>
        </div>
      </div>

      <div class="row finish-actions">
        ${missedPrim.length && !G.encore ? `<button class="btn grow" id="encore">encore: ${missedPrim.length} to rehearse</button>` : `<button class="btn grow" id="again">play again</button>`}
        <a class="btn ghost grow" href="#/map">back to the map</a>
      </div>

      <div class="coach">
        <div class="dog-host">${dog('cheer')}</div>
        <div class="bubble">${missedPrim.length && !G.encore ? `the encore replays the ${missedPrim.length === 1 ? 'one you missed' : missedPrim.length + ' you missed'}. half fans, zero pressure.` : vip ? 'that’s a vip night. framing this ticket.' : 'good show. the bus leaves when you’re ready.'}</div>
      </div>
    </section>`;

  const e = root.querySelector('#encore'); if (e) e.onclick = onEncore;
  const a = root.querySelector('#again'); if (a) a.onclick = onAgain;
  window.scrollTo(0, 0);
  const st = root.querySelector('.stamp'); if (st) setTimeout(() => peelOn(st), 450);
  const tw = root.querySelector('.ticketwrap'); if (tw) peelOn(tw);
}
