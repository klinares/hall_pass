# World Tour (Unity): phase 7 report

## For the owner: what you do next

1. **Let her play the new build** (see How to try it). Tell the advisor how it went.
2. **Switch modes on as her class moves.** Edit `tour/StreamingAssets/math_config.json` and push; no rebuild is needed. `world_tour_unity/Tools/MATH_CONFIG.md` explains each setting. Use the table below.
3. **Listen once** for the sound and the read-aloud voice.
4. **Commit `world_tour/`** (six files, as in the phase 6 report).
5. **Push to GitHub.** Everything else is committed locally (last commit 980e30f).

| mode | skill | on? | top level | her class unit |
|---|---|---|---|---|
| snack table (new) | fm | on | 3 | unit 3, now |
| pack the truck | vol | on | 3 | unit 1, done |
| tour budget | ops | on | 2 | review |
| soundcheck | flu | on | 2 | level 3 is decimal powers of ten, unit 5 |
| ticket sales | md | on | 1 | multi-digit, unit 4 |
| merch table | dec | **off** | 3 | decimals, unit 5 |
| beat maker | frac | **off** | 3 | unlike denominators, unit 6 |
| stage designer | xy | **off** | 3 | coordinates, unit 7 |

## Phase and status

Phase 7 is **done** and committed locally. Nothing is pushed.

## What now works

- **Difficulty:**
  - Every math mode now starts at a new warm-up level 0 of 4th-grade review.
  - She needs 6 right in a row to move up.
  - Each skill has a top level, set in `math_config.json`. A save above it plays at the top level and is never lowered.
- **Gentle/standard switch:** in the manager's office, one per subject. Gentle lowers the top level by one and needs 8 in a row to move up.
- **Snack table (new):** fraction multiplication and division, with area and tape diagrams. Any equivalent answer counts.
- **Kid-fit fixes:**
  - easier ticket sales warm-up;
  - division steps show on the card;
  - the ticket buttons stay in view;
  - shorter snack prompts;
  - plainer words;
  - a dog line on the map that says where to start.

## Kid-fit review (7.4)

A tester played as her on day one, starting from a fresh save. The target was 7 of the first 10 right without hints.

| mode | first ten | what changed |
|---|---|---|
| ticket sales | about 5 | Level 0 is now facts and easy 2-digit problems (32×3, 63÷9); not re-scored |
| pack the truck | 10 | nothing |
| tour budget | 8 | nothing |
| soundcheck | 8 | nothing |
| snack table | about 7 | prompts fit in 3 lines; level 1 shows the tray |
| missing vowels | 5–6 | her real word list; plainer clue wording sent to the advisor (Q25) |

Rewards are fine as they are. Every show earns a sticker and at least 5,000 fans, which buys the cheapest backstage item.

## Test results

Edit-mode 1797/1797, play-mode 26/26, browser 18/18. 0 console errors. Download 13.28 MB.

## What the tester looked at

- Every phase 7 screen at both sizes in both themes: one tester pass and two fix rounds.
- Then the kid-fit pass and one fix round, checked from the builder's screenshots.

## Not done or not verified

- **Ticket sales:** she reaches level 1 (the web's level, with regrouping) after 6 right. It wasn't re-scored after the fix.
- **Snack table:** some prompts with stacked fractions still wrap to four lines.
- **First-try rule:** the ticket now counts a replayed miss, but the VIP percent keeps the web's rule.
- **Earlier polish list:** see TODO.md.

## Departures from the brief

- Workers ran on Sonnet, and no paid or headless use.
- New intended differences 39–43 are copy changes, listed in IntendedDifferences.md.
- Ticket sales level 0 is new, and levels 1–3 are unchanged.

## Open questions

Q25 (clue wording) is with the advisor.

## How to try it

From the repo root, run `node world_tour_unity/Tests/Web/server.mjs tour`, open the address in Chrome, and press start.
