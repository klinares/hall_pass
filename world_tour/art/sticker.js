/* sticker(id) → inline svg that draws a sheet sticker with its white border and shadow */
export function sticker(id, { cls = '', rot = 0, label = '', small = false } = {}) {
  const aria = label ? `role="img" aria-label="${label}"` : 'aria-hidden="true"';
  return `<svg class="stk ${cls}" viewBox="-10 -10 124 124" style="--r:${rot}deg" ${aria}><g filter="url(#${small ? 'wt-stk-sm' : 'wt-stk'})"><use href="#s-${id}"/></g></svg>`;
}

export function icon(id, label = '') {
  const aria = label ? `role="img" aria-label="${label}"` : 'aria-hidden="true"';
  return `<svg class="ico" viewBox="0 0 24 24" ${aria}><use href="#${id}"/></svg>`;
}

export function venueArt(id, cls = '') {
  return `<svg class="vart ${cls}" viewBox="-6 -6 132 112" aria-hidden="true"><g filter="url(#wt-stk-sm)"><use href="#v-${id}"/></g></svg>`;
}

/* hand-drawn underline: a slightly wobbly marker stroke */
export function scribble(text) {
  return `<span class="scribble">${text}<svg viewBox="0 0 200 20" preserveAspectRatio="none" aria-hidden="true"><path d="M4 12c30-6 60-8 96-5s66 4 96-3"/></svg></span>`;
}

/* a journal sticker: a sheet sticker, or a venue drawing for venue milestones */
export const journalArt = (id, opts) => id.startsWith('venue:') ? venueArt(id.slice(6)) : sticker(id, opts);
