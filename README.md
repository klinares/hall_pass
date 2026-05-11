<p align="center">

</p>

# lunchline

![](images/clipboard-3835007521.png)

Scrape your kid's school cafeteria menu and push it straight into Google Calendar — all from R.

The pain this solves: every Monday morning, the question is *"what's for lunch today, can I pack instead?"* The cafeteria publishes menus on a clunky [MealViewer](https://schools.mealviewer.com) page that nobody remembers to check. This pulls them down, formats them as nice all-day calendar events, and pushes them to a dedicated Google Calendar that shows up on every family device automatically.

## How it works

1.  **Scrape** — hits the public MealViewer JSON API and walks the four-level nest of `menuSchedules → menuBlocks → cafeteriaLineList → foodItemList`.
2.  **Shape** — collapses each `(date, meal_type)` into a single all-day event, with a clean headline (skipping the daily-repeat items like cereal and yogurt) and a full menu in the description.
3.  **Emit** — writes a properly-escaped RFC 5545 `.ics` file *and* pushes events to Google Calendar via the v3 REST API. Event IDs are deterministic per `(date, meal)`, so re-running the doc updates events in place instead of duplicating them.

Everything lives in one Quarto document, [`lunchline.qmd`](lunchline.qmd), which doubles as runnable code and as the documentation you're reading the executive summary of.

## Quick start

``` r
# one-time:
install.packages(c("tidyverse", "httr2", "lubridate", "googleAuthR", "cli"))

# render the whole pipeline:
quarto::quarto_render("lunchline.qmd")
```

That alone gets you `fallsmead_menus.ics`, which you can import into any calendar app. For the Google Calendar direct-push, follow the **Google Cloud setup** section inside the Quarto doc — it's a one-time 15-minute browser walkthrough to get OAuth credentials.

## Configuring for a different school

Open `lunchline.qmd` and change two constants:

``` r
SCHOOL_KEY  <- "FallsmeadES"   # the slug from your school's mealviewer URL
SCHOOL_NAME <- "Fallsmead ES"  # display name used in event titles
```

Find your school's key by visiting [`schools.mealviewer.com`](https://schools.mealviewer.com), searching for your school, and copying the trailing slug from the URL.

## Scheduling

Once the OAuth token is cached (first interactive run), the doc can render unattended. Schedule it weekly:

**macOS / Linux** (`crontab -e`):

``` cron
0 6 * * 0 cd /path/to/lunchline && quarto render lunchline.qmd
```

**Windows** — Task Scheduler, action: `quarto render lunchline.qmd` in this folder.

## What's in the repo

| File                  | Purpose                                           |
|------------------------------------|------------------------------------|
| `lunchline.qmd`       | The whole pipeline + documentation. Render this.  |
| `README.md`           | This file.                                        |
| `hero.svg`            | The fun image up top.                             |
| `.gitignore`          | Keeps OAuth secrets and cached tokens off GitHub. |
| `fallsmead_menus.ics` | Latest scraped menus (regenerated every render).  |

## Design notes

A few things that were worth getting right rather than fast:

-   **Idempotent event IDs.** The previous version of this script used `Sys.time()` in the UID, which meant every re-scrape produced fresh UIDs and re-importing the .ics created duplicate events. We now derive a stable ID from the date and meal type, so re-runs cleanly update in place.
-   **Proper RFC 5545 escaping.** Item names contain commas (`Chik'N Nuggets w/ Green Beans, Corn, and Dinner Roll`), which would break the ICS format if dropped in raw. We escape commas, semicolons, backslashes, and newlines per spec, and fold long lines at 75 octets.
-   **Direct REST over a wrapper package.** The R packages for Google Calendar (`gcalendr`, `googlecalendar`, `gcalendar`) all stopped getting updates around 2019–2020 and none made it to CRAN. We use `googleAuthR` purely for the OAuth dance and call the Calendar v3 endpoints directly with `httr2`. That stack is actively maintained and the surface area we need is small.
-   **Quarto, not Shiny.** The original concept floated a Shiny dashboard with a date picker. After thinking about it, a dashboard you have to open and click defeats the goal of "as automated as possible" — Quarto + cron just runs.

## License

MIT. Have at it.
