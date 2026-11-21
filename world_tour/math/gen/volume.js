/* PACK THE TRUCK: volume of rectangular prisms (5.MD.3-5).
   L1: count unit crates in a small stack, all visible
   L2: l × w × h from labeled sides, or load the truck floor to an exact count
   L3: two boxes stacked (add the volumes), or find a missing side */
import { cleanTraps } from '../mistakes.js';
import { guarded } from './guard.js';

export const SKILL = 'vol';
const f = n => n.toLocaleString('en-US');

export const generate = guarded(build);

function build(level, seed, r) {
  const kind = level === 1 ? 'count' : level === 2 ? r.pick(['lwh', 'build']) : r.pick(['two', 'missing']);
  let answer, prompt, label, steps, traps = [], boxes, params = {};

  if (kind === 'count') {
    const b = { l: r.int(2, 4), w: r.int(1, 3), h: r.int(1, 3) };
    if (b.w * b.h === 1) b.h = 2;
    const V = b.l * b.w * b.h;
    boxes = [b];
    answer = { type: 'int', value: V };
    prompt = `how many crates are in this stack? every crate is 1 cubic unit.`;
    label = `count ${b.l}×${b.w}×${b.h}`;
    steps = [
      `count the crates in the top layer, then count how many layers.`,
      b.h === 1
        ? `the top layer is ${b.l} across and ${b.w} deep. how many crates is that? there is 1 layer.`
        : `the top layer is ${b.l} by ${b.w}: that’s ${b.l * b.w} crates. there are ${b.h} layers.`,
      `${b.l * b.w} × ${b.h} = <b>${V}</b> cubic units.`
    ];
    traps = [{ value: b.l + b.w + b.h, why: 'added-sides' }, { value: b.l * b.w, why: 'one-layer' }, { value: b.l * b.h + b.l * b.w, why: 'visible-faces' }];
  } else if (kind === 'lwh') {
    let b;
    do b = { l: r.int(3, 12), w: r.int(2, 8), h: r.int(2, 6) }; while (b.l * b.w * b.h > 1000);
    const V = b.l * b.w * b.h;
    boxes = [b];
    answer = { type: 'int', value: V };
    prompt = `the gear case is ${b.l} units long, ${b.w} wide, and ${b.h} tall. what’s its volume in cubic units?`;
    label = `${b.l} × ${b.w} × ${b.h}`;
    steps = [
      `volume = length × width × height. find the floor first: ${b.l} × ${b.w}.`,
      `the floor is ${b.l} × ${b.w} = ${b.l * b.w} square units. stack that ${b.h} high.`,
      `${b.l * b.w} × ${b.h} = <b>${f(V)}</b> cubic units.`
    ];
    traps = [{ value: b.l + b.w + b.h, why: 'added-sides' }, { value: b.l * b.w, why: 'one-layer' }, { value: 2 * (b.l * b.w + b.l * b.h + b.w * b.h), why: 'surface-area' }];
  } else if (kind === 'build') {
    const b = { l: r.int(2, 4), w: r.int(2, 3), h: r.int(2, 4) };
    const V = b.l * b.w * b.h;
    boxes = [b];
    answer = { type: 'int', value: b.h };
    params = { floor: { l: b.l, w: b.w }, target: V, maxH: 6 };
    prompt = `the truck floor fits ${b.l} by ${b.w} crates. load exactly ${V} crates, with every stack the same height.`;
    label = `load ${V} crates`;
    steps = [
      `every layer is one crate on each spot of the floor. how many crates is one layer?`,
      `one layer is ${b.l} × ${b.w} = ${b.l * b.w} crates. how many layers of ${b.l * b.w} make ${V}?`,
      `${b.l} × ${b.w} × ${b.h} = ${V}: <b>${b.h}</b> crates high on every spot.`
    ];
  } else if (kind === 'two') {
    let A, B;
    do {
      A = { l: r.int(4, 10), w: r.int(3, 8), h: r.int(1, 4) };
      B = { l: r.int(2, A.l - 1), w: r.int(2, A.w - 1 || 2), h: r.int(1, 4) };
    } while (B.w > A.w || A.l * A.w * A.h + B.l * B.w * B.h > 1000);
    const VA = A.l * A.w * A.h, VB = B.l * B.w * B.h;
    boxes = [A, B];
    answer = { type: 'int', value: VA + VB };
    prompt = `the stage is a big block ${A.l} × ${A.w} × ${A.h}, with a drum riser ${B.l} × ${B.w} × ${B.h} on top. what’s the total volume?`;
    label = 'stage + riser';
    steps = [
      `find each block’s volume on its own, then add them.`,
      `the stage: ${A.l} × ${A.w} × ${A.h} = ${f(VA)}. now the riser.`,
      `${f(VA)} + ${B.l} × ${B.w} × ${B.h} = ${f(VA)} + ${f(VB)} = <b>${f(VA + VB)}</b> cubic units.`
    ];
    traps = [{ value: VA, why: 'one-box' }, { value: VB, why: 'one-box' }, { value: (A.l + B.l) * (A.w + B.w) * (A.h + B.h), why: 'added-sides' }];
  } else {
    let b;
    do b = { l: r.int(3, 12), w: r.int(2, 9), h: r.int(2, 10) }; while (b.l * b.w * b.h > 1000);
    const V = b.l * b.w * b.h;
    boxes = [b];
    answer = { type: 'int', value: b.h };
    params = { hideH: true };
    prompt = `a speaker case holds ${f(V)} cubic units. it’s ${b.l} long and ${b.w} wide. how tall is it?`;
    label = `missing height of ${f(V)}`;
    steps = [
      `length × width × height = ${f(V)}. you know two of the three.`,
      `the floor is ${b.l} × ${b.w} = ${b.l * b.w}. what times ${b.l * b.w} makes ${f(V)}?`,
      `${f(V)} ÷ ${b.l * b.w} = <b>${b.h}</b>.`
    ];
    traps = [{ value: V - b.l * b.w, why: 'subtracted' }, { value: V / b.l, why: 'divided-once' }];
  }

  return {
    skill: SKILL, level, kind, seed,
    params: { ...params, boxes },
    prompt, label, answer, accept: { simplest: false }, choices: null,
    traps: cleanTraps(answer, traps), steps,
    builder: kind === 'build'
  };
}
