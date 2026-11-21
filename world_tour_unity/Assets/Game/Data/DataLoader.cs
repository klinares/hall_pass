using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using WorldTour.Core.Data;
using WorldTour.Core.Maths;

namespace WorldTour.Game.Data
{
    /// <summary>
    /// Loads StreamingAssets/weeks.json and math_config.json at start, checks them with Core (strict parse, raw keys and
    /// types, then the meaning rules) and keeps the result. Nothing here throws. A week with an error is skipped and the
    /// other weeks load (<see cref="Weeks"/>.Skipped names it for a grown-up); a weeks file that is missing, can't be
    /// parsed or has no valid week leaves <c>Weeks.File</c> null with readable findings. A broken config falls back to
    /// the web defaults.
    /// The models are filled from the strict tree, not JsonUtility, so High managed stripping has no reflection to break.
    /// Run it once from Boot: <c>yield return StartCoroutine(DataLoader.Load());</c>
    /// </summary>
    public static class DataLoader
    {
        public static bool Loaded { get; private set; }

        /// <summary>weeks.json. <c>Weeks.File</c> holds the usable weeks; it is null when the file is missing, unparsable or has no valid week.</summary>
        public static WeeksLoad Weeks { get; private set; }

        /// <summary>math_config.json. <c>Math.Config</c> is never null (web defaults when the file is missing or wrong).</summary>
        public static MathConfigLoad Math { get; private set; }

        /// <summary>Every finding from both files, one readable line each ("weeks.json, line 14: ...").</summary>
        public static List<string> Messages =>
            (Weeks?.Findings ?? new List<Finding>()).Concat(Math?.Findings ?? new List<Finding>()).Select(f => f.ToString()).ToList();

        /// <summary>Tests only: read math_config.json from this folder instead of StreamingAssets, so play-mode tests do not depend on the shipped config. Null in the game.</summary>
        public static string MathConfigFolder;

        public static IEnumerator Load(string folder = null)
        {
            Loaded = false;
            folder = folder ?? Application.streamingAssetsPath;
            string weeksText = null, weeksFail = null, configText = null, configFail = null;
            yield return Fetch(folder, DataFiles.WeeksFileName, (t, e) => { weeksText = t; weeksFail = e; });
            yield return Fetch(MathConfigFolder ?? folder, DataFiles.MathConfigFileName, (t, e) => { configText = t; configFail = e; });

            Weeks = Guard(() => weeksFail != null ? Missing(new WeeksLoad(), DataFiles.WeeksFileName, weeksFail) : DataFiles.LoadWeeks(weeksText),
                e => Missing(new WeeksLoad(), DataFiles.WeeksFileName, "could not be read (" + e.Message + ")"));
            Math = Guard(() => configFail != null ? MissingConfig(configFail) : DataFiles.LoadMathConfig(configText),
                e => MissingConfig("could not be read (" + e.Message + ")"));

            WorldTour.Core.Maths.Gen.FracMul.DivisionEnabled = Math.Config.fmDivision;   // snack table: math_config.json fmDivision
            foreach (string m in Messages) Debug.LogWarning("[data] " + m);
            Loaded = true;
        }

        /// <summary>
        /// GETs one file. Over http(s) a time query defeats the browser and Pages caches, so a weeks.json edited on the
        /// server shows on the next reload (the template's no-store rule covers Unity's own loader, not this request).
        /// </summary>
        private static IEnumerator Fetch(string folder, string name, Action<string, string> done)
        {
            string url = folder.TrimEnd('/') + "/" + name;
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url += "?v=" + DateTime.UtcNow.Ticks;
            else if (!url.Contains("://"))
                url = "file://" + url;
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) done(req.downloadHandler.text, null);
                else done(null, req.responseCode == 404 ? "is missing from StreamingAssets" : "could not be loaded (" + req.error + ")");
            }
        }

        private static T Guard<T>(Func<T> run, Func<Exception, T> fail)
        {
            try { return run(); }
            catch (Exception e) { Debug.LogWarning("[data] " + e); return fail(e); }   // a warning, never an error: the game carries on
        }

        private static WeeksLoad Missing(WeeksLoad load, string file, string why)
        {
            load.Findings.Add(new Finding(Severity.Error, file, 0, 0, "the file " + why));
            return load;
        }

        private static MathConfigLoad MissingConfig(string why)
        {
            var load = new MathConfigLoad { Config = MathConfig.WebDefaults(), UsedDefaults = true };
            load.Findings.Add(new Finding(Severity.Error, DataFiles.MathConfigFileName, 0, 0, "the file " + why));
            return load;
        }
    }
}
