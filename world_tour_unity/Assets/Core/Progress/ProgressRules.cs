using System;
using System.Collections.Generic;
using System.Linq;
using WorldTour.Core.Data;
using WorldTour.Core.Maths;
using WorldTour.Core.Meta;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Progress
{
    /// <summary>
    /// Everything that changes saved progress, ported from world_tour/js/progress.js as functions on a
    /// <see cref="SaveBlob"/> (the JS works on the one stored state; here the blob is passed in). "Today" is a
    /// parameter, since Core has no clock. Saving after a change is the Game layer's job (store.save).
    /// Level rules are in <see cref="LevelRules"/>; the shop, milestone and journal rules are in Core/Meta (task 1.6),
    /// reached from here as progress.js:122-129 reaches meta.js.
    /// </summary>
    public static class ProgressRules
    {
        /// <summary>progress.js:103 the show history keeps the newest 200.</summary>
        public const int MaxShows = 200;
        /// <summary>progress.js:114 the time log keeps the newest 120 days.</summary>
        public const int MaxTimeDays = 120;
        /// <summary>progress.js:112 seconds added per tick of active play.</summary>
        public const int TimeTick = 15;

        // ---------- fans ----------

        /// <summary>
        /// progress.js:11-18 <c>addFans(n)</c>: adds to both balance and lifetime and returns the venues newly
        /// unlocked by lifetime fans (in tour order). Fans can't be taken away here: a negative amount throws.
        /// </summary>
        public static List<Venue> AddFans(SaveBlob s, long n)
        {
            if (n < 0) throw new ArgumentOutOfRangeException(nameof(n), "fans are only ever added");
            s.Fans.Balance += n;
            s.Fans.Lifetime += n;
            var newly = Venues.All.Where(v => s.Fans.Lifetime >= v.Fans && !s.Venues.Unlocked.Contains(v.Id)).ToList();
            foreach (Venue v in newly) s.Venues.Unlocked.Add(v.Id);
            return newly;
        }

        /// <summary>progress.js:20 <c>unlocked(id)</c>.</summary>
        public static bool Unlocked(SaveBlob s, string id)
        {
            return s.Venues.Unlocked.Contains(id);
        }

        /// <summary>progress.js:21 <c>newestVenue()</c>: the furthest unlocked venue on the route, or the first.</summary>
        public static Venue NewestVenue(SaveBlob s)
        {
            return Venues.All.Reverse().FirstOrDefault(v => s.Venues.Unlocked.Contains(v.Id)) ?? Venues.All[0];
        }

        /// <summary>progress.js:22 <c>nextVenue()</c>: the first venue on the route not yet unlocked, or null.</summary>
        public static Venue NextVenue(SaveBlob s)
        {
            return Venues.All.FirstOrDefault(v => !s.Venues.Unlocked.Contains(v.Id));
        }

        /// <summary>progress.js:23 <c>unseenUnlocks()</c>: unlocked ids she hasn't seen on the map yet, in unlock order.</summary>
        public static List<string> UnseenUnlocks(SaveBlob s)
        {
            return s.Venues.Unlocked.Where(id => !s.Venues.Seen.Contains(id)).ToList();
        }

        /// <summary>progress.js:24 <c>markSeen(id)</c>. Returns true when it changed the save.</summary>
        public static bool MarkSeen(SaveBlob s, string id)
        {
            if (s.Venues.Seen.Contains(id)) return false;
            s.Venues.Seen.Add(id);
            return true;
        }

        // ---------- day streak ----------

        /// <summary>progress.js:27-32 <c>dayStreak()</c>: the streak, if she played today or yesterday; else 0.</summary>
        public static int DayStreak(SaveBlob s, string today)
        {
            if (string.IsNullOrEmpty(s.Streak.Last)) return 0;
            int? gap = Dates.DaysBetween(s.Streak.Last, today);
            return gap.HasValue && gap.Value <= 1 ? s.Streak.Days : 0;
        }

        /// <summary>progress.js:33-39 <c>tickStreak()</c>, run when a show finishes.</summary>
        public static void TickStreak(SaveBlob s, string today)
        {
            StreakData st = s.Streak;
            if (st.Last == today) return;
            st.Days = !string.IsNullOrEmpty(st.Last) && Dates.DaysBetween(st.Last, today) == 1 ? st.Days + 1 : 1;
            st.Last = today;
            st.Best = Math.Max(st.Best, st.Days);
        }

        /// <summary>
        /// progress.js:40 <c>daysAway()</c>: days since the last show, or null if she never finished one. Also null
        /// where the JS would give NaN (a damaged stamp).
        /// </summary>
        public static int? DaysAway(SaveBlob s, string today)
        {
            if (string.IsNullOrEmpty(s.Streak.Last)) return null;
            return Dates.DaysBetween(s.Streak.Last, today);
        }

        // ---------- skills ----------

        /// <summary>progress.js:43-46 <c>skill(id)</c>: the skill's record, created at level 1 if new.</summary>
        public static SkillRecord Skill(SaveBlob s, string id)
        {
            SkillRecord k;
            if (!s.Skills.TryGetValue(id, out k) || k == null)
            {
                k = new SkillRecord();
                s.Skills[id] = k;
            }
            return k;
        }

        /// <summary>
        /// The skill's record for a show, with a skill nobody has played yet (no record, or an untouched level-1 one the
        /// map made while drawing) put at the policy's start level (phase 7.1: new skills start at the warm-up).
        /// </summary>
        public static SkillRecord SkillForShow(SaveBlob s, string id, LevelPolicy policy)
        {
            SkillRecord k = Skill(s, id);
            if (policy != null && LevelRules.Untouched(k)) k.Level = policy.Start;
            return k;
        }

        /// <summary>
        /// The level a skill plays at, for display, without creating a record: an unplayed skill is at the policy's start,
        /// a saved level above the cap shows as the cap.
        /// </summary>
        public static int PlayedLevel(SaveBlob s, string id, LevelPolicy policy)
        {
            SkillRecord k;
            if (s == null || !s.Skills.TryGetValue(id, out k) || k == null || LevelRules.Untouched(k)) return policy.Start;
            return policy.Effective(k.Level);
        }

        /// <summary>The level policy for a skill id in this save: math skills read the config, spelling its own rules; each honours its gentle switch.</summary>
        public static LevelPolicy PolicyFor(SaveBlob s, string id, MathConfig config)
        {
            bool spelling = id != null && id.StartsWith("spell:", StringComparison.Ordinal);
            bool gentle = s != null && (spelling ? s.Settings.GentleSpelling : s.Settings.GentleMath);
            return spelling ? LevelPolicy.ForSpelling(gentle) : LevelPolicy.ForMath(config, id, gentle);
        }

        /// <summary>progress.js:47 <c>level(id)</c>.</summary>
        public static int Level(SaveBlob s, string id)
        {
            return Skill(s, id).Level;
        }

        /// <summary>progress.js:48-63 <c>recordSkill</c>. Returns 1, 0 or -1 for how the level moved.</summary>
        public static int RecordSkill(SaveBlob s, string id, bool correct, bool hinted = false, string mistake = null)
        {
            return LevelRules.Record(Skill(s, id), correct, hinted, mistake);
        }

        /// <summary>progress.js:90-95 <c>noteMistake(id, tag)</c>.</summary>
        public static void NoteMistake(SaveBlob s, string id, string tag)
        {
            if (string.IsNullOrEmpty(tag)) return;
            LevelRules.NoteMistake(Skill(s, id), tag);
        }

        // ---------- words ----------

        /// <summary>
        /// progress.js:67-79 <c>recordWord(w, { clean, hinted, missed, keep })</c>. A miss puts the word in review; a
        /// clean answer takes it out unless it was missed earlier this show (keep).
        /// </summary>
        public static void RecordWord(SaveBlob s, string w, bool clean, bool hinted, bool missed, bool keep, string today)
        {
            WordStat e;
            if (!s.Words.TryGetValue(w, out e) || e == null)
            {
                e = new WordStat();
                s.Words[w] = e;
            }
            e.A++;
            e.Last = today;
            if (!missed) e.C++;
            if (clean) e.First++;
            if (hinted) e.H++;
            if (missed) e.Miss++;
            List<string> rv = s.Review.Words;
            if (missed && !rv.Contains(w)) rv.Add(w);
            if (clean && !keep) s.Review.Words = rv.Where(x => x != w).ToList();
        }

        /// <summary>progress.js:80 <c>wordStats(w)</c>, or null.</summary>
        public static WordStat WordStats(SaveBlob s, string w)
        {
            WordStat e;
            return s.Words.TryGetValue(w, out e) ? e : null;
        }

        /// <summary>The saved attempt count, for <see cref="ShowPicker.PickWords"/> (show.js: <c>st ? st.a : 0</c>).</summary>
        public static int WordAttempts(SaveBlob s, string w)
        {
            WordStat e = WordStats(s, w);
            return e != null ? e.A : 0;
        }

        /// <summary>progress.js:96 <c>reviewWords()</c>: a copy.</summary>
        public static List<string> ReviewWords(SaveBlob s)
        {
            return new List<string>(s.Review.Words);
        }

        // ---------- math ----------

        /// <summary>progress.js:83-87 <c>recordMath(skill, v, opts)</c>: a missed problem is saved whole.</summary>
        public static void RecordMath(SaveBlob s, string skill, MathItem v, bool clean, bool missed, bool keep)
        {
            s.Review.Math = Review.Record(s.Review.Math, skill, v, clean, missed, keep);
        }

        /// <summary>progress.js:88 <c>reviewMath(skill)</c>: this skill's saved problems, oldest first.</summary>
        public static List<ReviewEntry> ReviewMath(SaveBlob s, string skill)
        {
            return s.Review.Math.Where(r => r.Skill == skill).ToList();
        }

        /// <summary>show.js:32 the review items for a math show: the first three for the skill, through reviewItem.</summary>
        public static List<MathItem> ReviewItems(SaveBlob s, string skill)
        {
            return ReviewMath(s, skill).Take(Scoring.ReviewPerShow).Select(Review.Item).ToList();
        }

        // ---------- shows + time ----------

        /// <summary>
        /// progress.js:99-105 <c>finishShow(summary)</c>: ticks the day streak and adds the show to the history
        /// (newest 200 kept). The record is the fields show.js passes: mode, venue, n, first, hinted, fans, secs,
        /// best, sold, encore; plus missed (Unity only, QUESTIONS 10 addendum).
        /// </summary>
        public static ShowRecord FinishShow(SaveBlob s, ShowSummary summary, string today)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            TickStreak(s, today);
            var rec = new ShowRecord
            {
                D = today, Mode = summary.Mode, Venue = summary.Venue, N = summary.N, First = summary.First,
                Hinted = summary.Hinted, Fans = summary.Fans, Secs = summary.Secs, Best = summary.Best,
                Missed = summary.Missed, Sold = summary.Sold, Encore = summary.Encore
            };
            s.Shows.Add(rec);
            if (s.Shows.Count > MaxShows) s.Shows.RemoveRange(0, s.Shows.Count - MaxShows);
            return rec;
        }

        /// <summary>
        /// progress.js:109-116, the rule inside the 15-second timer: add play time to today and keep the newest
        /// 120 days. The Game layer decides when she is actively playing (page visible, input in the last minute).
        /// </summary>
        public static void AddPlayTime(SaveBlob s, string today, int secs = TimeTick)
        {
            if (secs < 0) throw new ArgumentOutOfRangeException(nameof(secs));
            int t;
            s.Time.TryGetValue(today, out t);
            s.Time[today] = t + secs;
            var keys = s.Time.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            if (keys.Count > MaxTimeDays)
            {
                // JS deletes the oldest keys; rebuild instead of Remove so the rest keep their insertion order
                var drop = new HashSet<string>(keys.Take(keys.Count - MaxTimeDays));
                var kept = new Dictionary<string, int>();
                foreach (var kv in s.Time) if (!drop.Contains(kv.Key)) kept[kv.Key] = kv.Value;
                s.Time = kept;
            }
        }

        // ---------- settings ----------

        /// <summary>progress.js:120 <c>setSetting('sound', v)</c>.</summary>
        public static void SetSound(SaveBlob s, bool on) { s.Settings.Sound = on; }
        /// <summary>Phase 10A: the music and ambience slider, 0 to 100 (clamped).</summary>
        public static void SetAmbience(SaveBlob s, int percent) { s.Settings.Ambience = System.Math.Max(0, System.Math.Min(100, percent)); }
        /// <summary>Phase 10A: the effects slider, 0 to 100 (clamped).</summary>
        public static void SetEffects(SaveBlob s, int percent) { s.Settings.Effects = System.Math.Max(0, System.Math.Min(100, percent)); }

        /// <summary>progress.js:120 <c>setSetting('motion', v)</c>: "auto" or "reduced".</summary>
        public static void SetMotion(SaveBlob s, string motion) { s.Settings.Motion = motion; }

        // ---------- backstage + journal (rules live in Core/Meta, as meta.js) ----------

        /// <summary>progress.js:123 <c>buyItem(id)</c>: <see cref="MetaRules.Buy"/>; the Game layer saves at once when it went through.</summary>
        public static BuyResult BuyItem(SaveBlob s, string id) { return MetaRules.Buy(s, id); }

        /// <summary>progress.js:124 <c>equipItem(id)</c>: <see cref="MetaRules.Equip"/>; save at once when true.</summary>
        public static bool EquipItem(SaveBlob s, string id) { return MetaRules.Equip(s, id); }

        /// <summary>
        /// progress.js:128 <c>awardStickers(modes)</c>: every milestone that now holds, dated today; returns the new
        /// ones (save at once when any). The JS keeps the mode list from <c>setModes</c>; here it is passed in, and
        /// null means none, as the JS default <c>ALL = []</c>.
        /// </summary>
        public static List<Milestone> AwardStickers(SaveBlob s, IReadOnlyList<ModeInfo> modes, string today)
        {
            return MetaRules.Earn(s, modes ?? Array.Empty<ModeInfo>(), today);
        }

        /// <summary>progress.js:129 <c>placeSticker(key, x, y, r)</c>: <see cref="MetaRules.Place"/>; save at once.</summary>
        public static void PlaceSticker(SaveBlob s, string key, double x, double y, double r = 0) { MetaRules.Place(s, key, x, y, r); }

        // ---------- connecting a show ----------

        /// <summary>
        /// Wires a show to the save, doing what show.js does through progress.js: fans go to the balance and
        /// lifetime (unlocks reported through <paramref name="onUnlock"/>), each answer is recorded as a word or a
        /// math problem, and the finished show goes into the history. The engine's <see cref="ShowSetup.SkillRecord"/>
        /// must be <c>Skill(s, skill)</c> so level changes land in the save. Right after the show is saved, stickers are
        /// awarded for <paramref name="modes"/> (show.js:222 <c>P.awardStickers()</c> after <c>P.finishShow</c>) and the new
        /// ones go to <paramref name="onStickers"/> for the ticket screen (called with an empty list when none).
        /// </summary>
        public static void Attach(ShowEngine engine, SaveBlob s, Func<string> today, Action<Venue> onUnlock = null,
            IReadOnlyList<ModeInfo> modes = null, Action<List<Milestone>> onStickers = null)
        {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (!ReferenceEquals(engine.SkillRecord, Skill(s, engine.Skill)))
                throw new ArgumentException("the show's skill record must be the save's record for " + engine.Skill);
            engine.Raised += e =>
            {
                switch (e.Kind)
                {
                    case ShowEventKind.FansAwarded:
                        foreach (Venue v in AddFans(s, e.Amount)) if (onUnlock != null) onUnlock(v);
                        break;
                    case ShowEventKind.ItemRecorded:
                        var m = e.Slot.Item as MathItem;
                        if (engine.IsMath && m != null) RecordMath(s, engine.Skill, m, e.Clean, e.Missed, e.Keep);
                        else
                        {
                            var w = e.Slot.Item as WordItem;
                            if (w != null) RecordWord(s, w.W, e.Clean, e.Hinted, e.Missed, e.Keep, today());
                        }
                        break;
                    case ShowEventKind.ShowFinished:
                        string day = today();
                        if (e.Summary.Gig) TickStreak(s, day);   // phase 8.1: a gig counts for the day streak, not the show history
                        else FinishShow(s, e.Summary, day);
                        List<Milestone> fresh = AwardStickers(s, modes, day);
                        if (onStickers != null) onStickers(fresh);
                        break;
                }
            };
        }
    }
}
