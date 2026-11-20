/* Task 4.3 (builder M2). Runs the web game's real ticket sales, tour budget and soundcheck modes
   (world_tour/modes/math/tickets.js, budget.js, soundcheck.js with parts.js, imported read-only) against the fake DOM in
   fake_dom.mjs, with sound and motion stubbed by builder_hooks.mjs, a fake clock for soundcheck's meter and
   Math.random seeded (mulberry32, as math/rng.js), and prints what they do for scripted presses: every ctx call, the
   feedback, and what's on screen after each press (the worked paper, the budget sheet and choices, the meter).
   The C# tests (MathTypedModesTests) replay the same presses through TicketsWork, BudgetRound and SoundcheckRound
   with the shared TypedMathRound / MathLadder, and compare.

   node Tools/math/typed_vectors.mjs             writes Assets/Tests/EditMode/Ui/typed_vectors.json
   node Tools/math/typed_vectors.mjs --stdout    prints it instead */
import { register } from 'node:module';
register('./builder_hooks.mjs', import.meta.url);
import { makeCtx, hint, T } from './fake_dom.mjs';

globalThis.window = {};
globalThis.addEventListener = () => {};
globalThis.removeEventListener = () => {};
globalThis.document = { documentElement: { dataset: {} } };
let clock = 0, intervals = [];
Object.defineProperty(globalThis, 'performance', { value: { now: () => clock }, configurable: true, writable: true });
globalThis.setInterval = (fn, ms) => { intervals.push({ fn, ms, next: clock + ms, on: true }); return intervals.length; };
globalThis.clearInterval = id => { if (intervals[id - 1]) intervals[id - 1].on = false; };
const advance = ms => { const end = clock + ms; for (;;) { const due = intervals.filter(t => t.on && t.next <= end).sort((a, b) => a.next - b.next)[0]; if (!due) break; clock = due.next; due.next += due.ms; due.fn(); } clock = end; };

const base = process.env.WT_WEB_URL ?? new URL('../../../world_tour/', import.meta.url).href;
const tickets = (await import(base + 'modes/math/tickets.js')).default;
const budget = (await import(base + 'modes/math/budget.js')).default;
const soundcheck = (await import(base + 'modes/math/soundcheck.js')).default;
const md = await import(base + 'math/gen/multdiv.js');
const ops = await import(base + 'math/gen/orderops.js');
const flu = await import(base + 'math/gen/fluency.js');
const { rng } = await import(base + 'math/rng.js');
const { show } = await import(base + 'math/answer.js');

const typeIn = (el, text) => { for (const ch of String(text)) el.querySelectorAll('.keypad [data-k]').find(b => b.dataset.k === ch).fire('click'); el.querySelector('#go').fire('click'); };
const cls = e => [...e.cls()];

/* ---------------- ticket sales ---------------- */
function workSnap(el) {
  const wp = el.querySelector('#work .workpaper');
  if (!wp) return null;
  const s = { asum: wp.querySelector('.asum') ? wp.querySelector('.asum').textContent : null };
  if (wp.querySelector('.area')) { s.kind = 'area'; s.heads = wp.querySelectorAll('.ahead').map(x => x.textContent); s.cells = wp.querySelectorAll('.acell').map(x => x.textContent); }
  else if (wp.querySelector('.chunks')) { s.kind = 'chunks'; s.rows = wp.querySelectorAll('.crow').map(r => [cls(r).filter(c => c !== 'crow').join(' '), r.querySelector('span').textContent, r.querySelector('small') ? r.querySelector('small').textContent : null]); }
  else { s.kind = 'remainder'; s.full = wp.querySelectorAll('i').filter(i => i.cls().has('full')).length; s.more = wp.querySelector('.more') ? wp.querySelector('.more').textContent : null; s.part = +wp.querySelector('i.part').attrs.style.match(/--p:([\d.e-]+)/)[1]; }
  return s;
}
function runTickets(level, seed, plan) {
  const prob = md.generate(level, seed);
  T.EV = []; globalThis.__ev = T.EV;
  const { ctx, st } = makeCtx(prob);
  tickets.render(ctx);
  const el = ctx.el;
  const board = el.querySelector('.letterboard').children.filter(c => c.tag === 'span').map(s => s.textContent);
  const head = { steps: prob.steps, board, lbl: el.querySelector('.cluelbl').textContent, check: el.querySelector('#go').textContent.trim() };
  const actions = [], snaps = [];
  const snap = a => { actions.push(a); snaps.push({ ev: T.EV.splice(0), work: workSnap(el), ro: el.querySelector('#ro').textContent, nx: !el.querySelector('#nx').hidden }); };
  snap(['begin']);
  const ans = prob.answer.value;
  for (const step of plan) {
    if (step === 'hint') { hint(st); snap(['hint']); }
    else if (step === 'right') { typeIn(el, ans); snap(['type', String(ans)]); }
    else if (step === 'wrong') { typeIn(el, ans + 1); snap(['type', String(ans + 1)]); }
    else if (step === 'trap') { const t = prob.traps[0]; if (t) { typeIn(el, t.value); snap(['type', String(t.value)]); } }
    else throw new Error(step);
  }
  return { level, seed, kind: prob.kind, head, actions, snaps };
}

/* ---------------- tour budget ---------------- */
function sheetSnap(el) {
  return el.querySelectorAll('#sheet .eline').map(l => [cls(l).filter(c => c !== 'eline').join(' '), l.textContent, l.querySelector('mark') ? l.querySelector('mark').textContent : '']);
}
function runBudget(level, seed, plan) {
  const prob = ops.generate(level, seed);
  T.EV = []; globalThis.__ev = T.EV;
  const { ctx, st } = makeCtx(prob);
  budget.render(ctx);
  const el = ctx.el;
  const head = { steps: prob.steps, lbl: el.querySelector('.cluelbl').textContent, prompt: el.querySelector('.mathq p').textContent, check: el.querySelector('#go').textContent.trim(),
    choices: el.querySelectorAll('#ex .choice').map(b => b.textContent) };
  const actions = [], snaps = [];
  const snap = a => {
    actions.push(a);
    snaps.push({ ev: T.EV.splice(0), sheet: sheetSnap(el), ro: el.querySelector('#ro').textContent, nx: !el.querySelector('#nx').hidden,
      pad: !el.querySelector('#padslot').hidden,
      choices: el.querySelectorAll('#ex .choice').map(b => (b.disabled ? 'off' : 'on') + (b.cls().has('good') ? ' good' : '') + (b.cls().has('gone') ? ' gone' : '')) });
  };
  snap(['begin']);
  const ans = prob.answer.value;
  const wrongIdx = () => el.querySelectorAll('#ex .choice').findIndex((b, i) => !prob.choices[i].correct && !b.disabled);
  for (const step of plan) {
    if (step === 'hint') { hint(st); snap(['hint']); }
    else if (step === 'right') { typeIn(el, ans); snap(['type', String(ans)]); }
    else if (step === 'wrong') { typeIn(el, ans + 1); snap(['type', String(ans + 1)]); }
    else if (step === 'pickright') { const i = prob.choices.findIndex(c => c.correct); el.querySelectorAll('#ex .choice')[i].fire('click'); snap(['pick', i]); }
    else if (step === 'pickwrong') { const i = wrongIdx(); if (i >= 0) { el.querySelectorAll('#ex .choice')[i].fire('click'); snap(['pick', i]); } }
    else throw new Error(step);
  }
  return { level, seed, kind: prob.kind, head, actions, snaps };
}

/* ---------------- soundcheck ---------------- */
function runSoundcheck(level, seed, rseed, plan) {
  const prob = flu.generate(level, seed);
  const r = rng(rseed); Math.random = r.next;
  clock = 0; intervals = [];
  T.EV = []; globalThis.__ev = T.EV;
  const { ctx, st } = makeCtx(prob);
  soundcheck.render(ctx);
  const el = ctx.el;
  const head = { steps: prob.steps, tag: el.querySelector('.desk-tag').textContent, labels: prob.choices.map(c => show({ type: prob.answer.type, value: c.value })), fact: prob.prompt };
  const actions = [], snaps = [];
  const snap = a => {
    actions.push(a);
    const lbl = el.querySelector('.meterlbl');
    snaps.push({ ev: T.EV.splice(0), width: el.querySelector('#lvl').style.width ?? null, label: lbl.textContent, out: lbl.cls().has('out'), nx: !el.querySelector('#nx').hidden,
      choices: el.querySelectorAll('#ch .choice').map(b => (b.disabled ? 'off' : 'on') + (b.cls().has('good') ? ' good' : '') + (b.cls().has('gone') ? ' gone' : '')) });
  };
  snap(['begin']);
  const right = prob.choices.findIndex(c => c.correct);
  const btn = i => el.querySelectorAll('#ch .choice')[i];
  for (const step of plan) {
    if (typeof step === 'number') { advance(step); snap(['wait', step]); }
    else if (step === 'hint') { hint(st); snap(['hint']); }
    else if (step === 'right') { btn(right).fire('click'); snap(['pick', right]); }
    else if (step === 'wrong') { const i = prob.choices.findIndex((c, k) => !c.correct && !btn(k).disabled); if (i >= 0) { btn(i).fire('click'); snap(['pick', i]); } }
    else throw new Error(step);
  }
  return { level, seed, rseed, kind: prob.kind, head, actions, snaps };
}

const out = { tickets: [], budget: [], soundcheck: [] };
const tPlans = [['hint', 'hint', 'wrong', 'wrong', 'wrong'], ['right'], ['wrong', 'hint', 'right'], ['trap', 'hint', 'trap', 'trap']];
for (const level of [1, 2, 3]) for (let i = 0; i < 16; i++) out.tickets.push(runTickets(level, 300 + level * 1000 + i * 41, tPlans[i % tPlans.length]));
const bPlans = {
  expr: [['hint', 'hint', 'wrong', 'wrong', 'wrong'], ['right'], ['wrong', 'hint', 'right']],
  story: [['pickwrong', 'hint', 'pickright', 'right'], ['hint', 'hint', 'wrong', 'right'], ['pickwrong', 'pickwrong', 'pickwrong', 'right'], ['pickright', 'wrong', 'wrong', 'wrong'], ['pickwrong', 'pickwrong', 'hint', 'right']]
};
for (const level of [1, 2, 3]) for (let i = 0; i < 12; i++) { const r = runBudget(level, 200 + level * 1000 + i * 29, level === 3 ? bPlans.story[i % 5] : bPlans.expr[i % 3]); out.budget.push(r); }
const sPlans = [[1000, 'right'], [500, 'wrong', 1000, 'hint', 9000, 'right'], ['wrong', 'wrong', 'wrong'], ['hint', 'hint', 300, 'right'], [12500, 'wrong', 'hint', 'right'], ['hint', 'wrong', 'hint', 'wrong']];
for (const level of [1, 2, 3]) for (let i = 0; i < 18; i++) out.soundcheck.push(runSoundcheck(level, 100 + level * 1000 + i * 31, 77 + i, sPlans[i % sPlans.length]));
const json = JSON.stringify(out);
if (process.argv.includes('--stdout')) process.stdout.write(json);
else {
  const { writeFileSync } = await import('node:fs');
  const file = new URL('../../Assets/Tests/EditMode/Ui/typed_vectors.json', import.meta.url);
  writeFileSync(file, json);
  console.log('wrote', file.pathname, json.length, 'bytes');
}
