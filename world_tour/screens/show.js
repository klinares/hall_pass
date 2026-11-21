/* #/show/<mode>?v=<venue>&w=<week>: starts a show. */
import { MODES, modeById, isAvailable } from '../modes/index.js';
import { WEEKS } from '../data/weeks.js';
import * as P from '../js/progress.js';
import { mountShow, pickItems } from '../js/show.js';
import { defaultWeek } from './venue.js';
import { warmUp } from '../js/speech.js';

export function mount(root, { id, query }) {
  const mode = modeById(id);
  if (!mode) { location.replace('#/map'); return; }
  const venue = P.unlocked(query.v) ? query.v : P.newestVenue().id;
  const week = WEEKS.find(w => w.id === query.w) || defaultWeek();
  if (!isAvailable(mode, week)) { location.replace('#/venue/' + venue); return; }
  P.setModes(MODES);
  warmUp();
  return mountShow(root, { mode, venue, week, items: pickItems(mode, week) });
}
