/* Shared checks every generator must pass. */
import assert from 'node:assert/strict';
import { valueOf, show } from '../../math/answer.js';

export const RUNS = 3000;
export const seeds = (level, n = RUNS) => Array.from({ length: n }, (_, i) => 1000 * level + i * 7919 + 1);

const key = v => typeof v === 'object' && v !== null ? ('x' in v ? `${v.x},${v.y}` : `${v.n / v.d}`) : String(v);

export function contract(p, skill, level) {
  assert.equal(p.skill, skill);
  assert.equal(p.level, level);
  assert.equal(typeof p.kind, 'string');
  assert.ok(p.params && typeof p.params === 'object', 'params');
  assert.ok(['money', 'int', 'decimal', 'fraction', 'point'].includes(p.answer.type), 'answer type ' + p.answer.type);
  assert.equal(typeof p.prompt, 'string');
  assert.equal(typeof p.label, 'string');
  assert.ok(Array.isArray(p.steps) && p.steps.length === 3 && p.steps.every(s => typeof s === 'string' && s.length > 3), 'three hint steps');
  const text = [p.prompt, p.label, ...p.steps].join(' ');
  assert.ok(!/undefined|NaN|null|\[object/.test(text), 'no broken text: ' + text);
  assert.ok(!/\d+\/\d+/.test(text.replace(/\[\[[^\]]*\]\]/g, '')), 'fractions only inside [[ ]] markup: ' + text);
  assert.doesNotThrow(() => show(p.answer));
  if (p.answer.type !== 'point') {
    const v = valueOf(p.answer);
    assert.ok(v.n >= 0, 'answer not negative');
  }
  /* the bulb's two steps never state the answer: only the worked step (3) may */
  if (['int', 'money', 'decimal'].includes(p.answer.type)) {
    const ans = show(p.answer).replace(/[$.]/g, m => '\\' + m);
    const leak = new RegExp(`= (<b>)?${ans}(</b>)?([.,:]|\\s|$)`);
    for (const s of p.steps.slice(0, 2)) assert.ok(!leak.test(s), `hint gives the answer away: ${s}`);
  }
  /* traps: tagged, different from the answer and from each other */
  const ak = key(p.answer.type === 'money' || p.answer.type === 'int' ? p.answer.value : p.answer.value);
  const seen = new Set();
  for (const t of p.traps || []) {
    assert.equal(typeof t.why, 'string', 'trap tagged');
    assert.notEqual(key(t.value), ak, 'trap differs from answer');
    assert.ok(!seen.has(key(t.value)), 'no duplicate traps');
    seen.add(key(t.value));
  }
  /* choices: exactly one correct, no duplicates, every distractor tagged */
  if (p.choices) {
    assert.ok(p.choices.length >= 3, 'at least 3 choices');
    assert.equal(p.choices.filter(c => c.correct).length, 1, 'exactly one correct choice');
    const ks = p.choices.map(c => key(c.value));
    assert.equal(new Set(ks).size, ks.length, 'no duplicate choices');
    for (const c of p.choices.filter(c => !c.correct)) assert.equal(typeof c.why, 'string', 'distractor tagged');
  }
}

/* same seed, same problem */
export function repeatable(gen, level, seed) {
  assert.deepEqual(JSON.stringify(gen(level, seed)), JSON.stringify(gen(level, seed)));
}
