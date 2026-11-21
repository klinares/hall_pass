/* The show engine. A show is 10 songs from one mode. It owns scoring, the hint button,
   the dog's reactions, re-queueing missed items, and the ticket + encore at the end.
   Modes only draw their interaction and call ctx.win / ctx.miss / ctx.reveal. */
import * as P from './progress.js';
import * as snd from './sound.js';
import { burst, toast } from './fx.js';
import { dog } from '../art/dog.js';
import { icon } from '../art/sticker.js';
import { pick, shuffle, fmt, esc } from './util.js';
import * as copy from './copy.js';
import { renderTicket } from '../screens/ticket.js';
import { venueById } from '../data/venues.js';
import { wobble, hop } from './motion.js';
import { newSeed } from '../math/rng.js';
import { kid } from './items.js';
import { reviewItem } from './review.js';

const SHOW_LEN = 10;
const MAX_BONUS = 2;
let live = null;   /* the show on screen; replacing it (encore, quit) silences the old one's timers */

/* the "sound it out" hint: it never spells the whole word. Every form leaves at least a third of the
   letters hidden, and never fewer than two. In order, the first form that does:
   first and last syllable around ___ ("ir - ___ - ___ - al"); the first syllable, then one underscore per
   hidden letter ("em - ____"); first two and last two letters ("st…hs"); else the first letter and the
   length ("j___"), always for words of 3 letters or fewer */
export function chunks(v) {
  const w = v.w || '', s = (v.s || w).split('-'), hide = Math.max(2, Math.ceil(w.length / 3));
  const firstAndLength = () => w.slice(0, 1) + '_'.repeat(Math.max(0, w.length - 1));
  if (w.length <= 3) return firstAndLength();
  if (s.length >= 3 && w.length - s[0].length - s[s.length - 1].length >= hide)
    return [s[0], ...s.slice(1, -1).map(() => '___'), s[s.length - 1]].join(' - ');
  if (s.length >= 2 && w.length - s[0].length >= hide)
    return [s[0], ...s.slice(1).map(p => '_'.repeat(Math.max(1, p.length)))].join(' - ');
  if (s.length < 2 && w.length > 4 && w.length - 4 >= hide) return `${w.slice(0, 2)}…${w.slice(-2)}`;
  return firstAndLength();
}


/* math: problems saved for review first (the stored problem, exactly as she missed it), then
   fresh ones, which take her level at the moment they're played */
export function pickProblems(mode, n = SHOW_LEN) {
  const review = P.reviewMath(mode.skill).slice(0, 3).map(reviewItem);
  const fresh = Array.from({ length: n - review.length }, () => ({ skill: mode.skill, level: null, seed: newSeed() }));
  return [...review, ...fresh];
}
export const pickItems = (mode, week) => mode.kind === 'math' ? pickProblems(mode) : pickWords(week, mode);

/* pick up to 10 words: missed last time first, then least practiced, then random */
export function pickWords(week, mode, n = SHOW_LEN) {
  const review = new Set(P.reviewWords());
  const words = mode && mode.pool ? mode.pool(week) : week.words;
  const scored = shuffle(words).map(v => {
    const st = P.wordStats(v.w);
    return { v, k: (review.has(v.w) ? 0 : 1) * 1000 + (st ? st.a : 0) };
  });
  scored.sort((a, b) => a.k - b.k);
  return shuffle(scored.slice(0, Math.min(n, scored.length)).map(x => x.v));
}

export function mountShow(root, { mode, venue, week, items, encore = false }) {
  const V = venueById(venue);
  const G = {
    mode, venue: V, week, encore,
    q: items.map(v => ({ v, bonus: false, state: '' })),
    i: 0, fans: 0, streak: 0, best: 0, firstTry: 0, hintOnly: 0, hintTotal: 0,
    hinted: new Set(), missed: new Set(), bonusLeft: encore ? 0 : MAX_BONUS,
    tries: 0, hints: 0, maxHints: 0, dead: false, started: Date.now(), unlocks: [],
    skill: mode.kind === 'math' ? mode.skill : 'spell:' + mode.id, math: mode.kind === 'math'
  };
  if (live) live.dead = true;
  live = G;
  let nudgeT = 0, applyHint = null, coachPose = 'idle';
  document.body.classList.add('in-show');

  root.innerHTML = `
    <section class="show">
      <div class="showbar">
        <button class="iconbtn" id="quit" aria-label="quit show">${icon('i-x')}</button>
        <div class="where">
          <div class="eyebrow">${esc(mode.name)}${encore ? ' ✶ encore' : ''} ✶ ${esc(V.short)}</div>
          <div class="songno" id="songno"></div>
        </div>
        <div class="showstats">
          <span class="chip" title="fans this show"><b id="sfans">0</b><small>fans</small></span>
          <span class="chip" title="streak"><svg viewBox="-10 -10 124 124" aria-hidden="true"><g filter="url(#wt-stk-sm)"><use href="#s-bolt"/></g></svg><b id="sstreak">0</b></span>
        </div>
      </div>
      <div class="pips" id="pips" aria-hidden="true"></div>
      <div class="stagewrap">
        <div class="play" id="play"></div>
        <aside class="coach" id="coach">
          <div class="dog-host" id="doghost">${dog('idle')}</div>
          <div class="bubble" id="bubble" aria-live="polite">${copy.dogShowStart()}</div>
        </aside>
      </div>
    </section>`;

  const $ = id => root.querySelector('#' + id);
  $('quit').onclick = () => { location.hash = '#/venue/' + V.id; };

  function sayDog(html, pose) {
    $('bubble').innerHTML = html;
    if (pose && pose !== coachPose) { coachPose = pose; $('doghost').innerHTML = dog(pose); if (pose === 'cheer') hop($('doghost')); }
  }
  function stats() {
    $('sfans').textContent = fmt(G.fans);
    $('sstreak').textContent = G.streak;
    const real = G.q.filter(x => !x.bonus).length, cur = G.q[G.i];
    const n = G.q.slice(0, G.i + 1).filter(x => !x.bonus).length;
    $('songno').innerHTML = cur && cur.bonus ? `bonus song <span>✶ one more try</span>` : `song ${Math.min(n, real)} <span>of ${real}</span>`;
    $('pips').innerHTML = G.q.map((x, i) => `<i class="${x.bonus ? 'bonus ' : ''}${i < G.i ? (x.state === 'miss' ? 'miss' : 'done') : i === G.i ? 'now' : ''}"></i>`).join('');
  }
  const later = (fn, ms) => { const g = G; setTimeout(() => { if (!g.dead) fn(); }, ms); };

  /* ---------- hint button ---------- */
  const hintBtn = () => `<button class="btn ghost sm hintbtn" id="hint">${icon('i-bulb')}<span id="hintlbl">hint</span></button>`;
  function paintHint() {
    const b = $('hint'); if (!b) return;
    const left = G.maxHints - G.hints;
    b.disabled = left <= 0;
    $('hintlbl').innerHTML = left > 0 ? `hint <small>(${left})</small>` : 'no hints left';
  }
  function armNudge() {
    clearTimeout(nudgeT);
    nudgeT = setTimeout(() => {
      const b = $('hint');
      if (b && !b.disabled && !G.dead) { b.classList.add('nudge'); sayDog(copy.dogIdleHint(), 'think'); }
    }, 15000);
  }
  function lockHints() { clearTimeout(nudgeT); const b = $('hint'); if (b) b.disabled = true; }
  function setupHints(max, apply) {
    G.maxHints = max; G.hints = 0; applyHint = apply; paintHint(); armNudge();
    $('hint').onclick = () => {
      if (G.hints >= G.maxHints) return;
      if (applyHint(G.hints + 1) === false) return;     /* nothing new to show: free */
      G.hints++; G.hintTotal++; G.hinted.add(kid(G.cur.v));
      snd.tap(); paintHint(); $('hint').classList.remove('nudge'); armNudge();
      if (coachPose !== 'think') { coachPose = 'think'; $('doghost').innerHTML = dog('think'); }
    };
  }

  /* ---------- outcomes ---------- */
  function record(clean, missed) {
    const v = G.cur.v, keep = G.missed.has(kid(v));
    if (G.math) P.recordMath(G.skill, v, { clean, missed, keep });
    else P.recordWord(v.w, { clean, hinted: G.hints > 0, missed, keep });
  }
  /* extra: bonus fans on top (soundcheck's beat-the-clock) */
  function win(extra = 0) {
    lockHints();
    const clean = G.tries === 0 && G.hints === 0;
    let gain;
    if (clean) { G.streak++; G.firstTry++; G.best = Math.max(G.best, G.streak); gain = 1000 * Math.min(G.streak, 5); }
    else if (G.tries === 0) { gain = 500; G.hintOnly++; }      /* hints but no wrong guesses: streak is safe */
    else gain = 300;
    if (G.encore) gain = Math.round(gain / 2);
    gain += extra;
    G.fans += gain;
    G.unlocks.push(...P.addFans(gain));
    record(clean, G.tries > 0);
    P.recordSkill(G.skill, { correct: true, hinted: G.hints > 0 });
    G.cur.state = G.tries > 0 ? 'miss' : 'done';
    snd.strum(); burst(clean ? 34 : 18);
    toast(clean && G.streak >= 3 ? `${G.streak} in a row!` : pick(copy.CHEERS));
    sayDog(clean ? copy.dogWin(G.streak) : G.tries === 0 ? copy.dogHintWin() : copy.dogAfterMiss(), 'cheer');
    stats();
    bump('sfans');
    if (G.tries > 0) requeue();
  }
  function miss() {
    G.tries++; G.streak = 0; G.missed.add(kid(G.cur.v));
    snd.thud(); stats();
    wobble($('play').querySelector('[data-wobble]') || $('play'));
    return G.tries;
  }
  function ladder() {
    const v = G.cur.v;
    return G.tries === 1
      ? `not quite. it’s <b>${v.w.length} letters</b> and starts with <b>“${v.w[0]}”</b>.`
      : `keep going${G.tries === 2 && Math.random() < .5 ? ', ' + copy.NAME : ''}. sound it out: <b>${chunks(v)}</b>`;
  }
  function reveal(msg) {
    lockHints();
    const v = G.cur.v;
    record(false, true);
    P.recordSkill(G.skill, { correct: false, hinted: G.hints > 0 });
    G.cur.state = 'miss';
    sayDog(msg || `it’s <b>${v.w}</b>. look closely, it’ll come back around.`, 'oops');
    requeue();
    stats();
  }
  function requeue() {
    if (G.bonusLeft <= 0 || G.cur.bonus) return;
    G.bonusLeft--;
    const at = Math.min(G.i + 4, G.q.length);
    G.q.splice(at, 0, { v: G.cur.v, bonus: true, state: '' });
  }
  function bump(id) { const el = $(id)?.closest('.chip'); if (!el) return; el.classList.remove('bump'); void el.offsetWidth; el.classList.add('bump'); }

  function next() { G.i++; show(); }

  function show() {
    if (G.dead) return;
    if (G.i >= G.q.length) return finish();
    G.tries = 0; G.hints = 0; G.maxHints = 0; clearTimeout(nudgeT);
    G.cur = G.q[G.i];
    stats();
    if (G.i > 0) sayDog(G.cur.bonus ? `this one again. you’ve got it now.` : pick(['next song.', 'keep it rolling.', 'crowd’s warmed up.', 'next one.']), 'idle');
    if (G.math) {
      const v = G.cur.v;
      if (v.level == null) v.level = P.level(G.skill);
      if (!v.prob) v.prob = mode.generate(v.level, v.seed);   /* a bonus replay reuses the same problem */
    }
    const play = $('play');
    play.innerHTML = '';
    play.dataset.item = kid(G.cur.v);      /* lets the browser tests find the problem (review queue, or regenerate from its seed) */
    mode.render({
      el: play, item: G.cur.v, prob: G.cur.v.prob, week: G.week, level: G.math ? G.cur.v.level : P.level(G.skill), bonus: G.cur.bonus,
      mistake: tag => P.noteMistake(G.skill, tag),
      get tries() { return G.tries; }, get hints() { return G.hints; },
      hintBtn, setupHints, lockHints, win, miss, ladder, reveal, later, say: sayDog,
      next: (ms = 0) => later(next, ms)
    });
  }

  function finish() {
    clearTimeout(nudgeT);
    const prim = G.q.filter(x => !x.bonus), n = prim.length;
    const pct = (G.firstTry + .5 * G.hintOnly) / Math.max(1, n);
    const secs = Math.round((Date.now() - G.started) / 1000);
    P.finishShow({ mode: mode.id, venue: V.id, n, first: G.firstTry, hinted: G.hinted.size, fans: G.fans, secs, best: G.best, sold: !G.encore && n >= 5 && pct >= .9 || undefined, encore: G.encore || undefined });
    const stickers = P.awardStickers();
    document.body.classList.remove('in-show');
    renderTicket(root, {
      G, pct, n, mode, venue: V, stickers,
      onEncore: () => mountShow(root, { mode, venue: V.id, week: G.week, items: shuffle(prim.filter(x => G.missed.has(kid(x.v))).map(x => x.v)), encore: true }),
      onAgain: () => mountShow(root, { mode, venue: V.id, week: G.week, items: pickItems(mode, G.week) })
    });
    snd.crowd(V.crowd);
    setTimeout(() => burst(pct >= .9 ? 120 : 50), 250);
  }

  show();
  return () => { if (live) live.dead = true; G.dead = true; clearTimeout(nudgeT); document.body.classList.remove('in-show'); };
}
