/* Task 5.1 (builder X1). Runs the web game's real backstage screen (world_tour/screens/backstage.js, with
   js/store.js, js/progress.js, js/meta.js and data/shop.js, all imported read-only) against the small fake DOM in
   Tools/math/fake_dom.mjs, with sound, motion, fx, art and look stubbed by hooks.mjs (look applies the real meta.js
   look() to a fake documentElement.dataset.theme). For scripted clicks (pick an item, press the mirror's button, let the
   3.5 s "tap again" timer run out) it records a snapshot of the screen: the wallet, every item's tag and classes,
   the mirror (eyebrow, name, blurb, button text, enabled, what it does), the theme the page wears, the sound/toast
   events, and the saved state. BackstageVectorTests replays the same clicks through WorldTour.Core.Meta.Backstage.

   node Tools/meta/backstage_vectors.mjs            writes Assets/Tests/EditMode/backstage_vectors.json
   node Tools/meta/backstage_vectors.mjs --stdout   prints it instead
   Deterministic: scripts and starting saves come from a fixed mulberry32 seed. */
import { register } from 'node:module';
register('./hooks.mjs', import.meta.url);
import { writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

globalThis.window = {};
globalThis.addEventListener = () => {};
globalThis.removeEventListener = () => {};
globalThis.matchMedia = () => ({ matches: false });
globalThis.document = { visibilityState: 'hidden', dispatchEvent() {}, documentElement: { dataset: {}, style: { setProperty() {} } } };
globalThis.__ev = [];

const { El } = await import('../math/fake_dom.mjs');
const base = new URL('../../../world_tour/', import.meta.url).href;
const store = await import(base + 'js/store.js');
const M = await import(base + 'js/meta.js');
const { SHOP } = await import(base + 'data/shop.js');
const backstage = await import(base + 'screens/backstage.js');

/* look.js applyLook, minus the CSS: the theme the page wears */
globalThis.__applyLook = () => {
  const L = M.look(store.get()), root = document.documentElement;
  if (L.theme === 'tour-pink') delete root.dataset.theme; else root.dataset.theme = L.theme;
  return L;
};

/* a manual clock for backstage.js's 3.5 s timer (and store.save's 500 ms one, which does nothing in memory) */
let tid = 0;
const timers = new Map();
globalThis.setTimeout = (f) => { const id = ++tid; timers.set(id, f); return id; };
globalThis.clearTimeout = (id) => { timers.delete(id); };
const runTimers = () => { const fs = [...timers.values()]; timers.clear(); fs.forEach(f => f()); };

function mulberry32(a) {
  return () => { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; };
}

const BALANCES = [0, 4999, 5000, 12000, 17999, 25000, 59999, 60000, 61000, 100000, 250000];
const IDS = SHOP.map(i => i.id);

/* tags are written in full when they change and as ~ when they are the same as the step before */
let lastTags = null;
function snap(root) {
  const s = store.get();
  const tags = root.querySelectorAll('.item').map(b => {
    const c = b.cls();
    return `${b.dataset.id}:${b.querySelector('[data-tag]').textContent}:${c.has('own') ? 1 : 0}${c.has('wearing') ? 1 : 0}${c.has('short') ? 1 : 0}`;
  }).join(',');
  const sel = root.querySelectorAll('.item').filter(b => b.getAttribute('aria-pressed') === 'true').map(b => b.dataset.id).join(',');
  const tagsOut = tags === lastTags ? '~' : tags;
  lastTags = tags;
  const mirror = root.querySelector('#mirror');
  const btn = mirror.querySelector('.row').children.find(c => c.tag !== '#text');
  const act = mirror.querySelector('#act');
  const doIt = act ? act.dataset.do : '';
  const m = `${mirror.querySelector('.eyebrow').textContent}/${mirror.querySelector('h2').textContent}/${btn.textContent}/${btn.tag === 'button' && btn.disabled ? 0 : 1}/${doIt}`;
  const eq = s.inventory.equipped;
  return [
    'bal=' + root.querySelector('#bal').textContent,
    'theme=' + (document.documentElement.dataset.theme || 'tour-pink'),
    'sel=' + sel,
    'tags=' + tagsOut,
    'mirror=' + m,
    'blurb=' + mirror.querySelector('.blurb').textContent,
    'ev=' + globalThis.__ev.join(';'),
    `state=${s.fans.balance}/${s.fans.lifetime}/${s.inventory.owned.join(',')}/${eq.theme},${eq.guitar},${eq.dog}`,
  ].join('|');
}

const cases = [];
const rnd = mulberry32(51051);
const N = 80;
for (let n = 0; n < N; n++) {
  store.reset();
  const s = store.get();
  const bal = BALANCES[Math.floor(rnd() * BALANCES.length)];
  s.fans.balance = bal;
  s.fans.lifetime = bal + Math.floor(rnd() * 50000);
  for (const id of IDS) if (SHOP.find(i => i.id === id).price > 0 && rnd() < 0.18) s.inventory.owned.push(id);
  /* equip something owned per slot sometimes; now and then an unknown theme or guitar (look() falls back) */
  for (const slot of ['theme', 'guitar', 'dog']) {
    const owned = s.inventory.owned.filter(id => id.startsWith(slot + ':'));
    const r = rnd();
    if (r < 0.45 && owned.length) s.inventory.equipped[slot] = owned[Math.floor(rnd() * owned.length)].slice(slot.length + 1);
    else if (r > 0.94 && slot !== 'dog') s.inventory.equipped[slot] = 'sparkle';
  }
  const init = { balance: s.fans.balance, lifetime: s.fans.lifetime, owned: [...s.inventory.owned], equipped: { ...s.inventory.equipped } };
  timers.clear();
  document.documentElement.dataset = {};
  globalThis.__applyLook();
  globalThis.__ev = [];
  lastTags = null;
  const root = new El('#root'); const el = new El('section'); el.parent = root; root.children.push(el);
  const cleanup = backstage.mount(el);
  const steps = [{ op: 'mount', arg: '', snap: snap(el) }];
  const nOps = 6 + Math.floor(rnd() * 10);
  for (let k = 0; k < nOps; k++) {
    globalThis.__ev = [];
    const r = rnd();
    let op, arg = '';
    if (r < 0.40) {
      op = 'select';
      /* lean toward themes and cheap items so buys happen */
      arg = rnd() < 0.3 ? IDS[Math.floor(rnd() * 2)] : IDS[Math.floor(rnd() * IDS.length)];
      el.querySelectorAll('.item').find(b => b.dataset.id === arg).fire('click');
    } else if (r < 0.88) {
      op = 'press';
      const act = el.querySelector('#act');
      if (act) act.fire('click');
    } else {
      op = 'wait';
      runTimers();
    }
    steps.push({ op, arg, snap: snap(el) });
  }
  if (typeof cleanup === 'function') { globalThis.__ev = []; cleanup(); }
  cases.push({ seed: n, init, steps });
}

const out = { note: 'made by Tools/meta/backstage_vectors.mjs from the web game (world_tour/screens/backstage.js, read only)', cases };
const text = JSON.stringify(out, null, 1).split('').map(c => c.charCodeAt(0) > 0x7e ? '\\u' + c.charCodeAt(0).toString(16).padStart(4, '0') : c).join('') + '\n';
if (process.argv.includes('--stdout')) process.stdout.write(text);
else {
  const file = join(dirname(fileURLToPath(import.meta.url)), '../../Assets/Tests/EditMode/backstage_vectors.json');
  writeFileSync(file, text);
  console.log(`wrote ${file} (${cases.length} cases, ${cases.reduce((a, c) => a + c.steps.length, 0)} steps)`);
}
process.exit(0);
