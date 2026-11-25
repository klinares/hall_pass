# Lead's to-do list

Kept on disk so a new session can resume from here (see the FYI in ADVISOR.md, 23:20). Newest status at the top of each phase.

## Open

- [x] 8.5 repo clean-up done (see REPORT.md); the lead commits it by path as its own commit. Finished phases 0 to 8 are in HISTORY.md.
- Look shots for the advisor (ADVISOR 23:22 and 04:35): unity_team/shots/phase8/<place>_1366.png and _1920.png (GPU flag, girl + crew in frame), plus coffee_gig_* and coffee_show_*; refresh each place's pair whenever its look changes. A look correction for coffee applies to the shared kit and every place.
- Keep: tour/ untouched and BuildWeb.Build unchanged; md MAX_LEVEL 0 (advisor 18:00); budgets 60 fps, <=60 MB, no file >50 MB, first place <10 s warm.

## Carried forward

- Phase 1, tasks 1.5 and 1.7: exercise weeks.json, math_config.json and the save blob in the built web player (High stripping); use JsonUtility or [Preserve]/link.xml (ADVISOR 23:37 #1)
- Phase 1: browser test that an edited weeks.json shows on reload (ADVISOR 22:35 #2). The template already fetches StreamingAssets with no-store
- Phase 2: make Caveat Brush and Courier Prime the TMP defaults; remove LiberationSans and EmojiOne (~1.7 MB)
- Every REPORT.md: build size; 20 MB ceiling for the finished game (raise with the advisor before crossing)
- Optional size cut kept in reserve: clear the 2D renderer's post-processing data (~2.5 MB film grain and SMAA). Not done, so bloom stays available for phase 2
- DECIDED (ADVISOR 00:12): the deployed `tour/StreamingAssets/weeks.json` and `math_config.json` are the source of truth. BuildWeb syncs them back into `Assets/StreamingAssets/` before building (being added in 0.5 now). Phase 1 (1.7): a validator runnable from the terminal on the deployed file. Phase 6 README: three lines saying "edit the deployed copy, or delete it so the next build recreates it"
- For the phase 0 report (ADVISOR 00:45 #2): the folder "deleted" in the 0.5 test was `canary_dir`, a throwaway the tester created in its own scratchpad under the system temp directory (canary.txt, sub/deep.txt). No owner files were touched. From now on, destructive-probe tests use only throwaway folders they create under the system temp directory
- Fix-round cap from phase 1 on (ADVISOR 00:45 #1): at most 2 fix rounds per task for anything that isn't a correctness or data-loss defect; the rest goes under "polish later"
- Dates: the web player may report UTC from DateTime.Now. Builder 3 is adding clock.jslib + Game/Util/LocalClock.TodayIso() (browser local date); Boot, the day streak and week opening must use it, never DateTime.Now in the player. Builder 2 has a "// LocalClock" seam to switch
- Phase 5 import screen (1.5 tester D2): Preview must also list level drops, the review queue, word stats and history changes, or back up the current save before Replace. Phase 2/5 (D6): 500 ms save throttle; save on pagehide and visibilitychange; a reset method (WT_StorageRemove exists)
- Phase 2 Game layer, from the 1.4 tester: ShowEngine throws InvalidOperationException on Win/Miss/Reveal twice or after Finished, so the UI must check Resolved/Finished first, since a double-click would throw and fail the browser tests (or add TryWin). Call SetupHints once per song. Collect venue unlocks and stickers from FansAwarded for the ticket screen ("new venue unlocked")
- Morning priority: a playable phase 2 (map + remix in `tour/`) over starting phase 3

## Polish later

- BuildWeb: alias paths (UNC `\localhost\D$\...`, `\?\`, junctions, symlinks) get past the "inside the repo" checks, because the comparison is string-only. They need deliberate, non-default arguments. Fix: refuse UNC and `\?\` paths and any path part with the ReparsePoint attribute
- BuildWeb: `SyncLiveData` pulls weeks.json and math_config.json from any output folder; it should run only when the output is `<repo>/tour`. Non-default arguments only
- BuildWeb: `-buildForceAdopt` prunes foreign files under `Build/` (subfolders on the adopt run, top-level ones on the next changed build). Fix: prune only files a BuildWeb build.json listed
- BuildWeb: `-buildSummary` outside the repo overwrites an existing file at that path
- Two "ERROR: Shader ... not supported on this GPU" console log lines (CoreCopy, HDRDebugView) under headless SwiftShader; check on a real GPU
- AIModule and AndroidJNIModule stubs still appear in the player's managed list despite 0.2; look at them when trimming size
- ExportCode: WT0- says "newer version", WT10- says "doesn't look like a code"; a zero-width space inside a pasted code is rejected (1.5 tester D3\/D4)

- 2026-11-20 03:22-04:20: a usage limit stopped all agents mid-task. They resumed at 04:21 from where they stopped (the phase 1 DoD tester from step 2, builder 6 from the 2.2 compile check, builder 5 from the 2.3 rig). No Unity process was left running
- Ticket barcode is often nearly empty because of the web game's float rounding (ported exactly; 38 of 210 seeds draw no bars): consider a fuller barcode as a deliberate change. End-of-show confetti burst (show.js:230) has no particle helper yet. Shout\/stroke materials use fixed ink (won't follow peach-navy)
- Linear colour space: translucent CSS colours look darker than on the web (8% white over black shows about 31% grey); check every screen (builder 6)
- **Phase 2 fix round (after the 2.7 report): no AudioListener in Boot.unity or Main.unity, so the game is silent (73k "no audio listeners" lines in play mode). Add one to the camera in both scenes via UiBuilder** (2.7 tester)
- **Phase 2 fix round: intermittent remix freeze after a miss** (5 of 14 runs; clicks and typing both ignored, so Busy or ctx is stuck; not hit-testing). Builder 2 preparing: an attempt token for stale callbacks, Later not dropped, WT_PICK logging, race tests. Plus the AudioListener via UiBuilder
- Polish later (2.7 tester): win toast + confetti (show.js win()), end-of-show burst, a crowd that grows and reacts (brief "more game"), empty space on the show screen; low-contrast pink "rehearse again"\/"admit one" on white; mute button overlaps the map corner and ticket when scrolled; hint reached after 13-16 Tabs; big wheel events scroll only ~240 px; straight apostrophe in clue data
- For QUESTIONS #10 (2.7 tester): a show with a miss and a hint scores "first try 9\/10" plus sold out\/vip; a one-song encore also gets sold out\/vip; a clean encore removes the word from the review queue, though the brief says misses return next session too
- Polish later (2.7 re-check): drag takes its tile from the pointer at the handled frame, not the press position (a fast sideways flick can pick the neighbour); a short press can be lost under SwiftShader; the no-save notice covers the map's "rec center" label and the bottom of riff's bubble on the ticket at 1920
- Phase 3 (from the task Q tester): D1: remix "sound it out" line + glowing-tile hints spell intercept, intercede and indigenous before the reveal. Count the letters the line already showed when applying the remix floor (same class as Q13). Screenshot check that underscore runs ("pop - _ - __ - ____") don't merge into one line in the typewriter font
- D3 (open): headless 1920 occasionally drops a tile press (no WT_PICK); the lost press is on the tile beside the previous pick. Log pointer down\/up\/click with frame numbers in LetterTile; the driver should wait for each WT_PICK
- Pre-existing: soundcheck hint 3 prints "8400" without a comma where level 2 prints "5,380"; the validator adds "(words of 4 or more letters work best)" after the approved Q12 line (owner-facing)

### polish later (from the 3.3 tester)
- P1 irrational / illogical fit each other's clue and lyric lines (owner's data; same in web game): owner rewords, or the deal skips near-synonyms
- P2 letter patterns and chunk hints wrap mid-pattern in the dog's bubble
- P3 missing letters/vowels can sell a letter the first miss already told her ("starts with i" then "here's a letter: i")
- P4 bill note small grey italic, low contrast, against the last row
- P5 venue card Tab order puts the sound toggle between rows
- P6 level 3 "what happens to the 0 at the end?" when two zeros drop (900 / 100)
- P7 storage notice uses a straight apostrophe
- beat note labels and cell numbers clipped by the 58 px note row (taller row ~72 px or a numbers strip under the note)
- ticket chunk notes still light grey; seat-row icons tiny (P9 partial)
- tour budget worked sheet grows up over the 'song N of 10' counter
- reduce motion in math checked in code only
- ticket 'sold out' and stroke titles stay black in peach (shared fixed-ink material)
- NOTE (ADVISOR 20:58): new builders/testers on model sonnet; short prompts quoting the needed lines; hand-backs < 200 words; lead's model if a sonnet builder fails a task twice (log it); report Departures names the worker model.
- backstage 1366 peach: 'yours!' stamp overlaps the 'changes the whole game' caption; pink 'yours' chip low contrast
- remix peach playthrough logged 'MISSING WT_SONG i=12' though it passed: check the driver
- venue card coffee shop art stays pink and black in peach (web art uses no theme colours either; matches the web)

### polish later (from the final 6.2 tester pass, 2026-11-21; ADVISOR 02:18)
- long letter patterns in the dog's bubble shrink to a very small font at 1366 and 1920
- beat maker slot numbers in the empty hatched area are faint
- peach still shows pink art in places: the awning, the vinyl label, the map stickers
- fraction key icon is a tiny dotted glyph
- peach "rehearse again" is only marginally readable
- not checked by a tester: fast sideways drag; feel timings under 400 ms (code and tests only)
- test-driver flakes: parallel-load timeouts; office reduce-motion save once at peach 1920; truck run with no builder problem; meta WT_HITS line missed once
- map "record store" sign: the vinyl disc covers the word inside the sprite itself (Assets/Art/Sprites/map/venue_records.png); needs new art
- peach lyric card at 1366: on a short lyric the text touches the card's bottom edge
