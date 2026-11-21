# World Tour (Unity)

The Unity version of the World Tour spelling and math game. The finished web build lives in `tour/` at the repo root and is what GitHub Pages serves.

## Try it locally

From the repo root:

```
node world_tour_unity/Tests/Web/server.mjs tour
```

Open the address it prints in Chrome and press start.

## Adding a week of words (no rebuild needed)

The game reads its word list at start-up, with caching turned off, so a new week shows up on the next visit without rebuilding.

1. Edit `tour/StreamingAssets/weeks.json`. This deployed copy is the master. Put the new week first, inside `"weeks": [ ... ]`. The fields are described in `Tools/WEEKS_FORMAT.md`.
2. Check it:

   ```
   node world_tour_unity/Tools/validate_data.mjs
   ```

   It prints `ok`, or a numbered list of problems. The game refuses a file that has errors, so fix them first.
3. Commit `tour/StreamingAssets/weeks.json` and push.

`tour/StreamingAssets/math_config.json` works the same way.

## Rebuilding after a code or art change

You need Unity 6000.3.29f1 with Web build support. From the repo root, run:

```
"D:\Unity\Editors\6000.3.29f1\Editor\Unity.exe" -batchmode -nographics -projectPath world_tour_unity -buildTarget WebGL -executeMethod BuildWeb.Build -buildSummary "%TEMP%\buildweb-summary.json" -logFile -
```

- The build takes several minutes and writes into `tour/`. It first copies the live `weeks.json` and `math_config.json` back into the project, so a rebuild never loses a week you added.
- Unity can crash while shutting down after a good build, so ignore the exit code. Instead, check that the summary file (`%TEMP%\buildweb-summary.json`) says it succeeded and that `tour/build.json` was just updated.
- Then run the browser tests: `node world_tour_unity/Tests/Web/run.mjs`.
- Commit `tour/` and push.

## Running the tests

- **Edit-mode and play-mode:** run Unity with `-runTests -testPlatform EditMode` or `PlayMode` and `-testResults <file>`. Judge the run by the counts in the results file, not by the exit code.
- **Browser:** `node world_tour_unity/Tests/Web/run.mjs` (needs a fresh build in `tour/`).

## Layout

- `Assets/Core`: game rules with no Unity dependency (words, hints, math generators, save).
- `Assets/Game`: screens, modes, UI and the show.
- `Assets/Editor`: builders that make the prefabs, plus `BuildWeb`.
- `Assets/Tests`: edit-mode and play-mode tests.
- `Tests/Web`: the browser tests.
- `Tools`: data checkers and the JS cross-check vector generators.
