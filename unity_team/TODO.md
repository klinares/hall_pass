# Lead's to-do list

Kept on disk so a new session can resume from here (see the FYI in ADVISOR.md, 23:20). Newest status at the top of each phase.

## Phase 0: pipeline (done)

## Phase 1: core (next)

- [ ] 1.1 rng, rational, money
- [ ] 1.2 answer, mistakes, config to math_config.json
- [ ] 1.3 generators + guard, generators.test port, fixed-seed runs, JS-vs-C# sample
- [ ] 1.4 show engine: queue, re-inserted misses, encore, scoring, level up/down
- [ ] 1.5 progress, review (full problem), save blob with version; test the save in the built web player (ADVISOR 23:37 #1)
- [ ] 1.6 meta: shop, milestones, venues
- [ ] 1.7 weeks_to_json, StreamingAssets/weeks.json (22 words), C# parser + validation, terminal validator on the deployed file; browser tests: real weeks.json loads in the stripped player, and an edited weeks.json shows on reload

## Phase 0 tasks

- [x] 0.1 Editor 6000.3.29f1 confirmed, build target Web (tester passed)
- [x] 0.2 Pipeline package added, unused packages and modules removed (tester passed, smoke build). D1 recorded: no bare `-quit`; exit through `EditorApplication.Exit`
- [x] 0.3 Web player settings (tester passed settings, build, compile and git; 12.30 MB, cold load 794 ms on localhost). One defect fixed by the lead: the template's cacheControl regex didn't match, so Build/ files were never cached. The fix was proven in a patched copy only, so 0.6 must confirm it on the real build. Failure screen now shows one short line
- [x] 0.4 `Boot` scene with one TMP label; Core, Game and both test asmdefs; one edit-mode test (tester passed; edit-mode 1/1, play-mode 0). Lead fixed `GameInfo.Title` const → static readonly so Game really references Core; 0.5 confirms it in the compiled DLL. Boot has no EventSystem yet: phase 2's click-to-start card needs one (Input System UI module)
- [x] 0.5 `Assets/Editor/BuildWeb.cs` builds to `tour/`: tester passed after 2 fix rounds. Allowlist guards; staging then publish; one previous build kept; deployed StreamingAssets never overwritten; weeks.json and math_config.json synced from tour/ to Assets/; summary file always written; InputSystem preloadedAssets entry kept. Closed at fix round 2 per ADVISOR 00:45 #1. Remaining items are under "polish later"
- [x] Backup of world_tour/ (ADVISOR 00:45 #3): `D:\repos\_backups\world_tour_2026-11-20\`, 692 files (tests/e2e/node_modules skipped), content checksum matches the source (672d984e…). Not staged or committed
- [x] 0.6 (tester passed; 6/6 browser tests; lead fixed the --only typo false pass) `Tests/Web/`: plain static server + Playwright test: loads `tour/`, fails on console errors, records size and cold load. Also: reload in the same context comes from Unity's cache (≈50 KB, not 12 MB); a test with storage blocked (ADVISOR 23:37 #2); a broken build shows the short lowercase failure message
- [x] 0.7 (DoD passed, REPORT.md written, committed locally) Tester runs the full definition of done; check .gitignore; commit by path only (`world_tour_unity/`, `tour/`, `unity_team/`, `.claude/agents/`); REPORT.md with size, largest file and cold load

## Carried forward

- Phase 1, tasks 1.5 and 1.7: exercise weeks.json, math_config.json and the save blob in the built web player (High stripping); use JsonUtility or [Preserve]/link.xml (ADVISOR 23:37 #1)
- Phase 1: browser test that an edited weeks.json shows on reload (ADVISOR 22:35 #2). The template already fetches StreamingAssets with no-store
- Phase 2: make Caveat Brush and Courier Prime the TMP defaults; remove LiberationSans and EmojiOne (~1.7 MB)
- Every REPORT.md: build size; 20 MB ceiling for the finished game (raise with the advisor before crossing)
- Optional size cut kept in reserve: clear the 2D renderer's post-processing data (~2.5 MB film grain and SMAA). Not done, so bloom stays available for phase 2
- DECIDED (ADVISOR 00:12): the deployed `tour/StreamingAssets/weeks.json` and `math_config.json` are the source of truth. BuildWeb syncs them back into `Assets/StreamingAssets/` before building (being added in 0.5 now). Phase 1 (1.7): a validator runnable from the terminal on the deployed file. Phase 6 README: three lines saying "edit the deployed copy, or delete it so the next build recreates it"
- For the phase 0 report (ADVISOR 00:45 #2): the folder "deleted" in the 0.5 test was `canary_dir`, a throwaway the tester created in its own scratchpad under the system temp directory (canary.txt, sub/deep.txt). No owner files were touched. From now on, destructive-probe tests use only throwaway folders they create under the system temp directory
- Fix-round cap from phase 1 on (ADVISOR 00:45 #1): at most 2 fix rounds per task for anything that isn't a correctness or data-loss defect; the rest goes under "polish later"
- Morning priority: a playable phase 2 (map + remix in `tour/`) over starting phase 3

## Polish later

- BuildWeb: alias paths (UNC `\localhost\D$\...`, `\?\`, junctions, symlinks) get past the "inside the repo" checks, because the comparison is string-only. They need deliberate, non-default arguments. Fix: refuse UNC and `\?\` paths and any path part with the ReparsePoint attribute
- BuildWeb: `SyncLiveData` pulls weeks.json and math_config.json from any output folder; it should run only when the output is `<repo>/tour`. Non-default arguments only
- BuildWeb: `-buildForceAdopt` prunes foreign files under `Build/` (subfolders on the adopt run, top-level ones on the next changed build). Fix: prune only files a BuildWeb build.json listed
- BuildWeb: `-buildSummary` outside the repo overwrites an existing file at that path
- Two "ERROR: Shader ... not supported on this GPU" console log lines (CoreCopy, HDRDebugView) under headless SwiftShader; check on a real GPU
- AIModule and AndroidJNIModule stubs still appear in the player's managed list despite 0.2; look at them when trimming size
