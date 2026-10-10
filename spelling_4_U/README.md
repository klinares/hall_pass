# Pop Quiz, Pop Star 🎸

A pink, pop-concert spelling game for a 5th grader. Spell each word right to win fans, build a streak, and sell out the show. Runs in any browser, with nothing to install, so it works on a laptop or iPad.

**Play:** <https://klinares.github.io/hall-pass/spelling_4_U/>

> The in-game title is *"you seem pretty smart for a 5th grader so in love with spelling 🎸"*.

## The five games

| Game | How it works |
|----|----|
| **missing letters** | A few letters are blanked out of the word. Type the whole word. |
| **missing vowels** | Every vowel is gone (`_mp_r_`). Put them back. |
| **remix** | Tap scrambled letter tiles in the right order. Great on a touchscreen. |
| **fan mail** | Pick the word that matches the definition. |
| **studio session** | The word is read aloud and Natalia types it, the closest thing to test day. |

Every game uses the same ten words for the week.

## Hints and scoring

Wrong answers get a little more help each time, never the whole word right away:

1.  **First miss:** how many letters, and the first letter.
2.  **Second miss:** the first and last syllable (`pop - ___ - ___ - tion`).
3.  **Third miss:** the word is shown and the game moves on. It comes back in the "rehearse again" list at the end.

Right on the first try earns fans (1,000 each, up to 5,000 as the streak grows). A right answer on a later try earns 300. After ten words she gets an "admit one" ticket and a setlist showing which words she nailed.

## Adding next week's words

All words live in one list near the top of the `<script>` section of `index.html`:

``` js
const WEEKS = [
  { id:"2026-10-10", label:"week of oct 10, 2026", words:[
    { w:"population", s:"pop-u-la-tion", h:"The total number of people living in a specific area" },
    ...
  ]}
];
```

To add a week, copy the whole block and paste it **above** the existing one, then change:

- `id`: the test date, `YYYY-MM-DD`
- `label`: what shows in the "tour date" dropdown
- each word's `w` (the word), `s` (syllables separated by hyphens) and `h` (the definition or clue)

The game opens to the newest week whose date is today or earlier. Older weeks stay in the dropdown for review.

## Run it locally

Double-click `index.html`, or serve the folder:

``` bash
cd spelling_4_U
python -m http.server 8000
# open http://localhost:8000
```

Read-aloud (the studio session game) uses the browser's built-in text-to-speech, which works best when the page is served over `https://` or `localhost`.

## Publish with GitHub Pages

1.  Make sure the game file is named `index.html`.
2.  In the repo: **Settings → Pages → Build and deployment → Deploy from a branch → `main` / root**.
3.  After a minute the game is live at `https://klinares.github.io/hall-pass/spelling_4_U/`.
4.  On the iPad, open the link in Safari and choose **Share → Add to Home Screen** for a one-tap icon.

Note: free GitHub Pages needs a public repo, so everything in it, including the first name that appears in the game, is publicly visible.

## Tech notes

- One self-contained file: plain HTML, CSS and JavaScript, with no build step or dependencies.
- Fonts come from Google Fonts. Offline, it falls back to Courier and a system handwriting font.
- Sound effects use the Web Audio API, and the read-aloud uses the Web Speech API.
- The design takes inspiration from the baby-pink, typewriter-and-handwriting look of an era of pop music. It's a fan-made study game and is **not affiliated with or endorsed by any artist or label**, and contains no official logos, images, lyrics or music.

## License

MIT. Have at it.
