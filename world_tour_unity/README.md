# World Tour (Unity)

The Unity version of the World Tour spelling and math game. The 3D game is `world/` at the repo root, a web build that GitHub Pages serves.

## Try it locally

From the repo root:

```
node world_tour_unity/Tests/Web/server.mjs world
```

Open the address it prints in Chrome and press start.

## Adding a week of words (no rebuild needed)

The game reads its word list at start-up, with caching turned off, so a new week shows up on the next visit without rebuilding.

1. Edit `world/StreamingAssets/weeks.json`. This deployed copy is the master. Put the new week first, inside `"weeks": [ ... ]`. The fields are described in `Tools/WEEKS_FORMAT.md`.
2. Check it:

   ```
   node world_tour_unity/Tools/validate_data.mjs
   ```

   It prints `ok`, or a numbered list of problems. The game refuses a file that has errors, so fix them first.
3. Commit `world/StreamingAssets/weeks.json` and push.

`world/StreamingAssets/math_config.json` works the same way.

## Rebuilding after a code or art change

You need Unity 6000.3.29f1 with Web build support. From the repo root, run:

```
"D:\Unity\Editors\6000.3.29f1\Editor\Unity.exe" -batchmode -nographics -projectPath world_tour_unity -buildTarget WebGL -executeMethod BuildWeb.BuildWorld -buildSummary "%TEMP%\buildworld-summary.json" -logFile -
```

- The build takes several minutes and writes into `world/`. It first copies `world/StreamingAssets/weeks.json` and `math_config.json` into the project and never overwrites them, so a rebuild never loses a week you added. The summary's `dataProof` shows the two files were identical before and after.
- If `world/StreamingAssets/` is missing the build stops with an error; there is no fallback copy.
- Unity can crash while shutting down after a good build, so ignore the exit code. Instead, check that the summary file says `Succeeded` and that `world/build.json` was just updated.
- Then run the browser tests, for example `node world_tour_unity/Tests/Web/world_smoke.mjs --gpu d3d11`.
- Commit `world/` and push.

## Running the tests

- **Edit-mode and play-mode:** run Unity with `-runTests -testPlatform EditMode` or `PlayMode` and `-testResults <file>`. Judge the run by the counts in the results file, not by the exit code.
- **Browser:** `node world_tour_unity/Tests/Web/world_smoke.mjs --gpu d3d11` (see `Tests/Web/README.md` for the other suites).

## Layout

- `Assets/Core`: game rules with no Unity dependency (words, hints, math generators, save).
- `Assets/Game`: screens, modes, UI and the show.
- `Assets/Editor`: builders that make the prefabs, plus `BuildWeb`.
- `Assets/Tests`: edit-mode and play-mode tests.
- `Tests/Web`: the browser tests.
- `Tools`: data checkers and the JS cross-check vector generators.
