/* The math review queue (js/review.js): missed problems are stored whole, not by seed. */
import test from 'node:test';
import assert from 'node:assert/strict';
import { recordReview, reviewItem, usable, REVIEW_MAX } from '../../js/review.js';
import { generate } from '../../math/gen/index.js';

const item = (skill, level, seed) => ({ skill, level, seed, prob: generate(skill, level, seed) });

test('a missed problem is stored whole: prompt, answer, choices, hints', () => {
  const v = item('ops', 3, 77);
  const [r] = recordReview([], 'ops', v, { missed: true, clean: false });
  assert.equal(r.prob.prompt, v.prob.prompt);
  assert.deepEqual(r.prob.answer, v.prob.answer);
  assert.deepEqual(r.prob.choices, v.prob.choices);
  assert.deepEqual(r.prob.steps, v.prob.steps);
  v.prob.prompt = 'changed later';                               /* a copy, not a reference */
  assert.notEqual(r.prob.prompt, 'changed later');
});

test('it comes back exactly as saved, even if the generator now makes something else', () => {
  const v = item('dec', 2, 12345);
  const saved = JSON.parse(JSON.stringify(recordReview([], 'dec', v, { missed: true, clean: false })));   /* through localStorage */
  saved[0].prob.prompt = 'a fan buys a thing the old generator wrote';
  const back = reviewItem(saved[0]);
  assert.equal(back.prob.prompt, 'a fan buys a thing the old generator wrote');
  assert.deepEqual([back.skill, back.level, back.seed], ['dec', 2, 12345]);
});

test('every generator\'s problems survive the save', () => {
  for (const sk of ['dec', 'md', 'frac', 'xy', 'vol', 'ops', 'flu']) for (const lv of [1, 2, 3]) for (let seed = 1; seed <= 60; seed++) {
    const v = item(sk, lv, seed);
    const r = JSON.parse(JSON.stringify(recordReview([], sk, v, { missed: true, clean: false })))[0];
    assert.deepEqual(reviewItem(r).prob, v.prob, `${sk} L${lv} seed ${seed}`);
  }
});

test('old seed-only entries still work, and get the full problem on the next miss', () => {
  const old = [{ skill: 'dec', level: 2, seed: 12345 }];
  assert.equal(reviewItem(old[0]).prob, undefined);              /* the show regenerates it from the seed */
  const v = item('dec', 2, 12345);
  const list = recordReview(old, 'dec', v, { missed: true, clean: false });
  assert.equal(list.length, 1);
  assert.ok(usable(list[0].prob));
  const broken = [{ skill: 'dec', level: 2, seed: 9, prob: { prompt: 'x' } }];   /* hand-edited or damaged */
  assert.equal(reviewItem(broken[0]).prob, undefined);
});

test('a clean answer clears it unless it was missed earlier this show; the queue is capped', () => {
  const v = item('frac', 1, 5);
  let list = recordReview([], 'frac', v, { missed: true, clean: false });
  assert.equal(recordReview(list, 'frac', v, { missed: false, clean: true, keep: true }).length, 1);
  assert.equal(recordReview(list, 'frac', v, { missed: false, clean: true }).length, 0);
  assert.equal(recordReview(list, 'frac', v, { missed: true, clean: false }).length, 1, 'no duplicates');
  list = [];
  for (let s = 1; s <= REVIEW_MAX + 5; s++) list = recordReview(list, 'md', item('md', 1, s), { missed: true, clean: false });
  assert.equal(list.length, REVIEW_MAX);
  assert.equal(list[0].seed, 6, 'oldest dropped first');
});
