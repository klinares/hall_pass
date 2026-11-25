# World Tour (Unity): phase 8 report

## For the owner: what you do next

1. **Let her play `world/`** on her laptop.
2. **Listen once** for sound (birds, steps, applause, cheer) and voice.
3. **Push** to publish `tour/` and `world/`. `world_tour/` is already committed.

## Phase and status

Phase 8 and the 8.5 clean-up are **done**, committed locally as separate commits. Nothing is pushed.

## What now works

- Eight 3D places: walk, talk to the crew, play gigs and the show, travel.
- One save with `tour/`, carried both ways; a reload resumes where she was.
- Shots: `unity_team/shots/phase8/` (8 places at 1366 and 1920, coffee gig and show).

## Glitch list (notes 21:45, 21:55)

| glitch | status |
|---|---|
| "browser does not support WebGL" | **fixed**: reload page on a lost context, save flushed first, dedicated GPU requested, memory flat across 8 places |
| "bricks kept reloading or looked stuck" | **fixed**: camera bob at rest, one-frame wall pops, ground mip shimmer, coplanar ground; the fade script had not been binding |
| reload lands in coffee | **fixed** |
| stage-to-exit walk at rec, theater | **fixed** (harness fault; reachability tested everywhere) |
| trunks: spawn inside, hiding crew | **fixed** |
| crew too close at rec; bubbles cover her | **fixed** |
| small snack labels; tiny Star; rooftop orbs | **fixed** |
| clipped club sign, exit buttons, ticket page | **fixed**; "THE" still touches a lamp |
| snack "n in all" bar blank | **fixed in `world/`**; `tour/` still has it |

Every place can be finished by keyboard alone or mouse alone.

## Test results

Edit-mode 1881/1882 (1 explicit skip), play-mode 29/29, `tour/` browser 18/18, all `world/` suites pass (smoke, places, GPU, stability). 60 fps everywhere; longest frame 33 to 67 ms. Build 16.8 MB (wasm 8.4 MB largest), cold load 1.7 s.

## What the tester looked at

One whole-tour GPU pass (8 places, both sizes, kid-fit on two), one fix round, a final-build check.

## Clean-up (8.5)

Removed previous builds (shipped builds unchanged), 74 unused art pieces, 11 unused packages, probe scripts, a duplicate agents folder; finished phases moved to `HISTORY.md`. Tracked files: 200 to 180 MB (history unchanged). Both builds build; suites green.

Could be removed, the advisor decides: `spelling_4_U/` (0.07 MB, nothing uses it); `world_tour/` (0.4 MB; test tools and Playwright depend on it); `tour/` and 2D-only scenes and art (25 MB and 3 MB; the live 2D game); the backup `D:\repos\_backups\world_tour_2026-11-20\` (121 MB, unused); `publish.bat` (the owner's). `KICKOFF.md` stays: BRIEF reads it.

## Not done or not verified

- Kid-fit used a scripted first ten (8/10), not a first five minutes by eye.
- Reload resume checked at three places, not all eight.
- Level-up at 6 in a row (phase 7) reaches mixed numbers in about three gigs.

## Departures from the brief

Workers on Sonnet; no paid or headless use. Over the build cap in 8.1, 8.3 and 8.4.

## Open questions

None.

## How to try it

From the repo root, run `node world_tour_unity/Tests/Web/server.mjs world` and open the address in Chrome.
