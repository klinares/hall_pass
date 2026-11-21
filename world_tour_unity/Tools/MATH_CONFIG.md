# math_config.json: how hard the math plays

Edit `tour/StreamingAssets/math_config.json`. The game reads it when it starts, so a change needs no rebuild: reload the page. Then run `node world_tour_unity/Tools/validate_data.mjs` and push only when it says ok.

## Switching a mode on or off

`"ENABLED_SKILLS"`: `true` shows the mode, `false` hides it. Ids: `dec` merch table, `md` ticket sales, `frac` beat maker, `xy` stage designer, `vol` pack the truck, `ops` tour budget, `flu` soundcheck.

## Levels

Every math mode has levels 0 to 3. Level 0 is a warm-up of fourth-grade review. A new mode starts there.

```
"LEVEL_UP_AFTER": 6,
"LEVEL_DOWN_AFTER": 2,
"LEVELS": {
  "md":  { "MAX_LEVEL": 1, "START_LEVEL": 0 },
  "ops": { "MAX_LEVEL": 2 },
  "flu": { "MAX_LEVEL": 2 },
  "vol": { }
}
```

- `LEVEL_UP_AFTER`: right answers in a row to go up a level. Default 6 (1 to 20).
- `LEVEL_DOWN_AFTER`: misses in a row to go down a level. Default 2 (1 to 20).
- `LEVELS.<mode>.MAX_LEVEL`: the highest level the mode plays, 0 to 3. Default 3 (no cap).
- `LEVELS.<mode>.START_LEVEL`: where a mode she has never played begins, 0 to 3. Default 0. A start above the cap is played at the cap (the validator says so as a warning).
- Every key is optional. A mode left out of `LEVELS`, and an empty `{ }`, use the defaults.
- A saved level above a cap plays at the cap. Nothing is taken away: the saved level stays, so raising the cap later brings her back to it.
- Spelling does not read this file. It keeps its own rules (levels 1 to 3, up after 4).

## Gentle mode

In the manager's office, "gentle spelling" and "gentle math" are switches kept in her save. Gentle puts each cap one level lower and asks for 8 right in a row to go up (or the file's `LEVEL_UP_AFTER`, if that is higher). A math mode capped at 1 plays only the warm-up while gentle is on.

## What each level is

| mode | level 0 (warm-up) | level 1 | level 2 | level 3 |
| --- | --- | --- | --- | --- |
| ticket sales `md` | single-digit facts, 2-digit x 1-digit (factor to 5), 2-digit / 1-digit | 2-digit x 1-digit, 3-digit / 1-digit | 2-digit x 2-digit, 3-digit / 2-digit | 3-digit x 2-digit, 4-digit / 2-digit, full rows |
| pack the truck `vol` | one flat layer to count | a small stack to count | l x w x h, load the floor | two boxes, a missing side |
| tour budget `ops` | two operations, no parentheses | two operations with parentheses | three operations | pick the expression for a story |
| soundcheck `flu` | facts to 5 | facts to 10 | facts to 12, x and / 10 and 100 | decimals, powers of ten |
| merch table `dec` | whole-dollar and 50-cent sums | two items under $10 | three items, quantity x price | decimal x decimal |
| beat maker `frac` | like denominators only | halves, quarters, eighths | unlike denominators | mixed numbers, taking out, subtracting |
| stage designer `xy` | axes to 4 | axes to 6 | axes to 10 | move it 3 right and 2 up |

Every problem at every level has one right answer, and hints 1 and 2 never give it away.
