# World Tour (Unity): phase 7 report

## For the owner: what you do next

1. **Let her play** the new build.
2. **Switch modes on as her class moves:** edit `tour/StreamingAssets/math_config.json` and push, no rebuild (see `world_tour_unity/Tools/MATH_CONFIG.md`).
3. **Listen once** for sound and voice.
4. **Commit `world_tour/`** (six files, as before) and **push**.

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

Phase 7 is **done** and committed locally (4cb0470). Nothing is pushed.

## What now works

- A warm-up level 0 in every math mode; 6 in a row to move up; per-skill top levels that never lower a save.
- An office gentle/standard switch per subject (top level one lower, 8 in a row).
- Snack table: fraction × and ÷ with area and tape diagrams; any equivalent answer counts.

## Kid-fit review (7.4)

A tester played as her on day one. Target: 7 of the first 10 without hints.

| mode | first ten | what changed |
|---|---|---|
| ticket sales | about 5 | Level 0 is now facts and easy 2-digit problems (32×3, 63÷9); not re-scored |
| pack the truck | 10 | nothing |
| tour budget | 8 | nothing |
| soundcheck | 8 | nothing |
| snack table | about 7 | prompts fit in 3 lines; level 1 shows the tray |
| missing vowels | 5–6 | her real word list; plainer clue wording sent to the advisor (Q25) |

Rewards are fine: every show earns a sticker and enough fans for the cheapest backstage item.

## Test results

Edit-mode 1797/1797, play-mode 26/26, browser 18/18. 0 console errors. Download 13.28 MB.

## What the tester looked at

Phase 7 screens at both sizes and themes (one pass, two fix rounds), then the kid-fit pass (one fix round, checked from screenshots).

## Not done or not verified

- Ticket sales reaches the web's level 1 (regrouping) after 6 right; not re-scored.
- Some snack prompts wrap to four lines.
- The VIP percent keeps the web's first-try rule.
- Earlier polish list: TODO.md.

## Departures from the brief

Workers on Sonnet; no paid or headless use. Copy changes are intended differences 39–43.

## Open questions

Q25 (clue wording) is with the advisor.

## How to try it

From the repo root, run `node world_tour_unity/Tests/Web/server.mjs tour`, open the address in Chrome, and press start.
