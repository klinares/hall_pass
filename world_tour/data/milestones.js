/* Tour journal stickers. Each rule reads saved progress and says whether it's earned.
   Earned stickers are kept forever, even if the thing that earned them changes later.
   art: a sticker id from art/sprite.js, or venue:<id> for a venue drawing.
   test(s, ctx): s is the saved state; ctx.modes lists every playable mode {id, kind}. */
const showsDone = s => s.shows.filter(x => !x.encore);
const soldOut = x => x.sold || (x.n >= 5 && x.first / x.n >= .9);
/* mastered: at level 3 and 10 right answers there (one hot show can reach level 3, so that alone isn't enough) */
export const MASTER_AT_3 = 10;
const maxLevel = (s, ids) => ids.some(id => s.skills[id]?.level === 3 && (s.skills[id].byLevel?.[3]?.[1] || 0) >= MASTER_AT_3);
/* the key a mode's level is saved under: math modes by skill, spelling modes as spell:<mode> */
export const skillKey = m => m.kind === 'math' ? m.skill : 'spell:' + m.id;

export const MILESTONES = [
  { id: 'first-show',  art: 'ticket',  name: 'opening night',      how: 'finish your first show',
    test: s => showsDone(s).length >= 1 },
  { id: 'sold-out',    art: 'star',    name: 'first sold-out show', how: 'get 9 of 10 right on the first try',
    test: s => showsDone(s).some(soldOut) },
  { id: 'clean-run',   art: 'bolt',    name: 'ten in a row',        how: 'play a whole show with no misses and no hints',
    test: s => showsDone(s).some(x => x.n >= 10 && x.first >= x.n) },
  { id: 'streak-3',    art: 'heart',   name: 'three-day streak',    how: 'play three days in a row',
    test: s => s.streak.best >= 3 },
  { id: 'streak-5',    art: 'lights',  name: 'five-day streak',     how: 'play five days in a row',
    test: s => s.streak.best >= 5 },
  { id: 'streak-10',   art: 'vinyl',   name: 'ten-day streak',      how: 'play ten days in a row',
    test: s => s.streak.best >= 10 },
  { id: 'master-any',  art: 'trophy',  name: 'mastered a skill',    how: 'get 10 right at level 3 in any mode',
    test: (s, c) => maxLevel(s, c.modes.map(skillKey)) },
  { id: 'master-spell', art: 'mic',    name: 'spelling headliner',  how: 'get 10 right at level 3 in a spelling mode',
    test: (s, c) => maxLevel(s, c.modes.filter(m => m.kind === 'spelling').map(skillKey)) },
  { id: 'master-math', art: 'amp',     name: 'math headliner',      how: 'get 10 right at level 3 in a math mode',
    test: (s, c) => maxLevel(s, c.modes.filter(m => m.kind === 'math').map(skillKey)) },
  { id: 'every-mode',  art: 'bus',     name: 'played every stage',  how: 'finish a show in every mode',
    test: (s, c) => c.modes.length > 0 && c.modes.every(m => s.shows.some(x => x.mode === m.id)) },
  { id: 'ten-shows',   art: 'setlist', name: 'ten shows',           how: 'finish ten shows',
    test: s => showsDone(s).length >= 10 },
  { id: 'fifty-shows', art: 'guitar',  name: 'fifty shows',         how: 'finish fifty shows',
    test: s => showsDone(s).length >= 50 },
  { id: 'hour',        art: 'cassette', name: 'an hour of practice', how: 'play for an hour in total',
    test: s => Object.values(s.time).reduce((a, b) => a + b, 0) >= 3600 },
  /* one per venue after the first */
  ...[['records', 'record store'], ['rec', 'rec center'], ['rooftop', 'rooftop'], ['club', 'the club'],
      ['theater', 'theater'], ['festival', 'festival'], ['arena', 'the arena']].map(([v, name]) => ({
    id: 'venue-' + v, art: 'venue:' + v, name, how: `unlock ${name.startsWith('the ') ? name : 'the ' + name}`,
    test: s => s.venues.unlocked.includes(v)
  }))
];

export const milestoneById = id => MILESTONES.find(m => m.id === id) || null;
