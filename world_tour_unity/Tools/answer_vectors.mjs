/* Prints test vectors from the web game's answer.js and mistakes.js (imported read-only), so the
   C# ports (Answers, Mistakes) can be checked against the real JS.

   node Tools/answer_vectors.mjs            writes Assets/Tests/EditMode/answer_vectors.json
   node Tools/answer_vectors.mjs --stdout   prints the JSON instead

   Non-ASCII characters are written as \uXXXX escapes so the file stays plain ASCII. */
import { writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { parseNumber, check, matches, show, parsePoint } from '../../world_tour/math/answer.js';
import { cleanTraps, choiceSet } from '../../world_tour/math/mistakes.js';
import { R } from '../../world_tour/math/rational.js';
import { rng } from '../../world_tour/math/rng.js';

const here = dirname(fileURLToPath(import.meta.url));
const outFile = join(here, '..', 'Assets', 'Tests', 'EditMode', 'answer_vectors.json');

/* a JS value <-> { k, a, b } */
const enc = v => v == null ? { k: 'null', a: 0, b: 0 }
  : typeof v === 'number' ? { k: 'num', a: v, b: 0 }
  : 'x' in v ? { k: 'pt', a: v.x, b: v.y } : { k: 'frac', a: v.n, b: v.d };
/* canonical text, same as C# MathValue.ToString */
const canon = v => v == null ? 'null' : typeof v === 'number' ? String(v) : 'x' in v ? `(${v.x},${v.y})` : `${v.n}/${v.d}`;
const bits = r => `${+!!r.ok}${+!!r.simplify}${+!!r.empty}${+!!r.bad}`;
const attempt = f => { try { return String(f()); } catch { return 'throws'; } };

const TARGETS = [
  { type: 'int', value: 5 }, { type: 'int', value: 1000 }, { type: 'int', value: 12 }, { type: 'int', value: 0 },
  { type: 'money', value: 350 }, { type: 'money', value: 5 }, { type: 'money', value: 1205 }, { type: 'money', value: 150 },
  { type: 'decimal', value: R(1, 2) }, { type: 'decimal', value: R(13, 20) }, { type: 'decimal', value: R(3, 2) },
  { type: 'fraction', value: R(3, 2) }, { type: 'fraction', value: R(1, 2) }, { type: 'fraction', value: R(11, 4) },
  { type: 'fraction', value: R(3, 4) }, { type: 'fraction', value: R(5, 4) }, { type: 'point', value: { x: 3, y: 5 } }
];

const TEXTS = [
  '3/4', '6/8', ' 1 1/2 ', '1 1/2', '1.50', '1.5', '$1.5', '$1.50', '$ 3.50', '3.50$', '$$3.50', '1,000', '1,0,0,0', '3,50', '1,5',
  '-2', '+5', '.5', '0.50', '.50', '0.5', '5.', '.', '3 /4', '3/ 4', '3 / 4', 'abc', '', '   ', '1/0', '0/0', '1 1/0', '1/', '/2',
  '2 3/4', '11/4', '22/8', '2 6/8', '1 5/4', '1 2/8', '1 1/4', '5/4', '10/8', '2/4', '1/2', '4/2', '5/1', '0/5', '0/1', '0',
  '00', '007', '5', '12', '12.0', '12.', '1000', '1000.00', '3.5', '3.05', '35', '350', '12.05', '$12.05', '0.65', '.65', '13/20',
  '1.2.3', '1e3', 'Infinity', '0x10', '1  1/2', '1\t1/2', '\u00a03/4', '\ufeff5', '\u00855', '\uff15', '\u0665', '5\n',
  '(3, 5)', '3,5', '(3,5', '3, 5)', ' ( 3 , 5 ) ', '(5, 3)', '(3 5)', '(-3, 5)', '(3.0, 5)', '(03, 05)', '1 1/2/3', '9/12', '1.25'
];

const texts = TEXTS.map(text => {
  const p = parseNumber(text);
  return {
    text,
    parse: p ? `${p.v.n}/${p.v.d} ${p.simplest} ${p.form}` : 'null',
    checks: TARGETS.flatMap(t => [bits(check(text, t)), bits(check(text, t, { simplest: true }))]),
    matches: TARGETS.map(t => matches(text, t.value, t.type))
  };
});

const SHOWN = [
  { type: 'money', value: 350 }, { type: 'money', value: 5 }, { type: 'money', value: 0 }, { type: 'money', value: 1205 }, { type: 'money', value: 999999 },
  { type: 'int', value: 0 }, { type: 'int', value: 7 }, { type: 'int', value: 999 }, { type: 'int', value: 1000 }, { type: 'int', value: 1234567 }, { type: 'int', value: 1000000000 },
  { type: 'decimal', value: R(13, 20) }, { type: 'decimal', value: R(1, 2) }, { type: 'decimal', value: R(450) }, { type: 'decimal', value: R(47, 10) }, { type: 'decimal', value: { n: 2, d: 4 } },
  { type: 'fraction', value: R(3, 2) }, { type: 'fraction', value: R(1, 4) }, { type: 'fraction', value: R(2) }, { type: 'fraction', value: { n: 6, d: 8 } },
  { type: 'point', value: { x: 3, y: 5 } }, { type: 'point', value: { x: 0, y: 10 } }
].map(a => ({ type: a.type, v: enc(a.value), out: attempt(() => show(a)) }));

const points = ['(3, 5)', '3,5', '(3,5', '3, 5)', ' ( 3 , 5 ) ', '(3 5)', '(-3, 5)', '', 'abc', '(10,0)', '\u00a0(1,2)\u00a0', '(1,2)\n', '((1,2))', '(1,,2)']
  .map(text => ({ text, out: canon(parsePoint(text)) }));

const T = (value, why) => ({ value, why });
const TRAP_SETS = [
  [{ type: 'money', value: 350 }, [T(325, 'no-carry'), T(350, 'same'), T(-50, 'neg'), T(3.5, 'decimal'), T(325, 'dup'), T(null, 'null'), T(0, 'zero'), T({ n: 7, d: 2 }, 'obj'), T(NaN, 'nan')]],
  [{ type: 'int', value: 12 }, [T(21, 'swap'), T(12, 'same'), T(1.5, 'half'), T(120, 'zero'), T(21, 'dup'), T(-12, 'neg')]],
  [{ type: 'fraction', value: R(1, 2) }, [T({ n: 2, d: 4 }, 'unreduced-same'), T({ n: 2, d: 6 }, 'unreduced'), T({ n: 1, d: 3 }, 'dup-value'), T({ n: 1, d: 0 }, 'zero-d'), T({ n: -1, d: 2 }, 'neg'), T({ n: 1, d: -2 }, 'neg-d'), T({ n: 1.5, d: 3 }, 'non-int'), T(1, 'number'), T({ n: 0, d: 5 }, 'zero'), T({ x: 1, y: 2 }, 'point')]],
  [{ type: 'decimal', value: R(47, 10) }, [T(R(47, 100), 'zeros-off'), T(R(47, 1000), 'zeros-off'), T(R(470, 100), 'same'), T(R(47, 1), 'zeros-off'), T({ n: 94, d: 200 }, 'dup')]],
  [{ type: 'point', value: { x: 3, y: 5 } }, [T({ x: 5, y: 3 }, 'swapped'), T({ x: 3, y: 5 }, 'same'), T({ x: -1, y: 5 }, 'neg'), T({ x: 5, y: 3 }, 'dup'), T({ x: 0, y: 0 }, 'origin'), T(null, 'null'), T({ x: 3, y: -5 }, 'neg-y')]],
  [{ type: 'int', value: 0 }, [T(0, 'same'), T(1, 'one'), T(10, 'ten')]]
];
const traps = TRAP_SETS.map(([answer, list]) => ({
  answer: { type: answer.type, v: enc(answer.value) },
  traps: list.filter(t => !Number.isNaN(t.value)).map(t => ({ v: enc(t.value), why: t.why })),
  out: attempt(() => cleanTraps(answer, list.filter(t => !Number.isNaN(t.value))).map(t => `${canon(t.value)}:${t.why}`).join('|'))
}));

const CHOICE_SETS = [
  [{ type: 'decimal', value: R(47, 10) }, [T(R(47, 100), 'zeros-off'), T(R(47, 1000), 'zeros-off'), T(R(47, 1), 'multiplied-exponent')], [R(47, 100)], 4],
  [{ type: 'int', value: 56 }, [T(54, 'near-fact'), T(63, 'near-fact')], [48, 64, 54, 56, 49], 4],
  [{ type: 'int', value: 1000 }, [T(100, 'zeros-off'), T(10000, 'zeros-off'), T(30, 'multiplied-exponent')], [], 4],
  [{ type: 'int', value: 9 }, [], [8, 10, 18, 9], 4],
  [{ type: 'int', value: 9 }, [T(8, 'a'), T(10, 'b'), T(11, 'c'), T(12, 'd')], [7], 3],
  [{ type: 'money', value: 125 }, [T(-5, 'neg'), T(115, 'x')], [135, 125, 105], 4]
];
const choices = [];
for (const [answer, list, fill, n] of CHOICE_SETS) for (const seed of [1, 42, 7919, 123456789]) {
  choices.push({
    seed, n, answer: { type: answer.type, v: enc(answer.value) }, traps: list.map(t => ({ v: enc(t.value), why: t.why })), fill: fill.map(enc),
    out: attempt(() => choiceSet(rng(seed), answer, list, fill, n).map(c => `${canon(c.value)}:${c.correct}:${c.why}`).join('|'))
  });
}

const out = { generatedBy: 'world_tour_unity/Tools/answer_vectors.mjs', targets: TARGETS.map(t => ({ type: t.type, v: enc(t.value) })), texts, shown: SHOWN, points, traps, choices };
const json = JSON.stringify(out, null, 1).replace(/[\u007f-\uffff]/g, c => '\\u' + c.charCodeAt(0).toString(16).padStart(4, '0')) + '\n';
if (process.argv.includes('--stdout')) process.stdout.write(json);
else { writeFileSync(outFile, json); console.log(`wrote ${outFile}: ${texts.length} texts x ${TARGETS.length} targets, ${SHOWN.length} shown, ${points.length} points, ${traps.length} trap sets, ${choices.length} choice sets`); }
