/* The math review queue. A missed problem is saved whole (prompt, answer, choices, hints and
   whatever its mode draws from), so it comes back exactly as she saw it, even after the
   generators change. Entries from older saves have only a seed; those are regenerated.
   Pure functions on the list, so plain Node tests them. */

export const REVIEW_MAX = 40;

const same = (skill, v) => r => r.skill === skill && r.level === v.level && r.seed === v.seed;

/* a stored problem is usable if it still has the parts every mode needs */
export const usable = p => !!p && typeof p === 'object' && typeof p.prompt === 'string'
  && !!p.answer && typeof p.answer === 'object' && Array.isArray(p.steps);

/* a deep copy, so later changes to the live problem can't leak into the save */
const copy = p => JSON.parse(JSON.stringify(p));

/* returns the new list; never longer than REVIEW_MAX (oldest dropped) */
export function recordReview(list, skill, v, { clean, missed, keep = false }) {
  let out = list.slice();
  const hit = out.find(same(skill, v));
  if (missed) {
    if (!hit) out.push({ skill, level: v.level, seed: v.seed, ...(usable(v.prob) ? { prob: copy(v.prob) } : {}) });
    else if (!usable(hit.prob) && usable(v.prob)) out = out.map(r => r === hit ? { ...r, prob: copy(v.prob) } : r);
  }
  if (clean && !keep) out = out.filter(r => !same(skill, v)(r));
  if (out.length > REVIEW_MAX) out = out.slice(out.length - REVIEW_MAX);
  return out;
}

/* a show item for a saved entry: the stored problem if it's usable, else the seed to regenerate */
export const reviewItem = r => ({ skill: r.skill, level: r.level, seed: r.seed, ...(usable(r.prob) ? { prob: copy(r.prob) } : {}) });
