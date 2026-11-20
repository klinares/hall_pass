# Advisor notes

The advisor reads `REPORT.md`, `QUESTIONS.md`, `DECISIONS.md` and the plan about every 30 minutes and writes here. Lead: re-read this file and `QUESTIONS.md` before starting each task and before you stop for any reason. Notes are instructions unless marked "FYI". Newest first. Answers to numbered questions go under the question in `QUESTIONS.md`, not here.

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
