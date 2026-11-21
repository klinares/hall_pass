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
   exactly when zeros drop (140 / 10 = 14); when two or more drop (900 / 100 = 9) it says "what happens to the zeros at the end?". Hint 3 puts thousands commas in numbers of 4 or more digits (8,400, as level 2 prints 5,380). Never prints the digits or the answer. Every decpow and decdiv problem
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
15. **Pack the truck builder: "load it" on an empty floor is not a try (QUESTIONS 15, answer 7's rule).** The web counts
    it as a miss (a fan-costing try and a step up the ladder for pressing the button before loading anything). Here the
    dog says "load a crate or two first." ("think" pose): no miss, no wobble, no fans lost, the streak and the ladder stay
    put, as the keypad does with an empty answer. Any crate on the floor makes "load it" a real try again, as on the web.
    `TruckBuild.Load` returns `TruckCheck.Empty`; `MathBuilderTests.TruckBuild_EmptyFloor_IsAMissOnTheWeb_ButNotHere`
    pins both sides against the real truck.js (builder_vectors.json `truckEmpty`).
16. **Ticket sales, a one-part area model at hint 2 (QUESTIONS 8's rule, applied to the picture).** When the factor that's
    broken up is a round number (70), the web's area model has a single cell, and from hint 2 that cell shows the
    product, which is the answer (36 x 70: the cell says 2,520 while the hint text says "36 x 7 = 252. now make that ten
    times bigger"). About 1 in 11 multiplication problems. Here the cell says "252 x 10" at hint 2, the step the text
    describes, and the product appears at the worked solution as on the web. `TicketsWork.Draw`; tested on every
    multiplication problem in the 3,000-seed set per level and replayed against the real tickets.js.
17. **Ticket sales, the seat rows at hint 2 (QUESTIONS 8's rule, applied to the picture).** For "how many rows will be
    completely full?" the web draws every full row from hint 2 (12 icons and "... 17 more full rows"), which counts out
    the answer while hint 2's text keeps the choice ("21 x 29 = 609 and 21 x 30 = 630. which one fits under 623?",
    answer 8.3). Here hint 2 draws one full row, a gap and the part row, with the web's line "21 seats per row. does the
    last row fill up?"; the worked solution draws the rows as the web does. Every level 3 rows problem.
18. **Stage designer: "set" before the light has moved is not a try (QUESTIONS 15, answer 15).** As on the web, "set" with
    nothing placed (place), or before both numbers are set (read), is no miss, no try and no fans lost, and the ladder
    stays put; the web's lines stay ("tap a spot on the stage first.", "set both numbers first."). On a move problem the
    gear already stands on the stage, so the web's "tap a spot on the stage first." didn't fit: here riff says "move the
    spotlight first." (the problem's gear; "think" pose). `StageLines.NeedMove`;
    `MathBuilderTests.Stage_SetBeforeAnything_IsNotATry_InEveryKind`.
19. **Beat maker: "play it" on an empty measure is not a try (QUESTIONS 15, answer 15; task 4.1).** On a fill problem with
    no note added, riff says "add a note or two first." ("think" pose): no miss, no try, no fans lost, the ladder stays
    put. The web counted it as a miss. A take-out problem starts too full, so playing it untouched is still a try.
    `BeatBoard.Play` returns `BeatPlay.Empty`.
20. **Math songs are laid out in two columns (task 4.1).** The desk and the controls on the left, the prompt card and the
    keypad (or the choices, or the stage-hand panel) on the right, 350 wide; tour budget's story choices sit under
    the sheet on the left, where there is room. The web's single column (desk, prompt,
    keypad, controls) is taller than a 1366x768 window. Same order of things to read; nothing is left out.
21. **Fractions are larger than the web's (task 4.1).** Stacked fractions are drawn as real fractions in TMP
    (`MathText.ToTmp`) and a little larger than math.css sets them: the prompt at 1.15 and the dog's bubble at 1.1 of the
    text size (the web's 1.05 and .95), so the numerator and denominator stay easy to read at 1366x768.
22. **The keypad and the keyboard (task 4.1).** Keypad keys aren't Tab stops (she types on the real keys); Tab goes to the
    hint and next buttons. Typing a digit, "." or "/" while one of those buttons has focus takes the focus back to the
    pad first, so the next Enter locks the answer in instead of pressing the button again. On the web the focused button
    kept Enter.
23. **The on-screen key moves when its real key is pressed (task 4.1).** Typing "7" on the keyboard presses the pad's 7
    with the same squash as a click, so she sees what was typed. The web's keys only move when clicked.
24. **Merch table: an item card widens to fit a long price tag (task 4.1).** A bulk tag ("2.5 ounces x $1.20") makes its
    card wider; on the web the tag ran past the edge of the 112 px card.
25. **Stage designer: the light can be dragged, and the arrow keys move it (task 4.2).** The web has a press on the stage
    and the four arrow buttons; here the light also follows a drag point to point, and with the stage focused the arrow
    keys move it and Enter or Space sets it. Read mode's pickers take Up/Down and the + and - keys. What counts as right
    is unchanged.
26. **Stage designer read mode: the pickers show the answer at the worked step (task 4.2).** The web set the pair line to
    the answer but left her numbers in the pickers, so the worked picture disagreed with itself.
27. **Pack the truck builder: a "crate" chip to drag (task 4.2).** One new word on a chip she can drag onto a spot, or press
    and then press a spot; a press on a spot alone still stacks a crate, as on the web.
28. **Tour budget: the keypad takes no keys until the expression is picked (task 4.3).** On a story problem the web's
    hidden keypad still listened, so typing a number before picking could submit it. Here it appears, and listens, once
    the expression is chosen (by her or by hint 2).
29. **Stage designer: no hint-2 route or big number onto the answer (answer 16; phase 4 fix round 1, B3).** The web's hint-2
    route runs across to the answer's x and highlights that bottom-line number. When the answer is on the bottom line
    (y = 0: place problems from (0, 0), read problems), the route ends on the answer and the big number sits on it, so the
    picture marks the answer. Then hint 2 draws neither and leaves the text alone (answer 9's low items); on a move
    problem with y = 0 the route stays (it ends off the answer) but the big number goes. The worked step draws them as
    the web does. `StageGame.Step`; `AnswerSixteenTests.B3_*` sweeps every kind at every level.
30. **Worked lines end with a full stop before "it'll come back around." (phase 4 fix round 1, P2).** The web ran them
    together ("... so 9 it'll come back around."). `MathLines.ComeBackAfter` adds the full stop unless the worked line
    already ends a sentence; the JS-match tests map the web's reveal lines the same way (`RevealExpect`).
31. **Pack the truck, missing height: hint 2 shows one layer (answer 16; phase 4 fix round 1, B1).** The web's hint 2 drew
    every unit line, up the sides too, so the layers (the hidden height) could be counted. Here hint 2 draws only the
    length x width grid on the top face, the height label stays "?", and every line comes with the worked step.
    `IsoGeometry.Typed` (`IsoLines.Top`); `AnswerSixteenTests.B1_*` checks every line lies on the top face.
32. **Tour budget: the hint-2 highlighter on story problems (phase 4 fix round 1, P3).** budget.js's pattern
    (`<b>(.*?) = `) ran across "</b>" in "it's <b>expr</b>. first: a + b = c." and found nothing, so story problems
    never got the highlighter. Here a bold part may not contain a tag, and a story's "first: a + b = " is used; the
    highlighted part is never the final value (swept).
33. **Beat maker, typed sum and difference: separate rows before the worked step (answer 16; phase 4 fix round 1, B2/P8).**
    The web drew the two notes end to end on the grid from the start, so the sum (or the solid remainder) could be
    counted off at any hint, and on a difference it wrote the whole's fraction on a block drawn as long as the
    remainder. Here, before the worked step, each note has its own row from the left edge (a difference: the whole, and
    the part to cut, faded); from hint 2 each is labelled in the common denominator (2/8, 5/8: the step the hint text
    names) with only its own cells numbered. The worked step draws the web's picture, the remainder labelled with its
    own length. `BeatTypedPicture`; `AnswerSixteenTests.B2_*`.
34. **Readable small type in the math screens (phase 4 fix round 1, P4, P5, P9).** The ticket setlist's fractions are
    1.1x the line's size in bold with taller lines (the web's .8em came out about 8 px and grey); the beat maker's note
    labels are 20 px and its cell numbers 14 px ink with a card outline (the web's .62rem on-ink vanished on light
    notes); ticket chunk notes, truck side labels (at least 15 px) and the "more full rows" line are larger and darker;
    the stage's bottom-line numbers sit a little lower so a light on the bottom line doesn't cover its number; a
    coordinate pair keeps together with a no-break space (P6); a four-line prompt clears the card's torn edge (P10).

## Meta (phase 5, task 5.1): proposed, awaiting the lead and advisor

35. **The tour journal is a paged sticker book (task brief).** The web journal is one long notebook page with the earned
    and bought stickers in a loose grid, then a "still to earn" list. Here each page has ten slots, one per milestone in
    milestones.js order: an earned sticker sits in its slot until she moves it; one still to earn shows an empty dashed
    slot with its greyed art, name and the web's "how" line (the "still to earn" list's content, placed in the book).
    Bought stickers get a last page. New copy: "page 1 of 3" and the arrow buttons (Page Up / Page Down too). Moving a
    sticker works as on the web (drag, or click it then click a spot; arrow keys nudge 3%), within its page. The empty
    book's and the full book's lines show in the caption. `JournalBook`; `JournalBookTests`.
36. **New stickers peel on when she first sees them in the book.** The web peels only stickers earned at the moment the
    journal opens (so the ones earned at a show's end never peel there). Here every sticker she hasn't seen yet peels on
    and slaps down when its page opens, and the book opens at the first such page; `journal.seen` (save version 2)
    remembers them. Nothing is ever removed.
37. **In peach + navy, sticker, venue and riff art keep their tour-pink colours.** The web's SVG art reads the theme's
    colours, so its outlines turn navy and its pink and lilac parts peach and blue. Here the UI colours, every text, and
    the scrapbook decor (paper, tape, bubbles, ticket stub, stamp, the map paper) follow the theme; the exported sticker,
    venue and dog sprites do not. A full peach set is already exported (1.48 MB more PNG: stickers 888 KB, venues 246 KB,
    riff 350 KB) and would plug into the same twin list if wanted.
38. **Backstage: no confetti burst on a purchase**, the toast only (no particle helper yet, as with show.js win()).

## Kid-fit (phase 7.4, round 1)

39. **Plain words in game copy.** "what does it come to?" is "what is the answer?" (tour budget's prompt card, evaluate
    problems); "tap the bulb" in riff's idle nudge is "tap hint" (the button says hint); soundcheck's first line is
    "quick as you can. the bar only changes bonus fans. you can't lose fans."; the ticket's low-score lede is "every star
    practices. try it again." The web-vector tests restore the web's text through `IntendedCopy.ToWeb`.
40. **Ticket sales warm-up (level 0) is easier.** Seven in ten problems are single-digit facts; the rest are 2-digit x
    1-digit with no regrouping or a multiple of ten (23 x 3, 40 x 6) and 2-digit / 1-digit with no regrouping and no
    remainder (84 / 4, 60 / 3). No 3-digit dividends. Levels 1 to 3 are the web's.
41. **Ticket sales division: the step is the question.** After a miss, hint 2 shows its step on the prompt card as
    "step: how many 4s fit in 4?" (the card used to keep asking the whole question); at level 0 the step line is
    "4 x 7 = 28. that leaves 4. how many 4s fit in 4?". The whole answer still wins at any point; the step's own answer is
    welcomed, never a miss.
42. **The ticket's "first try" counts main songs only.** A miss replayed clean as a bonus song no longer reads as a first
    try (the saved count, the sold-out rule and the VIP percent keep the web rule).
43. **Snack table prompts are three short lines**, the part-of-a-part problem opens on the whole tray with its first part
    shaded, and the dog's first line there names the numbers.
