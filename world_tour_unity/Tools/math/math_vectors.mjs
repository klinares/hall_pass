// Pinned values for Assets/Tests/EditMode/Ui/math_vectors.txt (MathModesTests, task 4.1). world_tour/ is read only.
// It runs the web game's real modes/math/parts.js (wireKeypad, ladder, typedAnswer, rich) on problems from the real
// generators, with the browser-only modules stubbed by hooks.mjs; beat.js layout() and cheapestCut() and merch.js tag()
// and receipt() are not exported, so their source is copied out of the files verbatim at run time and evaluated.
// Math.random is never used here; every choice comes from mulberry32 (math/rng.js), so the output is the same every run.
// Run from world_tour_unity/Tools/math:
//   node --import ./register.mjs math_vectors.mjs > ../../Assets/Tests/EditMode/Ui/math_vectors.txt
// One JSON object per line: t = "K" keypad, "L" layout, "C" cheapestCut, "M" merch tags and receipts, "S" a typed
// round (typedAnswer), "B" a beat builder ladder (ladder + wrong(null, why)).
import fs from 'node:fs';
const WT = new URL('../../../world_tour/', import.meta.url);
const imp = p => import(new URL(p, WT).href);
const parts = await imp('modes/math/parts.js');
const dec = await imp('math/gen/decimals.js');
const frac = await imp('math/gen/fractions.js');
const { R, num, sum, sub, str } = await imp('math/rational.js');
const { dollars } = await imp('math/money.js');
const { esc } = await imp('js/util.js');
const { show } = await imp('math/answer.js');
const { rng } = await imp('math/rng.js');

function extract(file, startRe) {
  const src = fs.readFileSync(new URL(file, WT), 'utf8');
  const m = src.match(startRe);
  if (!m) throw new Error('not found ' + startRe + ' in ' + file);
  let i = src.indexOf('{', m.index), depth = 0, j = i;
  for (; j < src.length; j++) { if (src[j] === '{') depth++; else if (src[j] === '}' && --depth === 0) break; }
  return src.slice(m.index, j + 1);
}
const beatSrc = extract('modes/math/beat.js', /function layout\(/) + '\n' + extract('modes/math/beat.js', /function cheapestCut\(/);
const { layout, cheapestCut } = new Function('num', beatSrc + '\nreturn { layout, cheapestCut };')(num);
const merchFile = fs.readFileSync(new URL('modes/math/merch.js', WT), 'utf8');
const tagSrc = merchFile.match(/const tag = [\s\S]*?;\n/)[0];
const { tag, receipt } = new Function('dollars', 'esc', tagSrc + extract('modes/math/merch.js', /function receipt\(/) + '\nreturn { tag, receipt };')(dollars, esc);

const out = [];
const emit = o => out.push(JSON.stringify(o).replace(/[^\x00-\x7f]/g, c => '\\u' + c.charCodeAt(0).toString(16).padStart(4, '0')));   // plain ASCII

// rich() html back to flat text: a stacked fraction becomes "w n/d" (C#: MathText.Plain on the raw markup)
const plain = h => String(h).replace(/<span class="mixed">(?:<span class="whole">(\d+)<\/span>)?<span class="frac"><span>(.*?)<\/span><span>(.*?)<\/span><\/span><\/span>/g,
  (_, w, n, d) => (w ? w + ' ' : '') + n + '/' + d);

function fakeRoot() {
  const ro = { innerHTML: '', isConnected: true };
  const cls = { toggle() {}, add() {}, remove() {} };
  return { ro, querySelector: s => s === '#ro' ? ro : s === '#readout' ? { classList: cls } : null, querySelectorAll: () => [] };
}
const keyEvent = k => ({ key: k, target: { closest: () => null }, preventDefault() {} });
const press = (ch) => {
  const fn = globalThis.__keys[globalThis.__keys.length - 1];
  fn(keyEvent(ch === '\n' ? 'Enter' : ch === '<' ? 'Backspace' : ch));
};

// ---------- K: the keypad's text and readout ----------
const BOX = '<i class="box"></i>';
function readout(html) {
  if (!html) return 'E';
  const m = html.match(/^<span class="mixed">(?:<span class="whole">(\d+)<\/span>)?<span class="frac"><span>(.*?)<\/span><span>(.*?)<\/span><\/span><\/span>$/);
  if (m) return 'F:' + (m[1] || '') + ':' + (m[2] === BOX ? '' : m[2]) + ':' + (m[3] === BOX ? '' : m[3]);
  return 'P:' + html;
}
{
  const r = rng(4101);
  const alphabet = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', '/', ' ', '<', '3', '7'];
  for (const type of ['money', 'decimal', 'fraction', 'int']) {
    for (let k = 0; k < 90; k++) {
      const root = fakeRoot();
      const pad = parts.wireKeypad(root, { type, onSubmit() {} });
      const n = r.int(1, 14), keys = [];
      for (let i = 0; i < n; i++) keys.push(alphabet[r.int(0, alphabet.length - 1)]);
      for (const ch of keys) press(ch);
      emit({ t: 'K', type, keys: keys.join(''), text: pad.text, readout: readout(root.ro.innerHTML) });
    }
  }
}

// ---------- L and C: beat.js layout() and cheapestCut() ----------
{
  const r = rng(4102);
  for (let level = 1; level <= 3; level++) {
    for (let seed = 1; seed <= 70; seed++) {
      const p = frac.generate(level, seed);
      if (!p.builder) continue;
      const pr = p.params;
      if (pr.removable) {
        emit({ t: 'C', pre: pr.pre, n: p.answer.value.n, d: p.answer.value.d, cut: cheapestCut(pr, p.answer.value) });
        const kept = pr.pre.filter((_, i) => r.chance(.6));
        const lens = kept.map(d => R(1, d)), total = num(sum(lens));
        const rows = Math.max(pr.bars, Math.min(pr.bars + 1, Math.ceil(total - 1e-9)));
        emit({ t: 'L', lens: kept.map(d => '1/' + d), rows, pieces: layout(lens.map(len => ({ len })), rows).flatMap((items, r) => items.map(b => [b.i, String(b.x), String(b.w), b.first, b.cut, r])) });
        continue;
      }
      const fill = frac.decompose(sub(pr.target, sum(pr.pre.map(d => R(1, d)))), pr.palette);
      const extra = [];
      const m = r.int(0, 4);
      for (let i = 0; i < m; i++) extra.push(pr.palette[r.int(0, pr.palette.length - 1)]);
      for (const ds of [[...pr.pre, ...fill], [...pr.pre, ...fill, ...extra], [...pr.pre, ...extra]]) {
        const lens = ds.map(d => R(1, d)), total = num(sum(lens));
        const rows = Math.max(pr.bars, Math.min(pr.bars + 1, Math.ceil(total - 1e-9)));
        emit({ t: 'L', lens: ds.map(d => '1/' + d), rows, pieces: layout(lens.map(len => ({ len })), rows).flatMap((items, r) => items.map(b => [b.i, String(b.x), String(b.w), b.first, b.cut, r])) });
      }
    }
  }
  // typed problems draw fractions that aren't unit notes
  for (let level = 2; level <= 3; level++) {
    for (let seed = 1; seed <= 120; seed++) {
      const p = frac.generate(level, seed);
      if (p.builder) continue;
      const pr = p.params;
      const lens = pr.op === '+' ? [pr.a, pr.b] : [sub(pr.a, pr.b), pr.b];
      const rows = pr.op === '+' ? Math.max(1, Math.ceil(num(pr.a) + num(pr.b) - 1e-9)) : 1;
      emit({ t: 'L', lens: lens.map(f => f.n + '/' + f.d), rows, pieces: layout(lens.map(len => ({ len })), rows).flatMap((items, r) => items.map(b => [b.i, String(b.x), String(b.w), b.first, b.cut, r])) });
    }
  }
  // made-up take-outs, including one with no exact cut
  for (let k = 0; k < 60; k++) {
    const ds = [2, 3, 4, 6, 8, 12], n = r.int(1, 9), pre = [];
    for (let i = 0; i < n; i++) pre.push(ds[r.int(0, 5)]);
    const over = r.chance(.8) ? R(1, ds[r.int(0, 5)]) : R(r.int(1, 5), 12);
    emit({ t: 'C', pre, n: over.n, d: over.d, cut: cheapestCut({ pre }, over) });
  }
}

// ---------- M: merch.js tag() and receipt() ----------
const receiptRows = html => [...html.matchAll(/<div( class="sum")?><span class="op">(.*?)<\/span><span>(.*?)<\/span><\/div>/g)].map(m => (m[1] ? 'S' : 'R') + '~' + m[2] + '~' + m[3]);
for (let level = 1; level <= 3; level++) {
  for (let seed = 1; seed <= 100; seed++) {
    const p = dec.generate(level, seed);
    emit({ t: 'M', level, seed, tags: p.params.lines.map(tag),
      work: p.work !== null, r2: p.work ? receiptRows(receipt(p.work, false)) : null, r3: p.work ? receiptRows(receipt(p.work, true)) : null });
  }
}

// ---------- S: typedAnswer ----------
const keyed = a => show(a).replace(/^\$/, '').replace(/\[\[|\]\]/g, '').replace(/,/g, '');
function wrongOf(p) {
  const a = p.answer;
  if (a.type === 'money') return keyed({ type: 'money', value: a.value + 100 });
  return keyed({ type: 'fraction', value: R(a.value.n + 1, a.value.d) });
}
function runTyped(p, script) {
  const log = [];
  let tries = 0, hints = 0, max = 0, apply = null;
  const ctx = {
    el: fakeRoot(),
    say: (h, pose) => log.push('say:' + pose + ':' + plain(h)),
    setupHints: (m, a) => { max = m; apply = a; log.push('setup:' + m); },
    miss: () => { tries++; log.push('miss:' + tries); return tries; },
    mistake: t => log.push('mistake:' + t),
    reveal: m => log.push('reveal:' + plain(m)),
    win: () => log.push('win'),
    lockHints: () => log.push('lock'),
    next: ms => log.push('next:' + ms)
  };
  const { pad } = parts.typedAnswer(ctx, p, { onStep: n => log.push('step:' + n) });
  for (const act of script) {
    if (act === 'h') {
      if (hints >= max) log.push('h:none');
      else if (apply(hints + 1) === false) log.push('h:free');
      else { hints++; log.push('h:' + hints); }
    } else for (const ch of act) press(ch);
    log.push('pad=' + pad.text);
  }
  return log;
}
function typedScripts(p) {
  const right = keyed(p.answer), wrong = wrongOf(p);
  const trap = p.traps.length ? keyed({ type: p.answer.type, value: p.traps[0].value }) : wrong;
  const s = [
    [wrong + '\n', wrong + '\n', 'h', wrong + '\n', right + '\n'],
    ['\n', 'h', 'h', 'h', trap + '\n', right + '\n', right + '\n'],
    [right + '\n'],
    ['0.\n', '<<<<' + right + '<' + right.slice(-1) + '\n'],
    ['h', trap + '\n', 'h', wrong + '\n', 'h']
  ];
  if (p.answer.type === 'fraction') {
    const v = p.answer.value;
    s.push([(v.n * 2) + '/' + (v.d * 2) + '\n', right + '\n']);           // not simplest: a nudge where it's asked for
    s.push(['1 \n', '/\n', right + '\n']);
  }
  return s;
}
for (let level = 1; level <= 3; level++)
  for (let seed = 1; seed <= 16; seed++) {
    const p = dec.generate(level, seed);
    for (const script of typedScripts(p)) emit({ t: 'S', skill: 'dec', level, seed, script, log: runTyped(p, script) });
  }
for (let level = 2; level <= 3; level++) {
  let n = 0;
  for (let seed = 1; n < 14; seed++) {
    const p = frac.generate(level, seed);
    if (p.builder) continue;
    n++;
    for (const script of typedScripts(p)) emit({ t: 'S', skill: 'frac', level, seed, script, log: runTyped(p, script) });
  }
}

// ---------- B: the beat builder's ladder (beat.js:133-174) ----------
function runBuilder(p, script) {
  const log = [];
  let tries = 0, hints = 0, max = 0, apply = null;
  const ctx = {
    say: (h, pose) => log.push('say:' + pose + ':' + plain(h)),
    setupHints: (m, a) => { max = m; apply = a; log.push('setup:' + m); },
    miss: () => { tries++; log.push('miss:' + tries); return tries; },
    mistake: t => log.push('mistake:' + t),
    reveal: m => log.push('reveal:' + plain(m)),
    win: () => log.push('win'),
    lockHints: () => log.push('lock')
  };
  const L = parts.ladder(ctx, p, { onStep: n => log.push('step:' + n) });
  for (const act of script) {
    if (act === 'h') {
      if (hints >= max) log.push('h:none');
      else if (apply(hints + 1) === false) log.push('h:free');
      else { hints++; log.push('h:' + hints); }
    } else if (act === 'right') L.right();
    else log.push('over=' + L.wrong(null, act));
  }
  return log;
}
for (let level = 1; level <= 3; level++) {
  let n = 0;
  for (let seed = 1; n < 16; seed++) {
    const p = frac.generate(level, seed);
    if (!p.builder) continue;
    n++;
    for (const script of [['too-short', 'too-long', 'too-short'], ['h', 'too-long', 'h', 'h', 'right'], ['h', 'h', 'too-short', 'too-short']])
      emit({ t: 'B', level, seed, script, log: runBuilder(p, script) });
  }
}

process.stdout.write(out.join('\n') + '\n');
process.exit(0);
