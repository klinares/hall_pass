/* Registry of playable modes, in setlist order.
   A spelling mode may define available(week) and pool(week) to limit which weeks/words it uses.
   A math mode has kind 'math', a skill id, and generate(level, seed); math/config.js can switch it off. */
import slots from './spelling/slots.js';
import vowels from './spelling/vowels.js';
import remix from './spelling/remix.js';
import fanmail from './spelling/fanmail.js';
import studio from './spelling/studio.js';
import lyric from './spelling/lyric.js';
import prefix from './spelling/prefix.js';
import merch from './math/merch.js';
import beat from './math/beat.js';
import tickets from './math/tickets.js';
import stage from './math/stage.js';
import truck from './math/truck.js';
import budget from './math/budget.js';
import soundcheck from './math/soundcheck.js';
import { ENABLED_SKILLS } from '../math/config.js';

const SPELLING = [slots, vowels, remix, fanmail, studio, lyric, prefix].map(m => ({ kind: 'spelling', ...m }));
const MATH = [merch, beat, tickets, stage, truck, budget, soundcheck].map(m => ({ ...m, kind: 'math' }));

export const MODES = [...SPELLING, ...MATH.filter(m => ENABLED_SKILLS[m.skill] !== false)];

export const modeById = id => MODES.find(m => m.id === id);
export const isAvailable = (m, week) => !m.available || m.available(week);
export const poolFor = (m, week) => (m.pool ? m.pool(week) : week.words);
