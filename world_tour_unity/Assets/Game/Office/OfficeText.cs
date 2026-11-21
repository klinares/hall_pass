using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Game.Map;
using OfficeRules = WorldTour.Core.Meta.Office;

namespace WorldTour.Game.Office
{
    /// <summary>One "at a glance" pair (office.js:39-46).</summary>
    public struct GlanceItem
    {
        public string Label;
        public string Value;
        public GlanceItem(string label, string value) { Label = label; Value = value; }
    }

    /// <summary>One line of "time played, last 14 days" (office.js:53).</summary>
    public struct DayLine
    {
        /// <summary>"fri, nov 20".</summary>
        public string Label;
        /// <summary>The bar's width in percent of the row, as the web's toFixed(1) gives it.</summary>
        public double Width;
        /// <summary>"12 min, 2 shows".</summary>
        public string Value;
    }

    /// <summary>
    /// Every word the manager's office shows, and the formatting of its numbers, ported from world_tour/screens/office.js
    /// (copy word for word; curly apostrophes and quotes kept). The export and import copy is Unity's own (the web game
    /// has neither), in the same voice. Plain C# with no UnityEngine, so it is tested in edit mode against the web's own
    /// helpers (Tools/office_vectors.mjs).
    /// </summary>
    public static class OfficeText
    {
        static readonly string Rq = ((char)0x2019).ToString();   // ’
        static readonly string Lq2 = ((char)0x201C).ToString();  // “
        static readonly string Rq2 = ((char)0x201D).ToString();  // ”
        static readonly string Ell = ((char)0x2026).ToString();  // …
        /// <summary>office.js:14 dash: an em dash for "no value".</summary>
        public static readonly string None = ((char)0x2014).ToString();

        // ---------- office.js copy ----------
        /// <summary>index.html:32, the footer link, word for word (straight apostrophe).</summary>
        public const string Link = "manager's office";
        public const string Back = "back to the game";
        /// <summary>The web backlink's i-back icon, as the arrow glyph (drawn from the TMP glyph sprites).</summary>
        public static readonly string BackArrow = ((char)0x2190).ToString();
        public static readonly string Title = "manager" + Rq + "s office";
        public const string Sub = "for grown-ups. everything here is saved on this device only.";
        public const string GlanceHead = "at a glance";
        public const string ShowsFinished = "shows finished";
        public const string TimePlayed = "time played";
        public const string BestStreak = "best day streak";
        public const string TotalFans = "total fans";
        public const string JournalStickers = "journal stickers";
        public const string WaitingReview = "waiting for review";
        public const string TimeHead = "time played, last 14 days";
        public const string TimeFoot = "counts only while the game is on screen and she has touched it in the last minute.";
        public const string AccuracyHead = "accuracy by mode";
        public static readonly string AccuracyFoot = Lq2 + "right" + Rq2 + " means she got there before the answer was shown (hints allowed); " + Lq2 + "hints" + Rq2
            + " is how often she used the bulb. levels go up after 4 right in a row and down after 2 misses in a row.";
        /// <summary>
        /// The accuracy note as the Unity build shows it: the web's first sentence, then the level rules actually in force per subject
        /// (the web's flat "4 right in a row" is only true of spelling here). <see cref="AccuracyFoot"/> stays the web's wording for the copy test.
        /// </summary>
        public static string AccuracyNote(int spellUp, int spellDown, int mathUp, int mathDown)
        {
            return Lq2 + "right" + Rq2 + " means she got there before the answer was shown (hints allowed); " + Lq2 + "hints" + Rq2
                + " is how often she used the bulb. levels go up after " + spellUp + " right in a row in spelling and " + mathUp
                + " in math, and down after " + (spellDown == mathDown ? spellDown + " misses in a row" : spellDown + " (spelling) or " + mathDown + " (math) misses in a row") + ".";
        }

        /// <summary>The level column for the office's math and spelling rows: "warm-up", "level 2", "level 2 (top for now)".</summary>
        public static string LevelCell(int level, int cap, int highest)
        {
            string s = level == 0 ? "warm-up" : "level " + level.ToString(CultureInfo.InvariantCulture);
            return cap < highest && level >= cap ? s + " (top for now)" : s;
        }

        public const string Spelling = "spelling";
        public const string Math = "math";
        public static readonly string[] SkillHeads = { "mode", "level", "tries", "right", "hints", "shows" };
        public const string WordsHead = "words missed most";
        public static readonly string[] WordHeads = { "word", "missed", "played", "1st try", "last" };
        public const string WordsEmpty = "no missed words yet.";
        public const string MistakesHead = "math mistakes seen most";
        public static readonly string[] MistakeHeads = { "mistake", "mode", "times" };
        public static readonly string MistakesFoot = "from wrong answers that match a known mistake. other wrong answers aren" + Rq + "t counted here.";
        public const string MistakesEmpty = "no math mistakes recorded yet.";
        public const string SettingsHead = "settings";
        public const string ReduceMotion = "reduce motion (also follows the device setting)";
        // ---------- how hard it plays (Unity only, phase 7.1; parent-facing) ----------
        public const string DifficultyHead = "how hard it plays";
        public const string DifficultyBody = "gentle takes the top level of a subject one step lower and asks for 8 right answers in a row before the level goes up. nothing she has earned changes either way. standard is the normal game.";
        public const string GentleSpelling = "gentle spelling";
        public const string GentleMath = "gentle math";

        /// <summary>The numbers in force right now, for the line under the switches.</summary>
        public static string RuleFoot(int mathUp, int mathDown, int spellUp, int spellDown)
        {
            return "right now, math goes up a level after " + mathUp + " right in a row and down after " + mathDown + " misses in a row; spelling goes up after "
                + spellUp + " and down after " + spellDown + ". each math mode" + Rq + "s top level is set in math_config.json.";
        }

        public const string ResetHead = "start over";
        public static readonly string ResetBody = "erases fans, levels, stickers, purchases and history on this device. the weekly words aren" + Rq + "t affected.";
        public static readonly string ResetButton = "reset progress" + Ell;
        public const string Sure = "are you sure?";
        public static readonly string SureRest = " this can" + Rq + "t be undone.";
        public const string Cancel = "cancel";
        public const string Erase = "yes, erase everything";

        // ---------- export and import (Unity only, same voice) ----------
        public const string KeepHead = "keep a copy";
        public const string KeepBody = "browser data can get cleared. save her progress as a file, or copy it as a code, and keep it somewhere safe.";
        public const string DownloadButton = "download save file";
        public const string CopyButton = "copy save code";
        public static readonly string DownloadFailed = "the browser didn" + Rq + "t save the file. try copying the code instead.";
        public static readonly string CopyFailed = "the browser wouldn" + Rq + "t copy. try the save file instead.";
        public const string LoadHead = "load a copy";
        public const string LoadBody = "replaces the progress on this device with a saved file or code. the progress here now is kept as a backup on this device first.";
        public static readonly string PickButton = "choose a save file" + Ell;
        public const string PastePrompt = "click here, then paste a save code (ctrl+v)";
        public const string PasteFocused = "paste now (ctrl+v)";
        public const string PickWaiting = "waiting for a file.";
        public const string PickCancelled = "no file chosen.";
        public static readonly string PickUnreadable = "that file couldn" + Rq + "t be read. nothing was changed.";
        public static readonly string PickTooBig = "that file" + Rq + "s too big to be a world tour save. nothing was changed.";
        public static readonly string PickEditor = "choosing a file only works in the browser. paste the code or the file" + Rq + "s text instead.";
        public const string NothingChanged = " nothing was changed.";
        public const string LoadYes = "yes, load this copy";
        public const string LoadQuestion = "load this copy? it replaces the progress here.";
        public static readonly string PickFailed = "the browser wouldn" + Rq + "t open a file picker. paste the code instead.";
        public const string HeadsUp = "heads up: ";
        public const string Loaded = "loaded. the progress from before is kept as a backup on this device.";
        public const string LoadedNothingToKeep = "loaded.";
        public static readonly string NotSaving = " this device isn" + Rq + "t saving progress, so it only lasts until the page closes.";
        public static readonly string BackupFailed = "couldn" + Rq + "t keep a backup of the progress here now, so nothing was changed.";

        // ---------- office.js helpers ----------

        /// <summary>office.js:12 plural: "1 word", "3 words".</summary>
        public static string Plural(long n, string w)
        {
            return n.ToString(CultureInfo.InvariantCulture) + " " + w + (n == 1 ? "" : "s");
        }

        /// <summary>office.js:13 dash.</summary>
        public static string Dash(string v) { return v ?? None; }

        /// <summary>office.js:14 pctCell: "86%", or a dash with no attempts.</summary>
        public static string PctCell(int? v) { return v.HasValue ? v.Value.ToString(CultureInfo.InvariantCulture) + "%" : None; }

        static readonly string[] Weekdays = { "sun", "mon", "tue", "wed", "thu", "fri", "sat" };
        static readonly string[] Months = { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };

        /// <summary>
        /// office.js:15 day: "yyyy-mm-dd" as en-US { weekday: short, month: short, day: numeric }, lowercased ("fri, nov 20").
        /// The names are written out, so no culture data is needed in the web player. Anything else comes back as given.
        /// </summary>
        public static string Day(string iso)
        {
            DateTime d;
            if (iso == null || !DateTime.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return iso ?? "";
            return Weekdays[(int)d.DayOfWeek] + ", " + Months[d.Month - 1] + " " + d.Day.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>office.js:20 peak: the longest day, at least a minute.</summary>
        public static int Peak(IReadOnlyList<OfficeDay> daily)
        {
            int p = 60;
            foreach (OfficeDay d in daily) if (d.Secs > p) p = d.Secs;
            return p;
        }

        /// <summary>office.js:53, one day's line.</summary>
        public static DayLine DayOf(OfficeDay d, int peak)
        {
            string v = OfficeRules.Mins(d.Secs) + (d.Shows != 0 ? ", " + d.Shows.ToString(CultureInfo.InvariantCulture) + " show" + (d.Shows > 1 ? "s" : "") : "");
            return new DayLine { Label = Day(d.D), Width = JsNum.ToFixedNumber((double)d.Secs / peak * 100, 1), Value = v };
        }

        /// <summary>The bar width as the web writes it in the style attribute ("45.3").</summary>
        public static string WidthText(double w) { return w.ToString("0.0", CultureInfo.InvariantCulture); }

        /// <summary>office.js:39-46.</summary>
        public static List<GlanceItem> Glance(OfficeReport o)
        {
            return new List<GlanceItem>
            {
                new GlanceItem(ShowsFinished, o.Shows.ToString(CultureInfo.InvariantCulture)),
                new GlanceItem(TimePlayed, OfficeRules.Mins(o.TotalSecs)),
                new GlanceItem(BestStreak, o.Best.ToString(CultureInfo.InvariantCulture)),
                new GlanceItem(TotalFans, MapText.Fmt(o.Lifetime)),
                new GlanceItem(JournalStickers, o.Stickers.ToString(CultureInfo.InvariantCulture) + " / " + o.StickersTotal.ToString(CultureInfo.InvariantCulture)),
                new GlanceItem(WaitingReview, Plural(o.ReviewWords, "word") + ", " + Plural(o.ReviewMath, "problem")),
            };
        }

        /// <summary>office.js:27, one row of the accuracy table.</summary>
        public static string[] SkillRow(OfficeSkill k)
        {
            return new[] { k.Name, Int(k.Level), Int(k.A), PctCell(k.Acc), PctCell(k.Hint), Int(k.Shows) };
        }

        /// <summary>office.js:27 tr.unplayed (drawn at half strength).</summary>
        public static bool Unplayed(OfficeSkill k) { return k.A == 0; }

        /// <summary>office.js:66, one most-missed word.</summary>
        public static string[] WordRow(OfficeWord w)
        {
            return new[] { w.W, Int(w.Miss), Int(w.A), PctCell(w.First), Dash(w.Last) };
        }

        /// <summary>office.js:75, one mistake pattern.</summary>
        public static string[] MistakeRow(OfficeMistake m)
        {
            return new[] { m.Label, m.Mode, Int(m.N) };
        }

        static string Int(long n) { return n.ToString(CultureInfo.InvariantCulture); }

        // ---------- export and import ----------

        /// <summary>The download's file name: "world-tour-save-2026-11-20.json".</summary>
        public static string FileName(string today) { return "world-tour-save-" + (today ?? "") + ".json"; }

        public static string Downloaded(string name) { return "the save file is in your downloads: " + name; }

        public static string DownloadedEditor(string path) { return "editor: the file is at " + path; }

        public static string Copied(int chars) { return "code copied (" + MapText.Fmt(chars) + " characters). paste it somewhere safe."; }

        public static string Pasted(int chars) { return "pasted " + MapText.Fmt(chars) + " characters."; }

        /// <summary>A failed check, ending with " nothing was changed."</summary>
        public static string Rejected(string error) { return (error ?? "that can" + Rq + "t be read.") + NothingChanged; }

        /// <summary>One save in a line: "12,400 total fans, 3 of 8 venues, 14 shows, 5 journal stickers".</summary>
        public static string Summary(SaveSummary s, int venuesTotal)
        {
            return MapText.Fmt(s.Lifetime) + " total fans, " + Int(s.Venues) + " of " + Int(venuesTotal) + " venues, "
                + Plural(s.Shows, "show") + ", " + Plural(s.Stickers, "journal sticker");
        }

        /// <summary>The import's question: what the copy has, what is here now, and what would be lost.</summary>
        public static List<string> PreviewLines(ImportPreview p, int venuesTotal)
        {
            var lines = new List<string>();
            if (p == null || !p.Ok) return lines;
            lines.Add("this copy: " + Summary(p.From, venuesTotal) + (p.From.LastPlayed != null ? ", last played " + Day(p.From.LastPlayed) : "") + ".");
            if (p.Current != null) lines.Add("here now: " + Summary(p.Current, venuesTotal) + (p.Current.LastPlayed != null ? ", last played " + Day(p.Current.LastPlayed) : "") + ".");
            foreach (string loss in p.Losses) lines.Add(HeadsUp + loss + ".");
            return lines;
        }

        /// <summary>What the office says after a confirmed import.</summary>
        public static string AfterImport(bool changed, BackupOutcome backup, bool saved)
        {
            if (!changed) return BackupFailed;
            string s = backup == BackupOutcome.NothingToKeep ? LoadedNothingToKeep : Loaded;
            return saved ? s : s + NotSaving;
        }

        /// <summary>The trace the JS cross-check compares (Tools/office/office_vectors.mjs <c>traceOf</c>), non-ASCII as \uXXXX.</summary>
        public static string Trace(OfficeReport o)
        {
            var t = new List<string>();
            t.Add("g;" + string.Join(";", Glance(o).Select(g => g.Label + "=" + g.Value)));
            int peak = Peak(o.Daily);
            foreach (OfficeDay d in o.Daily) { DayLine l = DayOf(d, peak); t.Add("d;" + l.Label + ";" + WidthText(l.Width) + ";" + l.Value); }
            foreach (OfficeSkill k in o.Skills) t.Add("s;" + k.Kind + ";" + string.Join(";", SkillRow(k)) + (Unplayed(k) ? ";unplayed" : ""));
            if (o.Words.Count == 0) t.Add("w0;" + WordsEmpty);
            foreach (OfficeWord w in o.Words) t.Add("w;" + string.Join(";", WordRow(w)));
            if (o.Mistakes.Count == 0) t.Add("m0;" + MistakesEmpty);
            foreach (OfficeMistake m in o.Mistakes) t.Add("m;" + string.Join(";", MistakeRow(m)));
            return Esc(string.Join("|", t));
        }

        public static string Esc(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c < 0x20 || c > 0x7e || c == '\\') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
