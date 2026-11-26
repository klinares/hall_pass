# Natalia's World Tour (Unity)

Natalia's World Tour is a spelling and 5th-grade math game where you walk around eight 3D places on a pop tour, talk to the crew, play gigs and shows, and earn fans. This Unity 6 project builds it as a web game into `world/` at the repo root, which GitHub Pages serves at `klinares.github.io/hall_pass/world/`.

Try it locally from the repo root: `node world_tour_unity/Tests/Web/server.mjs world`, then open the address it prints in Chrome or Edge.

## Add a week of words

1. Edit `world/StreamingAssets/weeks.json` (the master copy). Put the new week first, inside `"weeks": [ ... ]`. The fields are in `Tools/WEEKS_FORMAT.md`.
2. Check it: `node world_tour_unity/Tools/validate_data.mjs` should print `ok`.
3. Commit and push. No rebuild is needed: the game reads the file every time it starts.

## Change the math settings

Edit `world/StreamingAssets/math_config.json`, then validate and push the same way. What each switch does is in `Tools/MATH_CONFIG.md`.

## Rebuild

Needs Unity 6000.3.29f1 with Web build support. From the repo root:

```
"D:\Unity\Editors\6000.3.29f1\Editor\Unity.exe" -batchmode -nographics -projectPath world_tour_unity -buildTarget WebGL -executeMethod BuildWeb.BuildWorld -buildSummary "%TEMP%\buildworld-summary.json" -logFile -
```

The build writes `world/` and never overwrites `world/StreamingAssets/`. Unity can crash on exit after a good build, so check that the summary says `Succeeded` instead of trusting the exit code.

## Test

- Edit-mode and play-mode: run Unity with `-runTests -testPlatform EditMode` (or `PlayMode`) and `-testResults <file>`, and read the counts in the results file.
- Browser: run `npm ci` once in `world_tour_unity/Tests/Web`, then for example `node world_tour_unity/Tests/Web/world_smoke.mjs --gpu d3d11`. The other `world_*` suites are listed in `Tests/Web/README.md`.

Art, audio and font licences are in `ASSETS.md`.
