# World Tour: review queue, WebKit coverage, content fixes

## 1. Phase and status
All five requested items are done. Nothing is committed or pushed. `spelling_4_U/` is unchanged.

## 2. What changed
- **Review queue:** missed problems are saved whole (prompt, answer, choices, hints, mode data). They come back exactly as she saw them, even after a generator changes. The logic is in `js/review.js`. Old seed-only saves still work.
- **WebKit coverage:** WebKit now runs the same plan as Chromium. Both engines picked up the two checks that only WebKit used to run.
- **Poster boxes:** real sizes (12, 24, 25, 48, 50, 100, 144), at every level.
- **Division hint:** uses partial quotients.
  1. How many tens of the divisor fit?
  2. Subtract, give the running total, and ask what fits in the rest.
  3. The ones, then the final total.

  The worked chunks on screen show the total too.
- **Review sheet:** `tools/content_review.mjs`, documented in the README. `--week=<id>` reviews one week and flags mechanical problems (a missing blank, a word given away, syllables that don't match).

## 3. Test results
- Unit tests: **41/41** (5 new for the review queue; the ticket-sales test now checks box sizes and hints).
- Playwright: 10 separate foreground runs (10-minute timeout), **198/198, no console errors**. Each engine ran meta 21, spelling 9, spelling-more 32, math 9, math-more 28.
- The slowest run was WebKit spelling-more, at 4 min 47 s.

## 4. What I looked at myself
- A division hint at phone width (in a screenshot).
- Edge-case hints: answers under 10, exact tens, and 10 to 19.
- The regenerated sheet.

## 5. Not done or not verified
- WebKit had never run storage off, reduced motion, map to ticket, the level 3 full show, or meta at landscape and laptop. All of them pass now.
- Still not done: a real iPad, and hearing the beat sound.
- For exact tens, the hint starts one ten short ("start with 7 tens of 7") so it doesn't hand over the answer. Check that wording.

## 6. Departures
- Level 3 boxes are either a 144 box, or a 2-digit box with 100+ boxes. 100 is left out at level 3 because ×100 is too easy.
- The sheet notes that the Oct 10 week has no prefixes, so prefix mixer is hidden that week. That's existing data, not a bug.

## 7. Decisions needed
1. Keep realistic poster boxes at levels 1–2? I recommend yes.
2. Prices? I recommend a week of real play first.
3. A passcode on the office? I recommend no.
4. The dog's name? I recommend asking Natalia.

## 8. How to try it
Run `python -m http.server 8000` from the repo root and open `/world_tour/`. Play ticket sales and tap the hint twice on a division problem.

## 9. File map
- New: `js/review.js`, `tests/math/review.test.mjs`, `tools/content_review.mjs`.
- Changed: `js/progress.js`, `js/show.js`, `math/gen/multdiv.js`, `modes/math/tickets.js`, `tests/math/generators.test.mjs`, `tests/e2e/play.mjs`, `README.md`.
- Regenerated: `CONTENT_REVIEW.md` (not for commit).
