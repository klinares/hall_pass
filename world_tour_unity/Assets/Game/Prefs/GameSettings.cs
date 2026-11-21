using System;
using System.Runtime.InteropServices;
using UnityEngine;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Game.Audio;
using WorldTour.Game.Motion;
using WorldTour.Game.Save;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Prefs
{
    /// <summary>settings.motion: "auto" follows the device, "reduced" always reduces, "full" never does.</summary>
    public enum MotionMode { Auto, Reduce, Full }

    /// <summary>The motion setting's save strings. Plain C#.</summary>
    public static class MotionModes
    {
        public const string AutoText = "auto";
        public const string ReduceText = "reduced";
        public const string FullText = "full";

        /// <summary>"reduced" and "full" as written; anything else (including a missing or odd value) is Auto.</summary>
        public static MotionMode Parse(string s)
        {
            if (s == ReduceText) return MotionMode.Reduce;
            if (s == FullText) return MotionMode.Full;
            return MotionMode.Auto;
        }

        public static string ToSave(MotionMode m)
        {
            return m == MotionMode.Reduce ? ReduceText : (m == MotionMode.Full ? FullText : AutoText);
        }

        /// <summary>Whether motion is reduced for a mode, given what the device asks for (null = unknown, treated as no).</summary>
        public static bool IsReduced(MotionMode m, bool? devicePrefersReduced)
        {
            if (m == MotionMode.Reduce) return true;
            if (m == MotionMode.Full) return false;
            return devicePrefersReduced == true;
        }
    }

    /// <summary>
    /// Sound, reduce motion and theme, backed by the save's settings (SaveBlob.Settings) through SaveStore.
    /// Changing one writes the save at once (these are single clicks, so no throttle is needed) and applies it to
    /// <see cref="Sfx"/>, <see cref="MotionPrefs"/> and <see cref="Theme"/>. With no save (storage blocked or Main opened
    /// in the Editor without Boot), the defaults apply and changes last for the session only.
    /// The theme is the equipped backstage theme (MetaRules.GetLook, as look.js does); settings.theme is not read.
    /// </summary>
    public static class GameSettings
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int WT_PrefersReducedMotion();
#else
        static int WT_PrefersReducedMotion() { return -1; }
#endif

        static SaveStore s_Store;
        static SettingsData s_Session = new SettingsData();

        /// <summary>Raised after any setting changes or a save is bound.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_Store = null; s_Session = new SettingsData(); Changed = null; }

        static SettingsData Data { get { return s_Store != null && s_Store.Blob != null ? s_Store.Blob.Settings : s_Session; } }

        /// <summary>Uses this save from now on (Boot.Store), and applies its settings. Null keeps session-only defaults.</summary>
        public static void Bind(SaveStore store)
        {
            s_Store = store;
            Apply();
        }

        /// <summary>settings.sound, the remembered mute toggle.</summary>
        public static bool Sound
        {
            get { return Data.Sound; }
            set
            {
                if (s_Store != null && s_Store.Blob != null) ProgressRules.SetSound(s_Store.Blob, value);
                else s_Session.Sound = value;
                Commit();
                // main.js: turning sound back on plays a tap so she hears it worked.
                if (value) { Sfx.Unlock(); Sfx.Play(SfxId.Tap); }
            }
        }

        /// <summary>settings.motion.</summary>
        public static MotionMode Motion
        {
            get { return MotionModes.Parse(Data.Motion); }
            set
            {
                string s = MotionModes.ToSave(value);
                if (s_Store != null && s_Store.Blob != null) ProgressRules.SetMotion(s_Store.Blob, s);
                else s_Session.Motion = s;
                Commit();
            }
        }

        /// <summary>What the browser asks for: true, false, or null when it can't tell (always null outside the web player).</summary>
        public static bool? DevicePrefersReducedMotion
        {
            get
            {
                int v;
                try { v = WT_PrefersReducedMotion(); }
                catch (Exception) { v = -1; }
                return v < 0 ? (bool?)null : v == 1;
            }
        }

        /// <summary>True when motion is reduced right now.</summary>
        public static bool ReducedMotion { get { return MotionModes.IsReduced(Motion, DevicePrefersReducedMotion); } }

        /// <summary>The equipped theme id ("tour-pink" or "peach-navy").</summary>
        public static string ThemeId
        {
            get
            {
                if (s_Store == null || s_Store.Blob == null) return Palettes.TourPinkId;
                try { return MetaRules.GetLook(s_Store.Blob).Theme; }
                catch (Exception) { return Palettes.TourPinkId; }
            }
        }

        /// <summary>
        /// Pushes the current settings into sound, motion and theme. Call after equipping a theme backstage (phase 5),
        /// and when the window regains focus if the device setting might have changed.
        /// </summary>
        public static void Apply()
        {
            Sfx.Enabled = Sound;
            MotionPrefs.Reduced = ReducedMotion;
            LookArt.Apply(s_Store != null ? s_Store.Blob : null);
            Theme.Use(ThemeId);
            Changed?.Invoke();
        }

        static void Commit()
        {
            if (s_Store != null && s_Store.Blob != null && s_Store.Persistent && !s_Store.Save())
                Debug.LogWarning("[save] settings could not be written");
            Apply();
        }
    }
}
