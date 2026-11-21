# World Tour (Unity): report

## For the owner

- **`world_tour/` is ready for you to commit.** It's five files, unstaged:
  - js/show.js
  - math/gen/fluency.js
  - math/gen/multdiv.js
  - math/gen/volume.js
  - tests/math/generators.test.mjs

  Math tests pass 41/41, and the Chromium math suites pass 9/9 and 28/28.
- **Please listen once** to the game's sound and the studio session voice.

## Phase and status

Phase 5 (backstage, journal, manager's office, second theme, export and import) is **done** and committed locally. Next is phase 6:
1. The advisor's irrational and illogical clue rewording.
2. A polish pass over everything a player sees, including peach stickers, venues and riff.
3. The final build and the README.

## What now works

- **Backstage shop:** buy with fans using a two-tap buy, and try items on first.
- **Peach and navy theme:** it re-skins every screen and survives a reload.
- **Tour journal:** a paged sticker book. New stickers peel on, and she can move them by drag, by click or with the arrow keys.
- **Manager's office:**
  - parent stats and time played;
  - "keep a copy" (save file or code) and "load a copy" (file or paste, with a preview and a confirm);
  - "start over" with two confirms.

  Before an import or a reset, the current save is backed up. A save from the web game, copied into a file, also loads.
- **Saving:** old saves upgrade to the new version, and fields the game doesn't know are kept.

## Test results

- Edit-mode 1661/1661, play-mode 23/23, browser 18/18. 0 compile warnings.
- Playthroughs: shop and journal 4/4, office 8/8 (both themes, both sizes, mouse and keyboard), and a remix show in peach. 0 console errors.
- Download 13.05 MB (budget 20 MB).

## What the tester looked at

Every new screen in both themes at both sizes. A truncated import, a purchase without enough fans, reload persistence, and the reset confirms.

## Not done or not verified

- **Polish list (phase 6):**
  - stickers, venues and riff stay pink in peach;
  - backstage's "yours!" stamp overlaps a caption at 1366;
  - the ticket's "sold out" stays black in peach.
- **Unverified:** a full save hitting the browser's storage quota; sound by ear.

## Departures from the brief

- Two builds each for 5.1 and 5.2 (one fix round each).
- The first phase 5 builders were stopped and restarted.
- From 21:00 the workers ran on Sonnet, per the advisor; builders before that ran on Opus.
- Departures 35–38 are listed in IntendedDifferences.md.

## Open questions

None for the owner. Questions go to the advisor in QUESTIONS.md.

## How to try it

From the repo root, run `node world_tour_unity/Tests/Web/server.mjs tour`, open the address in Chrome, and press start.

## Files changed

- **`world_tour_unity/Assets/`:**
  - Core/Meta and Core/Progress/SaveTransfer
  - Game/Meta, Game/Office, Game/Save (PlayTimer), Ui (ThemeSkin)
  - the peach decor
  - Editor builders and tests
- **`world_tour_unity/Tests/Web/` and `Tools/`.**
- **`tour/`** (rebuilt) and **`unity_team/`**.
