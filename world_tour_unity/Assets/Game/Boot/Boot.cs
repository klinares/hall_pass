using System;
using System.Collections;
using UnityEngine;
using WorldTour.Core;
using WorldTour.Core.Data;
using WorldTour.Core.Progress;
using WorldTour.Game.Data;
using WorldTour.Game.Save;
using WorldTour.Game.Util;

namespace WorldTour.Game
{
    /// <summary>
    /// The world game's start-up: loads weeks.json and math_config.json (DataLoader) and opens the save (SaveStore), logging
    /// one WT_DATA_READY and one WT_SAVE_READY line for the browser tests. Data and save problems are warnings, never
    /// errors, and never stop the game. Also holds the session's save for the panels (Blob, Persist).
    /// </summary>
    public static class Boot
    {
        public const string DataReadyLog = "WT_DATA_READY";
        public const string SaveReadyLog = "WT_SAVE_READY";

        /// <summary>The save for this session, once it has been opened (null before, or if opening failed).</summary>
        public static SaveStore Store { get; private set; }

        /// <summary>True once data and save are loaded.</summary>
        public static bool Ready { get; private set; }

        static bool s_SaveAnnounced;
        static SaveBlob s_Session;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_SaveAnnounced = false; s_Session = null; Store = null; Ready = false; }

        /// <summary>The save the panels read and change: the stored one, or a default one kept for the session when storage failed.</summary>
        internal static SaveBlob Blob
        {
            get
            {
                if (Store != null && Store.Blob != null) return Store.Blob;
                return s_Session ?? (s_Session = SaveBlob.Defaults(LocalClock.TodayIso()));
            }
        }

        internal static void Persist() { if (Store != null) Store.Save(); }

        /// <summary>
        /// Phase 8.1: the world game's start. Loads weeks.json and math_config.json and opens the same save as Boot does
        /// (logging WT_DATA_READY and WT_SAVE_READY), with no start card and no scene change. Safe to run more than once.
        /// </summary>
        public static IEnumerator BootForWorld()
        {
            if (Ready) yield break;
            yield return DataLoader.Load();
            try { Debug.Log(DataSummary(LocalClock.TodayIso())); }
            catch (Exception e) { Debug.LogWarning("boot: could not summarize the data: " + e.Message); }
            OpenSave();
            Ready = true;
        }

        /// <summary>
        /// Opens the save (localStorage on the web, memory when storage is blocked), writes it once, and logs
        /// "WT_SAVE_READY persistent=true fans=0 unlocked=1 v=1 recovered=false". recovered = an unreadable save was found
        /// and kept in a rescue slot (worldtour.v1.unreadable, or .unreadable.2) before being replaced. If it couldn't be
        /// kept, the session is persistent=false and recovered=false, and the stored text is left as it was.
        /// </summary>
        static void OpenSave()
        {
            if (s_SaveAnnounced) return;
            s_SaveAnnounced = true;
            try
            {
                SaveStore st = SaveStore.Open();
                Store = st;
                if (st.Rescued) Debug.LogWarning("[save] the stored save could not be read and was kept in a rescue slot: " + st.LoadError);
                if (!st.Save() && st.Persistent) Debug.LogWarning("[save] the save could not be written");
                Debug.Log(SaveReadyLog + " persistent=" + (st.Persistent ? "true" : "false") + " fans=" + st.Blob.Fans.Lifetime
                    + " unlocked=" + st.Blob.Venues.Unlocked.Count + " v=" + st.Blob.V + " recovered=" + (st.Rescued ? "true" : "false"));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[save] could not open the save: " + e.Message);
            }
        }

        /// <summary>
        /// "WT_DATA_READY weeks=2 words=22 week=2026-10-26 config=ok skipped=0 today=2026-11-20". The week is the one that opens
        /// today (WeekRules.DefaultWeek), "none" when weeks.json was refused.
        /// </summary>
        static string DataSummary(string today)
        {
            WeeksFile file = DataLoader.Weeks?.File;
            int weeks = file?.weeks?.Length ?? 0, words = 0;
            if (file?.weeks != null) foreach (Week w in file.weeks) words += w.words?.Length ?? 0;
            Week open = file == null ? null : WeekRules.DefaultWeek(file.weeks, today);
            bool defaults = DataLoader.Math == null || DataLoader.Math.UsedDefaults;
            int skipped = DataLoader.Weeks?.Skipped?.Count ?? 0;
            return DataReadyLog + " weeks=" + weeks + " words=" + words + " week=" + (open?.id ?? "none")
                + " config=" + (defaults ? "defaults" : "ok") + " skipped=" + skipped + " today=" + today;
        }
    }
}
