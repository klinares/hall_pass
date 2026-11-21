# World Tour (Unity): final report

## For the owner: what you do next

1. **Listen once** for the game's sound and the read-aloud voice.
2. **Play it with your daughter** (see How to try it).
3. **Commit `world_tour/`.** Six files changed: js/show.js, math/gen/fluency.js, math/gen/multdiv.js, math/gen/volume.js, data/weeks.js and tests/math/generators.test.mjs. Math tests pass 41/41.
4. **Push to GitHub.** Everything else is committed locally (last commit 60e9548).

## Phase and status

Phase 6 is **done**, and with it the whole build. The irrational and illogical clue rewording is done (1589654, plus the advisor's edit to `world_tour/data/weeks.js`). The polish pass is closed per the advisor's 02:18 note. The final build is in `tour/`.

## What now works

All twelve modes, the map, shows, tickets, backstage shop, journal, manager's office, both themes, and save export and import.

The polish pass made text readable at 1366x768, removed overlaps, added the win toast, confetti, show burst and a reacting crowd (all off with reduce motion), fixed apostrophes, "the zeros" and Tab order, stopped hints repeating a letter, and added peach art.

`world_tour_unity/README.md` covers adding a week of words (no rebuild needed) and rebuilding.

## Test results

- Edit-mode 1678/1678, play-mode 23/23, browser 18/18. 0 console errors in the final sweep.
- Download 13.26 MB (budget 20 MB), largest file 7.9 MB. Cold load 1.34 s.

## What the tester looked at

Every screen at 1366 and 1920, in pink and peach, over three passes. The last 6.2F fixes were checked from the builder's own screenshots, per the advisor's limit of one more tester pass.

## Not done or not verified

**Polish later (also in TODO.md):**
- In the "record store" sign, the disc covers the word. This is in the art itself and needs new art.
- Peach still has some pink art: the awning, the vinyl label and the map stickers.
- Long letter patterns shrink small in the dog's bubble.
- Faint beat slot numbers.
- A tiny fraction-key icon.
- Peach "rehearse again" is only just readable.
- A short peach lyric card touches its bottom edge.

**Not verified:**
- sound by ear;
- the fast sideways drag and the effect timings (covered by code and tests only);
- a full save hitting the browser's storage quota.

## Departures from the brief

- The polish pass ran six fix rounds; the advisor lifted the two-round cap for it.
- The workers ran on Sonnet from 21:00, per the advisor's note; builders before that ran on Opus. No task needed the fallback to my model.
- Departures are listed in IntendedDifferences.md. The answer-14 mask stays until the math vectors are regenerated.

## Open questions

None. Questions 1–23 are answered in QUESTIONS.md.

## How to try it

From the repo root, run `node world_tour_unity/Tests/Web/server.mjs tour`, open the address in Chrome, and press start.

## Files changed

`world_tour_unity/Assets/` (Game, Core text rules, WebGL template, peach dog art, tests), `world_tour_unity/README.md`, `tour/`, `unity_team/`.
