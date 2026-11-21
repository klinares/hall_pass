/* A show item is a week's word ({ w, s, h, ... }) or a math problem ({ skill, level, seed, prob }). */
export const kid = v => v.w || `${v.skill}:${v.level}:${v.seed}`;
export const labelOf = v => v.w || (v.prob ? v.prob.label : '');
