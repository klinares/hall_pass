# Advisor notes

The advisor reads `REPORT.md`, `QUESTIONS.md`, `DECISIONS.md` and the plan about every 30 minutes and writes here. Lead: re-read this file and `QUESTIONS.md` before starting each task and before you stop for any reason. Notes are instructions unless marked "FYI". Newest first. Answers to numbered questions go under the question in `QUESTIONS.md`, not here.

## 2026-11-25 22:50 (advisor) PHASE 9: the repo keeps only lunchline and the 3D game

The owner: "In the repo lets keep lunchin, chippy, and only the 3d world tour game." So the 2D game and the first web game leave the repo. Billing rule stands. Lead plus one builder and one tester pass; do it as a small number of separate commits so each can be undone. Do not push.

HOLD: do not touch `spelling_4_U/` yet. No folder is called "chippy"; I have asked the owner what he means. Never touch `lunchline/`, `images/`, `.Rhistory`, or anything you did not create, beyond the README edit below.

1. Make `world/` stand alone first (commit 1). `world/StreamingAssets/weeks.json` and `math_config.json` become the master copies (today BuildWorld syncs them from `tour/StreamingAssets/`): BuildWorld must read and keep the deployed `world/StreamingAssets/` copies the way BuildWeb did for `tour/`, never overwrite them, and sync them back into the project. Validator, README and MATH_CONFIG.md point at `world/`. The save keeps its key and format so her progress stays.
2. Remove the 2D game (commit 2): `tour/`, the 2D scenes, 2D-only screens, art, atlases and builders, `BuildWeb.Build`, the `WorldTour` page template, the `tour/` browser suites and the 2D play-mode tests. Keep everything `world/` uses: Core, the mode panels and hint ladder shown in the docked panel, fonts, sprites those panels draw, jslib plugins, save, speech. If a 2D file is shared, keep it. Unused-asset check after, as in 8.5.
3. Remove `world_tour/` (commit 3). Core's tests compare against vectors generated from its JavaScript: freeze the current vectors as checked-in data files so the tests still run, and drop the generator scripts that need the JS source (`Tools/*_vectors.mjs`, `js_vectors.mjs` and the like) or mark them retired. Playwright moves to `world_tour_unity/Tests/Web/` with its own package.json if it lived under `world_tour/`. Also delete the backup `D:\repos\_backups\world_tour_2026-11-20\` only if your permissions allow it without asking; otherwise list it for the owner.
4. Paperwork (commit 4): `unity_team/` stops being tracked (git rm --cached, add to .gitignore) but stays on disk as our working channel, shots included. Put one short `world_tour_unity/README.md` (what the game is, how to add a week by editing `world/StreamingAssets/weeks.json`, how to change math settings, how to rebuild, how to test) and keep ASSETS.md with the licences. Root README.md: table and layout list show only `lunchline/`, `world/` (play link klinares.github.io/hall_pass/world/), `world_tour_unity/` and, pending the owner's answer, `spelling_4_U/`. `publish.bat`: add `world/StreamingAssets` instead of `tour/StreamingAssets` and print the `world/` address.
5. Verify: edit-mode and play-mode green, all `world/` browser suites green, `BuildWorld` builds, `world/` plays from the local server with a save that was made before the change. One tester pass: all eight places, one gig and the show in two of them. REPORT.md: what was removed, sizes before and after, anything kept and why, and "the 2D address klinares.github.io/hall_pass/tour/ will stop working after the push".

## 2026-11-25 03:12 (advisor) time-box the builder B follow-up

The P0 checkpoint (9339812) is in; thank you. The follow-up (freeze-bounds flips, wardrobe close by click, rooftop frame, stability rerun) has now taken over an hour and three builds. Close it at the next green stability run or by 04:00 local, whichever is first: commit what works, list what is left in TODO.md under the glitch list, and start the single whole-tour tester pass. A rare wall flip or one 100 ms frame is P1; getting the tester pass and fix round done with daylight to spare is what protects the 18:00 deadline.

## 2026-11-24 22:25 (advisor) refreshed shots reviewed: look corrections confirmed

Club (ARCADE sign), coffee gig (dark, larger question text; panel ends with its content), theater: all good, faces friendlier. Thank you. One pointer for the "bricks" glitch (note 21:55): the theater forecourt and the festival path are the brick-paved grounds, and the theater also has the lattice carpet under the marquee; check those two places first for shimmer, z-fighting and the marquee/pillar fade toggling.

## 2026-11-24 22:00 (advisor) DEADLINE: a finished, publishable game by Wednesday 2026-11-25, 18:00 local

The owner: "I need you to try and complete the game by tomorrow." His daughter already loves it; what is missing is that it works every time. From now on, stability beats features. Order of work, stop adding anything not on this list:
1. P0, must ship: (a) the WebGL failure (note of 21:45: context-lost handler with reload, memory per place, high-performance GPU); (b) "bricks kept reloading or looked stuck" (note of 21:55); (c) the tester's 8.4 failures that block play: reload lands in coffee instead of where she was, click-walk stage to exit failing at rec and theater, theater spawn inside the trunk ring, rec trunk hidden; (d) no way to get stuck anywhere: every place must be completable by keyboard alone and by mouse alone.
2. P1, ship if done by 14:00: bubbles covering her, snack labels under 12 px, star too small at the arena, rooftop orbs, the kid-fit pass on two places (coffee and one late place), not all eight.
3. Cut or defer to "polish later" without asking: optional arena floor dressing, any new props, any refactor, per-place extras, a second kid-fit round.
4. Process for speed: two builders in parallel on separate files; ONE combined tester pass at about 12:00 on a real-GPU browser run of all eight places end to end (walk, one gig, the show, travel), then one fix round only. No fix-round cap for P0 items.
5. By 16:00: final build, all suites green for both `tour/` and `world/`, commit, REPORT.md with the glitch list (fixed / not fixed), eight refreshed shots. Do not push; the owner publishes.
6. 8.5 clean-up runs AFTER the final commit and may finish after the deadline. It must not delay the game or touch anything the build needs.
If the plan limit stops you, write the time and the exact next step at the top of TODO.md before you stop, so the restart loses nothing. If something on the P0 list cannot be fixed in time, say so in QUESTIONS.md at once with the smallest safe fallback (for example: disable that place's click-walk shortcut, or ship seven places).

## 2026-11-24 21:55 (advisor) glitch report from the daughter's play: "the bricks kept reloading or looked stuck"

The owner's words: "the bricks in the video kept reloading or looked stuck." He has not said which place yet. Add to the 8.4 glitch hunt and reproduce on a real GPU at 1366 and 1920. Likely candidates, check each:
1. The occluder fade: brick walls, piers and the thin pieces fixed to them popping out and back on the 1.5 s rescan as she walks or stands near them (the hidden-props rescan was already buggy once in 8.1). Fading must be smooth (0.25 s or more), must not toggle while she stands still, and needs hysteresis so a wall at the edge of the test does not flicker.
2. Z-fighting or texture shimmer on brick and tile ground (coplanar ground pieces, the festival brick path, the club pavement): no two surfaces at the same height, anisotropic filtering on ground textures, mipmaps on.
3. Streaming or late-loading of a place's pieces after travel (walls appearing a moment late, or again after each gig panel closes), and anything that reloads a scene when a panel or show ends.
4. A stall: frame hitches when a gig or show opens or closes, which would read as "stuck". Record the longest frame time across a full place in the browser test; nothing over 100 ms after the first second.
Report what it was in REPORT.md under the glitch list.

## 2026-11-24 21:45 (advisor) first play on the daughter's laptop: she loves it; WebGL failure to fix in 8.4

The owner pushed the 8.3 build and his daughter played `world/` on her own laptop (dedicated GPU). Verdict from her: the 3D game is her favourite. But "there are several glitches", and she hit the failure screen: "the show couldn't start. try reloading the page, or open it in chrome or edge." followed by "Your browser does not support WebGL." She had been playing, so this is most likely a lost WebGL context (GPU memory, a scene change, the laptop switching GPUs or sleeping), after which the tab cannot get a new context. Treat as a correctness defect, top of 8.4, not subject to the fix-round cap:
1. Handle `webglcontextlost` / `webglcontextrestored` on the canvas in the `world/` template: prevent default, and on loss show one short lowercase line with a single big "reload" button that reloads the page. Never show Unity's raw "Your browser does not support WebGL" text; replace it with "this browser can't draw 3D right now. close the browser, open it again, and come back." Save before the reload path (the save must already be flushed on every gig and show end; check).
2. Cut GPU memory, since eight places now load in one session: confirm each place unloads fully on travel (Resources.UnloadUnusedAssets after the scene swap, no place assets in a shared always-loaded bundle), compress textures, cap render scale at 1.0 and MSAA at 2x, shadow map 2048 max, and record WebGL memory before and after travelling through all eight places in a browser test (it must not grow place over place).
3. Request `powerPreference: "high-performance"` in the WebGL context attributes so a two-GPU laptop picks the dedicated card.
4. Add a browser test: force context loss with the WEBGL_lose_context extension mid-walk and mid-show; the page must show the reload line and the save must survive.
5. In the 8.4 tester pass, hunt the "several glitches": play all eight places end to end as a child would (walk into every edge, spam clicks during travel, open a gig while Riff is in the way, leave mid-show) and list every one in REPORT.md with fixed / not fixed.

## 2026-11-24 07:40 (advisor) look review of the phase 8 shots: approved, with five corrections

Thank you for the shots. I looked at coffee (street, gig, show), arena, club and festival at 1366. Verdict: the look is approved. One consistent style, warm light, real shadows, each place clearly its own (morning street, neon night, festival, arena), crew read as modern. No rework of the kit. Fold these into 8.3/8.4 (they are look items, not new scope):
1. Club: remove the "BAR" neon sign. This is a game for an 11-year-old: use "ARCADE", "TACOS" or "RECORDS". Check every place for any other adult-venue wording (bar, pub, beer, wine, cocktails, casino).
2. Docked panel story text ("8 fans fit in each row...") is thin grey on white and the smallest text on screen. Make it ink-dark and at least as large as the dog's bubble text. The question is the point of the screen.
3. Docked panel: the lower fifth is an empty pink slab at 1366. Centre the content vertically or let the card end where the content ends, so more of the world shows.
4. Crew faces: several read as frowning or stern (lowered brows) in the street and festival shots. If the face is a texture or a brow piece, give the crew and the girl a neutral-to-smiling face; at minimum the crew member she is talking to should play the happy/Talk pose. Riff too should look at her.
5. The first-visit walking hint box sits over the path she walks down. Fade it after her first step or 6 seconds.
Optional if cheap: the arena floor is a large empty grid in the foreground; a few floor decals, cables or a camera dolly would fill it.
Refresh the affected shots in `unity_team/shots/phase8/` when done. Keep going with 8.3, then 8.4 and 8.5.

## 2026-11-24 04:35 (advisor) NOT ACTED ON YET: screenshots into the repo, and 8.5

The notes of 2026-11-23 20:58 and 23:22 and answer 26 have not been picked up (TODO.md has no 8.5; `unity_team/shots/phase8/` does not exist; 8.2 started without my look review). Re-read this file before each task, as agreed.
1. NOW, before more 8.2 work: copy the coffee street look-gate shots (scratchpad p81f1: street, a gig with the docked panel, the stage show; 1366 and 1920) into `unity_team/shots/phase8/`. I can only see files inside the repo. Do the same for each new place as it passes your look gate.
2. 8.2 may continue, but if I write a look correction for the coffee street it applies to the shared kit and every place.
3. Add 8.5 (repo clean-up, note of 20:58) to TODO.md after 8.4. The scratchpad shots folders are part of what 8.5 removes, so copy before then.

## 2026-11-23 23:22 (advisor) save look screenshots in the repo now, not only at 8.4

I review the look myself, and I can only see files inside the repo. From the next 8.1 build on, save two screenshots per place (1366 and 1920, taken with the GPU flag, the girl and at least one crew member in frame) under `unity_team/shots/phase8/` as `<place>_1366.png` and `<place>_1920.png`, and refresh them when the look changes. Do not start 8.2 until the coffee shop pair has been there for one advisor check (about 30 minutes) without a correction note here. Also add 8.5 (clean-up, note of 20:58) to TODO.md.

## 2026-11-23 20:58 (advisor) add task 8.5: repo clean-up (owner's request)

The owner: "make sure the repo gets cleaned up once we complete the task so we do not have files we no longer need". Add 8.5 as the LAST task of phase 8, after the 8.4 commit, as its own commit so it can be undone.
1. Remove what the team made and no longer needs: staging folders (b22_stage, b26_stage and the like), harness copies, scratch scripts, old test output and screenshots except `unity_team/shots/phase8/`, imported art that no scene or prefab uses, unused packages, the kept "previous" build under `tour/Build` and `world/Build` if BuildWeb can recreate it. Use `git rm` for tracked files; check .gitignore covers Library, Temp, Logs, obj, UserSettings and node_modules.
2. Shrink the team's paperwork: keep BRIEF.md, ADVISOR.md, QUESTIONS.md, DECISIONS.md and the final REPORT.md; move finished phases out of TODO.md into one `unity_team/HISTORY.md`; drop KICKOFF.md if nothing reads it.
3. Do NOT remove, only list in REPORT.md under "could be removed, the advisor decides" with size and what depends on each: `spelling_4_U/`, `world_tour/`, `tour/` and the 2D-only code and art (all live or playable today), `D:\repos\_backups\world_tour_2026-11-20\`, `publish.bat`, anything the team did not create.
4. After the clean-up: all suites green, both builds still build, then report the repo size before and after. Do not push.

## 2026-11-23 20:50 (advisor) PHASE 8: World Tour becomes a 3D explorable game

The owner's words: "more of an interactive game almost rpg style with rendered beautiful graphics since both of our laptops have dedicated GPUs". He chose: 3D stylized look, played from a browser link, the whole tour (all eight venues) before his daughter sees it. All decisions stay with the advisor; no questions to the owner. Billing rule of 15:55 Nov 21 stands (plan allowance only, no headless/API, no overage, nothing paid). Lead on its own model, workers on Sonnet, max 2 builders + 1 tester.

**What stays.** Core (generators, levels, hints, show engine, save, math_config.json, weeks.json) is not rewritten. Every learning rule in QUESTIONS answers 7-25 still holds. `tour/` (the current game) is not touched and stays live. The new game builds to a NEW folder `world/` at the repo root (address .../hall_pass/world/), with its own StreamingAssets copies of weeks.json and math_config.json synced FROM `tour/StreamingAssets/` at build time (tour/ stays the one master). Same localStorage save, so her fans, stickers and levels carry over; new fields are added keys, old game must still load the save.

**The game.**
- She walks a pop-star character around a small 3D place for each of the eight venues (a street or plaza plus the venue itself). Third person, fixed friendly camera that follows; no camera controls to learn. Move with WASD/arrows AND click-to-move; E, Space or click to talk. Riff the dog follows her.
- Crew characters stand around each place with a marker over their head. Talking to one starts a gig: one existing mode (spelling or math) opened as a panel over the world, reusing the current mode screens and hint ladder. Short gigs: 3 to 5 questions. Each place offers the enabled math modes and that week's spelling modes spread across its characters, so the world is how she picks what to play.
- Each place ends at the stage: the existing 10-song show, now on a lit 3D stage with a crowd that grows with her fans and reacts to right answers (lights, confetti, cheering). Fans unlock the next place as now, with a short travel moment.
- Backstage shop items become things she can see: outfits, hair and guitar colours on her character, stage light colours, a collar for Riff. Peach and navy outfit set available early.
- No combat, no timers that cost anything, no fail state, nothing earned is ever lost, no reading walls: a character line is at most 12 words, 5th-grade reading level.
- No real artists, songs, lyrics or logos. No ads, analytics or accounts.

**The look.** "Beautiful" is the owner's main ask, so it is a definition-of-done item, judged from screenshots.
- Art: CC0 packs only (Kenney, Quaternius, KayKit; check each pack's licence file says CC0). Import only the models used. List every pack with its source address and licence in `world_tour_unity/ASSETS.md`. Nothing bought, nothing with an attribution or non-commercial clause, nothing AI-generated that copies a known character.
- One consistent style across all eight places (do not mix a blocky pack with a smooth one). Each place gets its own palette, time of day and landmark so they feel different: small coffee shop street at morning up to the arena at night.
- URP 3D renderer added beside the 2D one: one directional light with soft shadows, baked or probe ambient, bloom, colour grading, light fog, a few particle effects (stage lights, fireflies, confetti). Idle life: characters breathe and look at her, signs sway.
- Budget: WebGL2, 60 fps at 1366x768 and 1920x1080 on a laptop GPU, never under 30 under the headless test; download of `world/` 60 MB or less compressed, no file over 50 MB; first place playable within 10 s on a warm cache. A quality drop-down is not needed: pick settings that meet the budget.

**Order of work.**
- 8.0 spike (one builder, half a session): 3D renderer, one CC0 character walking on a small lit ground with follow camera and click-to-move, built to `world/`, browser test loads it; report size, fps, one screenshot. If the budget cannot be met in a browser, say so in QUESTIONS.md before going on.
- 8.1 first place complete, end to end: walk, talk, one spelling gig and one math gig as panels, the stage show, rewards, save, unlock moment. Tester pass WITH a look review from screenshots at both sizes (composition, lighting, readable markers, nothing floating or clipping), then the 7.4 kid-fit script again on this place. Two fix rounds. Do not start 8.2 until this place looks good in the lead's own judgement of the screenshots; write the reasons in DECISIONS.md.
- 8.2 the other seven places from the same kit (shared prefabs for characters, markers, gig panels, stage; per-place layout, palette, landmark, time of day). Two builders in parallel, one build per placed batch.
- 8.3 wearable shop items, crowd growth, travel moments, sound for footsteps/ambience using the existing synth approach or CC0 clips listed in ASSETS.md.
- 8.4 tester pass on the whole tour + kid-fit, two fix rounds, final build, commit by path (`world/`, `world_tour_unity/`, `unity_team/`), REPORT.md with eight screenshots saved under `unity_team/shots/phase8/`, size, fps, what the owner does next. Do not push. Then stop.

Keep the old 2D screens compiling: `BuildWeb.Build` still produces `tour/` unchanged; add `BuildWeb.BuildWorld` for `world/`. Edit-mode, play-mode and browser suites must stay green for both.

## 2026-11-21 18:00 (advisor) phase 7 accepted; two data edits made by the advisor

Phase 7 report read and accepted. No further work is assigned; stay stopped.
1. Q25 clue rewrites applied by me in `tour/StreamingAssets/weeks.json` (irrational, interaction, intercede `h` lines, text as in answer 25). Validator: ok. Stored vectors and tests were not touched; if a test pins the old text, mask it as in 6.1 on the next build.
2. `math_config.json`: ticket sales `md` MAX_LEVEL 1 -> 0 until her class starts multi-digit work (unit 4). The kid-fit score at the old level was about 5 of 10 and level 1 was not re-scored. Raise it back to 1 in the file when unit 4 starts.

## 2026-11-21 15:55 - billing rule from the owner, and one more task: 7.4 kid-fit review

**Billing (the owner's words: "only use my monthly tokens, nothing else").** Work only on the plan allowance of this interactive session.
- Never run `claude -p` or any headless, SDK or API call; an `ANTHROPIC_API_KEY` is set on this machine and those would bill it per token. Do not read, use or export that key.
- Never accept or turn on extra or overage usage. If the plan limit is reached, stop cleanly, write the state into TODO.md, and wait for the reset; do not look for another route.
- No paid services, assets or cloud builds of any kind.
- Keep workers on Sonnet, two at most, short prompts and hand-backs, as in the 20:58 note.

**7.4 Kid-fit review (after 7.3, before the final report).** The owner wants the game "polished and relevant to kids", and his daughter's first reaction was that it was too hard. One tester pass, playing as an 11-year-old on her first day, with a fresh save and the shipped config, in pink at 1366x768:
1. First five minutes: start card to the first finished show without reading any instructions. Note every moment she would not know what to do next.
2. Play one full show of every enabled math mode from level 0 and one spelling mode. For each, record how many of the first ten questions a child in her class would get right without a hint (she is in unit 3 of grade 5: fraction multiplication and division; volume done; no decimal operations, no unlike-denominator addition, no coordinate plane, no multi-digit algorithms). The target is seven or more of the first ten. Anything under that goes back for an easier level 0 or level 1.
3. Read every sentence shown in those shows. Flag words or sentence shapes above a 5th-grade reading level, and any prompt longer than three short lines.
4. Rewards: she should earn something she can see (fans, a sticker, an unlock) by the end of her first show, and be able to afford something backstage within her first two or three shows. If not, lower the cheapest prices.
5. Fix what this finds (two fix rounds at most), rebuild, commit, and put the findings in REPORT.md as a short table: mode, first-ten score, what changed.

Then the final report and stop. Do not push.

## 2026-11-21 12:05 - PHASE 7: fit the math to her class (the owner's daughter found the math too hard)

She played the live game this morning. Her feedback: the math is too hard, fractions and decimals most of all. Her class (Amplify Desmos Math grade 5, which follows Illustrative Mathematics) is in Unit 3, multiplying and dividing fractions. Volume and fraction-as-division are behind her; multi-digit algorithms, decimal operations, adding fractions with unlike denominators and the coordinate plane have NOT been taught yet. So merch table and beat maker were asking for things she has not met.

Already done by the advisor (no build needed): `tour/StreamingAssets/math_config.json` now has `dec`, `frac` and `xy` switched off. Do not turn them back on.

One phase, Sonnet workers, at most two builders and one tester pass with two fix rounds:

**7.1 Difficulty controls in math_config.json (read at start, no rebuild to change).**
- Per skill: `MAX_LEVEL` (cap) and `START_LEVEL`. Global: `LEVEL_UP_AFTER` (default 6, was 4) and `LEVEL_DOWN_AFTER` (stays 2). A save whose level is above a cap plays at the cap; nothing she earned is removed.
- A new warm-up level 0 in every math generator, made of 4th-grade review: single-digit facts and 2-digit by 1-digit for ticket sales, a single visible layer for pack the truck, two operations without parentheses for tour budget, facts to 5 for soundcheck, whole-dollar and 50-cent sums for merch table, like denominators only for beat maker, axes to 4 for stage designer. New skills start at level 0.
- Manager's office: a "gentle / standard" switch per subject. Gentle = caps one level lower and 8 in a row to move up. Parent-facing text only.
- Ship these defaults: `md` MAX_LEVEL 1, `ops` MAX_LEVEL 2, `flu` MAX_LEVEL 2, `vol` uncapped, `dec`/`frac`/`xy` off.

**7.2 A new math mode for what she is learning now: fraction multiplication and division (skill `fm`).**
Name and theme it inside the tour world (for example the green room's snack table: parts of parts of a tray). Reuse the tour budget layout (prompt card, choices or keypad, hint sheet). Levels:
- L0: a unit fraction of a whole number that divides evenly (1/4 of 12).
- L1: unit fraction x unit fraction (1/2 of 1/4), shown as "a part of a part".
- L2: unit x non-unit and non-unit x non-unit, denominators up to 6; also "which expression matches the shaded region".
- L3: a mixed number x a fraction; a missing factor (2/3 x ? = 20/21); and comparing a product with one factor using <, > or = without multiplying.
- Division kinds, mixed into L2 and L3 behind a config switch `fmDivision` (default true; her class started it on Nov 20): a unit fraction shared by a whole number (1/4 / 2), and how many unit fractions are in a whole number (2 / 1/4).
- Visual model: an area diagram (rows x columns of one whole) for multiplication, a tape diagram for division. Hint 1 names the idea ("a part of a part"; "how many 1/4s fit in 2?"). Hint 2 draws the partitioned whole WITHOUT shading the product or counting it for her. Hint 3 is the worked solution: multiply numerators and denominators, or the division fact with its multiplication partner. Answers 8, 9, 11 and 16 apply.
- Accept any equivalent fraction (7/56 and 1/8 are both right), and mixed-number or improper forms. Never require simplest form in this mode.
- Distractors model real mistakes: adding numerators and denominators, multiplying only the numerators, dividing by the denominator the wrong way round.
- Write every story and number yourself in the game's voice. Do not copy problems, names or stories from the curriculum.
- Pure generator in Core with seeded tests (a few thousand seeds per level: one correct answer, unique among choices, denominators within bounds, hint rules hold). No JS twin exists; that is fine.

**7.3 Tester pass, final build, commit, short REPORT.md.** Include a table for the owner: each math mode, whether it is on, its cap, and which class unit it matches, so he can switch things on as the year goes (decimals: unit 5; unlike denominators: unit 6; coordinates: unit 7). Do not push.

## 2026-11-21 02:18 - close out the polish task and finish

The polish task has run for over four hours and several builds, and TODO.md has not been updated since the first rejection. To finish tonight:

1. **Update TODO.md now** with where 6.2 stands: how many tester passes, what is still open.
2. **One more tester pass at most.** After the fix you are on, run a single tester pass. Fix only what a player would notice at a glance (overlapping or clipped text, unreadable text, a wrong colour in the peach theme, a missing piece of the win and crowd feedback). Everything else goes on the "polish later" list with one line each. No further fix-and-retest loops.
3. **Then 6.3 and 6.4 straight away:** final build into `tour/`, size and cold-load numbers, commit, and the final REPORT.md with "What the owner does next" and a short, plain list of the polish items left open. Then stop.

## 2026-11-20 20:58 - usage: run builders and testers on a smaller model from now on

The owner's allowance is running down. The two worker definitions set no model, so every builder and tester inherits yours. From the next agent you start:

1. **Start builders and testers on the Sonnet model** (pass the model when you spawn them, or add `model: sonnet` to the frontmatter of `.claude/agents/unity-builder.md` and `unity-tester.md` if that is what takes effect in this session). Do not restart agents that are already running; let them finish.
2. **You stay on your current model** and do the judging: review each hand-back before sending it to the tester, and write the task prompts tightly (exact files, exact acceptance checks) so a smaller model can follow them.
3. **Exception:** if a smaller-model builder fails the same task twice, give that one task to a builder on your own model and log it in DECISIONS.md.
4. **Keep prompts and hand-backs short.** Ask agents to report in under 200 words plus file lists, and to read only the files a task names instead of whole folders. Do not have agents re-read BRIEF.md, DECISIONS.md or TODO.md in full; quote them the lines that matter.
5. In the next REPORT.md, add one line under "Departures" saying which model the workers ran on.

## 2026-11-20 19:30 - the owner wants a clean, polished game at the end: phase 6 includes a polish pass

The owner's words tonight: "I want to see a clean and polished version at the end of it." So phase 6 is not only the final build. After the clue rewording (19:25 note, item 3) and before the final build, do one polish task covering everything a player can see, then have the tester pass it with screenshots of every screen at both sizes:

- **Readability:** every low-contrast item on the list (pink "rehearse again" and "admit one", the grey bill note, light grey ticket-sales notes, tiny seat-row icons). Body text must be comfortably readable at 1366x768.
- **Clipping and overlap:** beat maker labels and cell numbers (taller rows), tour budget's worked sheet over the song counter, the no-save notice over a map label, patterns wrapping mid-pattern in the dog's bubble.
- **Feel the brief promised and the build still lacks:** the win toast and confetti, the end-of-show burst, and a crowd that grows and reacts during a show. Keep each under 400 ms and off when reduce motion is on.
- **Small correctness:** a paid hint must not sell a letter she was already told (P3); "the 0" when two zeros drop (P6); the comma in "8,400"; the curly apostrophe (P7); venue card Tab order (P5); the fast sideways drag picking the neighbouring tile.
- **Colour:** check translucent colours on every screen against the web game's look, since the project blends in linear space.

Not part of this: the BuildWeb hardening for non-default arguments, the SwiftShader-only items, and module trimming. Leave those on the list.

The two-fix-round cap does not apply to this task: it is done when the tester finds nothing on the list above still visible. Keep to two builders.

## 2026-11-20 19:25 - the owner has handed every decision to the advisor. Do not ask him anything

The owner said tonight that the advisor is to manage this build completely. From now on:

1. **No questions to the owner.** Do not end a turn or a report with a question for him, and do not list items as "waiting on you". Anything you would have asked goes in QUESTIONS.md and is answered there; carry on meanwhile. The only things left to him are physical: keeping this terminal open, listening to the sound, and pushing to GitHub at the end.
2. **The answer 14 web-game fix is done; drop that question.** The advisor applied it directly at 19:22: `world_tour/math/gen/fluency.js` now uses the same place-value wording as Unity's `PlaceValueHint` for the one-place decimal hints, and `world_tour/tests/math/generators.test.mjs` checks it (node tests 41/41). You did right to refuse it without the owner's word; nothing further is needed from you in `world_tour/`. The chromium suites were not rerun for this text-only change; if a later task of yours runs them anyway, note the result.
3. **Word clues: irrational and illogical (the tester's finding in the phase 3 report).** Decision: reword so each fan mail clue and lyric line fits one word only. Do this as the FIRST task of phase 6, not during phase 5, so no fixtures move under the builders:
   - illogical, clue `h`: "Not following the steps of good reasoning, like an argument whose conclusion doesn't follow"
   - illogical, line `x`: “it rained, so the show sold out” is an ___ argument. one thing doesn't lead to the other. (the sentence itself opens and closes with curly quotes around the first clause; use straight quotes if the font lacks them)
   - irrational, clue `h`: unchanged
   - irrational, line `x`: "i know the tiny spider can't hurt me, so being this scared of it is totally ___."
   Edit `tour/StreamingAssets/weeks.json` (the source of truth), let the build mirror it, regenerate any stored vectors that carry the old text, and run the suites. Leave `world_tour/data/weeks.js` alone; the advisor will apply the same text there once your side is committed. Say in the phase 6 report that it is done.
4. **Phase 5 scope reminder, to save usage:** importing a save exported from the web game is not required (answer 4 was a fresh save). One quick check is fine; do not build a converter.
5. **When you finish phase 6,** write the final REPORT.md with a short "What the owner does next" list (listen for sound and the read-aloud voice; play it with his daughter; commit `world_tour/`; push to GitHub) and stop.

## 2026-11-20 11:18 - answer 14 is not on your list yet: soundcheck decimal hint, Unity and web game

Answers 13 and 14 have been in QUESTIONS.md since 10:13, but TODO.md does not show the work answer 14 asks for. Add it as one task and do it after 3.2 is placed and before the phase 3 tester pass (3.3), so that pass and its build cover it:

1. **Unity Core:** replace the "the digits don't change; only the decimal point moves" hint with the place-value wording in answer 14, and add the test it describes. That wording was mine and is false when a zero drops (140 / 10) or is filled in (8.8 x 100).
2. **Web game:** the same wording in `world_tour/math/gen/fluency.js` and its test, under the same one-time approval as task W. The patch applied at 10:12 carries the wording answer 14 withdraws. Run `node --test world_tour/tests/math/` and the chromium math suites in the foreground. Do not stage or commit `world_tour/`.
3. **Report:** in the phase 3 REPORT.md, under "For the owner", say that `world_tour/` is now ready for him to commit and list every changed file. He has been told to wait for this before committing.

Reminder: answers to numbered questions are written in QUESTIONS.md under the question. Re-read that file before each task as well as this one.

## 2026-11-20 08:03 - owner says: fix it and keep going. This replaces the stop order below

The owner has seen the status and wants the build to continue. Do not stop after 2.7. Work in this order:

1. **Finish 2.7.** Complete the fix round, tester pass, rebuild `tour/`, commit locally, write the phase 2 REPORT.md.
2. **Apply answers 7 to 11 in the Unity Core** (they are in QUESTIONS.md under each entry, plus the clean-run sticker answer). Soundcheck's wrong-digits hint first. One task, with the general hint tests described in answers 8, 9 and 11. Rebuild and commit.
3. **Patch the web game's hint faults.** The owner's daughter can play `world_tour/` today, so this one-time change to that folder is approved, for these three faults only: (a) soundcheck decimal-point hint that prints wrong digits (answer 9 item 1); (b) hints 1 and 2 that state the answer in ticket sales and pack the truck (answers 8 and 9); (c) the syllable hint that spells out two-syllable words (answer 11). Use the same wording as the Unity fixes. Before touching anything, confirm the backup at `D:\repos\_backups\world_tour_2026-11-20\` is intact. Change only the generator and hint code and their tests; run `node --test world_tour/tests/math/` and the chromium math and spelling browser suites in the foreground; regenerate nothing else. Do not stage or commit `world_tour/`; list the changed files in the report for the owner to commit.
4. **Then phase 3** (the six remaining spelling modes), phase 4, 5 and 6, as in the brief.

Usage: the owner is watching his allowance. Run at most two builders at a time, plus the tester. Prefer one build and one browser run per task over one per fix. Keep the two-fix-round cap.

Advisor checks are back on, every 30 minutes. Questions go in QUESTIONS.md and are answered there; corrections come here.

## 2026-11-20 07:58 - owner is awake: finish 2.7, then stop. Questions 7 to 11 ARE answered

1. **Stop point.** The owner is concerned about usage. Finish the current 2.7 fix round with a single builder (no parallel builders from here on), get the tester's pass, rebuild `tour/`, commit locally, rewrite REPORT.md, and then STOP. Do not start phase 3 or any further work until the owner says so. If the fix round has not passed after one more round, stop anyway and report what is still broken.
2. **Questions 7 to 11 were answered overnight in QUESTIONS.md**, directly under each entry (01:48, 02:08, 02:22, 02:53, and the sticker addendum just now). The phase 1 report lists them as open and says the advisor was silent: answers go in QUESTIONS.md, not here, so re-read that file. Do not apply them now. In REPORT.md, list them under "Next session, first tasks", with the soundcheck wrong-digits hint (answer 9, item 1) first, and state plainly that the current build still has the web game's hint text.
3. **Advisor checks.** My checks stopped at 03:22 because the scheduled check did not reach me until the owner's message at 07:52. That is why nothing was written here between 00:45 and now. Checks are off while the owner is present; he will relay.

## 2026-11-20 00:45 - pace, the deleted staging folder, and a backup of world_tour/

The allowlist for BuildWeb is the right fix and the tester was right to probe `-buildOut ../world_tour`. Three instructions:

1. **Pace.** Phase 0 has taken about two hours and task 0.5 is in its second fix round. After this round passes the tester's re-check, stop hardening the build script: no further guard work unless a test shows real data loss on the default arguments. Finish 0.6 and 0.7 and move to phase 1. The owner wants something playable in the morning, and phases 1 and 2 are where that comes from. In later phases, cap fix rounds at two per task for anything that is not a correctness or data-loss defect; log the rest in TODO.md under "polish later".
2. **What got deleted?** DECISIONS.md says a non-empty staging folder outside the repo "got deleted". State in the phase 0 REPORT.md exactly which path that was and what it held. If it was anything other than a folder the tester created for the test, say so at the top of the report under "For the owner". From now on, tests that probe destructive behavior must use throwaway folders they create themselves under the system temp directory.
3. **Back up the web game now.** `world_tour/` is finished, uncommitted, and one bad path away from being lost. Before any further build or test run, copy it once to `D:\repos\_backups\world_tour_2026-11-20\` (skip `tests/e2e/node_modules`), verify the file count matches, and note the backup in REPORT.md. Do not stage or commit `world_tour/`; that stays the owner's call. This copy is the one approved write outside the repo.

## 2026-11-20 00:12 - decisions 10 to 13 and TODO.md reviewed; weeks.json source of truth decided

Good work: the cacheControl regex fix, the const-to-static fix so Game really references Core, staging builds before publishing, and keeping TODO.md on disk are all accepted. Keep TODO.md current; it is how a restarted session resumes.

**Decision on your carried-forward question (which weeks.json is the truth):** the deployed copy, `tour/StreamingAssets/weeks.json`, is the single source of truth, because that is the one file the owner edits each week and it must never need a rebuild. Same for `math_config.json`. So:

1. `BuildWeb.Build` starts by copying those two files from `tour/StreamingAssets/` back into `Assets/StreamingAssets/` whenever the deployed copy exists and differs, and logs that it did. The project copy is then a mirror, so tests and the Editor always run on the live words.
2. The build still never overwrites the deployed copies. It only creates them when missing (first build).
3. If someone needs to change the data from the project side (a new field, a converter rerun), they edit the deployed copy, or delete it so the next build recreates it. Say exactly this in the phase 6 README, in three lines.
4. Validate on load as planned, and make the validator runnable from the terminal on the deployed file (a small Editor or Node script), so the owner can check a week he just typed before pushing.

Reminder for 0.6: confirm the cacheControl fix on the real build in `tour/`, not the patched copy.

## 2026-11-19 23:37 - phase 0 build settings (decisions 5 to 9) reviewed

All five accepted: Gzip with fallback, hashed file names with no-store on the data files, size-first code settings, splash off, TMP essentials kept for now. Three follow-ups:

1. **High stripping only bites in the player.** Edit-mode and play-mode tests run unstripped, so they cannot catch a type that High managed stripping removes. Anything read or written through reflection (the weeks.json and math_config.json models, the save blob) must be exercised in the built web player by a browser test: load a real weeks.json, save, reload, read the save back. Prefer JsonUtility with plain serializable classes, or mark the models [Preserve] or list them in link.xml. Do this in phase 1 task 1.7 and 1.5, not later.
2. **Storage blocked.** Data caching uses IndexedDB. Add a browser test that loads the build with storage blocked and confirms it still starts and plays, since the save layer already promises this.
3. **Size budget.** 12.30 MB for the smoke build is acceptable as a starting point. Record the size in every REPORT.md, take out the TMP default font and emoji sprites once the two real fonts are in, and treat 20 MB for the finished game as the ceiling to raise with the advisor before crossing.

## 2026-11-19 23:25 - overnight: the owner is asleep until about 7 AM

Keep building through the night. Do not stop to wait for the owner, and do not address questions to him.

- Work through the phases in order without pausing between them. End each phase with the definition of done, a local commit, and a rewritten REPORT.md, then start the next.
- The advisor checks these files about every 30 minutes all night. For anything you would have asked, write it in QUESTIONS.md with your recommendation, carry on with other tasks, and re-read QUESTIONS.md and this file before each task.
- If a question would normally need the owner (money, an account or licence step, an outside asset, pushing to GitHub), do not wait: take the path that spends nothing, needs no sign-in, uses only the existing art, and can be undone, and log it in DECISIONS.md. If no such path exists, park that one task and move on.
- If a whole phase is blocked, write the question, wait 35 minutes, re-read, and continue. Only stop if the answer still isn't there after two waits.
- If a tool asks for a permission you don't have, don't wait on it: find a route that needs no new permission, or park the task.
- Priority for the morning: the owner wants something his daughter can open and play. If phase 1 is done and time is short, a polished phase 2 (map plus remix, playable in the browser from tour/) matters more than starting phase 3.

## 2026-11-19 23:20 - decisions 3 and 4 reviewed

Both accepted. The package strip is confirmed by the smoke build, so drop "provisional" from your own tracking. Two instructions:

1. **Exit codes are not test results.** With the shutdown crash (D1) and the `EditorApplication.Exit(code)` workaround, a run can exit 0 after doing nothing, or crash after passing. For every test run, pass `-testResults <file>` and judge pass or fail from the counts in that results file; treat a missing or empty results file as a failure. For builds, check that the expected output files exist and are newer than the run's start. Report counts from the results file, not from the exit code.
2. **Foreground only.** The 0.2 build check ran in the background. The brief says every suite and build runs in the foreground with a 10-minute timeout, one at a time. A background Editor that never exits is what stalled the web version overnight. If a web build needs longer than 10 minutes, raise the timeout for that command and say so in the report; don't background it.

FYI: the owner hit a usage limit tonight. If a session stops mid-task, resume from the to-do list and the files on disk; do not restart the phase.

## 2026-11-19 22:35 - review of PLAN.md and the first two decisions

PLAN.md is approved as written. uGUI with TextMeshPro and staying on 6000.3.29f1 are both right. Four additions:

1. **Commits.** `world_tour/` (the finished web game) is still uncommitted in this repo and belongs to the owner. Stage by path only: `world_tour_unity/`, `tour/`, `unity_team/`, `.claude/agents/`. Never `git add -A` or `git add .` from the repo root. Confirm the Unity .gitignore keeps `Library/`, `Temp/`, `Logs/`, `obj/` and `UserSettings/` out before the first commit.
2. **Stale word lists.** GitHub Pages lets browsers cache files for several minutes, and browsers may hold `weeks.json` longer. The owner edits that file weekly and needs the change to show on the next visit. Request `weeks.json` and `math_config.json` with caching disabled (a no-store request, or a timestamp query string), and have the phase 0 or phase 1 browser test prove it: load, change the served file, reload, see the change.
3. **Phase 0 numbers.** In the first REPORT.md give the empty build's total download size, the size of the largest file, and the time from navigation to the label being visible, measured on a cold cache. If the download is over about 12 MB or the cold load over about 8 seconds, list what you would strip next before moving on; do not start phase 2 on a build that is already slow.
4. **Stripping (task 0.2).** After removing packages and modules, run a web build and the browser test before recording the list, since uGUI, TextMeshPro, audio, 2D sprites, particles and UnityWebRequest must all survive. If a removal breaks the build, restore it and note which one.

FYI: `storage.jslib` over localStorage with an in-memory fallback is a good choice for saves on a static host. Keep the whole save under one key and well under 1 MB.
