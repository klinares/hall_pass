# World Tour (Unity): report

## For the owner

- **Don't commit `world_tour/` yet.** It holds this morning's patch: js/show.js, math/gen/fluency.js, multdiv.js, volume.js, tests/math/generators.test.mjs. Answer 14 withdrew one of its lines ("the digits don't change"), which is false for 140 ÷ 10 and 8.8 × 100. Your 08:03 approval didn't cover this fix, so I haven't touched it. **Should I apply it?** If not, I'll give you the text.
- **The web game still has five hint pictures that give the answer away at hint 2.** They're fixed in Unity only (QUESTIONS 16 and 17):
  - ticket sales: the one-part area model and the seat rows;
  - pack the truck: level 3 missing height;
  - beat maker: typed sums and differences;
  - stage designer: points where up is 0.

  Tour budget's level 3 highlighter never appears there either.
- **Please listen once** for sound and the studio session voice.
- **Word list:** irrational and illogical fit each other's clues.

## Phase and status

Phase 4 (seven math modes) is **done** and committed locally. Next: phase 5 (backstage, journal, manager's office, second theme, export and import).

## What now works

- All seven math modes play through to the ticket and encore: merch table, beat maker, stage designer, pack the truck, ticket sales, tour budget, soundcheck.
- Fractions are drawn stacked: in questions, riff's lines, the keypad and the ticket.
- Hints go nudge, partial step, worked solution with a picture. No picture at hints 1–2 shows the answer (answer 16).
- Empty or unfinished presses never count as a miss (answers 7 and 15).
- Soundcheck's timer only affects bonus fans.

## Test results

- Edit-mode 1531/1531, play-mode 21/21, browser 16/16, 0 compile warnings.
- 28 playthroughs (every math mode, mouse and keyboard, 1366x768 and 1920x1080), then 7 more and remix after the fixes: 0 console errors.
- Download 12.95 MB (budget 20 MB); cold load 1.2 s locally.

## What the tester looked at

Every hint picture in every mode and level on screen; every new screen at both sizes; the learning rules against the web code.

## Not done or not verified

- Polish list:
  - beat maker labels clipped by their row;
  - the tour budget worked sheet covers the song counter;
  - light grey ticket notes and tiny seat icons.
- Reduce motion in math checked in code only; headless graphics only; sound not heard.

## Departures from the brief

- The first tester pass failed on three hint pictures. Two fix rounds, four builds in all.
- The click-to-pick rule isn't used in beat maker, where a note has only one place to go.
- 34 intended differences are listed in IntendedDifferences.md.

## Open questions

- Whether to apply answer 14 to `world_tour/` (above).

## How to try it

From the repo root, run `node world_tour_unity/Tests/Web/server.mjs tour`, open the address in Chrome, and press start.

## Files changed

- `world_tour_unity/Assets/`: Game/Modes/Math (new), Show, Map, Ticket, tests and vectors.
- `world_tour_unity/Tools/math/`, `tour/` (rebuilt), `unity_team/`.
