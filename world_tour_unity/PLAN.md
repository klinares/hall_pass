# world tour (unity): plan

`unity_team/BRIEF.md` is the contract and settles platform, architecture, phases and rules. This file only covers what the brief leaves open: folder and assembly layout, the UI system, scenes, the sprite pipeline, and the tasks for phases 0 and 1. Answers to settled questions are in `unity_team/QUESTIONS.md`.

## Baseline

- Editor **6000.3.29f1** (from `ProjectSettings/ProjectVersion.txt`), installed at `D:\Unity\Editors`. 2D URP template. Target: Web (desktop Chrome/Edge). Output goes to `tour/` at the repo root.
- Editor control through the Pipeline package (`com.unity.pipeline`, approved in Q6) and the `unity` CLI.

## UI system: uGUI (with TextMeshPro)

The look is sprite stickers at slight rotations, hard offset shadows, squash-and-spring on every press, particles over the UI, and a scrolling map that sits alongside world-space sprites (the dog, the crowd). In uGUI that's ordinary RectTransform rotation and scale, sprite Images, Canvas sorting with particles and sprites, and per-element tweens. UI Toolkit can do much of this through USS transforms, but its sprite and particle mixing and per-element motion work against this style. uGUI is already in the project, so no package is added. The choice is recorded in `unity_team/DECISIONS.md` in phase 0. Text uses TextMeshPro, with Caveat Brush and Courier Prime bundled as SDF font assets.

## Folders and assemblies

```
Assets/
  Core/            WorldTour.Core.asmdef   plain C#, noEngineReferences: true
    Math/          Rational, Money, Answer, Mistakes, Rng, Config
    Math/Gen/      Decimals, MultDiv, Fractions, Coords, Volume, OrderOps, Fluency, Guard
    Show/          ShowEngine, Scoring, LevelRules
    Progress/      Progress, Review, Meta (shop, milestones), SaveBlob (versioned JSON)
    Data/          Week/Word models, WeeksParser + validation, Venues, Shop, Milestones
  Game/            WorldTour.Game.asmdef    refs Core; scenes, UI, motion, audio, input
    Boot/ Screens/ Modes/Spelling/ Modes/Math/ Ui/ Motion/ Audio/ Speech/ Save/
  Plugins/WebGL/   speech.jslib, storage.jslib (localStorage, with an in-memory fallback)
  Art/Sprites/     exported PNGs (generated, see below)
  Fonts/
  Scenes/
  StreamingAssets/ weeks.json, math_config.json
  Tests/
    EditMode/      WorldTour.Core.Tests.asmdef  (Core only, fixed seeds)
    PlayMode/      WorldTour.Game.Tests.asmdef
Tests/Web/         Playwright scripts + static server (no custom headers)
Tools/             export_sprites.mjs (Playwright, from world_tour/art/), weeks_to_json.mjs
```

## Scenes

1. `Boot`: loads weeks.json and math_config.json, the save and the fonts, then shows a "click to start" card that unlocks audio and speech.
2. `Main`: one scene with a screen stack: map (home), venue card, show (one prefab per mode, loaded into the show frame), ticket, backstage, journal, office. Screens swap with transitions inside one scene, so there are no scene loads mid-flow and input is never blocked.

Phase 0 only adds `Boot`, empty apart from a visible label.

## Sprite pipeline

`Tools/export_sprites.mjs` uses the Playwright install in `world_tour/tests/e2e` to load the SVG from `world_tour/art/` (read-only). It renders every sticker, map piece, dog pose and outfit layer to transparent PNG at 2x the largest size it appears at 1920x1080, and writes them to `Assets/Art/Sprites/<group>/`. A manifest JSON lists each sprite with its size and pivot. The dog's layers stay separate. The script is repeatable: running it again overwrites the PNGs. Import settings (no mipmaps, bilinear, sprite atlas per group) come from an Editor script, not hand edits. Built in phase 2.

## Phase 0 tasks (pipeline)

0.1 Confirm the editor version and the Web module (blocked until installed), and switch the build target to Web.
0.2 Add `com.unity.pipeline`. Remove unused packages and modules (visual scripting, multiplayer center, collab proxy, aseprite, psd importer, cloth, terrain, vehicles, vr, xr, wind, ai, umbra, 3D physics, android jni, analytics, video). Record the list in DECISIONS.md.
0.3 Web player settings: compression Gzip with decompression fallback, a minimal template, 1366x768 default canvas, no data caching problems on a static host. Record the choice.
0.4 The `Boot` scene with a single label. Core, Game and both test asmdefs, plus one edit-mode test (`Rational` add or a placeholder Core class).
0.5 A build script (`Editor/BuildWeb.cs`) that writes the Web build to `tour/`.
0.6 `Tests/Web/`: a plain static server and one Playwright test that loads `tour/`, waits for the Unity canvas, fails on any console error, and records the load time and download size.
0.7 Tester runs the definition of done. Write DECISIONS.md and REPORT.md, then commit locally.

## Phase 1 tasks (core)

Each task ports its JS and its Node tests together into EditMode tests.

1.1 `rng.js` (seeded, with the same sequence as JS for cross-checks), `rational.js`, `money.js`.
1.2 `answer.js` (parsing and equivalent forms), `mistakes.js`, `config.js` → `math_config.json`.
1.3 Generators: decimals, multdiv, fractions, then coords, volume, orderops, fluency, plus `guard.js`. Port `generators.test.mjs`. The tester adds fixed-seed runs of a few thousand per level and a JS-vs-C# sample comparison.
1.4 `show.js`: queue, re-inserted misses (3 later), encore, scoring, level up/down.
1.5 `progress.js`, `review.js` (stores the full problem), the save blob with a version and migrations. Port `core.test.mjs` and `review.test.mjs`.
1.6 `meta.js` with `data/shop.js`, `milestones.js` and `venues.js`. Port `meta.test.mjs`.
1.7 `Tools/weeks_to_json.mjs` → `StreamingAssets/weeks.json` (22 words), plus the C# parser with validation and readable errors.
