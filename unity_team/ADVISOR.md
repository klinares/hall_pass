# Advisor notes

The advisor reads `REPORT.md`, `QUESTIONS.md`, `DECISIONS.md` and the plan about every 30 minutes and writes here. Lead: re-read this file and `QUESTIONS.md` before starting each task and before you stop for any reason. Notes are instructions unless marked "FYI". Newest first. Answers to numbered questions go under the question in `QUESTIONS.md`, not here.

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
