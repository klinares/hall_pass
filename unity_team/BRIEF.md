# World Tour (Unity): team brief

This is the contract for the Unity build. If something here conflicts with the web game, this file wins. If something here is silent, the web game in `world_tour/` is the reference.

## What we are making

A learning game for Natalia, age 11, 5th grade. She is the headliner of a fictional pop tour. Spelling and math mini-games earn fans; fans unlock venues and cosmetic rewards. A complete web version exists in `world_tour/` and stays untouched. Read `world_tour/README.md` and `world_tour/PLAN.md` before planning anything.

The Unity version exists to make it feel like a real game: richer animation, a living tour map, a mascot with personality, more satisfying feedback. It must stay easy to pick up and it must teach. Where "more game" and "more learning" pull apart, learning wins.

## Platform and delivery

- Unity 6 LTS, 2D, Universal Render Pipeline. Project at `world_tour_unity/`.
- Target: Unity Web build, played in a desktop browser (Chrome or Edge) on a laptop that is a different machine from this one. Keyboard, mouse and trackpad. Minimum window 1366x768, landscape. No mobile, no touch-only design.
- Hosting: GitHub Pages, which serves this repo's folders as static files. The build output goes in `tour/` at the repo root, so the link will be `klinares.github.io/hall_pass/tour/`.
- GitHub Pages cannot add response headers. Choose build compression settings that work on a plain static host (decompression fallback, or compression off), and prove it by loading the build from a local static server with no custom headers.
- No single file over 50 MB in the build, and a first load under about 15 seconds on home wifi. Keep the download small: strip unused packages and engine modules.
- If the project was created for Windows only, add the Web build module and switch the target.

## Hard rules

- No real artists, songs, lyrics, album art or logos. The pop world is fictional.
- No ads, purchases, analytics, accounts or network calls. A private family game.
- No downloaded or generated third-party assets without asking the owner first. Art comes from the existing vector art in `world_tour/art/` (convert it to sprites) or is drawn by the team in the same style.
- Never modify `world_tour/` or `spelling_4_U/`. Never push to GitHub. Never change Pages settings.
- Commit locally at the end of each phase once the tester passes it, with a clear message. Local commits only.

## Architecture

- **Core** assembly: plain C# with no UnityEngine references. Holds the math generators, exact fractions, money in integer cents, answer parsing and equivalence, scoring, level rules, the show engine (queue, re-inserted misses, encore), progress and the review queue. Ported from `world_tour/math/` and `world_tour/js/show.js`, `progress.js`, `review.js`. Seeded random numbers so tests repeat.
- **Game** assembly: scenes, UI, animation, audio, input. Calls Core.
- **Tests**: edit-mode for Core (port the Node tests in `world_tour/tests/math/`), play-mode for full shows, browser tests for the web build.
- Pick one UI system (UI Toolkit or uGUI) in phase 0, record why in `DECISIONS.md`, and use only that one.
- **Word data stays one editable file.** Put the weeks in a JSON file under StreamingAssets with the same fields as `world_tour/data/weeks.js` (`id`, `label`, `words` with `w`, `s`, `h`, optional `x` and `prefix`). The game loads it at start, so the owner adds a week by editing that one file in the deployed `tour/` folder, with no rebuild. Validate the file on load and show a readable message if it is malformed.
- **Math skill switches** stay in one config file, also loaded at start.
- **Saving**: one versioned JSON blob. It must survive a page reload in the web build. Include export and import of progress as a small text code or file, since browser data can be cleared. The game must stay playable if storage is blocked.
- **Read-aloud** (studio session): Unity has no built-in speech. In the web build, call the browser's speech synthesis through a small JavaScript plugin. If no voice is available, show the clue instead, as the web game does.
- **Sound**: no downloaded audio. Generate the clips in code at start-up, as the web game synthesizes its sounds. Check Unity's web audio limits before choosing the method. Sound starts only after the first click, and there is a remembered mute toggle.

## Look and feel

Pop-punk tour scrapbook: torn paper, washi tape, halftone dots, polaroid frames, ticket stubs, thick-outlined stickers with a white border, slight rotations, hard offset shadows. Default palette: baby pink #fdb1cb, deeper pink #f98bb0, black, white, lilac #a8a9fc, ink blue #2f2fc1. Second theme in peach and navy, unlockable. Display text in a brush-script face, body and numbers in a typewriter face, all lowercase (the web game uses Caveat Brush and Courier Prime, both open-licensed; bundle the font files). Voice: short, lowercase, a little dry, never babyish.

Everything is squishy: buttons squash and spring, correct answers pop, stickers peel and slap down, wrong answers wobble and never flash red. Animations under 400 ms and never block input. Honor a reduce-motion toggle in settings.

What Unity should add over the web game: the mascot dog animated and reacting throughout; a tour map that scrolls and comes alive as venues unlock; a crowd that grows and reacts during a show; particle bursts and screen transitions with weight. Stacked fractions must be drawn as real fractions, large and unambiguous.

## Content to port (all of it)

- Seven spelling modes: missing letters, missing vowels, remix, fan mail, studio session, lyric writer, prefix mixer.
- Seven math modes: merch table (decimals), ticket sales (multiply and divide), beat maker (fractions), stage designer (coordinates), pack the truck (volume), tour budget (order of operations), soundcheck (facts, powers of ten). Beat maker, stage designer and pack the truck are hands-on builders, not multiple choice.
- Eight venues with the fan thresholds in `world_tour/data/venues.js`, backstage shop, tour journal sticker book, manager's office parent view with a confirmed reset.
- Mouse dragging must also work as click-to-pick then click-to-place, and typed answers must work from the keyboard alone.

## Learning rules (do not weaken these)

- A wrong answer never just says wrong. Hint ladder: nudge, then a partial step, then a fully worked solution with a visual model, then move on.
- Hints cost some fans and never break a streak.
- Scoring, level-up (4 right in a row) and level-down (2 misses in a row) match the web game exactly.
- A missed item returns three questions later and again in the encore and the next session. The review queue stores the full problem, not a seed.
- No countdown timers except soundcheck, where time only affects bonus fans.
- Numeric answers accept equivalent forms unless the problem asks for simplest form and says so.
- Every generated problem has exactly one correct answer, distractors that model real mistakes, and numbers a 5th grader can work without a calculator.
- Nothing she has earned is ever taken away.

## Phases

0. **Pipeline.** Confirm the Editor version, choose the UI system, and get an empty scene building for the web, served locally, loading in a browser with no console errors, with one passing edit-mode test and one passing browser test. Nothing else until this works end to end.
1. **Core.** Port the pure logic and its tests.
2. **Look and feel.** Design system, sticker sprites, the mascot, the tour map home, and one complete spelling mode (remix) with the ticket screen.
3. **Spelling.** The other six modes, saving, encore.
4. **Math.** The seven modes, starting with merch table and beat maker.
5. **Meta.** Backstage, journal, manager's office, second theme, export and import.
6. **Ship.** Final web build into `tour/`, size and load-time check, a README section on adding a week of words and rebuilding.

## Definition of done, for every phase

- Compiles with zero errors and zero new warnings.
- All edit-mode, play-mode and browser suites pass, run in the foreground.
- The tester has looked at screenshots of every new screen at both sizes.
- The web build loads from a plain static server with no console errors.
- `unity_team/REPORT.md` is rewritten (format in `KICKOFF.md`).
