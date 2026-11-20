# World Tour (Unity): report

## Phase and status

Phase 1 (core logic) is **done** and committed locally. Phase 2 (look and feel, remix mode) is being placed next; most of it is already written in staging.

## What now works

- All game rules are in plain C#, matching the real JavaScript: all seven math generators (105,000 problems identical byte for byte), answer checking, shows (misses, encore, fans, levels), progress, review, shop, stickers and the parent office.
- `tour/StreamingAssets/weeks.json` is the master word list (22 words); a broken week is skipped. `node world_tour_unity/Tools/validate_data.mjs` checks an edited week before you push.
- The save survives a reload. An unreadable save is backed up, not lost. With storage blocked, the game still runs. Export and import are by code or file.
- Dates use the browser's local date.

## Test results

- Edit-mode: 945 run, 945 passed. Play-mode: 6 run, 6 passed.
- Browser, real build, plain static server: 14 run, 14 passed. They cover words loading, an edited week appearing on reload, a broken week skipped, the time zone, save and reload, an unreadable save, storage blocked, and the size budget.
- Compile: 0 errors, 0 warnings.
- Download 11.88 MB (largest file 7.41 MB). Cold load 0.6–1.2 s on localhost; repeat visit 59 KB.

## What the tester looked at

Every task, against the JavaScript, with inputs the builders didn't pick: rule reviews, fuzzing of the JSON reader and export codes, 33,000 random play sequences (nothing earned ever lost), every shop and sticker text (no real names), and screenshots at both sizes. No new screens this phase.

## Not done or not verified

- Screens: still only the title; phase 2 adds them.
- The advisor has been silent since 00:43; I followed the standing notes. A usage limit paused work from 03:22 to 04:20.
- Some builds ran in the background, against the foreground rule (moved by the tool, and once by a builder). All finished; summary files decide.
- Unity still sometimes crashes at shutdown after finishing (D1); results files decide.

## Departures from the brief

None. Where the web game breaks a learning rule, the port stays exact until answered (questions below).

## Open questions (none block)

- 7: typed-answer quirks. Fix messages only.
- 8, 9, 11: hints that give the answer away, and one with wrong digits ("8.82 × 10: the digits stay 441"). Fix.
- 10: misses beyond two return only in the encore; the "clean run" sticker. Keep the cap, document it.

## How to try it

From the repo root, run `node world_tour_unity/Tests/Web/server.mjs tour` and open the address in Chrome. It shows the title only for now.

## Files changed

- `world_tour_unity/Assets/`: Core/Math, Gen, Show, Progress, Meta and Data; Game/Boot, Data, Save and Util; Plugins/WebGL; StreamingAssets; tests.
- `world_tour_unity/Tools/`: JS cross-check scripts and the validator.
- `tour/`: the rebuilt build, plus StreamingAssets.
- `unity_team/`: decisions, questions, to-do list and this report.
