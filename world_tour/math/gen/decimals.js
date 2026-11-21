/* MERCH TABLE: adding, subtracting and multiplying decimals with money (5.NBT.7).
   L1: 2 items under $10, the total or the change from $10
   L2: 3 items, one bought as quantity × price, the total or the change from $20
   L3: decimal × decimal (2.5 ounces at $1.20 an ounce), alone or with change from $50 */
import { dollars, noCarryAdd, noBorrowSub } from '../money.js';
import { cleanTraps } from '../mistakes.js';
import { guarded } from './guard.js';

export const SKILL = 'dec';

export const ITEMS = [
  { id: 'tee', name: 'band tee', a: 'a', pl: 'band tees', icon: 'tee', lo: 900, hi: 1500 },
  { id: 'pin', name: 'enamel pin', a: 'an', pl: 'enamel pins', icon: 'star', lo: 150, hi: 450 },
  { id: 'stickers', name: 'sticker sheet', a: 'a', pl: 'sticker sheets', icon: 'heart', lo: 105, hi: 350 },
  { id: 'picks', name: 'pick pack', a: 'a', pl: 'pick packs', icon: 'pick', lo: 150, hi: 400 },
  { id: 'poster', name: 'tour poster', a: 'a', pl: 'tour posters', icon: 'setlist', lo: 300, hi: 650 },
  { id: 'band', name: 'glow wristband', a: 'a', pl: 'glow wristbands', icon: 'bolt', lo: 105, hi: 300 },
  { id: 'tape', name: 'cassette', a: 'a', pl: 'cassettes', icon: 'cassette', lo: 350, hi: 650 },
  { id: 'vinyl', name: 'mini vinyl', a: 'a', pl: 'mini vinyls', icon: 'vinyl', lo: 450, hi: 1200 },
  { id: 'phones', name: 'pair of earplugs', a: 'a', pl: 'pairs of earplugs', icon: 'phones', lo: 105, hi: 250 }
];
/* sold by weight or length, for decimal × decimal */
const BULK = [
  { id: 'glitter', name: 'face glitter', unit: 'ounce', an: 'an', icon: 'star' },
  { id: 'ribbon', name: 'merch ribbon', unit: 'yard', an: 'a', icon: 'heart' },
  { id: 'candy', name: 'tour-bus candy', unit: 'pound', an: 'a', icon: 'coffee' }
];

const price = (r, lo, hi, step) => lo + step * r.int(0, Math.floor((hi - lo) / step));
/* items that can be priced inside [lo, hi], and a price for one of them inside both ranges */
const fits = (lo, hi) => it => Math.max(lo, it.lo) <= Math.min(hi, it.hi);
const priceFor = (r, it, lo, hi, step) => price(r, Math.ceil(Math.max(lo, it.lo) / step) * step, Math.min(hi, it.hi), step);

export const generate = guarded(build);

function build(level, seed, r) {
  let kind, lines, paid = 0, total, answer, traps = [], steps, prompt, label;

  if (level === 1) {
    const pool = r.shuffle(ITEMS.filter(fits(105, 475)));
    lines = [0, 1].map(i => ({ ...pool[i], qty: 1, each: priceFor(r, pool[i], 105, 475, 5) }));
    total = lines[0].each + lines[1].each;
    kind = r.chance(.5) ? 'total' : 'change';
  } else if (level === 2) {
    for (;;) {
      const multi = r.pick(ITEMS.filter(fits(110, 395)));
      const rest = r.shuffle(ITEMS.filter(it => it !== multi && fits(105, 650)(it)));
      lines = [
        { ...multi, qty: r.int(2, 4), each: priceFor(r, multi, 110, 395, 5) },
        { ...rest[0], qty: 1, each: priceFor(r, rest[0], 105, 650, 1) },
        { ...rest[1], qty: 1, each: priceFor(r, rest[1], 105, 650, 1) }
      ];
      total = lines.reduce((s, l) => s + l.qty * l.each, 0);
      if (total < 2000) break;
    }
    kind = r.chance(.5) ? 'total' : 'change';
  } else {
    const b = r.pick(BULK);
    let tenths;
    do tenths = r.int(11, 49); while (tenths % 10 === 0);
    const per = price(r, 110, 490, 10);                 /* a whole number of dimes, so the cost lands on whole cents */
    const bulk = { ...b, amount: tenths, each: per, cost: tenths * per / 10 };
    lines = [bulk];
    kind = r.chance(.5) ? 'decmul' : 'change';
    if (kind === 'change') { const it = r.pick(ITEMS.filter(fits(250, 1500))); lines.push({ ...it, qty: 1, each: priceFor(r, it, 250, 1500, 5) }); }
    total = bulk.cost + (lines[1] ? lines[1].each : 0);
  }

  const lineCost = l => l.cost ?? l.qty * l.each;
  const costs = lines.map(lineCost);

  if (kind === 'change') {
    paid = level === 1 ? 1000 : level === 2 ? 2000 : 5000;
    answer = { type: 'money', value: paid - total };
    traps = [
      { value: total, why: 'gave-total' },
      { value: noBorrowSub(paid, total), why: 'no-borrow' },
      { value: paid - noCarryAdd(costs), why: 'no-carry' }
    ];
  } else if (kind === 'decmul') {
    answer = { type: 'money', value: total };
    traps = [
      { value: total * 10, why: 'decimal-place' },
      { value: total % 10 === 0 ? total / 10 : null, why: 'decimal-place' },
      { value: lines[0].amount * 10 + lines[0].each, why: 'added-instead' }
    ];
  } else {
    answer = { type: 'money', value: total };
    traps = [{ value: noCarryAdd(costs), why: 'no-carry' }];
  }

  /* the words on the order slip */
  const lineText = l => l.unit
    ? `${(l.amount / 10).toFixed(1)} ${l.unit}s of ${l.name} at ${dollars(l.each)} ${l.an} ${l.unit}`
    : l.qty > 1 ? `${l.qty} ${l.pl} at ${dollars(l.each)} each` : `${l.a} ${l.name} for ${dollars(l.each)}`;

  if (kind === 'change') {
    prompt = `a fan buys ${lines.map(lineText).join(', ').replace(/, ([^,]*)$/, ' and $1')}. they pay with ${dollars(paid)}. how much change?`;
    label = `change from ${dollars(paid)}`;
  } else if (kind === 'decmul') {
    prompt = `a fan wants ${lineText(lines[0])}. what does it cost?`;
    label = `${(lines[0].amount / 10).toFixed(1)} × ${dollars(lines[0].each)}`;
  } else {
    prompt = `a fan buys ${lines.map(lineText).join(', ').replace(/, ([^,]*)$/, ' and $1')}. what’s the total?`;
    label = `total of ${lines.length} items`;
  }

  /* hint ladder: nudge → partial step → worked solution */
  const qtyLine = lines.find(l => l.qty > 1);
  const bulk = lines.find(l => l.unit);
  const partials = [];
  if (qtyLine) partials.push(`${qtyLine.qty} × ${dollars(qtyLine.each)} = <b>${dollars(qtyLine.qty * qtyLine.each)}</b>`);
  if (bulk) partials.push(`${bulk.amount} × ${bulk.each} = ${bulk.amount * bulk.each}. ${(bulk.amount / 10).toFixed(1)} has 1 decimal place and ${dollars(bulk.each).slice(1)} has 2, so put back 3: <b>${dollars(bulk.cost)}</b>`);
  const totalLine = `${costs.map(dollars).join(' + ')} = <b>${dollars(total)}</b>`;

  if (kind === 'change') {
    steps = [
      `change is what they paid minus what it costs. find the total first${qtyLine ? ', starting with the ' + qtyLine.pl : bulk ? ', starting with the ' + bulk.name : ''}.`,
      partials.length ? partials.join('. ') + (costs.length > 1 ? `. total: ${totalLine}.` : '.') : `the total is ${totalLine}. now take that away from ${dollars(paid)}.`,
      `${dollars(paid)} − ${dollars(total)} = <b>${dollars(paid - total)}</b>`
    ];
  } else if (kind === 'decmul') {
    steps = [
      `multiply like whole numbers first: ${bulk.amount} × ${bulk.each}. then put the decimal point back.`,
      `${bulk.amount} × ${bulk.each} = ${bulk.amount * bulk.each}. count the decimal places in ${(bulk.amount / 10).toFixed(1)} and ${dollars(bulk.each).slice(1)}.`,
      partials[0]
    ];
  } else {
    steps = [
      qtyLine ? `start with the ${qtyLine.qty} ${qtyLine.pl}: that’s ${qtyLine.qty} × ${dollars(qtyLine.each)}.` : `line up the decimal points and add the cents first.`,
      qtyLine ? `${partials[0]}. now add the other items.` : (c => c >= 100 ? `the cents make ${c}¢. that’s ${Math.floor(c / 100)} extra dollar${c >= 200 ? 's' : ''}${c % 100 ? ' and ' + (c % 100) + '¢' : ''}.` : `the cents make ${c}¢, less than a dollar, so nothing carries. now the dollars.`)(costs.reduce((s, c) => s + c % 100, 0)),
      totalLine
    ];
  }

  return {
    skill: SKILL, level, kind, seed,
    params: { lines: lines.map(l => ({ id: l.id, name: l.name, icon: l.icon, qty: l.qty || 1, each: l.each, amount: l.amount ?? null, unit: l.unit ?? null, cost: lineCost(l) })), paid, total },
    prompt, label, answer,
    accept: { simplest: false },
    choices: null,
    traps: cleanTraps(answer, traps),
    steps,
    /* the column sum the worked hint draws */
    work: kind === 'change' ? { op: '−', rows: [paid, total], result: paid - total } : kind === 'decmul' ? null : { op: '+', rows: costs, result: total }
  };
}
