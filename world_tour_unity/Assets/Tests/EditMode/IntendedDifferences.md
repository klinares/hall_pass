# Intended differences from the web game

The Unity version matches `world_tour/` exactly, except for the items below. Each was decided by the advisor in
`unity_team/QUESTIONS.md` (the number in brackets). The JS-match tests stay exact everywhere else:

- generator vectors (`ProblemVectorTests`): `IntendedDifferences.ToJsForm` puts the web wording back into exactly the
  changed step, and the problem must then hash to the JS vector; for the one tour budget seed the story differs
  entirely, the JS hash is listed with it and the Unity story is checked against the bound;
- typed-answer vectors (`AnswerVectorTests`): the JS results are adjusted by the three rules of item 7 only, and a test
  pins exactly which vector texts those rules touch;
- remix and show vectors: the changed cases have their own tests (`ShowEngineIntendedTests`, `RemixRulesTests`).

## Hints (generators)

1. **Soundcheck, moving the decimal point (9.1, 14).** Level 3 hint 2 for x and / by 10, 100 and 1,000. The web game
   said "the digits stay N" for 10 (wrong digits for 8.82 x 10, "441", and the answer itself for 6.1 x 10, "61") and
   "8.8 x 10 = 88. keep going..." for 100 and 1,000. Now place-value wording, true for every problem:
   "x 100 moves every digit 2 places to the left." plus " what fills the empty place?" exactly when a zero is filled in
   (8.8 x 100 = 880), and "/ 10 moves every digit 1 place to the right." plus " what happens to the 0 at the end?"
   exactly when zeros drop (140 / 10 = 14). Never prints the digits or the answer. Every decpow and decdiv problem
   (774 at 10: 529 x, 245 /; the rest at 100 and 1,000). Level 2's "the 0 in the ones place drops off" is unchanged.
   Tests: `IntendedDifferenceTests.Soundcheck_PlaceValueHint_TrueForEveryDecimalPointProblem`,
   `HintLadderTests.NoHintStatesAFalseNumber`.
2. **Ticket sales, level 2, quotient 2 or 3 (8.1).** Hint 1's "try a few" list reached d x q = N. Now:
   "how many 40s fit in 120? 40 x 2 = 80. is there room for another?" (q = 2 lists only d x 1). 28 of 3,000.
3. **Ticket sales, level 2, quotient 10 (8.2).** Hint 1 printed d x 10 = N. Now: "what happens to a number when you
   multiply it by 10? try it with 29." 49 of 3,000.
4. **Pack the truck, level 1, one-layer stacks (9.2).** Hint 2 said "that's 12 crates", the answer. Now: "the top
   layer is 4 across and 3 deep. how many crates is that? there is 1 layer." 675 of 3,000.
5. **Tour budget, level 3 stories (9.4).** A story never shows more than $150 (`BOUNDS.storyDollars`, a new key that
   only the Unity config has; `moneyCents` keeps meaning decimal money). The web strings story reached $156 in about
   1 of 3,200 seeds; the Unity generator picks another story for those. In the vector seeds: ops level 3 seed 5783871.

Kept as the web game has them (by the same answers): ticket sales level 3 rows hint 2 (two candidates, 8.3); beat
maker's "you need x more" (by design); merch table's whole-number partial product; ops level 1 trivial last step and
ops level 2 zero terms (9.3).

## Typed answers (7)

6. **Commas.** Only thousands grouping reads ("1,000", "12,500", "1,000,000", "12,500.5"). Any other comma
   ("3,50", "1,0,0,0", ",5", "12,34.5") makes the text unreadable: "that doesn't read as a number yet". The web game
   removed every comma, so "3,50" was 350, a plain miss.
7. **Spaces around the slash** are accepted ("3 / 4", "1 3 / 4"). The web game said the text didn't read.
8. **Unreadable text on a point answer** gets "that doesn't read as a number yet. type it like (4, 4): across, then
   up." (Check result "bad"); the web game said "type your answer on the keys first" (Check result "empty").
   Unreadable input is never a miss and costs nothing (`Answers.IsRetype`, `Answers.Nudge`).

Kept: "1 5/4" counts as 9/4 where simplest form isn't asked (7.4); equivalent decimals and fractions (7.5).

## Shows (10 and the addendum)

9. **A miss on the final song doesn't replay at once.** It goes to the encore and the review queue only, and its bonus
   slot stays unused (event `RequeueSkipped`, tag "final-song"). The web game replayed it straight after its own worked
   solution. Other misses: three songs later, or as the last song when fewer than three remain; at most two per show.
10. **"Clean run" needs no misses and no hints.** The web game checked first >= n, which a show with a miss reaches
    when the replay is clean (scenario A). The summary carries `Missed` (the engine's MissedKids count); the saved
    show record gets `"missed"` only when it is above 0, so saves without misses match the web game byte for byte.
    Scoring and the VIP ticket keep the web rule.

## Spelling hints (11)

11. **No hint shows the whole word before the reveal.** Every pre-reveal hint leaves at least a third of the letters
    hidden and never fewer than two (`SpellingHints`). "Sound it out": two-syllable words show the first syllable and
    one underscore per hidden letter ("em - ____"); longer words whose first and last syllables would show too much
    get the same treatment ("il - __ - ___"). Of the 22 current words, 8 change: empire, temple (two syllables) and
    illegal, population, tropical, pyramid, rainforest, monument; the other 14 keep the web form
    ("ir - ___ - ___ - al"). Words of 3 letters or fewer, and one-syllable words too short for the first-two/last-two form, show
    the first letter and the length ("j___"). Remix's glowing-tile hint never lights a letter past the floor ("the last
    letters are yours to find. sound it out."). The data validator warns about a word of 3 letters or fewer.
12. **Missing letters and missing vowels keep the last blanks hidden through every hint (lead's 3.1 ruling under 11).**
    A letter hint fills the leftmost open blank and never brings the blanks below the floor, so a song has 1 + (blanks
    above the floor) hints instead of the web's one per blank but the last. The "sounds like" hint and the ladder's
    second line ("sound it out") show the chunks with the blanks that are hers masked as "_" (`LetterHints.Masked`,
    `MaskedChunks`): with "t??ple" the web would say "tem - ple", which finishes the word; here it says "t__ - ___".
    On the web, hint 1 plus the visible row gave away every blank in about 4% of missing-letters deals. Tested against
    every live word at levels 1 to 3 (300 deals each) and in missing vowels: after all pre-reveal hints, at least the
    floor (or every blank, when there are fewer) is still unknown. The ladder's first line drops "and starts with x"
    when the first letter is one of her masked blanks ("not quite. it's 6 letters."), and is the web line otherwise.
13. **Remix: the glowing tile and the ladder count each other (task Q tester D1, under 11).** The glow used to stop
    only at `SpellingHints.MaxShown`, so after the second-miss line "in - ___ - cept" the glows could still light the
    rest and spell intercept, intercede or indigenous before the reveal. `RemixBoard` now keeps what has been shown (glows
    up to their letter, line 1's first letter, line 2's chunks) across the resets: a tile glows only while at least the
    floor stays unknown (it may relight letters the line already showed), and a later ladder line masks chunk letters,
    or drops "and starts with x", when they would go past the floor. Tested on every live word in six orders of glows and
    ladder lines.
14. **Studio session and typed lyric writer hold the floor across the whole hint sequence (lead's 3.2 ruling under 11).**
    Studio's web hints together (sounds like, first and last letter, consonants only) left fewer than a third of the
    letters unknown for 20 of the 22 live words, and "just the consonants" alone did for pyramid. `HintFloor` keeps what
    every hint and ladder line has shown; each step keeps the web's wording and order, shows letters only while at least
    the floor stays unknown (leftmost first), and masks the rest as "_". Pyramid: "just the consonants: p y r _ _ _ d"
    (web "p y r _ m _ d"). A first letter past the floor is left out of the ladder's first line, as in 12, and of lyric
    writer's "it starts with" hint ("it has 7 letters."). Lyric writer's live words already kept the floor; it uses the
    same rule so a short word added later can't break it. Tested on every live word with the hints and ladder lines in
    five orders.
