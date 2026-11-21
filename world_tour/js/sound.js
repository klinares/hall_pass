/* All audio is synthesized. No files.
   strum: Karplus-Strong plucked strings rendered once into a buffer.
   thud: soft low knock. crowd: filtered noise swell plus scattered claps. */
let ac = null, master = null, on = true;
const cache = {};

export function setEnabled(v) { on = !!v; if (master) master.gain.value = on ? 1 : 0; }
export const enabled = () => on;

function ctx() {
  if (ac) return ac;
  const C = window.AudioContext || window.webkitAudioContext;
  if (!C) return null;
  ac = new C();
  master = ac.createGain(); master.gain.value = on ? 1 : 0;
  const comp = ac.createDynamicsCompressor();
  master.connect(comp).connect(ac.destination);
  return ac;
}

/* iPad Safari only plays audio after a user gesture: call this from the first tap */
export function unlock() {
  const a = ctx(); if (!a) return;
  if (a.state === 'suspended') a.resume();
  try { const b = a.createBuffer(1, 1, 22050), s = a.createBufferSource(); s.buffer = b; s.connect(master); s.start(0); } catch (e) { /* ignore */ }
}

function pluckBuffer(freqs, stagger = .028, dur = 1.6, bright = .996) {
  const a = ctx(), sr = a.sampleRate, len = Math.floor(sr * (dur + stagger * freqs.length));
  const buf = a.createBuffer(1, len, sr), out = buf.getChannelData(0);
  freqs.forEach((f, n) => {
    const N = Math.max(2, Math.round(sr / f)), ring = new Float32Array(N);
    for (let i = 0; i < N; i++) ring[i] = Math.random() * 2 - 1;
    const start = Math.floor(n * stagger * sr), gain = .22 * (1 - n * .06);
    let idx = 0, prev = 0;
    for (let i = start; i < len; i++) {
      const cur = ring[idx];
      const next = bright * .5 * (cur + prev);
      prev = cur; ring[idx] = next; idx = (idx + 1) % N;
      out[i] += cur * gain;
    }
  });
  return buf;
}

function play(buf, vol = 1, rate = 1) {
  const a = ctx(); if (!a || !on) return;
  const s = a.createBufferSource(), g = a.createGain();
  s.buffer = buf; s.playbackRate.value = rate; g.gain.value = vol;
  s.connect(g).connect(master); s.start();
}

function osc(freq, { type = 'sine', dur = .12, vol = .1, delay = 0, to = null } = {}) {
  const a = ctx(); if (!a || !on) return;
  const t = a.currentTime + delay, o = a.createOscillator(), g = a.createGain();
  o.type = type; o.frequency.setValueAtTime(freq, t);
  if (to) o.frequency.exponentialRampToValueAtTime(to, t + dur);
  g.gain.setValueAtTime(0, t); g.gain.linearRampToValueAtTime(vol, t + .006);
  g.gain.exponentialRampToValueAtTime(.0008, t + dur);
  o.connect(g).connect(master); o.start(t); o.stop(t + dur + .02);
}

function noise(dur) {
  const a = ctx(), n = Math.floor(a.sampleRate * dur), b = a.createBuffer(1, n, a.sampleRate), d = b.getChannelData(0);
  let last = 0;
  for (let i = 0; i < n; i++) { const w = Math.random() * 2 - 1; last = (last + .02 * w) / 1.02; d[i] = last * 3.5 + w * .25; }
  return b;
}

/* G major, strummed down: G2 B2 D3 G3 B3 G4 */
const CHORDS = {
  win: [98, 123.5, 146.8, 196, 246.9, 392],
  big: [130.8, 164.8, 196, 261.6, 329.6, 523.3]   /* C major for bigger moments */
};

export function strum(kind = 'win') {
  if (!ctx() || !on) return;
  cache[kind] = cache[kind] || pluckBuffer(CHORDS[kind]);
  play(cache[kind], 1);
}

export function thud() {
  if (!ctx() || !on) return;
  osc(110, { type: 'sine', dur: .22, vol: .32, to: 48 });
  const a = ac, s = a.createBufferSource(), f = a.createBiquadFilter(), g = a.createGain();
  cache.thud = cache.thud || noise(.12);
  s.buffer = cache.thud; f.type = 'lowpass'; f.frequency.value = 380; g.gain.value = .5;
  s.connect(f).connect(g).connect(master); s.start();
}

export const tap = () => osc(1250, { type: 'sine', dur: .04, vol: .05 });
export const pop = () => osc(420, { type: 'triangle', dur: .11, vol: .12, to: 980 });
export const peel = () => osc(1800, { type: 'sawtooth', dur: .06, vol: .02, to: 600 });

/* end of show: a crowd swell scaled by the venue size (0..1) */
export function crowd(size = .3) {
  const a = ctx(); if (!a || !on) return;
  const dur = 2.6 + size * 1.4, t = a.currentTime;
  cache.crowd = cache.crowd || noise(4.2);
  const s = a.createBufferSource(), bp = a.createBiquadFilter(), g = a.createGain();
  s.buffer = cache.crowd; bp.type = 'bandpass'; bp.frequency.value = 900 + size * 500; bp.Q.value = .6;
  const peak = .18 + size * .32;
  g.gain.setValueAtTime(0, t); g.gain.linearRampToValueAtTime(peak, t + .9); g.gain.linearRampToValueAtTime(peak * .7, t + dur * .6); g.gain.linearRampToValueAtTime(0, t + dur);
  s.connect(bp).connect(g).connect(master); s.start(t); s.stop(t + dur);
  const claps = Math.round(12 + size * 40);
  for (let i = 0; i < claps; i++) {
    const c = a.createBufferSource(), hp = a.createBiquadFilter(), cg = a.createGain(), at = t + .25 + Math.random() * (dur - .6);
    c.buffer = cache.crowd; hp.type = 'highpass'; hp.frequency.value = 1400 + Math.random() * 1200;
    cg.gain.setValueAtTime(0, at); cg.gain.linearRampToValueAtTime(.12 + Math.random() * .1, at + .004); cg.gain.exponentialRampToValueAtTime(.001, at + .06);
    c.connect(hp).connect(cg).connect(master); c.start(at, Math.random() * 3, .08);
  }
  setTimeout(() => strum('big'), 350);
}

/* beat maker: play a measure. notes = [{ len (fraction of a whole), d (denominator) }].
   one whole lasts 1.6 s; every note gets a pitch by size and a kick lands on each downbeat. */
const NOTE_HZ = { 2: 196, 3: 233.1, 4: 293.7, 6: 349.2, 8: 392, 12: 466.2 };
export function beat(notes, whole = 1.6) {
  if (!ctx() || !on) return 0;
  let t = 0;
  notes.forEach(n => {
    osc(NOTE_HZ[n.d] || 330, { type: 'triangle', dur: Math.min(.4, n.len * whole * .9), vol: .16, delay: t });
    osc(NOTE_HZ[n.d] * 2 || 660, { type: 'sine', dur: .05, vol: .04, delay: t });
    t += n.len * whole;
  });
  for (let k = 0; k < t - 1e-6; k += whole / 4) osc(k % whole < 1e-6 ? 70 : 55, { type: 'sine', dur: .14, vol: k % whole < 1e-6 ? .35 : .15, delay: k, to: 38 });
  return t;
}

/* soundcheck: a short tick for the countdown */
export const tick = () => osc(2100, { type: 'square', dur: .02, vol: .015 });
