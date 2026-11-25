# hall pass 🎟️

![](images/clipboard-2706480395.jpeg)

Small tools that make life with a school-age kid a little easier. Built for Fallsmead, shared in case your school needs the same thing.

| Project | What it does | Try it |
|------------------------|------------------------|------------------------|
| [`chippy/`](chippy/) | **Operation: Where Is Chippy?**, a 3–5 minute spy-briefing game for a 1st-grade family-traditions presentation: find Chippy the elf hidden around the house, then learn his true identity. Works on a smartboard, laptop or iPad. | [Play it](https://klinares.github.io/hall_pass/chippy/) |
| [`lunchline/`](lunchline/) | Scrapes the school cafeteria menu into a `.ics` file you can import into Google Calendar, so "what's for lunch, can I pack instead?" is answered before Monday morning. | R + Quarto, [read more](lunchline/README.md) |
| [`spelling_4_U/`](spelling_4_U/) | **Pop Quiz, Pop Star**, a pink pop-concert spelling game that gets a 5th grader ready for the weekly test. Works on a laptop or iPad, no install. | [Play it](https://klinares.github.io/hall_pass/spelling_4_U/) |
| [`tour/`](tour/) | **World Tour**, the spelling and 5th-grade math game as a Unity web build. This is the version to play. | [Play it](https://klinares.github.io/hall_pass/tour/) |
| [`world_tour/`](world_tour/) | The original plain HTML/JavaScript version of World Tour, kept as the reference design. | [Play it](https://klinares.github.io/hall_pass/world_tour/), [read more](world_tour/README.md) |
| [`world_tour_unity/`](world_tour_unity/) | Unity source project that builds `tour/`. | [read more](world_tour_unity/README.md) |

## How the repo is laid out

```
hall_pass/
├── chippy/            static HTML "find Chippy" family-traditions game
├── lunchline/         R + Quarto pipeline for cafeteria menus
├── spelling_4_U/      static HTML spelling game
├── tour/              World Tour, Unity web build served by GitHub Pages
├── world_tour/        World Tour, original HTML/JS version
├── world_tour_unity/  Unity source project for tour/
├── unity_team/        planning notes and reports from building the Unity version
├── images/            images used by this README
├── publish.bat        double-click to publish word-list and math-setting edits to tour/
└── README.md          you are here
```

Each project lives in its own folder with its own README, so they never get in each other's way.

## Hosting

Everything is served by **GitHub Pages** (Settings → Pages → deploy from the `main` branch, root folder), so any folder is reachable at `https://klinares.github.io/hall_pass/<folder>/`. The games are static files: `spelling_4_U/` and `world_tour/` are plain HTML, and `tour/` is a prebuilt Unity WebGL build.

To change the weekly words or math settings for `tour/`, edit the files in `tour/StreamingAssets/` and double-click `publish.bat`; the live game updates a minute or two later.

## Adding a new project

1.  Make a new folder (e.g. `math-facts/`).
2.  Give it a short `README.md`: what it does, how to run it.
3.  Add a row to the table above.

## License

MIT. Have at it.
