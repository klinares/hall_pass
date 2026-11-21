/* Each generator: 3,000 fixed seeds per level, checked against an independent calculation,
   the generator contract, and the grade-level bounds. */
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { seeds, contract, repeatable } from './helpers.mjs';
import * as dec from '../../math/gen/decimals.js';
import * as md from '../../math/gen/multdiv.js';
import * as frac from '../../math/gen/fractions.js';
import * as xy from '../../math/gen/coords.js';
import * as vol from '../../math/gen/volume.js';
import * as ops from '../../math/gen/orderops.js';
import * as flu from '../../math/gen/fluency.js';
import { valueOf } from '../../math/answer.js';

const digits = n => String(n).length;
const close = (a, b) => Math.abs(a - b) < 1e-9 * Math.max(1, Math.abs(b));
const asNum = ans => { const v = valueOf(ans); return v.n / v.d; };

function each(mod, skill, fn) {
  for (const level of [1, 2, 3]) {
    test(`${skill} level ${level}`, () => {
      const kinds = new Set();
      repeatable(mod.generate, level, 12345);
      for (const s of seeds(level)) {
        const p = mod.generate(level, s);
        contract(p, skill, level);
        kinds.add(p.kind);
        fn(p, level);
      }
      assert.ok(kinds.size >= 1);
    });
  }
}

each(dec, 'dec', (p, level) => {
  const { lines, paid, total } = p.params;
  /* independent total: quantities × prices, bulk = tenths × cents ÷ 10 */
  let t = 0;
  for (const l of lines) {
    if (l.unit) { assert.equal((l.amount * l.each) % 10, 0, 'bulk cost lands on whole cents'); t += l.amount * l.each / 10; }
    else t += l.qty * l.each;
  }
  assert.equal(t, total);
  const want = p.kind === 'change' ? paid - total : total;
  assert.equal(p.answer.value, want);
  assert.equal(p.answer.type, 'money');
  assert.ok(want > 0 && want < 10000, 'money under $100');
  if (p.kind === 'change') assert.equal(paid, [0, 1000, 2000, 5000][level]);
  if (level === 1) { assert.equal(lines.length, 2); assert.ok(total < 1000); }
  if (level === 2) { assert.equal(lines.length, 3); assert.ok(lines.some(l => l.qty > 1)); assert.ok(total < 2000); }
  if (level === 3) { assert.ok(lines[0].unit); assert.ok(lines[0].amount % 10 !== 0, 'a real decimal amount'); assert.ok(total < 5000); }
});

each(md, 'md', (p, level) => {
  const { a, b, N, d } = p.params;
  if (p.kind === 'mul') {
    assert.equal(p.answer.value, a * b);
    const [da, db] = [[2, 1], [2, 2], [3, 2]][level - 1];
    assert.equal(digits(a), da); assert.equal(digits(b), db);
    if (p.prompt.includes('poster')) {
      const per = +p.prompt.match(/(\d+) posters|posters come (\d+)/).slice(1).find(Boolean);
      assert.ok(md.BOX_SIZES.includes(per), 'a real box size: ' + per);
    }
  } else if (p.kind === 'div') {
    /* partial quotients: the chunks add up to the answer, use up N, and the hints keep a running total */
    const w = p.work;
    assert.equal(w.chunks.reduce((s, c) => s + c.c, 0), p.answer.value);
    assert.equal(w.chunks[w.chunks.length - 1].left, 0);
    assert.ok(w.chunks.length === 1 || (w.chunks[0].c % 10 === 0 && w.chunks[1].c < 10), 'tens first, then ones');
    assert.ok(p.steps[1].includes('running total') && p.steps[2].includes('running total'));
    assert.ok(!p.steps.join(' ').includes('start big'));
    if (p.answer.value > 10) assert.ok(p.steps[0].startsWith('how many tens of'));
    if (p.answer.value === 10) assert.ok(p.steps[0].startsWith('what happens to a number when you multiply it by 10'), 'quotient 10: hint 1 prints no product');
    assert.equal(N % d, 0, 'exact division');
    assert.equal(p.answer.value, N / d);
    const [dn, dd] = [[3, 1], [3, 2], [4, 2]][level - 1];
    assert.equal(digits(N), dn); assert.equal(digits(d), dd);
  } else {
    assert.equal(level, 3);
    assert.notEqual(N % d, 0, 'a remainder problem has a remainder');
    assert.equal(p.answer.value, Math.floor(N / d));
    assert.equal(digits(d), 2);
  }
  assert.ok(p.answer.value > 0);
});

each(frac, 'frac', (p, level) => {
  const pr = p.params, ds = [...pr.palette, ...pr.pre, pr.grid];
  assert.ok(ds.every(x => [2, 3, 4, 6, 8, 12].includes(x)), 'denominators from the allowed set, at most 12');
  assert.ok(pr.palette.every(x => pr.grid % x === 0) && pr.pre.every(x => pr.grid % x === 0), 'every block sits on the grid');
  const v = asNum(p.answer);
  assert.ok(v > 0, 'something to add or take away');
  if (p.builder) {
    const written = pr.pre.reduce((s, x) => s + 1 / x, 0), target = pr.target.n / pr.target.d;
    assert.ok(close(v, p.kind === 'takeout' ? written - target : target - written), 'builder answer matches the measure');
    /* the worked hint's blocks really fill it */
    const fill = frac.decompose(p.answer.value, pr.palette);
    if (p.kind === 'takeout') {
      assert.ok(close(fill.reduce((s, x) => s + 1 / x, 0), v));
    } else assert.ok(frac.filled(pr, fill), 'the decomposed blocks fill the measure exactly');
    if (level === 1) { assert.ok(pr.palette.every(x => [2, 4, 8].includes(x))); assert.equal(pr.target.n / pr.target.d, 1); }
    if (p.kind === 'mixed') { assert.ok(target > 1 && target < 2); assert.equal(pr.bars, 2); }
  } else {
    const A = pr.a.n / pr.a.d, B = pr.b.n / pr.b.d;
    assert.ok(close(v, pr.op === '+' ? A + B : A - B), 'typed answer matches');
    assert.notEqual(pr.a.d, pr.b.d, 'unlike denominators');
    if (p.kind === 'diff') assert.equal(p.accept.simplest, true);
  }
});

each(xy, 'xy', (p, level) => {
  const { x, y } = p.answer.value, max = p.params.max;
  assert.equal(max, level === 1 ? 6 : 10);
  assert.ok(x >= 0 && y >= 0 && x <= max && y <= max, 'on the grid');
  if (p.kind === 'move') {
    const { start, dx, dy } = p.params;
    assert.equal(x, start.x + dx); assert.equal(y, start.y + dy);
    assert.ok(start.x >= 0 && start.y >= 0 && start.x <= max && start.y <= max);
    assert.ok(dx !== 0 && dy !== 0);
  }
  for (const t of p.traps) assert.ok(t.value.x <= max && t.value.y <= max && t.value.x >= 0 && t.value.y >= 0);
});

each(vol, 'vol', (p, level) => {
  const bx = p.params.boxes, V = bx.reduce((s, b) => s + b.l * b.w * b.h, 0);
  assert.ok(V <= 1000, 'volume at most 1,000');
  if (p.kind === 'build') { assert.equal(p.answer.value, bx[0].h); assert.equal(p.params.target, V); }
  else if (p.kind === 'missing') { assert.equal(p.answer.value * bx[0].l * bx[0].w, V); }
  else assert.equal(p.answer.value, V);
  if (level === 1) assert.ok(V <= 36, 'level 1 stacks are small enough to count');
  if (p.kind === 'two') { assert.ok(bx[1].l <= bx[0].l && bx[1].w <= bx[0].w, 'the riser fits on top'); }
});

/* an evaluator that shares nothing with the generator: JavaScript's own precedence */
const jsEval = e => Function(`return (${e.replace(/×/g, '*').replace(/÷/g, '/').replace(/−/g, '-')})`)();

each(ops, 'ops', (p, level) => {
  const v = jsEval(p.params.expr);
  assert.equal(p.answer.value, v);
  assert.ok(Number.isInteger(v) && v > 0 && v <= 300);
  const steps = ops.solve(p.params.expr);
  assert.ok(steps, 'every step stays whole and non-negative');
  const nops = (p.params.expr.match(/[+−×÷]/g) || []).length;
  if (level === 1) { assert.equal(nops, 2); assert.ok(p.params.expr.includes('(')); }
  if (level === 2) assert.equal(nops, 3);
  if (level === 1 && p.params.expr.includes('(')) assert.notEqual(jsEval(p.params.expr.replace(/[()]/g, '')), v, 'the parentheses matter');
  if (level === 3) {
    const right = p.choices.find(c => c.correct);
    assert.equal(right.value, p.params.expr);
    for (const c of p.choices.filter(c => !c.correct)) assert.notEqual(jsEval(c.value), v, 'a wrong expression gives a different total');
  }
});

const SUP = { '¹': 1, '²': 2, '³': 3, '⁴': 4 };
each(flu, 'flu', (p, level) => {
  const expr = p.prompt.replace(' = ?', '').replace(/,/g, '').replace(/10([¹²³⁴])/g, (_, s) => `(10**${SUP[s]})`);
  const v = jsEval(expr);
  assert.ok(close(asNum(p.answer), v), `${p.prompt} → ${v}`);
  assert.equal(p.choices.length, 4, 'four choices');
  if (level === 1) assert.ok(/^\d+ [×÷] \d+ = \?$/.test(p.prompt));
  if (level < 3) assert.ok(Number.isInteger(v), 'whole numbers below level 3');
  if (p.kind === 'mul' || p.kind === 'div') {
    const [a, b] = p.label.split(/ [×÷] /).map(Number);
    const top = level === 1 ? 10 : 12;
    if (p.kind === 'mul') assert.ok(a <= top && b <= top);
    else assert.ok(b <= top && v <= top);
  }
  /* moving the decimal point (× or ÷ 10, 100, 1,000): hint 2 uses place-value wording that holds for this
     problem (digits move k places, left for ×, right for ÷), asks about the zero exactly when one is filled in
     or drops off, and never prints the digits or the answer */
  if (p.kind === 'decpow' || p.kind === 'decdiv') {
    const [a, b] = p.label.replace(/,/g, '').split(/ [×÷] /);
    const h2 = p.steps[1];
    const times = p.kind === 'decpow';
    const k = Math.round(Math.log10(Number(b)));
    assert.equal(10 ** k, Number(b), p.label);
    assert.ok(!/digits (stay|don’t change)/.test(h2), 'no claim that the digits stay the same');
    assert.ok(!/keep going/.test(h2), 'hint 2 is the place-value line at every power');
    assert.ok(h2.startsWith(`${times ? '×' : '÷'} ${p.label.split(/ [×÷] /)[1]} moves every digit ${k} place${k === 1 ? '' : 's'} to the ${times ? 'left' : 'right'}.`), h2);
    const decimals = (a.split('.')[1] || '').length;
    const filled = times && k > decimals;
    const dropped = !times && Number(a) % 10 === 0;
    assert.equal(h2.includes('what fills the empty place?'), filled, `${p.label}: ${h2}`);
    assert.equal(h2.includes('what happens to the 0 at the end?'), dropped, `${p.label}: ${h2}`);
    const exact = times ? Number(a) * 10 ** k : Number(a) / 10 ** k;
    assert.ok(Math.abs(v - exact) < 1e-9, `${p.label}: answer ${v} is the digits moved ${k} places`);
    assert.ok(!h2.replace(/^[×÷] [\d,]+ moves/, '').includes(String(v)), `hint 2 does not state the answer: ${h2}`);
  }
});
