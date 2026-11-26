# weeks.json: adding a week

Edit `world/StreamingAssets/weeks.json` and put the new week first, inside `"weeks": [ ... ]`, with a comma after its closing `}`.

- `id`: the test date, written 2026-11-23. The newest week on or before today opens by itself.
- `label`: the name in the week menu, like "week of nov 23, 2026".
- `words`: one `{ }` per word, separated by commas. Each needs `w` (the word, lowercase), `s` (syllables with hyphens, like "ex-am-ple") and `h` (the clue).
- `x` (optional): a sentence with `___` where the word goes. Lyric writer needs 4 words that have one.
- `prefix` (optional): `{ "p": "ir", "root": "rational", "means": "not" }`, where p + root spells the word. Prefix mixer needs 4.
- Use double quotes, and no comma after the last item in a list. Then run `node world_tour_unity/Tools/validate_data.mjs`, and push only when it says ok.
