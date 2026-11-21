/* Builds CONTENT_REVIEW.md: every spelling sentence and clue, and one example of every math
   word problem at every level, straight from data/weeks.js and the generators, so the sheet
   always shows what the game shows.

   usage (from the repo root, Node 20+):
     node world_tour/tools/content_review.mjs            writes world_tour/CONTENT_REVIEW.md
     node world_tour/tools/content_review.mjs --week=2026-11-23   only that week's words, no math
     node world_tour/tools/content_review.mjs --out=some/file.md

   The sheet is for reading, not for the site. Delete it, or leave it out of commits. */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { WEEKS } from '../data/weeks.js';
import { generate } from '../math/gen/index.js';
import { ITEMS } from '../math/gen/decimals.js';
import { show } from '../math/answer.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const arg = k => (process.argv.find(a => a.startsWith(`--${k}=`)) || '').split('=')[1];
const weekId = arg('week');
const outFile = path.resolve(arg('out') || path.join(here, '..', 'CONTENT_REVIEW.md'));

const strip = s => String(s).replace(/<[^>]+>/g, '')
  .replace(/\[\[(\d+) (\d+)\/(\d+)\]\]/g, '$1 $2/$3').replace(/\[\[(\d+)\/(\d+)\]\]/g, '$1/$2').replace(/\[\[(\d+)\]\]/g, '$1');
const cell = s => String(s).replace(/\|/g, '\\|');
const dollars = c => '$' + (c / 100).toFixed(2);
const NAMES = {
  dec: 'merch table (decimals)', md: 'ticket sales (multiply/divide)', frac: 'beat maker (fractions)',
  vol: 'pack the truck (volume)', ops: 'tour budget (order of operations)', xy: 'stage designer (coordinates)',
  flu: 'soundcheck (facts, powers of ten)'
};

const weeks = weekId ? WEEKS.filter(w => w.id === weekId) : WEEKS;
if (!weeks.length) { console.error(`no week with id ${weekId}. ids: ${WEEKS.map(w => w.id).join(', ')}`); process.exit(1); }

let out = `# World Tour: content review

Everything Natalia reads in the spelling sentences${weekId ? '' : ' and math word problems'}, in one place, built from the game's own data by \`tools/content_review.mjs\`. Mark anything to change. This sheet is for review only; delete it when done.

## Spelling: lyric writer sentences and fan mail clues

The sentence is lyric writer (pick or type the word for the blank); the clue is fan mail and the hints.

`;

const flags = [];
for (const wk of weeks) {
  out += `### ${wk.label}\n\n| word | lyric writer sentence | clue | prefix |\n|---|---|---|---|\n`;
  for (const w of wk.words) {
    out += `| **${w.w}** | ${cell(w.x ?? '(none)')} | ${cell(w.h)} | ${w.prefix ? `${w.prefix.p} + ${w.prefix.root} (${w.prefix.means})` : ''} |\n`;
    /* mechanical checks; whether a sentence fits only its own word still needs a person */
    if (w.x && (w.x.match(/___/g) || []).length !== 1) flags.push(`${w.w}: the sentence needs exactly one ___`);
    if (w.x && w.x.toLowerCase().includes(w.w.toLowerCase())) flags.push(`${w.w}: the sentence gives the word away`);
    if (w.h && w.h.toLowerCase().includes(w.w.toLowerCase())) flags.push(`${w.w}: the clue gives the word away`);
    if (w.s && w.s.replace(/-/g, '') !== w.w) flags.push(`${w.w}: syllables "${w.s}" don't spell the word`);
  }
  const withX = wk.words.filter(w => w.x).length, withP = wk.words.filter(w => w.prefix).length;
  if (withX < 4) flags.push(`${wk.label}: only ${withX} sentences, so lyric writer is hidden that week (it needs 4)`);
  if (withP < 4) flags.push(`${wk.label}: only ${withP} prefixes, so prefix mixer is hidden that week (it needs 4)`);
  out += '\n';
}
out += `**Check by hand:** each sentence should fit only its own word, not another word from the same week.\n\n`;
out += flags.length ? `**Found automatically:**\n${flags.map(f => `- ${f}`).join('\n')}\n\n` : `**Found automatically:** nothing (one blank per sentence, no word given away, syllables match, enough sentences and prefixes).\n\n`;

if (!weekId) {
  out += `## Math word problems

One example of each kind of problem at each level, with the answer and the three hint steps (the bulb gives steps 1 and 2; step 3 comes after a third miss). The numbers change every time; the wording doesn't.

`;
  for (const sk of Object.keys(NAMES)) {
    out += `### ${NAMES[sk]}\n\n`;
    for (const lv of [1, 2, 3]) {
      const seen = new Map();
      for (let seed = 1; seed < 3000; seed++) {
        const p = generate(sk, lv, seed);
        const shape = p.kind + '|' + strip(p.prompt).replace(/[\d.,$]+/g, '#').replace(/(a|an) [a-z -]+ for #/g, 'ITEM').slice(0, 30);
        if (!seen.has(shape)) seen.set(shape, p);
      }
      const byKind = {};
      for (const p of seen.values()) (byKind[p.kind] ||= []).push(p);
      out += `**level ${lv}**\n\n`;
      for (const ps of Object.values(byKind)) for (const p of ps.slice(0, sk === 'ops' && lv === 3 ? 6 : sk === 'md' ? 3 : 1)) {
        const ch = p.choices && sk === 'ops' ? ` (choices: ${p.choices.map(c => c.value).join(' · ')})` : '';
        out += `- ${strip(p.prompt)} → **${strip(show(p.answer))}**${ch}\n  - hints: ${p.steps.map(strip).join(' / ')}\n`;
      }
      out += '\n';
    }
  }
  out += `Merch prices by item: ${ITEMS.map(i => `${i.name} ${dollars(i.lo)}–${dollars(i.hi)}`).join(', ')}.\n`;
}

fs.writeFileSync(outFile, out);
console.log(`wrote ${outFile} (${out.split(/\s+/).length} words${flags.length ? `, ${flags.length} flagged` : ''})`);
