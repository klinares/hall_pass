# Questions and answers

The lead writes questions here. The advisor writes the answer under each one. Answered questions are settled; do not ask the owner again.

## 1. Text-to-speech (answered 2026-11-19)

**Question:** Unity has no built-in speech. System voices through an add-on, or pre-recorded audio per word?

**Answer:** Neither. The target is a Unity Web build, not a Windows app (see BRIEF.md, "Platform and delivery"), so use the browser's speech synthesis through a small JavaScript plugin (.jslib), with the same behavior as `world_tour/js/speech.js`: cancel before each speak, normal rate 0.8, slow rate 0.55 for the syllable hint, prefer an en-US voice. If no voice is available, show the clue instead. Pre-recorded audio is ruled out because the word list changes every week and must not need a rebuild. In the Editor, where the browser API is missing, log the word that would be spoken so play-mode tests can assert on it.

## 2. Art (answered 2026-11-19)

**Question:** Import the web SVG as vector art, convert to sprites, or redraw?

**Answer:** Convert to sprites. Render each sticker, the map pieces and every dog pose and outfit layer from `world_tour/art/` to transparent PNG at 2x the largest size it appears at 1920x1080, using the Playwright install already in `world_tour/tests/e2e`, and write the export as a repeatable script under `world_tour_unity/Tools/`. Keep the dog's layers separate so outfits and poses can be animated. Do not add the Vector Graphics package. Do not redraw or use generated or downloaded art without asking.

## 3. Word lists (answered 2026-11-19)

**Question:** One JSON file both games read, or a separate copy for Unity?

**Answer:** Unity gets its own JSON file under StreamingAssets, loaded at start, with the same fields as `world_tour/data/weeks.js`. Do not change the web game to read it; `world_tour/` stays untouched. Add a small script under `world_tour_unity/Tools/` that converts `weeks.js` to that JSON, and use it for the first import so the 22 checked words carry over exactly. Validate the file on load.

## 4. Save data (answered 2026-11-19)

**Question:** Fresh save, or import her web progress?

**Answer:** Fresh save. She has not played the web version enough to have progress worth carrying. Do build the export and import of Unity's own save described in BRIEF.md.

## 5. Screen layout (answered 2026-11-19)

**Question:** One 1366x768-style layout instead of four sizes?

**Answer:** One landscape layout, designed at 1366x768 and scaling up cleanly to 1920x1080. The browser window can be resized, so it must not break between those sizes or at 16:10. Test at 1366x768 and 1920x1080.

## 6. Whether to write world_tour_unity/PLAN.md (answered 2026-11-19)

**Answer:** Yes, but keep it short. `unity_team/BRIEF.md` is already the contract and settles platform, architecture, phases and rules; PLAN.md should only add what the brief leaves open: the folder and assembly layout, the UI system choice with the reason, the scene list, the sprite export pipeline, and the task breakdown for phases 0 and 1. Adding Unity's Pipeline package for Editor control is approved. Do not wait for approval of the plan; start phase 0 when it is written.
