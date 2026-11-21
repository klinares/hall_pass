/* Dev-only browser check for world_tour.
   Serves the repo root and drives the game like a player:
   - full show of every spelling mode on a phone (Chromium) and iPad portrait (WebKit),
     with a wrong answer and a hint in each, through the ticket
   - every mode's first song + hint at the other sizes, with screenshots
   - map → venue → remix → ticket → map at all sizes (fans must save)
   - a run with localStorage throwing, and one with reduced motion
   - math: full shows of every math mode (level 1 and level 3), first song + hint at other sizes,
     the missed problem saved for review, and math with storage off and reduced motion
   - meta: backstage purchase + theme, journal drag and tap-to-place, office stats + reset,
     a sticker earned at the end of a show, and the three rooms with storage off
   Exits non-zero on any console error.
   usage: node play.mjs [--only=chromium|webkit] [--suite=spelling|spelling-more|math|math-more|meta]
   Run each engine separately, in the foreground, so a hang shows up as a timeout. */
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium, webkit } from 'playwright';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../..');
const shots = path.join(here, 'screenshots');
fs.mkdirSync(shots, { recursive: true });

const TYPES = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.svg': 'image/svg+xml', '.json': 'application/json', '.png': 'image/png' };
const server = http.createServer((req, res) => {
  let p = decodeURIComponent(new URL(req.url, 'http://x').pathname);
  if (p.endsWith('/')) p += 'index.html';
  const f = path.join(repo, p);
  if (!f.startsWith(repo) || !fs.existsSync(f)) { res.writeHead(404); return res.end('nope'); }
  res.writeHead(200, { 'content-type': TYPES[path.extname(f)] || 'application/octet-stream' });
  fs.createReadStream(f).pipe(res);
});
await new Promise(r => server.listen(0, '127.0.0.1', r));
const BASE = `http://127.0.0.1:${server.address().port}/world_tour/`;

const SIZES = {
  phone: { width: 375, height: 812, touch: true },
  'ipad-portrait': { width: 820, height: 1180, touch: true },
  'ipad-landscape': { width: 1180, height: 820, touch: true },
  laptop: { width: 1366, height: 768, touch: false }
};
const MODES = ['slots', 'vowels', 'remix', 'fanmail', 'studio', 'lyric', 'prefix'];
const WEEK = '2026-10-26';   /* the prefix week, so every mode is on the bill */
const args = process.argv.slice(2);
const only = (args.find(a => a.startsWith('--only=')) || '').split('=')[1];
const quick = args.includes('--quick');
const suiteArg = (args.find(a => a.startsWith('--suite=')) || '').split('=')[1];   /* spelling | math | math-more | (all) */
const MATH = ['merch', 'beat', 'tickets', 'stage', 'truck', 'budget', 'soundcheck'];
const errors = [];
const wait = ms => new Promise(r => setTimeout(r, ms));

/* tally: every check is one test; it fails on an exception or any console error raised while it ran */
const results = [];
async function test(suite, browser, size, name, fn) {
  const before = errors.length;
  let ok = true, note = '';
  try { await fn(); } catch (e) { ok = false; note = e.message.split(/\r?\n/)[0]; errors.push(`[${browser}-${size}] ${suite}/${name}: ${note}`); }
  if (errors.length > before) { ok = false; note = note || errors.slice(before).join(' | ').slice(0, 200); }
  results.push({ suite, browser, size, name, ok, note });
}

function watch(page, tag) {
  page.on('console', m => { if (m.type() === 'error') errors.push(`[${tag}] console: ${m.text()}`); });
  page.on('pageerror', e => errors.push(`[${tag}] pageerror: ${e.message}`));
  page.on('requestfailed', r => { if (!r.url().includes('fonts.g')) errors.push(`[${tag}] request failed: ${r.url()}`); });
}

/* record what the game asks text-to-speech to say (studio session hides the word) */
const SPY = () => {
  window.__spoken = [];
  if ('speechSynthesis' in window) {
    const real = speechSynthesis.speak.bind(speechSynthesis);
    speechSynthesis.speak = u => { window.__spoken.push(u.text); try { real(u); } catch (e) { /* headless */ } };
  }
};

/* screenshot prefix: chromium keeps the plain names, other engines get theirs in front */
const pre = name => name === 'chromium' ? '' : name + '-';

async function newPage(browser, size, tag, extra = {}) {
  const s = SIZES[size];
  const ctx = await browser.newContext({ viewport: { width: s.width, height: s.height }, hasTouch: s.touch, deviceScaleFactor: 1, ...extra });
  await ctx.addInitScript(SPY);
  const page = await ctx.newPage();
  watch(page, tag);
  return { ctx, page };
}

/* work out tonight's word from what's on screen */
async function currentWord(page) {
  return page.evaluate(async () => {
    const { WEEKS } = await import('./data/weeks.js');
    const words = WEEKS.flatMap(w => w.words);
    const txt = s => document.querySelector(s)?.textContent.trim();
    const clue = txt('.clue p');
    if (clue) { const v = words.find(v => v.h === clue); if (v) return v; }
    const dm = txt('.dm-msg i');
    if (dm) { const v = words.find(v => v.h.toLowerCase() === dm); if (v) return v; }
    const before = document.querySelector('.lyric-line')?.dataset.before;
    if (before != null) { const v = words.find(v => v.x && v.x.split('___')[0] === before); if (v) return v; }
    const root = txt('#root');
    if (root) { const v = words.find(v => v.prefix && v.prefix.root === root); if (v) return v; }
    const said = (window.__spoken || []).filter(t => t.trim()).pop();
    if (said && document.querySelector('#spk')) { const v = words.find(v => v.w === said); if (v) return v; }
    return null;
  });
}

async function tapLetters(page, word) {
  for (const ch of word) {
    await page.locator('.tile:not(.used):not(.picked):not([disabled])', { hasText: new RegExp(`^${ch}$`) }).first().click();
    await wait(170);
  }
}

async function dragTo(page, from, to) {
  const a = await page.locator(from).boundingBox(), b = await page.locator(to).boundingBox();
  await page.mouse.move(a.x + a.width / 2, a.y + a.height / 2);
  await page.mouse.down();
  await page.mouse.move(a.x + a.width / 2 + 20, a.y + a.height / 2 - 10, { steps: 4 });
  await page.mouse.move(b.x + b.width / 2, b.y + b.height / 2, { steps: 12 });
  await page.mouse.up();
}

/* answer one song. wrong: give one wrong answer first. useDrag: prefix by drag instead of tap-to-place */
async function answer(page, mode, v, { wrong = false, useDrag = true } = {}) {
  const has = async s => (await page.locator(s).count()) > 0;
  if (mode === 'remix') {
    if (await page.locator('.slot.fill').count()) { await page.click('#clear'); await wait(120); }
    if (wrong) { const w = v.w[0] !== v.w[1] ? v.w[1] + v.w[0] + v.w.slice(2) : [...v.w].reverse().join(''); await tapLetters(page, w); await wait(1000); }
    await tapLetters(page, v.w);
    return;
  }
  if (mode === 'prefix') {
    if (wrong) {
      const bad = await page.locator(`.pchip:not([disabled]):not([data-p="${v.prefix.p}"])`).first().getAttribute('data-p');
      await page.click(`.pchip[data-p="${bad}"]`); await page.click('#drop'); await wait(800);
    }
    if (useDrag) await dragTo(page, `.pchip[data-p="${v.prefix.p}"]`, '#drop');
    else { await page.click(`.pchip[data-p="${v.prefix.p}"]`); await page.click('#drop'); }
    await page.waitForSelector('#step2:not([hidden])');
    await wait(250);
    await page.click(`#step2 .choice[data-v="${v.prefix.means}"]`);
    return;
  }
  if (await has('#ans')) {
    if (wrong) { await page.fill('#ans', 'zzzz'); await page.click('#go'); await wait(500); }
    await page.fill('#ans', v.w); await page.click('#go');
    return;
  }
  /* multiple choice: fan mail (one shot) or lyric writer (pick) */
  if (wrong) {
    await page.locator(`.choice:not([disabled]):not([data-v="${v.w}"])`).first().click(); await wait(500);
    if (mode === 'fanmail') { await page.click('#nx'); return; }
  }
  await page.click(`.choice[data-v="${v.w}"]`);
}

async function playShow(page, mode, shot) {
  for (let song = 0; song < 20; song++) {
    await page.waitForSelector('.play #hint, .finish', { timeout: 10000 });
    if (await page.locator('.finish').count()) break;
    await wait(450);
    const v = await currentWord(page);
    if (!v) throw new Error(`${mode}: could not work out the word on song ${song + 1}`);
    if (song === 2) { await page.click('#hint'); await wait(350); if (shot) await shot(`${mode}-hint`); }
    await answer(page, mode, v, { wrong: song === 1, useDrag: song % 2 === 0 });
    if (song === 1 && shot) { await wait(300); await shot(`${mode}-miss`); }
    await page.waitForFunction(() => !document.querySelector('.play #hint') || document.querySelector('.play #hint').disabled || document.querySelector('.finish'), null, { timeout: 8000 }).catch(() => {});
    await wait(mode === 'prefix' ? 2300 : 1700);
    if (await page.locator('#nx:not([hidden])').count()) { await page.click('#nx'); await wait(300); }
  }
  await page.waitForSelector('.finish', { timeout: 10000 });
  await wait(700);
}

async function fullShows(browserType, label, size) {
  const browser = await browserType.launch();
  for (const mode of MODES) {
    const tag = `${label}-${size}`;
    const { ctx, page } = await newPage(browser, size, tag);
    const shot = n => page.screenshot({ path: path.join(shots, `${tag}-${n}.png`) });
    await test('full show', label, size, mode, async () => {
      await page.goto(`${BASE}#/show/${mode}?v=coffee&w=${WEEK}`);
      await page.waitForSelector('.play #hint'); await page.evaluate(() => document.fonts.ready); await wait(500);
      await shot(`${mode}-start`);
      try { await playShow(page, mode, shot); await page.screenshot({ path: path.join(shots, `${tag}-${mode}-ticket.png`), fullPage: true }); }
      catch (e) { await shot(`${mode}-FAILED`).catch(() => {}); throw e; }
    });
    await ctx.close();
  }
  await browser.close();
}

async function firstSongs(browserType, label, size) {
  const browser = await browserType.launch();
  const tag = `${label}-${size}`;
  const { ctx, page } = await newPage(browser, size, tag);
  for (const mode of MODES) await test('first song + hint', label, size, mode, async () => {
    await page.goto(`${BASE}#/show/${mode}?v=coffee&w=${WEEK}`);
    await page.waitForSelector('.play #hint'); await page.evaluate(() => document.fonts.ready); await wait(450);
    await page.screenshot({ path: path.join(shots, `${tag}-${mode}-start.png`) });
    await page.click('#hint'); await wait(350);
    await page.screenshot({ path: path.join(shots, `${tag}-${mode}-hint.png`) });
    const v = await currentWord(page);
    if (!v) throw new Error('no word found on screen');
    await answer(page, mode, v, { useDrag: true });
    await wait(500);
    if (!(await page.locator('.play #hint[disabled]').count())) throw new Error('song did not resolve after the right answer');
    await page.screenshot({ path: path.join(shots, `${tag}-${mode}-solved.png`) });
  });
  await ctx.close(); await browser.close();
}

async function tourFlow(browserType, label, size) {
  const browser = await browserType.launch();
  const tag = `${label}-${size}`;
  const { ctx, page } = await newPage(browser, size, tag);
  const shot = (n, full = true) => page.screenshot({ path: path.join(shots, `${tag}-${n}.png`), fullPage: full });
  await page.goto(BASE + '#/map');
  await page.waitForSelector('.venue'); await page.evaluate(() => document.fonts.ready); await wait(500);
  await shot('map');
  await page.click('#tonight'); await page.waitForSelector('.moderow'); await wait(400);
  await shot('venue');
  await page.click('.moderow[data-mode="remix"]'); await page.waitForSelector('.tile');
  await playShow(page, 'remix', null);
  await shot('ticket');
  await page.click('a[href="#/map"].btn'); await page.waitForSelector('.venue'); await wait(500);
  const fans = await page.evaluate(() => JSON.parse(localStorage.getItem('worldtour.v1')).fans.lifetime);
  if (!(fans > 0)) throw new Error(`fans did not save (got ${fans})`);
  /* a missed word should be first up next session */
  const review = await page.evaluate(() => JSON.parse(localStorage.getItem('worldtour.v1')).review.words);
  if (!review.length) throw new Error('missed word was not saved for review');
  await shot('map-after');
  await ctx.close(); await browser.close();
}

async function noStorage(bt = chromium, name = 'chromium') {
  const browser = await bt.launch();
  const { page } = await newPage(browser, 'phone', pre(name) + 'nostorage');
  await page.context().addInitScript(() => { Object.defineProperty(window, 'localStorage', { get() { throw new DOMException('denied', 'SecurityError'); } }); });
  await page.goto(BASE + '#/map');
  await page.waitForSelector('.venue');
  if (!(await page.isVisible('#nosave'))) throw new Error('the "won\'t save" note is not showing');
  await page.goto(`${BASE}#/show/fanmail?v=coffee&w=${WEEK}`);
  await playShow(page, 'fanmail', null);
  await page.screenshot({ path: path.join(shots, pre(name) + 'nostorage-ticket.png'), fullPage: true });
  await browser.close();
}

async function reducedMotion(bt = chromium, name = 'chromium') {
  const browser = await bt.launch();
  const { page } = await newPage(browser, 'ipad-portrait', pre(name) + 'reduced', { reducedMotion: 'reduce' });
  await page.goto(`${BASE}#/show/prefix?v=coffee&w=${WEEK}`);
  await page.waitForSelector('.pchip');
  const v = await currentWord(page);
  await answer(page, 'prefix', v, { useDrag: false }); await wait(300);
  await page.screenshot({ path: path.join(shots, pre(name) + 'reduced-prefix.png') });
  await browser.close();
}

/* ================= math ================= */

/* every math level is set before each page load (the game's own pagehide save can't undo it) */
const forceLevel = lv => {
  if (!lv) return;
  const sk = {};
  for (const id of ['dec', 'md', 'frac', 'xy', 'vol', 'ops', 'flu']) sk[id] = { level: lv, run: 0, missRun: 0, a: 0, c: 0, h: 0, byLevel: { 1: [0, 0], 2: [0, 0], 3: [0, 0] }, mistakes: {} };
  try { localStorage.setItem('worldtour.v1', JSON.stringify({ v: 1, skills: sk })); } catch (e) { /* storage off */ }
};

/* the problem on screen: the stored copy if it came from the review queue, else regenerated from the seed on #play */
async function mathProblem(page) {
  return page.evaluate(async () => {
    const it = document.querySelector('#play')?.dataset.item;
    if (!it || !it.includes(':')) return null;
    const [skill, level, seed] = it.split(':');
    const g = await import('./math/gen/index.js'), R = await import('./math/rational.js'), F = await import('./math/gen/fractions.js');
    let saved = null;
    try { saved = (JSON.parse(localStorage.getItem('worldtour.v1'))?.review?.math || []).find(r => r.skill === skill && r.level === +level && r.seed === +seed && r.prob); } catch (e) { /* storage off */ }
    const p = saved ? saved.prob : g.generate(skill, +level, +seed), a = p.answer;
    const text = a.type === 'money' ? (a.value / 100).toFixed(2) : a.type === 'int' ? String(a.value)
      : a.type === 'fraction' ? R.str(a.value) : a.type === 'decimal' ? R.dec(a.value) : null;
    let fill = null, cut = null;
    if (skill === 'frac' && p.builder) {
      if (p.params.removable) {
        const pre = p.params.pre, want = a.value.n / a.value.d;
        for (let m = 1; m < 1 << pre.length && !cut; m++) {
          const ds = pre.filter((_, i) => m & (1 << i));
          if (Math.abs(ds.reduce((s, d) => s + 1 / d, 0) - want) < 1e-9) cut = ds;
        }
      } else fill = F.decompose(a.value, p.params.palette);
    }
    return { skill, level: +level, kind: p.kind, answer: a, text, params: p.params, choices: p.choices, builder: !!p.builder, fill, cut };
  });
}

async function typeKeys(page, text) {
  for (const ch of text) { await page.click(`.keypad [data-k="${ch}"]`); await wait(40); }
}

async function clickPoint(page, pt, max) {
  await page.locator('#plot svg').scrollIntoViewIfNeeded();
  const box = await page.locator('#plot svg').boundingBox(), S = max + 1.6;
  await page.mouse.click(box.x + (pt.x + 1.1) / S * box.width, box.y + (max - pt.y + .5) / S * box.height);
  await wait(80);
}

/* answer one math song; wrong: one wrong answer first */
async function answerMath(page, mode, p, { wrong = false } = {}) {
  const typed = async () => {
    if (wrong) { await typeKeys(page, p.text === '1' ? '2' : '1'); await page.click('#go'); await wait(450); }
    await typeKeys(page, p.text); await page.click('#go');
  };
  if (mode === 'beat' && p.builder) {
    if (wrong) { await page.click('#playit'); await wait(500); }
    if (p.params.removable) for (const d of p.cut) { await page.locator(`.track button.note.pre.n${d}`).first().click(); await wait(80); }
    else for (const d of p.fill) { await page.click(`.nblock[data-d="${d}"]`); await wait(80); }
    await page.click('#playit');
    return;
  }
  if (mode === 'truck' && p.builder) {
    if (wrong) { await page.click('#go'); await wait(450); }
    for (const spot of await page.locator('.spot').all()) for (let k = 0; k < p.answer.value; k++) { await spot.click(); await wait(30); }
    await page.click('#go');
    return;
  }
  if (mode === 'stage') {
    const a = p.answer.value, max = p.params.max;
    if (p.kind === 'read') {
      const cur = { x: null, y: null };
      const setTo = async (k, v) => {
        const id = k === 'x' ? '#px' : '#py';
        if (cur[k] === null) { if (v === 0) await page.click(`${id} button[data-d="-1"]`); else for (let i = 0; i < v; i++) await page.click(`${id} button[data-d="1"]`); }
        else { const d = v - cur[k]; for (let i = 0; i < Math.abs(d); i++) await page.click(`${id} button[data-d="${d > 0 ? 1 : -1}"]`); }
        cur[k] = v;
      };
      if (wrong) { await setTo('x', a.x === max ? a.x - 1 : a.x + 1); await setTo('y', a.y); await page.click('#set'); await wait(450); }
      await setTo('x', a.x); await setTo('y', a.y); await page.click('#set');
      return;
    }
    if (wrong) { await clickPoint(page, { x: a.x === max ? a.x - 1 : a.x + 1, y: a.y }, max); await page.click('#set'); await wait(450); }
    await clickPoint(page, a, max);
    const shown = await page.textContent('#pair');
    if (shown.replace(/\s/g, '') !== `(${a.x},${a.y})`) throw new Error(`tap landed on ${shown}, wanted (${a.x}, ${a.y})`);
    await page.click('#set');
    return;
  }
  if (mode === 'soundcheck' || (mode === 'budget' && p.kind === 'story')) {
    const right = p.choices.findIndex(c => c.correct), bad = p.choices.findIndex(c => !c.correct);
    const sel = mode === 'budget' ? '#ex .choice' : '#ch .choice';
    /* budget: the second hint already picks the expression, so skip straight to the keypad */
    const picked = mode === 'budget' && (await page.locator('#padslot:not([hidden])').count()) > 0;
    if (!picked) {
      if (wrong) { await page.click(`${sel}[data-i="${bad}"]`); await wait(450); }
      await page.click(`${sel}[data-i="${right}"]`);
    }
    if (mode === 'budget') { await page.waitForSelector('#padslot:not([hidden])'); await wait(250); await typeKeys(page, p.text); await page.click('#go'); }
    return;
  }
  await typed();
}

/* wait until the next song is drawn (or the ticket), however long the win animation runs */
async function mathShow(page, mode, shot) {
  for (let song = 0; song < 20; song++) {
    await page.waitForSelector('.play #hint, .finish', { timeout: 10000 });
    if (await page.locator('.finish').count()) break;
    await wait(350);
    const p = await mathProblem(page);
    if (!p) throw new Error(`${mode}: no problem on song ${song + 1}`);
    if (song === 2) { await page.click('#hint'); await wait(350); if (shot) await shot(`${mode}-hint`); }
    await page.evaluate(() => { const c = document.querySelector('#play > *'); if (c) c.dataset.old = '1'; });
    await answerMath(page, mode, p, { wrong: song === 1 });
    if (song === 1 && shot) { await wait(250); await shot(`${mode}-miss`); }
    await page.waitForFunction(() => {
      if (document.querySelector('.finish')) return true;
      const nx = document.querySelector('#nx:not([hidden])'); if (nx) { nx.click(); return false; }
      const c = document.querySelector('#play > *'); return c && !c.dataset.old;
    }, null, { timeout: 12000, polling: 150 }).catch(() => { throw new Error(`${mode}: song ${song + 1} (${p.kind}, level ${p.level}) never moved on`); });
  }
  await page.waitForSelector('.finish', { timeout: 10000 });
  await wait(600);
}

async function mathFull(browserType, label, size, level) {
  const browser = await browserType.launch();
  for (const mode of MATH) {
    const tag = `${label}-${size}-L${level}`;
    const { ctx, page } = await newPage(browser, size, tag);
    if (level > 1) await ctx.addInitScript(forceLevel, level);
    const shot = n => page.screenshot({ path: path.join(shots, `${tag}-${n}.png`) });
    await test(`math full show L${level}`, label, size, mode, async () => {
      await page.goto(`${BASE}#/show/${mode}?v=coffee`);
      await page.waitForSelector('.play #hint'); await page.evaluate(() => document.fonts.ready); await wait(400);
      await shot(`${mode}-start`);
      try { await mathShow(page, mode, shot); await page.screenshot({ path: path.join(shots, `${tag}-${mode}-ticket.png`), fullPage: true }); }
      catch (e) { await shot(`${mode}-FAILED`).catch(() => {}); throw e; }
      /* the problem missed on song 2 is saved whole for next session; the skill was recorded */
      const st = await page.evaluate(() => JSON.parse(localStorage.getItem('worldtour.v1')));
      const skill = { merch: 'dec', beat: 'frac', tickets: 'md', stage: 'xy', truck: 'vol', budget: 'ops', soundcheck: 'flu' }[mode];
      const saved = st.review.math.find(r => r.skill === skill);
      if (!saved) throw new Error('missed problem was not saved for review');
      if (!(saved.prob && saved.prob.prompt && saved.prob.answer && Array.isArray(saved.prob.steps))) throw new Error('saved problem is missing its prompt, answer or hints');
      if (!(st.skills[skill]?.a >= 10)) throw new Error('skill attempts not recorded');
    });
    await ctx.close();
  }
  await browser.close();
}

async function mathFirst(browserType, label, size, level) {
  const browser = await browserType.launch();
  const tag = `${label}-${size}-L${level}`;
  const { ctx, page } = await newPage(browser, size, tag);
  await ctx.addInitScript(forceLevel, level);
  for (const mode of MATH) await test(`math first song L${level}`, label, size, mode, async () => {
    await page.goto(`${BASE}#/show/${mode}?v=coffee`);
    await page.waitForSelector('.play #hint'); await page.evaluate(() => document.fonts.ready); await wait(400);
    await page.screenshot({ path: path.join(shots, `${tag}-${mode}-start.png`), fullPage: true });
    await page.click('#hint'); await wait(300);
    await page.click('#hint'); await wait(350);
    await page.screenshot({ path: path.join(shots, `${tag}-${mode}-hint.png`), fullPage: true });
    const p = await mathProblem(page);
    await answerMath(page, mode, p);
    await page.waitForSelector('.play #hint[disabled]', { timeout: 4000 }).catch(() => { throw new Error(`song did not resolve after the right answer (${p.kind})`); });
    await wait(300);
    await page.screenshot({ path: path.join(shots, `${tag}-${mode}-solved.png`) });
  });
  await ctx.close(); await browser.close();
}

async function mathNoStorage(bt = chromium, name = 'chromium') {
  const browser = await bt.launch();
  const { page } = await newPage(browser, 'phone', pre(name) + 'nostorage-math');
  await page.context().addInitScript(() => { Object.defineProperty(window, 'localStorage', { get() { throw new DOMException('denied', 'SecurityError'); } }); });
  await page.goto(`${BASE}#/show/merch?v=coffee`);
  await mathShow(page, 'merch', null);
  await page.screenshot({ path: path.join(shots, pre(name) + 'nostorage-math-ticket.png'), fullPage: true });
  await browser.close();
}

async function mathReduced(bt = chromium, name = 'chromium') {
  const browser = await bt.launch();
  const { page } = await newPage(browser, 'ipad-portrait', pre(name) + 'reduced-math', { reducedMotion: 'reduce' });
  await page.goto(`${BASE}#/show/beat?v=coffee`);
  await page.waitForSelector('.nblock');
  await answerMath(page, 'beat', await mathProblem(page));
  await page.waitForSelector('.play #hint[disabled]', { timeout: 4000 });
  await page.screenshot({ path: path.join(shots, pre(name) + 'reduced-beat.png') });
  await browser.close();
}

/* ================= meta: backstage, journal, manager's office ================= */

/* a save from a few weeks of play, written once per tab (sessionStorage guard), so purchases survive reloads */
const SEED_RICH = () => {
  if (sessionStorage.getItem('wt-seeded')) return;
  sessionStorage.setItem('wt-seeded', '1');
  const iso = d => new Date(Date.now() - d * 864e5 - new Date().getTimezoneOffset() * 6e4).toISOString().slice(0, 10);
  const time = {}; [0, 1, 2, 4, 5, 6, 9, 12].forEach((d, i) => { time[iso(d)] = 300 + i * 170; });
  const sk = (level, a, c, h, mistakes = {}) => ({ level, run: 0, missRun: 0, a, c, h, byLevel: { 1: [a, c], 2: [0, 0], 3: [0, 0] }, mistakes });
  const modes = ['remix', 'slots', 'fanmail', 'merch', 'beat', 'tickets', 'stage', 'remix', 'vowels', 'merch', 'budget', 'studio'];
  localStorage.setItem('worldtour.v1', JSON.stringify({
    v: 1, created: iso(20),
    fans: { balance: 150000, lifetime: 260000 },
    streak: { days: 2, best: 6, last: iso(0) },
    venues: { unlocked: ['coffee', 'records', 'rec', 'rooftop', 'club'], seen: ['coffee', 'records', 'rec', 'rooftop', 'club'] },
    skills: { 'spell:remix': sk(3, 40, 36, 6), 'spell:slots': sk(2, 18, 14, 4), 'spell:fanmail': sk(1, 10, 6, 3),
              dec: sk(2, 22, 15, 5, { 'no-borrow': 4, 'gave-total': 2 }), frac: sk(1, 12, 6, 6, { 'added-denominators': 5, 'too-long': 2 }),
              md: sk(2, 10, 8, 1, { 'no-placeholder': 3 }), xy: sk(1, 10, 7, 2, { swapped: 3 }) },
    words: { illegible: { a: 6, c: 4, first: 2, h: 2, miss: 3, last: iso(1) }, irresponsible: { a: 5, c: 4, first: 2, h: 1, miss: 2, last: iso(2) },
             intercept: { a: 4, c: 3, first: 3, h: 0, miss: 1, last: iso(0) }, immature: { a: 5, c: 5, first: 5, h: 0, miss: 0, last: iso(0) } },
    review: { words: ['illegible'], math: [{ skill: 'dec', level: 2, seed: 12345 }] },
    shows: modes.map((m, i) => ({ d: iso(12 - i), mode: m, venue: 'coffee', n: 10, first: 6 + (i % 5), hinted: 2, fans: 20000 + i * 1000, secs: 240 })),
    time
  }));
};
const fansOf = page => page.evaluate(() => JSON.parse(localStorage.getItem('worldtour.v1')).fans);

async function metaFresh(browserType, label, size) {
  const browser = await browserType.launch();
  const tag = `${label}-${size}`;
  const { ctx, page } = await newPage(browser, size, tag);
  const shot = n => page.screenshot({ path: path.join(shots, `${tag}-${n}.png`), fullPage: true });
  await test('meta', label, size, 'fresh: empty rooms', async () => {
    await page.goto(BASE + '#/backstage'); await page.waitForSelector('.item'); await page.evaluate(() => document.fonts.ready); await wait(400);
    if (!(await page.locator('#mirror .btn[disabled]').count())) throw new Error('equipped item should show a disabled "wearing it"');
    await page.click('.item[data-id="dog:beanie"]'); await wait(250);
    if (!(await page.locator('#mirror .btn[disabled]', { hasText: 'more fans needed' }).count())) throw new Error('unaffordable item should say how many more fans');
    await shot('backstage-fresh');
    await page.goto(BASE + '#/journal'); await page.waitForSelector('.jempty'); await wait(300);
    await shot('journal-fresh');
    await page.goto(BASE + '#/office'); await page.waitForSelector('.office'); await wait(300);
    if ((await page.locator('.oempty').count()) < 2) throw new Error('office should say nothing is missed yet');
    await shot('office-fresh');
  });
  await test('meta', label, size, 'sticker earned at the end of a show', async () => {
    await page.goto(`${BASE}#/show/fanmail?v=coffee&w=${WEEK}`);
    await playShow(page, 'fanmail', null);
    const t = await page.locator('.newstk').textContent().catch(() => '');
    if (!t.includes('opening night')) throw new Error(`ticket did not show the first-show sticker (got "${t.trim().slice(0, 60)}")`);
    await shot('ticket-sticker');
    await page.click('.newstk'); await page.waitForSelector('.jstk[data-key="first-show"]');
  });
  await ctx.close(); await browser.close();
}

async function metaRich(browserType, label, size) {
  const browser = await browserType.launch();
  const tag = `${label}-${size}`;
  const { ctx, page } = await newPage(browser, size, tag);
  await ctx.addInitScript(SEED_RICH);
  const shot = (n, full = true) => page.screenshot({ path: path.join(shots, `${tag}-${n}.png`), fullPage: full });
  const theme = () => page.evaluate(() => document.documentElement.dataset.theme || '');

  await test('meta', label, size, 'backstage: buy, wear, theme', async () => {
    await page.goto(BASE + '#/backstage'); await page.waitForSelector('.item'); await page.evaluate(() => document.fonts.ready); await wait(400);
    await page.click('.item[data-id="guitar:sunburst"]'); await wait(200);
    await page.click('#act'); await wait(150);
    let f = await fansOf(page);
    if (f.balance !== 150000) throw new Error('one tap should not buy (two-tap confirm)');
    if (!(await page.locator('#act', { hasText: 'tap again' }).count())) throw new Error('no "tap again to buy"');
    await page.click('#act'); await wait(400);
    f = await fansOf(page);
    if (f.balance !== 132000 || f.lifetime !== 260000) throw new Error(`after buying: balance ${f.balance}, lifetime ${f.lifetime}`);
    const gtr = await page.evaluate(() => document.documentElement.style.getPropertyValue('--gtr'));
    if (gtr !== '#ffb23f') throw new Error(`guitar skin not applied (got ${gtr})`);
    await page.click('.item[data-id="dog:phones"]'); await page.click('#act'); await page.click('#act'); await wait(400);
    await shot('backstage-bought');
    await page.click('.item[data-id="theme:peach-navy"]'); await wait(200);
    if (await theme() !== 'peach-navy') throw new Error('theme try-on did not repaint the page');
    await page.click('#act'); await page.click('#act'); await wait(400);
    await shot('backstage-peach');
    await page.goto(BASE + '#/map'); await page.waitForSelector('.venue'); await wait(500);
    if (await theme() !== 'peach-navy') throw new Error('bought theme did not stick');
    await shot('map-peach');
    await page.reload(); await page.waitForSelector('.venue'); await wait(300);
    const eq = await page.evaluate(() => JSON.parse(localStorage.getItem('worldtour.v1')).inventory.equipped);
    if (eq.theme !== 'peach-navy' || eq.guitar !== 'sunburst' || eq.dog !== 'phones') throw new Error('equipped items did not save: ' + JSON.stringify(eq));
    if (await theme() !== 'peach-navy') throw new Error('theme lost on reload');
  });

  await test('meta', label, size, 'journal: drag and tap to place', async () => {
    await page.goto(BASE + '#/journal'); await page.waitForSelector('.jstk'); await wait(900);
    const n = await page.locator('.jstk').count();
    if (n < 8) throw new Error(`expected the seeded milestones on the page, got ${n}`);
    await shot('journal');
    const st = page.locator('.jstk').first();
    await st.scrollIntoViewIfNeeded();
    const k = await st.getAttribute('data-key');
    const b = await st.boundingBox();
    await page.mouse.move(b.x + b.width / 2, b.y + b.height / 2); await page.mouse.down();
    const pg = await page.locator('#page').boundingBox();
    await page.mouse.move(pg.x + pg.width * .5, pg.y + pg.height * .6, { steps: 8 }); await page.mouse.up(); await wait(400);
    const lay = await page.evaluate(() => JSON.parse(localStorage.getItem('worldtour.v1')).journal.layout);
    if (!lay[k] || Math.abs(lay[k].x - .5) > .05 || Math.abs(lay[k].y - .6) > .05) throw new Error('drag did not save the spot: ' + JSON.stringify(lay[k]));
    /* tap to select, then tap the page */
    const s2 = page.locator('.jstk').nth(1); const k2 = await s2.getAttribute('data-key');
    await s2.click(); await wait(150);
    if (!(await page.locator('.jstk.sel').count())) throw new Error('tap did not select the sticker');
    let pg2 = await page.locator('#page').boundingBox();
    await page.evaluate(y => window.scrollBy(0, y - innerHeight / 2), pg2.y + pg2.height * .85); await wait(150);
    pg2 = await page.locator('#page').boundingBox();
    await page.mouse.click(pg2.x + pg2.width * .2, pg2.y + pg2.height * .85); await wait(400);
    const lay2 = await page.evaluate(() => JSON.parse(localStorage.getItem('worldtour.v1')).journal.layout);
    if (!lay2[k2] || Math.abs(lay2[k2].x - .2) > .05 || Math.abs(lay2[k2].y - .85) > .05) throw new Error('tap-to-place did not move it: ' + JSON.stringify(lay2[k2]));
    await shot('journal-moved');
  });

  await test('meta', label, size, 'office: stats and reset', async () => {
    await page.goto(BASE + '#/office'); await page.waitForSelector('.office'); await wait(300);
    const txt = await page.locator('.office').innerText();
    for (const want of ['illegible', 'added the denominators', 'subtracted without regrouping', 'remix', 'beat maker']) if (!txt.includes(want)) throw new Error(`office is missing "${want}"`);
    await shot('office');
    await page.click('#reset'); await page.click('#cancel');
    if ((await fansOf(page)).lifetime !== 260000) throw new Error('cancel should not reset');
    await page.click('#reset'); await page.click('#erase'); await page.waitForSelector('.venue'); await wait(400);
    const after = await page.evaluate(() => JSON.parse(localStorage.getItem('worldtour.v1')));
    if (after.fans.lifetime !== 0 || after.shows.length || Object.keys(after.journal.stickers).length) throw new Error('reset left progress behind');
    if (await theme()) throw new Error('reset should go back to tour pink');
  });
  await ctx.close(); await browser.close();
}

async function metaNoStorage(bt = chromium, name = 'chromium') {
  const browser = await bt.launch();
  const { page } = await newPage(browser, 'phone', pre(name) + 'meta-nostorage');
  await page.context().addInitScript(() => { Object.defineProperty(window, 'localStorage', { get() { throw new DOMException('denied', 'SecurityError'); } }); });
  for (const [r, sel] of [['backstage', '.item'], ['journal', '.journal'], ['office', '.office']]) {
    await page.goto(BASE + '#/' + r); await page.waitForSelector(sel); await wait(200);
  }
  await page.click('#reset'); await page.click('#erase'); await page.waitForSelector('.venue');
  if (!(await page.isVisible('#nosave'))) throw new Error('the "won\'t save" note is not showing');
  await page.screenshot({ path: path.join(shots, pre(name) + 'meta-nostorage.png'), fullPage: true });
  await browser.close();
}

/* each suite fits well inside a 10-minute run: spelling, spelling-more, math, math-more */
const want = s => !suiteArg || suiteArg === s;
const doSpell = want('spelling'), doSpellMore = want('spelling-more'), doMath = want('math'), doMore = want('math-more'), doMeta = want('meta');
try {
  /* WebKit runs exactly what Chromium runs: same suites, same sizes */
  for (const [bt, name] of [[chromium, 'chromium'], [webkit, 'webkit']]) {
    if (only && only !== name) continue;
    if (doSpell) {
      await fullShows(bt, name, 'phone');
      await test('storage disabled', name, 'phone', 'fan mail show', () => noStorage(bt, name));
      await test('reduced motion', name, 'ipad-portrait', 'prefix song', () => reducedMotion(bt, name));
    }
    if (doSpellMore) {
      for (const s of ['phone', 'ipad-portrait', 'ipad-landscape', 'laptop']) await firstSongs(bt, name, s);
      for (const s of Object.keys(SIZES)) await test('map to ticket', name, s, 'remix + save', () => tourFlow(bt, name, s));
    }
    if (doMath) {
      await mathFull(bt, name, 'phone', 1);
      await test('storage disabled', name, 'phone', 'merch table show', () => mathNoStorage(bt, name));
      await test('reduced motion', name, 'ipad-portrait', 'beat maker song', () => mathReduced(bt, name));
    }
    if (doMore) {
      await mathFull(bt, name, 'laptop', 3);
      await mathFirst(bt, name, 'phone', 3);
      for (const s of ['ipad-portrait', 'ipad-landscape']) await mathFirst(bt, name, s, 2);
    }
    if (doMeta) {
      for (const s of Object.keys(SIZES)) { await metaRich(bt, name, s); await metaFresh(bt, name, s); }
      await test('storage disabled', name, 'phone', 'backstage, journal, office', () => metaNoStorage(bt, name));
    }
  }
} catch (e) {
  errors.push('run failed: ' + (e.stack || e.message));
} finally {
  server.close();
}
console.log('\nRESULTS');
for (const s of [...new Set(results.map(r => r.suite))]) {
  const rs = results.filter(r => r.suite === s), pass = rs.filter(r => r.ok).length;
  console.log(`${s}: ${pass}/${rs.length} passed  [${[...new Set(rs.map(r => `${r.browser} ${r.size}`))].join(', ')}]`);
  rs.filter(r => !r.ok).forEach(r => console.log(`  FAIL ${r.browser} ${r.size} ${r.name}: ${r.note}`));
}
fs.writeFileSync(path.join(shots, 'results.json'), JSON.stringify(results, null, 1));
if (errors.length) { console.log('\nPROBLEMS:\n' + errors.join('\n')); process.exit(1); }
console.log('ok: no console errors; screenshots in', shots);
