# Web build tests

All of these test the 3D game in `world/` in a real (headless) Chrome, with the Playwright pinned in `Tests/Web/package.json` (1.65.1 in the lockfile; run `npm ci --prefer-offline` once in this folder; `node_modules` is git-ignored; the browsers come from the Playwright cache in the user profile). Run them from the repo root after a build; `--gpu d3d11` uses this machine's GPU (fps floors are only meaningful that way). `server.mjs` is a plain static server (Content-Type and Content-Length only, like GitHub Pages); `node world_tour_unity/Tests/Web/server.mjs world` plays the build locally.

- `world_smoke.mjs [--gpu d3d11]`: loads `world/` at 1366x768 and 1920x1080, waits for `WT_WORLD_READY`, moves by keyboard and by click, measures fps, plays a gig and a show, visits places and the wardrobe (uses `world_places.mjs` and `world_wardrobe.mjs`).
- `world_places.mjs`: the place-by-place checks used by the smoke test (also runnable alone).
- `world_gpu.mjs`: GPU, context-loss and memory checks.
- `world_stability.mjs`: nothing gets stuck, no frame hitches.
- `world_tour.mjs`: the whole tour in legs; with `--state <file>` it writes the save it ended with.
- `world_warm_load.mjs`: first and second load times.
- `world_wardrobe.mjs`: the wardrobe panel.
- `world_old_save.mjs [--state <file>]`: a save made before the 2D game was removed (`fixtures/old_save_v1.json`, a real `worldtour.v1` blob from the build that shipped before 9.2) loads in `world/` with the same fans, venues, words, inventory and place, keeps its version and key, and a blocked-storage run still plays.

Unity clears `Temp/` when the Editor quits, so copy any results file you want to keep.
