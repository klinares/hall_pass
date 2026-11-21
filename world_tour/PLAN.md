# world tour: plan

Phase 1 of 5. Nothing is built yet. This file is the contract for the build; open questions are at the bottom.

## What carries over from `spelling_4_U/`

`spelling_4_U/` itself is not touched. What World Tour copies from it, and keeps working the same way:

| Thing | How it works today | In World Tour |
|---|---|---|
| Word data | `WEEKS` array, `{id, label, words:[{w, s, h}]}`, newest first; opens the newest week on or before today | Same shape in `data/weeks.js`, plus optional `x` (sentence) and `prefix` fields |
| Scoring | clean answer: `1000 × min(streak, 5)`; hints but no wrong guess: 500, streak kept; after a wrong guess: 300, streak reset | Unchanged, and used for math too |
| Hint button | 💡 with the number left, each press gives a stronger clue, never the whole word, wiggles after 15 s idle, a press with nothing new to show is free | Shared `hints.js`; each mode supplies its own ladder |
| Wrong-answer ladder | miss 1: letter count and first letter; miss 2: first and last syllable (`chunks()`); miss 3: show the word and move on | Unchanged for spelling; math gets nudge → partial step → worked solution |
| Per-mode hints | the five ladders in the README's table | Ported one-for-one |
| Text-to-speech | silent warm-up utterance on the first tap (iPad Safari), `cancel()` before every `speak()`, rate .8, slow .55 for the syllable hint, falls back to showing the clue when there's no speech | `speech.js`, same steps, plus choosing an `en-US` voice when one exists |
| Sound | Web Audio oscillators, no files | `sound.js`, richer synth (below), with a mute toggle that's remembered |
| Voice | lowercase, `CHEERS = iconic, yes!!, flawless, encore!, obsessed, crowd screaming, main character`, uses her name now and then | `copy.js` holds every line of text in one place |
| Finish | "admit one" ticket stub, vip/ga, setlist marked nailed it / used a hint / rehearse again | Same stub, restyled, plus an encore of the misses |
| Leftover timers | `later()` ignores timeouts from a game she quit | Same guard in the show engine |

## Ground rules

- Static files only. ES modules, no build step, no runtime dependencies, relative paths only (`./css/...`, `../data/...`).
- ES modules don't load from `file://`, so double-clicking `index.html` won't work the way it does for the spelling game. Locally: `python -m http.server` from the repo root (Python 3.11 is installed). GitHub Pages is fine as is.
- GitHub Pages runs Jekyll by default, and Jekyll skips files and folders whose names start with `_`. No file name in `world_tour/` will start with `_`, so no `.nojekyll` or Pages change is needed.
- All art is inline SVG, CSS, or canvas. CSS masks for torn edges use SVG paths written in code (`data:image/svg+xml`), never raster images.
- Routing uses the URL hash (`#/map`, `#/show/remix`, `#/backstage`, `#/journal`, `#/office`), so the iPad back gesture works and nothing depends on server routes.

## File layout

```
world_tour/
├── index.html              shell: fonts, sprite mount, #app, footer (sound toggle, manager link)
├── README.md               play link, how to update words, run locally, run tests
├── PLAN.md                 this file
├── .gitignore              tests/e2e/node_modules, tests/e2e/screenshots
├── css/
│   ├── tokens.css          colors, type scale, spacing, shadows, radii, z-index; [data-theme] overrides
│   ├── base.css            reset, lowercase type, focus rings, reduced-motion overrides
│   ├── paper.css           torn edges, washi tape, halftone, polaroid, perforation, scribble underline, arrows
│   ├── components.css      squish buttons, tiles, slots, hint button, stacked fraction, toast, ticker, dog bubble
│   └── screens.css         map, show frame, ticket, backstage, journal, office
├── js/
│   ├── main.js             boot: load state, apply theme, unlock audio on first tap, start router
│   ├── router.js           hash → screen module, paper-slide transition, input never blocked
│   ├── store.js            localStorage under one versioned key, migrations, in-memory fallback
│   ├── progress.js         fans, day streak, skill levels, per-word stats, review queue, time played
│   ├── show.js             show engine: builds the queue, re-inserts misses, scores, ticket, encore
│   ├── hints.js            shared hint button, 15 s nudge, hint accounting
│   ├── sound.js            synth: strum, thud, tap, pop, crowd swell; mute
│   ├── speech.js           text-to-speech with the iPad warm-up
│   ├── fx.js               confetti canvas (mini stickers, hearts, stars), toast
│   ├── dnd.js              pointer-event drag, plus tap-to-select then tap-to-place
│   ├── motion.js           reduced-motion flag, squish/pop/wobble helpers (Web Animations API)
│   ├── copy.js             all cheers, dog lines, ticker text
│   └── util.js             shuffle, pick, escape, local-date helpers
├── art/
│   ├── sprite.js           <symbol> defs for every sticker, injected once into index.html
│   ├── sticker.js          sticker(id, {rotate, size, peel}) → markup
│   ├── dog.js              the tour dog: poses and outfit layers
│   └── map.js              the tour map: paper, route path, venue drawings
├── screens/
│   ├── map.js              home: tour map, fans, day streak, dog
│   ├── venue.js            venue card: pick tonight's show (spelling or math mode), pick week
│   ├── ticket.js           ticket stub + setlist + encore button
│   ├── backstage.js        shop and locker (phase 5)
│   ├── journal.js          sticker book (phase 5)
│   └── office.js           manager's office (phase 5)
├── modes/
│   ├── spelling/           slots, vowels, remix, fanmail, studio, lyric, prefix
│   └── math/               merch, tickets, beat, stage, truck, budget, soundcheck
├── math/                   pure functions only: no DOM, no storage, runs in Node
│   ├── config.js           ENABLED_SKILLS and per-skill settings, the one switchboard
│   ├── rng.js              seeded PRNG (mulberry32) so tests are repeatable
│   ├── rational.js         exact fractions with integer numerator/denominator, gcd, lcm
│   ├── money.js            decimals held as integer hundredths, so 0.1 + 0.2 is never 0.30000000000000004
│   ├── answer.js           parses and compares typed answers (see "equivalent answers")
│   ├── mistakes.js         distractor builders, each tagged with the mistake it models
│   └── gen/
│       ├── decimals.js     merch table
│       ├── multdiv.js      ticket sales
│       ├── fractions.js    beat maker
│       ├── coords.js       stage designer
│       ├── volume.js       pack the truck
│       ├── orderops.js     tour budget
│       └── fluency.js      soundcheck
├── data/
│   ├── weeks.js            the only file to edit each week
│   ├── venues.js           the route: venues, fan thresholds, backdrops
│   ├── shop.js             cosmetic catalog and prices
│   └── milestones.js       journal sticker rules
└── tests/
    ├── math/*.test.js      node --test, one file per generator plus answer.js and rational.js
    └── e2e/
        ├── package.json    playwright, dev-only, never loaded by the game
        └── play.mjs        plays each mode, screenshots at 375×812, 820×1180, 1180×820, 1366×768;
                            second pass with localStorage throwing; fails on any console error
```

## Weekly word data

`data/weeks.js` exports `WEEKS` in the same shape as today, newest week first. Two optional fields:

```js
export const WEEKS = [
  { id:"2026-10-26", label:"week of oct 26, 2026", words:[
    { w:"irrational", s:"ir-ra-tion-al",
      h:"Not based on reason or clear thinking, like a fear that makes no sense",
      x:"being scared of a tiny spider is totally ___, but here we are.",   // lyric writer; ___ marks the blank
      prefix:{ p:"ir", root:"rational", means:"not" } },                    // prefix mixer
    ...
```

- `x`: one sentence with `___` where the word goes. Words without `x` are skipped in lyric writer, and the mode is hidden for a week where fewer than 4 words have one.
- `prefix`: `p` (prefix), `root`, `means` (what the prefix adds: `not`, `between`, `again`, ...). Prefix mixer only shows for weeks where at least 4 words have it. The prefixes on the board come from that week's own words, and the game checks that each root combines with exactly one of them into a week word.
- I'll copy both existing weeks and write draft `x` sentences for all 22 words, and `prefix` for the oct 26 list, for you to check.
- `spelling_4_U/` keeps its own copy of `WEEKS`. If both games stay live, a new week has to go in both files.

## Game structure

**Venues** (`data/venues.js`), unlocked by lifetime fans:

| # | venue (fictional) | lifetime fans to unlock |
|---|---|---|
| 1 | the corner coffee shop, open mic | 0 |
| 2 | record store basement | 20,000 |
| 3 | rec center battle of the bands | 60,000 |
| 4 | rooftop party | 125,000 |
| 5 | the club downtown | 225,000 |
| 6 | old movie-palace theater | 375,000 |
| 7 | summer festival side stage | 600,000 |
| 8 | the arena | 900,000 |

A perfect 10-question show is worth 40,000 fans under the current scoring, so a new venue roughly every 2 to 6 sessions early on, slowing toward the arena.

All modes are playable from day one, because she needs this week's words now. A venue changes the show's backdrop, crowd size, crowd sound, and ticket design, and unlocking one awards a journal sticker. (Open question 2.)

**Two fan numbers**, so nothing is ever taken away: `lifetime` only goes up and drives the map; `balance` is what she spends backstage. The map shows "total fans"; backstage shows "fans to spend".

**A show:** 10 questions from one mode.
- Spelling picks 10 of the week's words in this order: words she missed last session, then least practiced, then random. With 12 words, every word gets played within two shows.
- Math picks problems at her current level for that skill, starting with any problems saved for review.
- A missed item comes back three questions later in the same show, at most twice per show, as a bonus song that doesn't count toward the 10.
- The show ends with the ticket stub, then "encore": a short replay of everything she missed, worth half fans.

**Day streak:** a day counts when she finishes at least one show, using the device's local date. Missing a day resets it; the best streak is kept for the journal.

## Saved state

One key: `worldtour.v1`. `store.js` loads it in try/catch, runs it through `migrate()` (which fills in missing fields with defaults, so older saves keep working), and saves with a 500 ms debounce plus on `visibilitychange`. If storage throws or is missing, the game runs on an in-memory copy and the footer shows a small "progress won't save on this device" note.

```js
{
  v: 1,
  created: "2026-11-17", updated: 1763400000000,
  settings:  { sound: true, theme: "tour-pink", motion: "auto" },          // motion: auto | reduced
  fans:      { balance: 0, lifetime: 0 },
  streak:    { days: 0, best: 0, last: null },                              // last = "YYYY-MM-DD"
  venues:    { unlocked: ["coffee"], seen: ["coffee"] },                     // seen = unlock animation already played
  skills: {                                                                 // one entry per math skill and per spelling mode
    "frac-add": { level: 1, run: 0, missRun: 0,                              // run: correct in a row; missRun: misses in a row
                  a: 0, c: 0, h: 0,                                         // attempts, correct, needed hints
                  byLevel: { "1": [0,0], "2": [0,0], "3": [0,0] },          // [attempts, correct]
                  mistakes: { "added-denominators": 0 } }                   // distractor tags she picked
  },
  words: {                                                                  // keyed by the word itself
    "illegible": { a: 0, c: 0, first: 0, h: 0, miss: 0, last: null }
  },
  review: {
    words: ["illegible"],                                                   // missed last session, played first next time
    math:  [{ skill: "frac-add", level: 2, seed: 918273 }]                  // regenerated from the seed, so it's the same problem
  },
  shows:   [{ d: "2026-11-17", mode: "remix", venue: "coffee", n: 10,
              first: 8, hinted: 1, fans: 31000, secs: 260 }],              // last 200 shows
  time:    { "2026-11-17": 1340 },                                          // active seconds per day, last 120 days
  inventory: { owned: ["theme:tour-pink", "guitar:classic", "dog:none"],
               equipped: { theme: "tour-pink", guitar: "classic", dog: "none" } },
  journal:   { stickers: { "first-show": "2026-11-17" },                    // id → date earned
               layout: { "first-show": { x: .32, y: .18, r: -2 } } }        // where she placed it (0–1 of the page)
}
```

Active time counts only while the tab is visible and she has touched something in the last 60 seconds.

Adaptive levels (math and spelling modes alike): every skill starts at level 1, goes up after 4 correct in a row, goes down after 2 misses in a row, and stays within 1 to 3. "Correct" here means right before the third wrong answer; hints don't count as misses.

## Math

**Generator contract.** Every file in `math/gen/` exports pure functions; the modes only draw what they return.

```js
generate(level, rng) → {
  skill, level, kind,            // kind: which variant, e.g. "fill-measure" or "make-change"
  params,                        // the numbers the mode needs to draw the scene
  answer,                        // { type: "money"|"int"|"fraction"|"point"|"expr", value }
  accept: { simplest: false },   // true → the prompt says "in simplest form"
  choices,                       // multiple-choice steps only: [{ value, correct, why }]; why = mistake tag
  steps                          // hint ladder data: [nudge, partial, worked]; the mode draws the visual
}
```

**Skills** (all on by default in `math/config.js`; set any to `false` to hide its mode):

| Mode | Skill | L1 | L2 | L3 |
|---|---|---|---|---|
| merch table | decimals (5.NBT.7) | 2 items under $10, change from $10 | 3 items, a quantity × price, change from $20 | decimal × decimal (2.5 × 1.2), change from $50 |
| ticket sales | mult/div (5.NBT.5–6) | 2-digit × 1-digit, 3-digit ÷ 1-digit | 2-digit × 2-digit, 3-digit ÷ 2-digit | 3-digit × 2-digit, 4-digit ÷ 2-digit, "how many full rows" with a remainder |
| beat maker | fraction add/subtract (5.NF.1–2) | fill one whole with halves, quarters, eighths | unlike denominators from {2,3,4,6,8,12} | mixed-number targets and taking notes out |
| stage designer | coordinate plane (5.G.1–2) | place or read a point, axes to 6 | axes to 10 | "move it 3 right and 2 up", then where is it |
| pack the truck | volume (5.MD.3–5) | count unit crates that are all visible | l × w × h | two prisms stacked, or find a missing side |
| tour budget | order of operations (5.OA.1–2) | 2 operations with parentheses | 3 operations | pick the expression for a 2-step story, then evaluate it |
| soundcheck | fluency, powers of 10 (5.NBT.1–2) | × ÷ facts to 10 | facts to 12, × and ÷ by 10 and 100 | ×/÷ by 10, 100, 1000 with decimals, 10³ |

Grade-level bounds the tests enforce: money under $100, products at most 3 digits × 2 digits, every division exact unless the problem is about a remainder, denominators at most 12, coordinates 0–10, volumes at most 1,000 cubic units, no negative numbers anywhere along the way.

**Builders, not quizzes.** Beat maker: tap or drag note blocks (½, ¼, ⅛, ⅓, ⅙, 1⁄12) into a measure drawn on the common-denominator grid; it plays the beat when the measure is exactly full, and too much shows the overflow sticking out past the bar line. Stage designer: tap a grid point to place a light; to read one, she enters the ordered pair with two number pickers. Pack the truck: an isometric SVG trailer where tapping a floor cell stacks a crate; she can also type the volume.

**Equivalent answers** (`math/answer.js`). `0.5`, `.5`, `.50`, `0.50` are equal; `$3.50` and `3.5` are equal for money; `1/2` and `2/4` are equal unless `accept.simplest` is set, in which case the prompt says "simplest form" and `2/4` gets "right amount, now simplify it" instead of a miss; `1 1/2`, `3/2`, and `1.5` are equal for fraction answers. Typed input uses an on-screen keypad with a fraction key, so nothing depends on the iPad keyboard.

**Hint ladder.** Wrong answers and the 💡 button share one ladder: (1) a nudge ("the denominators don't match yet"); (2) a partial step (rewrites both fractions in twelfths); (3) the worked solution with the visual model (fraction bars, area model for multiplication, number line for decimals, the grid for coordinates, crate layers for volume), then move on. Each step costs fans, never the streak, exactly like the spelling hints. Soundcheck is the only mode with a timer, and it only changes bonus fans.

**Tests** (`node --test world_tour/tests/math`): each generator runs 3,000 times per level with fixed seeds, and each run is checked for: an answer that an independent calculation agrees with; exactly one correct choice and no duplicate choices; distractors that differ from the answer and carry a mistake tag; and every number inside the bounds above.

## Art

**Themes.** `tokens.css` defines every color as a custom property: `--paper`, `--paper-deep`, `--ink`, `--card`, `--accent`, `--accent-2`, `--pop`, plus shadow and halftone colors. The default `tour-pink` uses `#fdb1cb`, `#f98bb0`, black, white, `#a8a9fc`, and `#2f2fc1`. The unlockable `peach-navy` swaps them for a peach paper (around `#ffcfb5`) and navy ink (around `#1f2a4a`). Every SVG asset fills from these variables, so the art changes with the theme.

**Type.** Caveat Brush for display, Courier Prime for body and numbers, lowercase throughout. Fractions are a stacked component (numerator, bar, denominator) sized for the 375px screen; there is never an inline `3/4` in a problem.

**Paper kit** (`paper.css`): torn top and bottom edges (CSS mask), washi tape strips in 3 patterns, halftone dots, polaroid frame, ticket perforation, hand-drawn underline and arrow (SVG strokes), the `6px 6px 0` hard shadow, and a random 1–3° tilt on cards and stickers.

**Stickers** (`art/sprite.js`), each a `<symbol>` with a thick black outline and a white sticker border:

| | | | |
|---|---|---|---|
| guitar | amp | microphone | tour bus |
| merch tee | vinyl record | stage lights | speaker crate |
| star | heart | lightning bolt | ticket |
| cassette | headphones | setlist page | backstage pass |
| guitar pick | music note | coffee cup (venue 1) | trophy (milestones) |

The first 11 are required; the rest are for the journal and venues.

**Tour dog** (`art/dog.js`). One small scruffy dog in a band tee, the same silhouette in every pose: idle (tail wag, slow blink), cheering (jumps, ears up), thinking (head tilt, paw on chin, used with hints), sleepy (curled up on the amp; shown when she returns after a break), and oops (wince; wrong answers). Outfit layers drawn on top: bandana, beanie, star sunglasses, spiked collar, tiny headphones.

**Map** (`art/map.js`). A torn notebook page with a dashed route drawn in marker; eight venue doodles along it; the tour bus sits at her newest venue; locked venues are pencil sketches with the fans needed on a sticky note. On a phone the route runs top to bottom and scrolls; on iPad landscape and laptop it winds left to right.

**Venue drawings:** coffee shop, record store, rec center, rooftop, club, theater, festival tent, arena. Each is a simple line drawing in the sticker style.

**Motion** (all under 400 ms, none blocks input, all replaced by short fades under `prefers-reduced-motion`): buttons squash to 0.92 and spring back; right answers pop with overshoot; stickers peel on and slap down; wrong answers wobble; screens slide like paper. Confetti is drawn on canvas.

**Sound** (`sound.js`, Web Audio only): a strummed major chord (six staggered plucked tones) for right answers, a muffled low thud for misses, a soft tick for taps, a pop for stickers, and a crowd swell (filtered noise plus claps) at the end of a show. Bigger venues get a bigger crowd. Audio starts on her first tap, which iPad Safari requires.

**Fictional world only.** No real artists, songs, lyrics, album art, logos, or venue names. The ticker, posters, and setlists use made-up song titles written for the game.

## Phases

1. **Plan:** this file. ← here
2. **Look and feel:** tokens and paper kit, the full sticker sheet, the dog in all poses, the map home screen, the venue card, and one complete spelling mode with its hints and the ticket stub. I'd pick **remix**, because letter tiles show off the touch feel and the squishy motion best.
3. **Spelling parity:** the other four original modes, lyric writer, prefix mixer, saved progress, review queue, encore.
4. **Math:** `rational.js`, `money.js`, `answer.js`, and all seven generators with tests first; then the modes, merch table and beat maker first.
5. **Meta:** backstage, journal, manager's office, peach-navy theme, `README.md`.

Each phase ends with: unit tests passing, Playwright screenshots at 375×812, 820×1180 (iPad portrait), 1180×820 (iPad landscape), and 1366×768 that I've looked at and fixed, zero console errors, a run with `localStorage` disabled, and a plain list of what isn't done or verified. I won't push or change Pages settings.

## Open questions

1. **The dog's name.** It talks to her, so it needs one. My placeholder is "riff". Natalia may want to pick it.
2. **Venues as backdrops, not gates.** I've planned all modes open from day one, with venues changing the backdrop, crowd, and ticket. The alternative is that venues unlock modes, which would hold back math modes her class might need. OK to keep everything open?
3. **Prefix roots that aren't words.** In the oct 26 list, `intercept` and `intercede` have Latin roots (`cept`, `cede`) rather than standalone words. Options: leave them out of prefix mixer, or include them and label the piece "root" instead of "word". I'd leave them out.
4. **Installing dev tools.** This machine has Python but no Node, so I can't run Node's test runner or Playwright yet. I'd like to install Node LTS with `winget install OpenJS.NodeJS.LTS`, then `npm install` Playwright and its Chromium and WebKit browsers inside `world_tour/tests/e2e/` (git-ignored). WebKit is the closest stand-in for iPad Safari, but it isn't the real thing, so you'd still want a quick look on the iPad.
5. **The spelling mode for phase 2.** I'd build remix. Say so if you'd rather see studio session (listening) first.
