/* skill id → generator module */
import * as dec from './decimals.js';
import * as md from './multdiv.js';
import * as frac from './fractions.js';
import * as xy from './coords.js';
import * as vol from './volume.js';
import * as ops from './orderops.js';
import * as flu from './fluency.js';

export const GENERATORS = { dec, md, frac, xy, vol, ops, flu };
export const generate = (skill, level, seed) => GENERATORS[skill].generate(level, seed);
