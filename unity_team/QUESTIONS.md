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

## 7. Typed-answer quirks inherited from the web game (asked 2026-11-20 01:35, not blocking)

**Context:** Task 1.2 ported `world_tour/math/answer.js` exactly, and a JS cross-check confirms it (146 vector tests, 3,128 `check` results). The port shows these behaviours, which a child typing an answer could meet. None marks a right answer wrong in a way that costs fans except where noted. All of them are in the web game today.

1. A comma is removed anywhere: `"3,50"` reads as 350 (a plain miss on a $3.50 answer), and `"1,0,0,0"` is accepted as 1000.
2. Spaces around the slash are rejected: `"3 / 4"`, `"3 /4"` and `"3/ 4"` get "that doesn't read as a number yet". It isn't a miss, but it's friction.
3. On a typed point answer, unreadable text (`"(3 5)"`, `"abc"`) gets "type your answer on the keys first", although she typed something. In the Unity version stage designer is a hands-on builder, so typed points may rarely occur.
4. `"1 5/4"` is accepted as 9/4 when simplest form isn't required.
5. Decimal answers accept fractions (`"1/2"` for 0.5) and fraction answers accept decimals. This matches "accept equivalent forms" in the brief.

**Options:**
- (a) Keep the exact web behaviour everywhere. Same as today; no surprises between the two versions.
- (b) Fix only the feedback, not what counts as right: accept spaces around the slash (2), give unreadable point text the "doesn't read as a number" message (3), and treat a lone comma followed by exactly two digits at the end (`"3,50"`) as unreadable rather than as 350 (1). Items 4 and 5 stay.
- (c) Option (b), plus require a proper fraction part in mixed numbers (4).

**Recommendation:** (b). It only changes messages and accepts one more harmless spelling, so it never takes a right answer away or turns a miss into a right answer. The learning rules ("a wrong answer never just says wrong", "accept equivalent forms") favour it. Until answered, the Unity port keeps option (a) and nothing is blocked. If (b) is chosen, it's a small change in `Answers.Check`, with new C#-side tests marked as intended differences from the JS.

**Answer (advisor, 2026-11-20 01:48):** Option (b), with a slightly wider comma rule. The principle: unreadable input is never a miss, never costs fans and never touches the streak; it only asks her to retype. A right value is never taken away.

1. Commas: accept a comma only as proper thousands grouping (one to three digits, then groups of exactly three: `1,000`, `12,500`, `1,000,000`, with an optional decimal part after the last group). Any other comma (`3,50`, `1,0,0,0`, `,5`, `12,34.5`) is unreadable and gets the "doesn't read as a number yet" message. Do not guess that `3,50` meant 3.50.
2. Spaces around the slash: accept (`3 / 4`, `1 3 / 4`).
3. Unreadable text on a typed point answer: use the "doesn't read as a number yet" message, with the expected shape shown, for example `(3, 5)`.
4. `1 5/4`: keep accepting it as 9/4 where simplest form isn't required. The value is right, and the simplest-form problems already reject it with their own message.
5. Keep, as the brief says.

Mark the new C# tests as intended differences from the JS, and add a line to the phase 1 report listing them so the web game can be brought in line later. Do not change `world_tour/`.

## 8. A hint in the web game gives the answer away (asked 2026-11-20 01:55, not blocking)

**Context:** Task 1.3a ported the multiply/divide generator exactly (problems match the JS byte for byte). The port shows that in **ticket sales (md), level 2**, when the quotient is 3, hint 1 lists tries up to the answer. Example (seed 120786, `120 ÷ 40`): *"how many 40s fit in 120? try a few: 40 × 2 = 80, 40 × 3 = 120…"*. That is 77 of 3,000 level-2 problems. The brief's learning rules say the first hint is a nudge and only the third step is the worked solution. The web game's guard only looks for "= 3", so it misses this, and so does the web game's own contract test.

**Options:**
- (a) Keep the exact web text.
- (b) Stop the "try a few" list one step before the answer, e.g. "40 × 2 = 80, 40 × 4 = 160…" or "try 40 × 2 first…", so hint 1 stays a nudge. Add a C# test that no hint-1 or hint-2 text contains a product that equals the dividend. The Unity problem would then differ from the web game for those 77 problem shapes.
- (c) Option (b) applied to every generator: a general check that hints 1–2 never contain "a × b = dividend" for the true answer.

**Recommendation:** (c). The brief says it wins where it conflicts with the web game, and this is the learning rule the hint ladder exists for. It changes only hint wording, never what counts as right. Until answered, the port keeps (a), and the JS-match tests stay exact.

**Correction to question 8 (01:58, from the 1.3a tester's sweep of all 27,000 volume problems):** there are three giveaway shapes, all in ticket sales (md), all copied exactly from the web game:
- **md L2, quotient 3** (28 of 3,000): hint 1 "try a few: 40 × 2 = 80, 40 × 3 = 120…" (seed 120786).
- **md L2, quotient 10** (49 of 3,000): hint 1 "one ten of 29 is 29 × 10 = 290." for 290 ÷ 29 (seed 334599).
- **md L3, rows** (all 996 rows problems): hint 2 "33 × 6 = 198 and 33 × 7 = 231. which one fits under 203?" names the answer as one of two candidates (seed 3001). This is closer to a partial step than an outright giveaway, and option (c)'s "a × b = dividend" check would not catch it, because there's a remainder.

The other patterns the sweep found are by design or coincidence: builder fractions' hint 2 "you need [[x]] more" (the child still places the blocks), decimal-multiply hint 2's partial product, and prices that happen to equal the change.

**Revised recommendation:** fix the first two (hint 1 must not contain the answer as a factor or as a product equal to the dividend) with a general test across all generators. Leave the L3 rows hint 2 as an intended partial step unless the advisor prefers it vaguer, e.g. "try 33 × 6 and the next one up." Until answered, the port stays exact.

**Answer (advisor, 2026-11-20 02:08):** Your revised recommendation, with the wording below. Good catch by the tester; this is exactly what the hint ladder is for.

The rule, for every generator: hint 1 is a nudge and must not contain the answer in any form (not as a factor, not as the result of a finished equation, not as a product equal to the dividend). Hint 2 is a partial step: it may do part of the work or narrow the choice, but must leave the last step to her. Only hint 3 states the answer.

1. **md L2, quotient 3 (and any small quotient):** the "try a few" list stops below the answer and hands the next try to her. Example for 120 / 40: "how many 40s fit in 120? 40 x 2 = 80. is there room for another?" When the quotient is 2, list only 40 x 1.
2. **md L2, quotient 10:** do not print the product. Example for 290 / 29: "what happens to a number when you multiply it by 10? try it with 29."
3. **md L3 rows, hint 2:** keep it. Two candidates with a question is a fair partial step, and deciding which fits (and what to do with the leftover) is the thinking the problem is for.
4. **Test:** one general edit-mode test across all generators and levels, a few thousand seeds each: hint 1 never contains the answer's digits as a standalone number or any equation whose result is the answer or the dividend; hint 2 never states the final answer outright. Where a hint legitimately contains the same digits by coincidence (a price equal to the change), exempt it by shape with a comment, not by seed.
5. **JS-match tests:** keep them exact for everything except the step text of the two fixed shapes; compare those problems with the steps masked, and list the shapes as intended differences in the phase 1 report alongside the question 7 items.

The by-design patterns you listed (builder fractions "you need x more", the decimal partial product, coincidental prices) stay as they are.

## 9. Two more hint problems inherited from the web game (asked 2026-11-20 02:15, not blocking)

**Context:** Task 1.3b ported the volume and fluency generators exactly (problems match the JS byte for byte). It found:

1. **Pack the truck (vol), level 1, one-layer stacks: hint 2 states the answer.** Example (seed 24758, "count 4×3×1"): *"the top layer is 4 by 3: that's 12 crates. there is 1 layer."* The answer is 12. This is 675 of 3,000 level-1 problems. The guard misses it because there's no "=".
2. **Soundcheck (flu), level 3, moving the decimal point one place: hint 2 states wrong digits.** Example (seed 42596, "8.82 × 10 = ?"): *"the digits stay 441; only the decimal point moves, one place right."* The digits are 882. The JS prints the numerator of the reduced fraction (8.82 = 441/50). This is 297 of 529 such problems, about 10% of level 3. **It teaches something false.**

**Options:**
- (a) Keep both exactly as in the web game.
- (b) Fix both in the Unity version. For 1, hint 2 for a one-layer stack says "count the crates in the top layer" without the total. For 2, print the decimal's own digits ("the digits stay 882"). Add C# tests: no hint 1–2 states the answer in any form, and "the digits stay N" matches the decimal's digits.
- (c) Fix 2 only.

**Recommendation:** (b). Item 2 is a factual error, and the brief says learning wins and the brief beats the web game. Item 1 breaks the hint ladder the same way question 8 does. Both change only hint wording. Until answered, the port stays exact.

**Addendum to question 9 (02:20, from the 1.3b tester's sweep of all 36,000 volume problems; all copied exactly from the web game):**
- Both item 1 (vol L1, 675/3,000) and item 2 (flu decpow, 297/529) are confirmed. Item 2 hits one-decimal problems too: 150 have two places and 147 have one (seed 486060: 9.2 × 10, "the digits stay 46").
- **New:** in 115 flu L3 decpow problems hint 2's digits *are* the answer (seed 501898: 6.1 × 10, "the digits stay 61"), so the hint gives it away. Option (b)'s fix ("print the decimal's own digits") would turn these into "the digits stay 61" for 6.1 → 61, which still gives it away. A vaguer wording such as "the digits don't change; only the decimal point moves" avoids both problems.
- **New, low:** ops L1, 122 of 3,000: after hint 2 only a trivial step is left ("first: 5 − 4 = 1. that leaves 12 × 1."). ops L2, 55 of 3,000 are built around a zero term (6 + 8 × (8 − 8)).
- **New, bounds:** in 107 of 3,000 ops L3 stories the prompt shows $100 or more ("the band has $114…"). `BOUNDS.moneyCents` says money stays under $100, but neither game checks dollar amounts outside the decimals skill.

**Revised recommendation for question 9:** fix the flu decpow hint to "the digits don't change; only the decimal point moves N place(s) right/left", and the vol one-layer hint so it doesn't state the total. Leave the low items as they are. Decide whether ops story money may reach $100–$150 (recommendation: allow it and document it in BOUNDS, because it's whole dollars in a budget story, not decimal money work).

**Answer (advisor, 2026-11-20 02:22):** Your revised recommendation. The rule from answer 8 applies to every generator: hints 1 and 2 never state the answer, and no hint ever states something false.

1. **Soundcheck, moving the decimal point:** "the digits don't change; only the decimal point moves N place(s) to the right" (or left). Never print the digits. This is the important one: the web wording teaches something false, so add a test that no hint in any generator contains a number that is neither in the prompt, nor a correct intermediate value, nor the answer (hint 3 only).
2. **Pack the truck, one-layer stacks:** hint 2 describes the layer without the total, for example "the top layer is 4 across and 3 deep. how many crates is that? there is 1 layer."
3. **Low items (ops L1 trivial last step, ops L2 zero term):** leave as they are.
4. **Story money in tour budget:** allow whole-dollar amounts up to $150 in order-of-operations stories. Add it to `BOUNDS` as its own key (for example `storyDollars: 150`) so `moneyCents` keeps meaning decimal money work, and test it.

List all of these as intended differences in the phase 1 report. Put item 1 at the top of that report under "For the owner": the live web game shows wrong digits in about one in ten level 3 soundcheck problems, and he needs to know before his daughter plays it.

## 10. Missed items in a show: the web game caps re-inserts (asked 2026-11-20 02:35, not blocking)

**Context:** Task 1.4 extracted the show rules from `world_tour/js/show.js` and pinned them against the real JS (8 scripted shows run through `mountShow`). The brief says: *"A missed item returns three questions later and again in the encore and the next session."* The web game does this with two limits:
1. **At most 2 misses per show come back** (`MAX_BONUS = 2`, show.js l.19). A third or fourth miss returns only in the encore (and the next session through review). Example: miss songs 1–4, and songs 3 and 4 never come back in the show.
2. **A miss near the end comes back sooner than three later.** It's re-inserted at `min(i+4, length)`. Missing song 9 brings it back after one song, and missing song 10 brings it back immediately.

Settled by the brief, so no question: scoring and level rules "match the web game exactly". So a clean bonus replay counts as first try (a miss can still earn a VIP ticket), and the level counts songs, not tries.

**Options:**
- (a) Keep the web rules exactly (2-miss cap; end-of-show misses come back sooner).
- (b) Remove the cap: every miss comes back 3 later, so a show can grow from 10 to 13–14 songs on a bad day. Near the end, append to the queue so it still plays (it can't be "three later" if fewer than 3 remain).
- (c) Keep the cap at 2 (shows stay short and don't pile up on a bad day), and state in the brief that misses beyond two return in the encore and the next session.

**Recommendation:** (c). It keeps a struggling 11-year-old's show from growing longer and more discouraging, every miss still returns (encore, then review next session), and it matches the web game she knows. Until answered, the engine keeps (a), which is the same as (c) in behaviour.

**Answer (advisor, 2026-11-20 02:22):** Option (c), with one change. This answer amends the brief's sentence on missed items.

- At most two misses return inside a show; further misses return in the encore and the next session. A struggling child should not watch the show get longer.
- A miss returns three questions later when at least three remain. When fewer remain, it returns as the last song of the show, except that a miss on the final song does not replay immediately: she has just been shown the worked solution, so repeating it at once tests memory of the last ten seconds, not understanding. That one goes to the encore and the review queue only.
- Scoring and level rules stay exactly as the web game has them.

Pin the changed last-song case with its own test and list it as an intended difference in the phase 1 report.

## 11. The syllable hint spells out two-syllable words (asked 2026-11-20 02:40, not blocking)

**Context:** The 1.4 tester found that the web game's `chunks()` (show.js), which builds the "sounds like" syllable hint, returns the whole word for two-syllable words: `em-pire` gives "em - pire" and `tem-ple` gives "tem - ple". That's 2 of the 22 current words. The function's own comment says it never spells the whole word. It's used for the ladder after the second wrong try, and as a hint step in lyric writer and studio session (the first step in studio session when no voice is available). Ported exactly.

**Options:** (a) keep it; (b) for a two-syllable word, show only the first chunk plus blanks ("em - ____"); (c) as (b), with a test that no hint ever contains the whole word.

**Recommendation:** (c), for the same reason as questions 8 and 9. Until answered, the port stays exact.

**Answer (advisor, 2026-11-20 02:53):** Option (c), widened into the spelling-side twin of the rule in answers 8 and 9: before the final reveal (third miss), no hint in any spelling mode shows the whole word.

- Two-syllable words: first chunk plus blanks, one underscore per hidden letter ("em - ____"), so the hint still tells her how long the rest is.
- One-syllable words and words with no syllable data: the existing first-two, last-two fallback spells short words outright (a four-letter word shows all four). Any pre-reveal hint must leave at least a third of the letters hidden, and never fewer than two; for short words show only the first letter and the length.
- The same floor applies to the letter-by-letter hints in missing letters, missing vowels and remix: the last hidden letters are always hers to find.
- Test it against every word in weeks.json rather than fixed examples, so it holds for weeks added later, and have the terminal validator warn when a new word is so short that the hints have almost nothing to hide.

List it as an intended difference in the phase 1 report.

**Addendum to question 10 (03:00, from task 1.6):** because a clean bonus replay counts as a first try (web rule, kept), a show with a miss can still earn the **"clean run"** sticker, whose text says *"play a whole show with no misses and no hints"* (JS show scenario A: one miss, replayed right, `first` = 10/10). Nothing earned is taken away, but the sticker's text and its rule disagree. Options: (a) keep it; (b) reword the sticker ("finish a show with every song right in the end"); (c) award "clean run" only when the show had no misses (needs a "missed" count in the summary, which the engine already tracks as MissedKids). **Recommendation:** (c) if the advisor wants the sticker to mean what it says, otherwise (b). Kept exact until answered.
