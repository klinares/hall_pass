/* #/office: the manager's office. A plain parent view: accuracy per mode, the words and
   problem types she misses most, time played per day, a couple of settings, and a reset
   that needs a second, explicit tap. */
import * as store from '../js/store.js';
import * as P from '../js/progress.js';
import * as M from '../js/meta.js';
import { MODES } from '../modes/index.js';
import { icon } from '../art/sticker.js';
import { applyLook } from '../js/look.js';
import { fmt, esc, localISO } from '../js/util.js';

const plural = (n, w) => `${n} ${w}${n === 1 ? '' : 's'}`;
const dash = v => v == null ? '—' : v;
const pctCell = v => v == null ? '—' : v + '%';
const day = d => new Date(d + 'T12:00').toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' }).toLowerCase();

export function mount(root) {
  P.setModes(MODES);
  P.awardStickers();
  const s = store.get();
  const o = M.office(s, MODES, localISO());
  const peak = Math.max(60, ...o.daily.map(d => d.secs));
  const table = kind => {
    const rows = o.skills.filter(k => k.kind === kind);
    return `<table class="otab">
      <thead><tr><th>mode</th><th>level</th><th>tries</th><th>right</th><th>hints</th><th>shows</th></tr></thead>
      <tbody>${rows.map(k => `<tr class="${k.a ? '' : 'unplayed'}"><th scope="row">${esc(k.name)}</th><td>${k.level}</td><td>${k.a}</td><td>${pctCell(k.acc)}</td><td>${pctCell(k.hint)}</td><td>${k.shows}</td></tr>`).join('')}</tbody>
    </table>`;
  };

  root.innerHTML = `
    <section class="office">
      <a class="backlink" href="#/map">${icon('i-back')}back to the game</a>
      <h1>manager’s office</h1>
      <p class="osub">for grown-ups. everything here is saved on this device only.</p>

      <div class="ocard">
        <h2>at a glance</h2>
        <dl class="glance">
          <div><dt>shows finished</dt><dd>${o.shows}</dd></div>
          <div><dt>time played</dt><dd>${M.mins(o.totalSecs)}</dd></div>
          <div><dt>best day streak</dt><dd>${o.best}</dd></div>
          <div><dt>total fans</dt><dd>${fmt(o.lifetime)}</dd></div>
          <div><dt>journal stickers</dt><dd>${o.stickers} / ${o.stickersTotal}</dd></div>
          <div><dt>waiting for review</dt><dd>${plural(o.review.words, 'word')}, ${plural(o.review.math, 'problem')}</dd></div>
        </dl>
      </div>

      <div class="ocard">
        <h2>time played, last 14 days</h2>
        <ul class="days">
          ${o.daily.map(d => `<li><span class="dd">${day(d.d)}</span><span class="bar"><i style="width:${(d.secs / peak * 100).toFixed(1)}%"></i></span><span class="dv">${M.mins(d.secs)}${d.shows ? `, ${d.shows} show${d.shows > 1 ? 's' : ''}` : ''}</span></li>`).join('')}
        </ul>
        <p class="ofoot">counts only while the game is on screen and she has touched it in the last minute.</p>
      </div>

      <div class="ocard">
        <h2>accuracy by mode</h2>
        <p class="ofoot">“right” means she got there before the answer was shown (hints allowed); “hints” is how often she used the bulb. levels go up after 4 right in a row and down after 2 misses in a row.</p>
        <h3>spelling</h3>
        <div class="oscroll">${table('spelling')}</div>
        <h3>math</h3>
        <div class="oscroll">${table('math')}</div>
      </div>

      <div class="ocard">
        <h2>words missed most</h2>
        ${o.words.length ? `<div class="oscroll"><table class="otab">
          <thead><tr><th>word</th><th>missed</th><th>played</th><th>1st try</th><th>last</th></tr></thead>
          <tbody>${o.words.map(w => `<tr><th scope="row">${esc(w.w)}</th><td>${w.miss}</td><td>${w.a}</td><td>${pctCell(w.first)}</td><td>${dash(w.last)}</td></tr>`).join('')}</tbody>
        </table></div>` : '<p class="oempty">no missed words yet.</p>'}
      </div>

      <div class="ocard">
        <h2>math mistakes seen most</h2>
        ${o.mistakes.length ? `<div class="oscroll"><table class="otab">
          <thead><tr><th>mistake</th><th>mode</th><th>times</th></tr></thead>
          <tbody>${o.mistakes.map(m => `<tr><th scope="row">${esc(m.label)}</th><td>${esc(m.mode)}</td><td>${m.n}</td></tr>`).join('')}</tbody>
        </table></div>
        <p class="ofoot">from wrong answers that match a known mistake. other wrong answers aren’t counted here.</p>` : '<p class="oempty">no math mistakes recorded yet.</p>'}
      </div>

      <div class="ocard">
        <h2>settings</h2>
        <label class="ocheck"><input type="checkbox" id="motion" ${s.settings.motion === 'reduced' ? 'checked' : ''}> reduce motion (also follows the device setting)</label>
      </div>

      <div class="ocard danger">
        <h2>start over</h2>
        <p>erases fans, levels, stickers, purchases and history on this device. the weekly words aren’t affected.</p>
        <button class="obtn" id="reset">reset progress…</button>
        <div class="confirm" id="confirm" hidden>
          <p><b>are you sure?</b> this can’t be undone.</p>
          <div class="orow"><button class="obtn" id="cancel">cancel</button><button class="obtn red" id="erase">yes, erase everything</button></div>
        </div>
      </div>
    </section>`;

  const $ = id => root.querySelector('#' + id);
  $('motion').onchange = e => {
    P.setSetting('motion', e.target.checked ? 'reduced' : 'auto');
    if (e.target.checked) document.documentElement.dataset.motion = 'reduced'; else delete document.documentElement.dataset.motion;
  };
  $('reset').onclick = () => { $('confirm').hidden = false; $('reset').hidden = true; $('cancel').focus(); };
  $('cancel').onclick = () => { $('confirm').hidden = true; $('reset').hidden = false; };
  $('erase').onclick = () => {
    store.reset();
    applyLook();
    delete document.documentElement.dataset.motion;
    location.hash = '#/map';
  };
}
