/* Every line of in-game text that has a voice lives here.
   Lowercase, short, a little dry, never babyish. Name used now and then, not constantly. */
import { pick } from './util.js';

export const NAME = 'natalia';
export const DOG = 'riff';

export const CHEERS = ['iconic', 'yes!!', 'flawless', 'encore!', 'obsessed', 'crowd screaming', 'main character'];

/* made-up songs for the ticker and setlists. no real artists or titles. */
export const SONGS = ['glitter on the gym floor', 'homework (acoustic)', 'paper airplane heart', 'bus window daydream',
  'detention for two', 'sticker sheet summer', 'cafeteria anthem', 'locker combo', 'second chorus, first try', 'riff’s lullaby'];

export const ticker = () => {
  const t = `now playing: ${SONGS.map(s => `“${s}”`).join(' ✶ ')} ✶ tonight’s headliner: ${NAME} ✶ `;
  return t + t;
};

export const dogHome = ({ next, toGo, streak, away }) => {
  if (away != null && away >= 3) return `oh hey. i napped for ${away} days. ready when you are.`;
  const lines = [];
  if (next) lines.push(`next stop: <b>${next}</b>. ${toGo} fans to go.`);
  else lines.push('we made it to the arena. every show from here is a victory lap.');
  if (streak >= 2) lines.push(`${streak} days in a row. don’t break the chain.`);
  else lines.push('one show a day keeps the streak alive.');
  return pick(lines);
};

export const dogShowStart = () => pick(['sound check done. let’s go.', 'deep breath. first song.', 'crowd’s in. first song.', 'i’ll be right here if you need a hint.']);
export const dogWin = streak => streak >= 3 ? `${streak} in a row. the crowd is losing it.` : pick(['clean.', 'nailed it.', 'that one’s going on the poster.', 'yep. that’s the one.', 'easy work.']);
export const dogHintWin = () => pick(['got there. streak’s safe.', 'hint + you = still a hit.', 'that counts. streak’s still going.']);
export const dogAfterMiss = () => pick(['got it on the rebound.', 'comeback kid.', 'second take was the keeper.']);
export const dogIdleHint = () => `stuck? tap the bulb. your streak is safe, you just win fewer fans.`;

export const finishHead = pct => pct >= .9 ? 'sold out' : pct >= .7 ? 'what a show' : 'rehearsal night';
export const finishSub = pct => pct >= .9 ? `${NAME}, you’re so ready for that test.` : pct >= .7 ? 'a couple more run-throughs and it’s perfect.' : 'every star rehearses. run it back.';
