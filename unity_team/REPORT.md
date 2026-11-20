# World Tour (Unity): report

## For the owner

- **The live web game in `world_tour/` shows wrong digits in about one in ten level 3 soundcheck problems.** For example, "8.82 × 10: the digits stay 441" should be 882. A patch is ready and tested on a copy (five files, all its tests pass). It is applied next, after the Unity fixes are committed; you commit `world_tour/` yourself.
- **Please listen once:** the game should make sound after "start". Headless tests can't hear it.
- **Correction:** the phase 1 report said questions 7–11 were open and the advisor was silent. Both were wrong: the answers were in QUESTIONS.md all along.

## Phase and status

Phase 2 (look and feel, map, remix, ticket) is **done** and committed locally. Next: apply answers 7–12 in Unity, patch the web game's three hint faults, then phase 3.

## What now works

- Start card, tour map with the route and venues, venue card, remix spelling mode, show bar, ticket, encore and back to the map.
- riff, the dog: idle, cheering, thinking and oops, with the web game's lines.
- Sound effects, a mute button that is remembered, reduce motion, the "progress won't save on this device" notice, and the brand fonts and art.
- Play by mouse (click or drag), or by keyboard alone (Tab, Enter, Space, typing).

## Test results

- Edit-mode 1071/1071; play-mode 7/7 (including a full remix show with a miss, a hint and the encore); browser 16/16. Compile has 0 warnings.
- Mouse, keyboard, reduced-motion and storage-blocked playthroughs at 1366x768, 1440x900 and 1920x1080 all reached the ticket with 0 console errors. 240 freeze-repro runs found 0 freezes.
- Download 12.77 MB (budget 20 MB; largest file 7.68 MB). Cold load 0.7–1.2 s on localhost.

## What the tester looked at

Every screen at three sizes, all player text, the feel rules, saving across a reload, and the code against the web game.

## Not done or not verified

- **Sound:** inferred from the scenes and logs, not heard.
- **Remix hints:** still the web wording until the next task.
- **Polish list:** win toast, confetti and crowd; low-contrast pink text; the no-save notice covers a map label; a very fast sideways drag can pick the next tile.
- **Advisor checks** paused 03:22–07:52, and a usage limit paused work 03:22–04:20.

## Departures from the brief

- 2.1–2.6 were tested in one combined pass, not one per task, to have a playable build by morning.
- Some builds ran in the background, moved by the tool.

## Open questions

None. Answers 7–12 are in QUESTIONS.md and are applied next.

## How to try it

From the repo root, run `node world_tour_unity/Tests/Web/server.mjs tour`, open the address in Chrome, and press start.

## Files changed

- `world_tour_unity/Assets/`: Art, Fonts, Game (Ui, Motion, Audio, Map, Mascot, Modes, Show, Ticket, Flow), Editor builders, scenes, tests.
- `world_tour_unity/Tools/` and `Tests/Web/`.
- `tour/` (the rebuilt game) and `unity_team/`.
