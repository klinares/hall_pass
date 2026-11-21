/* STAGE DESIGNER: the first quadrant of the coordinate plane (5.G.1-2).
   L1: place a light at a point, or read where one is, axes to 6
   L2: the same with axes to 10
   L3: "move it 3 right and 2 up": tap where it ends up, axes to 10 */
import { cleanTraps } from '../mistakes.js';
import { guarded } from './guard.js';

export const SKILL = 'xy';
const GEAR = [['spotlight', 'lights'], ['mic stand', 'mic'], ['amp', 'amp'], ['speaker', 'crate'], ['drum riser', 'star']];
const pt = p => `(${p.x}, ${p.y})`;

export const generate = guarded(build);

function build(level, seed, r) {
  const max = level === 1 ? 6 : 10;
  const kind = level === 3 ? r.pick(['move', 'move', 'read']) : r.pick(['place', 'read']);
  const [gear, icon] = r.pick(GEAR);
  let p, start = null, dx = 0, dy = 0;
  const rand = () => { let q; do q = { x: r.int(0, max), y: r.int(0, max) }; while (q.x === q.y || (q.x === 0 && q.y === 0)); return q; };

  if (kind === 'move') {
    for (;;) {
      start = { x: r.int(0, max), y: r.int(0, max) };
      dx = r.int(-4, 4); dy = r.int(-4, 4);
      if (!dx || !dy) continue;
      p = { x: start.x + dx, y: start.y + dy };
      if (p.x < 0 || p.y < 0 || p.x > max || p.y > max || p.x === p.y) continue;
      break;
    }
  } else p = rand();

  const answer = { type: 'point', value: p };
  const words = (n, pos, neg) => `${Math.abs(n)} ${n > 0 ? pos : neg}`;
  let prompt, label, steps;
  if (kind === 'place') {
    prompt = `put the ${gear} at ${pt(p)}. tap that spot on the stage.`;
    label = `place ${pt(p)}`;
    steps = [
      `the first number goes across, the second goes up. start at (0, 0).`,
      `go across to <b>${p.x}</b> on the bottom line. now go up from there.`,
      `across ${p.x}, up ${p.y}. that’s <b>${pt(p)}</b>.`
    ];
  } else if (kind === 'read') {
    prompt = `where is the ${gear}? set the two numbers.`;
    label = `read ${pt(p)}`;
    steps = [
      `the first number is how far across, the second is how far up.`,
      `follow the ${gear} straight down to the bottom line: it’s at <b>${p.x}</b> across.`,
      `across ${p.x}, up ${p.y}: <b>${pt(p)}</b>.`
    ];
  } else {
    prompt = `the ${gear} is at ${pt(start)}. move it ${words(dx, 'right', 'left')} and ${words(dy, 'up', 'down')}. tap where it ends up.`;
    label = `${pt(start)} moved`;
    steps = [
      `right and left change the first number. up and down change the second.`,
      `${words(dx, 'right', 'left')}: ${start.x} ${dx > 0 ? '+' : '−'} ${Math.abs(dx)} = <b>${p.x}</b>. now the up-and-down part.`,
      `${start.y} ${dy > 0 ? '+' : '−'} ${Math.abs(dy)} = ${p.y}. it lands on <b>${pt(p)}</b>.`
    ];
  }
  const traps = [
    { value: { x: p.y, y: p.x }, why: 'swapped' },
    kind === 'move' ? { value: { x: start.x + dy, y: start.y + dx }, why: 'moves-swapped' } : null,
    kind === 'move' ? { value: { x: start.x - dx, y: start.y - dy }, why: 'wrong-direction' } : null
  ].filter(Boolean);

  return {
    skill: SKILL, level, kind, seed,
    params: { max, gear, icon, start, dx, dy, point: kind === 'read' ? p : null },
    prompt, label, answer, accept: { simplest: false }, choices: null,
    traps: cleanTraps(answer, traps.filter(t => t.value.x <= max && t.value.y <= max)), steps
  };
}
