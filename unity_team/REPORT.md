# World Tour (Unity): phase 8 report

## For the owner: what you do next

1. **Let her play `world/`** on her laptop.
2. **Listen once** for sound (birds, steps, applause, cheer) and voice.
3. **Push** to publish `tour/` and `world/`. `world_tour/` is already committed.

## Phase and status

Phase 8 is **done** and committed locally. Nothing is pushed. 8.5 (repo clean-up) follows as its own commit.

## What now works

- Eight 3D places in tour order. In each she walks by keys or clicks, talks to the crew, plays gigs and the show, and travels.
- Wearables in a wardrobe trunk, crowds that grow with fans, stage lights, synthesized ambience plus five CC0 clips.
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

Every place can be finished by keyboard alone and by mouse alone, including edge walks, click spam and leaving mid-show.

## Test results

Edit-mode 1881/1882 (1 explicit skip), play-mode 29/29, `tour/` browser 18/18, all `world/` suites pass (smoke, places, GPU, stability). 60 fps everywhere; longest frame 33 to 67 ms. Build 16.8 MB (wasm 8.4 MB largest), cold load 1.7 s.

## What the tester looked at

One whole-tour pass on a real GPU (8 places, both sizes, glitch hunt, kid-fit on two places), one fix round, then a final-build check.

## Not done or not verified

- Kid-fit used a scripted first ten (8/10), not a first five minutes by eye.
- Reload resume checked at three places, not all eight.
- Level-up at 6 in a row (phase 7) reaches mixed numbers in about three gigs.
- Harmless shader log lines remain.

## Departures from the brief

Workers on Sonnet; no paid or headless use. Over the build cap in 8.1, 8.3 and 8.4. A final-build check ran after the one tester pass.

## Open questions

None.

## How to try it

From the repo root, run `node world_tour_unity/Tests/Web/server.mjs world` and open the address in Chrome.
