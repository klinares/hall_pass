// Cross-check for ShowPicker: show.js pickWords/pickProblems bodies (verbatim), Math.random swapped for mulberry32.
const WT = new URL('../../../world_tour/', import.meta.url).href;   // read only
const { rng, newSeed } = await import(WT + 'math/rng.js');
const { shuffle } = await import(WT + 'js/util.js');
const SHOW_LEN = 10;
let REVIEW = [], STATS = {};
const P = { reviewWords: () => [...REVIEW], wordStats: w => STATS[w] || null, reviewMath: s => [] };
function pickWords(week, mode, n = SHOW_LEN) {
  const review = new Set(P.reviewWords());
  const words = mode && mode.pool ? mode.pool(week) : week.words;
  const scored = shuffle(words).map(v => {
    const st = P.wordStats(v.w);
    return { v, k: (review.has(v.w) ? 0 : 1) * 1000 + (st ? st.a : 0) };
  });
  scored.sort((a, b) => a.k - b.k);
  return shuffle(scored.slice(0, Math.min(n, scored.length)).map(x => x.v));
}
function pickProblems(skill, review, n = SHOW_LEN) {
  const fresh = Array.from({ length: n - review.length }, () => ({ skill, level: null, seed: newSeed() }));
  return [...review, ...fresh];
}
const words = Array.from({ length: 14 }, (_, i) => ({ w: 'w' + i }));
const out = {};
for (const seed of [1, 42, 20261120]) {
  const r = rng(seed); Math.random = r.next;
  REVIEW = ['w3', 'w11']; STATS = { w0: { a: 2 }, w1: { a: 0 }, w2: { a: 5 }, w5: { a: 1 }, w7: { a: 1 }, w9: { a: 3 }, w12: { a: 1 } };
  const a = pickWords({ words }, null).map(v => v.w);
  const after = r.next();
  out['words' + seed] = { a, after };
}
{ const r = rng(7); Math.random = r.next; REVIEW = []; STATS = {}; out.small = pickWords({ words: words.slice(0, 4) }, null).map(v => v.w); }
{ const r = rng(99); Math.random = r.next; out.probs = pickProblems('dec', [{ skill: 'dec', level: 2, seed: 5 }, { skill: 'dec', level: 1, seed: 6 }]).map(p => p.seed); out.probsAfter = r.next(); }
{ const r = rng(3); Math.random = r.next; out.shuf = shuffle(['a','b','c','d','e']); }
console.log(JSON.stringify(out, null, 1));
