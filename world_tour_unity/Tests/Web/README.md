# Web build tests

All of these test the 3D game in `world/` in a real (headless) Chrome (the smoke test and the blank-canvas guard also run in Firefox, see below), with the Playwright pinned in `Tests/Web/package.json` (1.65.1 in the lockfile; run `npm ci --prefer-offline` once in this folder; `node_modules` is git-ignored; the browsers come from the Playwright cache in the user profile). Run them from the repo root after a build; `--gpu d3d11` uses this machine's GPU (fps floors are only meaningful that way). `server.mjs` is a plain static server (Content-Type and Content-Length only, like GitHub Pages); `node world_tour_unity/Tests/Web/server.mjs world` plays the build locally.

- `world_smoke.mjs [--gpu d3d11]`: loads `world/` at 1366x768 and 1920x1080, waits for `WT_WORLD_READY`, moves by keyboard and by click, measures fps, plays a gig and a show, visits places and the wardrobe (uses `world_places.mjs` and `world_wardrobe.mjs`).
- `world_places.mjs`: the place-by-place checks used by the smoke test (also runnable alone).
- `world_gpu.mjs`: GPU, context-loss and memory checks.
- `world_stability.mjs`: nothing gets stuck, no frame hitches.
- `world_tour.mjs`: the whole tour in legs; with `--state <file>` it writes the save it ended with.
- `world_warm_load.mjs`: first and second load times.
- `world_wardrobe.mjs`: the wardrobe panel.
- `world_old_save.mjs [--state <file>]`: a save made before the 2D game was removed (`fixtures/old_save_v1.json`, a real `worldtour.v1` blob from the build that shipped before 9.2) loads in `world/` with the same fans, venues, words, inventory and place, keeps its version and key, and a blocked-storage run still plays.
- `world_audio.mjs [--label before|after] [--seconds 60] [--places ...] [--birds-only] [--gpu d3d11]`: sound levels per place (phase 10A). Walks for 60 s in each place while the game plays every one-off sound once, and logs peak and RMS in dBFS per source (the game's own meter, `AudioMeter`, only on with `?wt_test`) plus the real mix on the browser's audio output (a pass-through tap on the AudioContext destination). `--label after` also fails on any source over -12 dBFS (applause and the end-of-show crowd excepted), a mix that reaches full scale, a murmur or bed, or birds anywhere but coffee and festival; `--birds-only` watches coffee and festival for 130 s and checks the gaps between chirps are 20 to 40 s. Results go to `Temp/world-audio-<label>.json`.
- `world_blank_guard.mjs [--browser chromium|firefox] [--gpu d3d11]`: the page's WebGL request (Chrome and Edge ask for `high-performance`, Firefox for the default), the page title, and the guard that shows a reload note if the game has not said `WT_WORLD_GAME_READY` 75 s after loading.

Unity clears `Temp/` when the Editor quits, so copy any results file you want to keep.

## Firefox

Playwright's Firefox is installed with the same `npm ci` (`npx playwright install firefox` if the cache has no Firefox yet). It draws with Firefox's own WebGL, so run it without `--gpu`:

```
node world_tour_unity/Tests/Web/world_smoke.mjs --browser firefox
node world_tour_unity/Tests/Web/world_blank_guard.mjs --browser firefox
```

`world_smoke.mjs --browser firefox` loads `world/` at both window sizes, moves by keyboard and click, measures fps (headless Firefox on software GL runs well under Chrome's numbers, so judge it by "no console errors, the canvas draws, she moves", not by the fps floor) and visits the places. The same smoke test in Chrome is `--gpu d3d11` on this machine. Chrome behaviour must not change: `world_blank_guard.mjs` (no `--browser`) checks Chrome still asks for the high-performance GPU.

A black screen that only the owner's Firefox shows could not be reproduced here (Firefox 159 on an Intel GPU draws the local and the published build). The page therefore asks for the default GPU on Firefox and shows a reload note if the game never reports ready.
