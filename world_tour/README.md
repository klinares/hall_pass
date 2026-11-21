# world tour

A spelling and 5th-grade math game for Natalia. Static HTML, CSS and JavaScript modules: no build step and nothing to install to play it.

**Play:** https://klinares.github.io/hall_pass/world_tour/ (once this folder is committed and pushed; GitHub Pages serves it as is).

## How it plays

- **The map** is home. Lifetime fans unlock eight fictional venues; a venue changes the backdrop, crowd and ticket. Every mode is open from day one.
- **A show** is 10 questions from one mode. A miss comes back three questions later as a bonus song, and the end-of-show encore replays the misses for half fans.
- **Spelling modes:** missing letters, missing vowels, remix, fan mail, studio session, lyric writer, prefix mixer.
- **Math modes:** merch table (decimals), ticket sales (multiply/divide), beat maker (fractions), stage designer (coordinates), pack the truck (volume), tour budget (order of operations), soundcheck (facts, powers of ten).
- **Levels** (1 to 3, per mode) go up after 4 right in a row and down after 2 misses in a row. Hints never break the streak; they only earn fewer fans.
- **Backstage** sells cosmetic things for fans to spend: a second color theme, guitar skins, outfits for riff the dog, journal stickers. Total fans never go down.
- **Tour journal** is a sticker book. Milestones (first show, sold-out show, streaks, mastering a mode, venues) add stickers she can drag around the page.
- **Manager's office** (small link in the footer) is the parent view: accuracy per mode, the words and math mistakes she misses most, time played per day, and a reset with a confirmation step.

Progress is saved in the browser under one key, `worldtour.v1`, on that device only. If storage is blocked (some private modes), the game still runs and the footer says progress won't save.

## Adding a week of words

Edit `data/weeks.js` and add the new week at the **top** of `WEEKS`:

```js
{ id:"2026-11-23", label:"week of nov 23, 2026", words:[
  { w:"example", s:"ex-am-ple", h:"a clue for fan mail", x:"a sentence with ___ for lyric writer" },
```

- `id` is the test date. The newest week on or before today opens automatically.
- `s` (syllables) is the hint after a second miss. `x` is optional; lyric writer needs 4 words with one.
- `prefix: { p, root, means }` is optional; prefix mixer needs 4 words with one.
- `spelling_4_U/` has its own copy of the words. If both games stay live, add the week to both.

## Reviewing the content

After adding a week, rebuild the review sheet and read it:

```bash
node world_tour/tools/content_review.mjs --week=2026-11-23
```

That writes `world_tour/CONTENT_REVIEW.md` with the week's sentences, clues and prefixes. It also flags mechanical problems: a sentence without exactly one blank, a sentence or clue that gives the word away, syllables that don't spell the word, and too few sentences or prefixes for lyric writer or prefix mixer. Whether each sentence fits only its own word still needs a person.

Leave out `--week` for every week plus one example of every math word problem at every level (with answers and hints). Add `--out=path.md` to write somewhere else. The sheet is for reading only: delete it, or leave it out of commits.

## Choosing math topics

`math/config.js` has one switch per math skill. Set a skill to `false` to hide its mode.

## Running it locally

ES modules don't load from `file://`, so serve the repo root:

```bash
python -m http.server 8000
```

Then open http://localhost:8000/world_tour/

## Tests

Math generators and the backstage/journal/office logic (Node 20 or later):

```bash
node --test world_tour/tests/math/
```

Browser checks (dev only, git-ignored; Playwright with Chromium and WebKit). One-time setup in `world_tour/tests/e2e`: `npm install`, then `npx playwright install chromium webkit`. Run one engine and one suite at a time:

```bash
node world_tour/tests/e2e/play.mjs --only=chromium --suite=meta
```

Suites: `spelling`, `spelling-more`, `math`, `math-more`, `meta`. Engines: `chromium`, `webkit`. Each run fails on any console error and writes screenshots to `tests/e2e/screenshots/`.

## Files

- `index.html`: the page shell. `js/`: engine, saving, sound, speech, motion.
- `screens/`: map, venue, show, ticket, backstage, journal, office.
- `modes/spelling/`, `modes/math/`: one file per mode.
- `math/`: pure math (exact fractions, money in cents, answer checking) and `gen/` (one generator per skill).
- `art/`: stickers, riff, the map, all inline SVG. `css/`: tokens (themes), paper kit, components, screens.
- `data/`: weeks, venues, the backstage catalog, journal milestones.
- `tools/content_review.mjs`: builds the content review sheet.
- `PLAN.md`: the original build plan. `REPORT.md`: the latest phase report.
