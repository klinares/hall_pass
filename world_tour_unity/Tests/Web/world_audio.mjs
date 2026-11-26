// Phase 10A: sound levels per place, measured in the real build in a real (headless) browser.
//   node Tests/Web/world_audio.mjs [--label before|after] [--seconds 60] [--places coffee,records,...] [--out <json>] [--gpu d3d11]
// For each place: load world/ with ?wt_test, travel there, click once (the audio unlock), start the game's own level meter (AudioMeter, which rebuilds what
// each sound source hands to the audio engine) and the page's own tap on the browser's audio output (every connection to the destination goes through a
// pass-through ScriptProcessor that records the real mix: peak, RMS, and how many samples reached full scale). Then she walks back and forth for --seconds
// (footsteps), the game plays every one-off sound once (AudioProbeCues: taps, cues, applause, the guitar phrases) and the meter is read.
// Output: one WT_AUDIO_LEVELS line per source per place (dBFS), the mix from the tap, and a table at the end. Fails if a source is over -12 dBFS peak
// (applause and the end-of-show crowd excepted), if the mix reaches full scale, or if a night or indoor place has a bird or a bed (--label after only).
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { startServer } from './server.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const project = path.resolve(here, '..', '..');
const repo = path.resolve(project, '..');
const { chromium } = createRequire(path.join(here, 'package.json'))('playwright');
const argv = process.argv.slice(2);
const opt = (n, d) => { const i = argv.indexOf(n); return i >= 0 && i + 1 < argv.length ? argv[i + 1] : d; };
const label = opt('--label', 'run');
const seconds = Number(opt('--seconds', '60'));
const gpu = opt('--gpu', 'swiftshader') === 'd3d11';
const outFile = path.resolve(opt('--out', path.join(project, 'Temp', `world-audio-${label}.json`)));
const FANS = { coffee: 0, records: 20000, rec: 60000, rooftop: 125000, club: 225000, theater: 375000, festival: 600000, arena: 900000 };
const ORDER = ['coffee', 'records', 'rec', 'rooftop', 'club', 'theater', 'festival', 'arena'];
const places = opt('--places', ORDER.join(',')).split(',').filter(Boolean);
const BIRD_PLACES = ['coffee', 'festival'];
const sleep = ms => new Promise(r => setTimeout(r, ms));

// every connection to the audio destination goes through one pass-through node that measures what actually goes out
const TAP = `(() => {
  const tap = window.__wtTap = { peak: 0, sumsq: 0, n: 0, full: 0, on: false, contexts: 0, rate: 0 };
  window.__wtTapReset = () => { tap.peak = 0; tap.sumsq = 0; tap.n = 0; tap.full = 0; tap.on = true; };
  const connect = AudioNode.prototype.connect;
  const procs = new WeakMap();
  AudioNode.prototype.connect = function (dest, ...rest) {
    try {
      if (typeof AudioDestinationNode !== 'undefined' && dest instanceof AudioDestinationNode && !this.__wtTap) {
        const ctx = this.context;
        let sp = procs.get(ctx);
        if (!sp) {
          sp = ctx.createScriptProcessor(2048, 2, 2);
          sp.__wtTap = true;
          tap.contexts++; tap.rate = ctx.sampleRate;
          sp.onaudioprocess = e => {
            for (let c = 0; c < e.outputBuffer.numberOfChannels; c++) {
              const inp = e.inputBuffer.getChannelData(Math.min(c, e.inputBuffer.numberOfChannels - 1)), out = e.outputBuffer.getChannelData(c);
              out.set(inp);
              if (!tap.on || c > 0) continue;
              for (let i = 0; i < inp.length; i++) { const a = Math.abs(inp[i]); if (a > tap.peak) tap.peak = a; if (a >= 0.999) tap.full++; tap.sumsq += a * a; }
              tap.n += inp.length;
            }
          };
          connect.call(sp, ctx.destination);
          procs.set(ctx, sp);
        }
        return connect.call(this, sp, ...rest);
      }
    } catch (e) {}
    return connect.call(this, dest, ...rest);
  };
})();`;

const db = x => +(20 * Math.log10(Math.max(x, 1e-6))).toFixed(1);
const world = await startServer(path.join(repo, 'world'));
const browser = await chromium.launch({ headless: true, args: gpu ? ['--use-gl=angle', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl', '--autoplay-policy=no-user-gesture-required'] : ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--autoplay-policy=no-user-gesture-required'] });
const results = { label, seconds, places: {}, failures: [] };
const fail = m => { results.failures.push(m); console.log('FAIL ' + m); };

for (const id of places) {
  const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 } });
  await ctx.addInitScript(TAP);
  const page = await ctx.newPage();
  const lines = [];
  const errors = [];
  page.on('console', m => { lines.push(m.text().trim()); if (m.type() === 'error') errors.push(m.text()); });
  page.on('pageerror', e => errors.push(String(e)));
  const wait = async (re, ms, from = 0) => { const end = Date.now() + ms; while (Date.now() < end) { for (let i = from; i < lines.length; i++) if (re.test(lines[i])) return i; await sleep(100); } return -1; };
  const send = (fn, arg) => page.evaluate(([f, a]) => window.__wtInstance && window.__wtInstance.SendMessage('WorldGame', f, a), [fn, arg ?? '']);
  try {
    await page.goto(world.url + '?wt_test=1', { waitUntil: 'load' });
    if ((await wait(/^WT_WORLD_GAME_READY/, 120000)) < 0) { fail(`${id}: never ready`); continue; }
    const idx = ORDER.indexOf(id);
    await page.evaluate(([f, u]) => { const j = JSON.parse(localStorage.getItem('worldtour.v1')); j.fans.lifetime = f; j.fans.balance = Math.max(j.fans.balance || 0, f); j.venues.unlocked = u; j.venues.seen = u; localStorage.setItem('worldtour.v1', JSON.stringify(j)); }, [FANS[id], ORDER.slice(0, idx + 1)]);
    const from = lines.length;
    await page.goto(world.url + '?wt_test=1&wt_goto=' + id, { waitUntil: 'load' });
    if ((await wait(new RegExp('^WT_WORLD_(ARRIVED|GAME_READY) place=' + id), 120000, from)) < 0) { fail(`${id}: never arrived`); continue; }
    await page.evaluate(() => { const c = document.getElementById('unity-canvas'); if (c) c.focus(); });
    await wait(/^WT_WORLD_MODE mode=Free/, 15000, from);
    await sleep(1500);
    await page.mouse.click(683, 400);                         // the audio unlock
    await sleep(500);
    await send('AudioProbeStart', id);
    await page.evaluate(() => window.__wtTapReset && window.__wtTapReset());
    const t0 = Date.now(); let dir = 'KeyD', cues = false;
    await page.keyboard.down(dir);
    while ((Date.now() - t0) / 1000 < seconds) {
      await sleep(1000);
      if (!cues && Date.now() - t0 > 4000) { cues = true; await send('AudioProbeCues'); }
      if (Math.floor((Date.now() - t0) / 1000) % 3 === 0) { await page.keyboard.up(dir); dir = dir === 'KeyD' ? 'KeyA' : 'KeyD'; await page.keyboard.down(dir); }
    }
    await page.keyboard.up(dir);
    const mark = lines.length;
    await send('AudioProbeReport');
    await wait(/^WT_AUDIO_PROBE_REPORT_DONE/, 10000, mark);
    const tap = await page.evaluate(() => ({ ...window.__wtTap }));
    const sources = {};
    for (const l of lines.slice(mark)) {
      const m = /^WT_AUDIO_LEVELS label=\S+ source=(\S+) peak=(-?[\d.]+) rms=(-?[\d.]+)/.exec(l);
      if (m) sources[m[1]] = { peak: +m[2], rms: +m[3] };
    }
    const birds = lines.filter(l => /^WT_AUDIO .*bird/i.test(l)).length;
    results.places[id] = { audio: lines.filter(l => l.startsWith('WT_AUDIO ')).slice(0, 60), sources, tap: tap.n ? { peakDb: db(tap.peak), rmsDb: db(Math.sqrt(tap.sumsq / tap.n)), fullScaleSamples: tap.full, rate: tap.rate, contexts: tap.contexts, seconds: +(tap.n / (tap.rate || 1)).toFixed(1) } : null, birdLogs: birds, errors: errors.slice(0, 3) };
    console.log(`${id}: ` + Object.entries(sources).map(([k, v]) => `${k} ${v.peak}/${v.rms}`).join('  ') + (tap.n ? `  | browser mix peak ${db(tap.peak)} rms ${db(Math.sqrt(tap.sumsq / tap.n))} full-scale samples ${tap.full}` : '  | no browser tap'));
    if (errors.length) fail(`${id}: console errors: ${errors.slice(0, 2).join(' | ')}`);
  } catch (e) {
    fail(`${id}: ${e.message}`);
  } finally {
    await ctx.close();
  }
}

// rules (the after run)
if (label === 'after') {
  for (const [id, r] of Object.entries(results.places)) {
    for (const [src, v] of Object.entries(r.sources)) {
      if (src === 'mix' || src === 'cheer' || src === 'sfx:crowd') continue;
      if (v.peak > -12) fail(`${id}: ${src} peaks at ${v.peak} dBFS (over -12)`);
    }
    if (r.sources.crowd) fail(`${id}: a crowd murmur is still playing`);
    if (r.tap && r.tap.fullScaleSamples > 0) fail(`${id}: the browser mix reached full scale (${r.tap.fullScaleSamples} samples)`);
    const birds = r.sources.bed;
    if (BIRD_PLACES.includes(id) ? false : !!birds) fail(`${id}: a bed plays in a place without birds`);
  }
}
fs.mkdirSync(path.dirname(outFile), { recursive: true });
fs.writeFileSync(outFile, JSON.stringify(results, null, 1));
console.log('\nsource peak/rms dBFS by place (' + label + ')');
const srcNames = [...new Set(Object.values(results.places).flatMap(r => Object.keys(r.sources)))].sort();
for (const s of srcNames) console.log(s.padEnd(18) + places.map(p => { const v = results.places[p] && results.places[p].sources[s]; return (v ? `${v.peak}/${v.rms}` : '-').padStart(14); }).join(''));
console.log('browser mix'.padEnd(18) + places.map(p => { const t = results.places[p] && results.places[p].tap; return (t ? `${t.peakDb}/${t.rmsDb}` : '-').padStart(14); }).join(''));
console.log('results: ' + outFile);
await browser.close(); await world.close();
console.log(results.failures.length ? 'FAILED: ' + results.failures.length : 'PASSED');
process.exit(results.failures.length ? 1 : 0);
