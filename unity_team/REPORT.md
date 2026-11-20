# World Tour (Unity): report

## For the owner

- **Don't commit `world_tour/` yet.** It holds this morning's patch: js/show.js, math/gen/fluency.js, multdiv.js, volume.js, tests/math/generators.test.mjs. Answer 14 then withdrew one of its lines: level 3's "the digits don't change; only the decimal point moves" is false for 140 ÷ 10 and 8.8 × 100. The advisor asked for the same fix in `fluency.js`. Your 08:03 approval covered three faults, not this one, so I haven't touched it. **Should I apply it?** (Same terms as this morning.) If not, I'll give you the text.
- **Please listen once:** sound, and the studio session's spoken words. Tests only used a stand-in for the voice.
- **Your word list:** irrational and illogical each fit the other's fan mail clue and lyric line. Reword one (the web game shares this).

## Phase and status

Phase 3 (six more spelling modes) is **done** and committed locally. Next: phase 4, the math modes, merch table and beat maker first.

## What now works

- All seven spelling modes play through to the ticket and the encore: remix, missing letters, missing vowels, fan mail, studio session (the word is read aloud; the clue shows if there's no voice), lyric writer, prefix mixer.
- The venue card lists every mode and notes any not on a week's bill.
- No hint, alone or combined with others, shows the whole word before the third miss (answer 11).
- The decimal-point hint uses the new place-value wording (answer 14) in the Unity game.

## Test results

- Edit-mode 1233/1233, play-mode 14/14 (each mode plays a full show with a miss, a hint and the encore), browser 16/16, 0 compile warnings.
- 18 browser playthroughs (every mode at 1366x768 and 1920x1080, by mouse and keyboard, plus no voice and storage blocked) reached the ticket with 0 console errors.
- An independent hint checker: 110,533 checks, 0 breaks.
- Download 12.84 MB (budget 20 MB); ready in 0.7–1.3 s on this computer.

## What the tester looked at

Every mode's screens at both sizes (216 screenshots), all new text, speech calls, saving, and the decimal hint test.

## Not done or not verified

- Real voices and sound; a real graphics card; home-wifi load time.
- Soundcheck on screen (math modes arrive in phase 4).
- Polish list: long patterns wrap in riff's bubble; a paid hint can repeat a known letter; "the 0" when two zeros drop; low-contrast note; venue card Tab order.

## Departures from the brief

- 3.2 used two builds and two browser runs, not one: the first caught two faults that would have shipped.
- Long builds were moved to the background by the tool.

## Open questions

- Whether to apply answer 14 to `world_tour/` (above).

## How to try it

From the repo root, run `node world_tour_unity/Tests/Web/server.mjs tour`, open the address in Chrome, and press start.

## Files changed

- `world_tour_unity/Assets/`: Game (Modes/Spelling, Speech, Map, Show, Ui), Core/Math/Gen/Fluency.cs, Plugins/WebGL/speech.jslib, tests.
- `world_tour_unity/Tests/Web/`, `tour/` (rebuilt), `unity_team/`.
