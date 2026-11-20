# Web build tests

Run from `world_tour_unity/` after a web build into `tour/`: `node Tests/Web/run.mjs`. It writes `Temp/web-results.json` (the evidence: per-test pass/fail, counts, numbers, warnings) and screenshots in `Temp/web-shots/`. Unity clears `Temp/` when the Editor quits, so copy anything you want to keep, or pass `--out` and `--shots`. It uses the Playwright already installed in `world_tour/tests/e2e/node_modules` and installs nothing. `server.mjs` is a plain static server (Content-Type and Content-Length only, like GitHub Pages); run it on its own with `node Tests/Web/server.mjs ../tour` to play the build locally.

- **cold_load_1366x768 / cold_load_1920x1080**: a fresh browser loads `tour/` and reaches `WT_BOOT_READY`, with no console or page errors and no requests outside the local server. The canvas fills the window with no scrollbars. Records bytes, the largest file and ms to ready, and takes a screenshot.
- **cached_reload**: a second visit transfers under 200 KB, because Unity's cache serves framework, data and wasm (this proves the template's cacheControl works on the real build).
- **storage_blocked**: with `indexedDB.open` and `localStorage` throwing, the game still starts with no console errors.
- **broken_build**: a temp copy with the wasm deleted shows the lowercase failure message with one short detail line, not a blank page or Unity's banner.
- **size_budget**: the download must be under 20 MB (`--budget-mb`). Over 12 MB, or a cold load over 8 s, is recorded as a warning.
