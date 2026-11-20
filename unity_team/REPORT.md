# World Tour (Unity): report

## Phase and status

Phase 0 (pipeline) is **done** and committed locally. Phase 1 (core logic) starts next.

## What now works

- Unity 6000.3.29f1 project, Web target, uGUI with TextMeshPro. Unused packages and engine modules removed.
- A `Boot` scene: "world tour" on the palette pink. The text comes from the plain-C# Core assembly, which proves Core runs in the web player.
- `BuildWeb.Build` builds into `tour/` with one command:
  - It stages first and keeps the previous build's files, so a page Pages cached still loads after a redeploy.
  - It writes only into folders it owns.
  - It never overwrites the deployed `weeks.json` or `math_config.json`. Those are the master copies; each build copies them back into the project.
- Gzip with decompression fallback works on a plain static host, and repeat visits load the engine from the browser cache.

## Test results

- Edit-mode tests: 1 run, 1 passed, 0 failed. Play-mode tests: 0 (valid, none written yet).
- Browser tests on the real build, served by a plain static server: 6 run, 6 passed, 0 failed. They cover:
  - cold load at 1366x768 and 1920x1080
  - cached reload
  - storage blocked
  - broken build shows the failure message
  - size budget
- Compile: 0 errors, 0 warnings.
- Download: **11.81 MB** total; largest file is the wasm, 7.36 MB.
- Cold load to the title showing: 0.6–1.3 s. That's localhost in a headless browser; on home wifi the download adds about 5 s at 20 Mbit/s.
- Repeat visit: 52 KB.

## What the tester looked at

Boot screenshots at both sizes (centred, no scrollbars, plain as a placeholder should be), the failure screen, a code review of the build script and test runner, and deliberate failures proving each test can fail.

## Not done or not verified

- Not checked on the other laptop, a real GPU, Edge, or real GitHub Pages.
- No clean build from an empty Library this phase. Earlier clean builds took 4–5 minutes.
- Unity crashes on shutdown after some batch runs (D1), always after the work is saved. Runs are judged by their results files, never exit codes.
- Build-script hardening stopped at the advisor's request. Four edge cases needing unusual arguments are in `TODO.md` under "Polish later".
- The "deleted folder" in a 0.5 test was `canary_dir`, a throwaway the tester made in its own temp folder. No owner files were touched.

## Departures from the brief

None.

At the advisor's request, `world_tour/` was copied to `D:\repos\_backups\world_tour_2026-11-20\` (692 files, checksums match, `tests/e2e/node_modules` skipped). Nothing in `world_tour/` was changed, staged or committed.

## Open questions

None.

## How to try it

- From the repo root, `node world_tour_unity/Tests/Web/server.mjs tour`, then open the printed address in Chrome.
- Rebuild: Unity `-executeMethod BuildWeb.Build`. Tests: `node world_tour_unity/Tests/Web/run.mjs`.

## Files changed

- `world_tour_unity/`: Core, Game, Tests and Editor/BuildWeb.cs under Assets; Boot scene; WebGL template; TextMesh Pro essentials; Tests/Web; settings; packages.
- `tour/` (the build), `unity_team/` and `.claude/agents/`.
