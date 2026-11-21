using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using WorldTour.Core.Progress;

namespace WorldTour.Game.Save
{
    /// <summary>Where the save's text lives. Every call is wrapped: a failure returns null or false, never throws.</summary>
    public interface ISaveBackend
    {
        /// <summary>True when writes are kept after a reload.</summary>
        bool Persistent { get; }
        string Read(string key);
        bool Write(string key, string text);
    }

    /// <summary>The web build: localStorage through Plugins/WebGL/storage.jslib.</summary>
    public sealed class WebStorageBackend : ISaveBackend
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int WT_StorageAvailable();
        [DllImport("__Internal")] private static extern string WT_StorageGet(string key);
        [DllImport("__Internal")] private static extern int WT_StorageSet(string key, string value);
#else
        private static int WT_StorageAvailable() { return 0; }
        private static string WT_StorageGet(string key) { return null; }
        private static int WT_StorageSet(string key, string value) { return 0; }
#endif
        private readonly bool _ok;

        public WebStorageBackend()
        {
            try { _ok = WT_StorageAvailable() == 1; }
            catch (Exception) { _ok = false; }
        }

        public bool Persistent { get { return _ok; } }

        public string Read(string key)
        {
            if (!_ok) return null;
            try { return WT_StorageGet(key); }
            catch (Exception) { return null; }
        }

        public bool Write(string key, string text)
        {
            if (!_ok) return false;
            try { return WT_StorageSet(key, text) == 1; }
            catch (Exception) { return false; }
        }
    }

    /// <summary>The Editor and desktop players: one file per key under Application.persistentDataPath.</summary>
    public sealed class FileBackend : ISaveBackend
    {
        private readonly string _dir;

        public FileBackend(string dir = null)
        {
            _dir = dir ?? Application.persistentDataPath;
        }

        public bool Persistent { get { return true; } }

        private string PathOf(string key) { return Path.Combine(_dir, key + ".json"); }

        public string Read(string key)
        {
            try { return File.Exists(PathOf(key)) ? File.ReadAllText(PathOf(key)) : null; }
            catch (Exception) { return null; }
        }

        public bool Write(string key, string text)
        {
            try
            {
                Directory.CreateDirectory(_dir);
                string tmp = PathOf(key) + ".tmp";
                File.WriteAllText(tmp, text);
                // Swap in one step, so a crash mid-save never leaves the key missing (no delete-then-move gap).
                if (File.Exists(PathOf(key))) File.Replace(tmp, PathOf(key), null);
                else File.Move(tmp, PathOf(key));
                return true;
            }
            catch (Exception) { return false; }
        }
    }

    /// <summary>No storage at all: the game still plays, and nothing survives a reload.</summary>
    public sealed class MemoryBackend : ISaveBackend
    {
        private readonly System.Collections.Generic.Dictionary<string, string> _d = new System.Collections.Generic.Dictionary<string, string>();
        public bool Persistent { get { return false; } }
        public string Read(string key) { string v; return _d.TryGetValue(key, out v) ? v : null; }
        public bool Write(string key, string text) { _d[key] = text; return true; }
    }

    /// <summary>
    /// The save on this device, ported from world_tour/js/store.js: one key (<see cref="SaveBlob.Key"/>) holds
    /// the whole save as canonical JSON. If storage is missing, blocked or full, the game runs on the in-memory
    /// copy and <see cref="Persistent"/> turns false (the web game's "wt:nosave"). Unreadable stored text is
    /// copied once to <see cref="RescueKey"/> before the first new save overwrites it.
    /// </summary>
    public sealed class SaveStore
    {
        /// <summary>Where unreadable stored text is kept so nothing earned is lost for good.</summary>
        public const string RescueKey = SaveBlob.Key + ".unreadable";
        /// <summary>The second slot, used when <see cref="RescueKey"/> already holds a different unreadable save.</summary>
        public const string RescueKey2 = RescueKey + ".2";

        private ISaveBackend _backend;

        /// <summary>The live save. Change it through Core's ProgressRules, then call <see cref="Save"/>.</summary>
        public SaveBlob Blob { get; private set; }
        /// <summary>True while saves are kept after a reload.</summary>
        public bool Persistent { get { return _backend.Persistent; } }
        /// <summary>Set when the stored save couldn't be read; the parser's message.</summary>
        public string LoadError { get; private set; }
        /// <summary>
        /// True when an unreadable stored save is safely kept in a rescue slot. When <see cref="LoadError"/> is set but this
        /// is false, the copy could not be made, so this session runs in memory and leaves the stored text untouched.
        /// </summary>
        public bool Rescued { get; private set; }
        /// <summary>Raised once when saving stops working, so the UI can say progress won't be kept.</summary>
        public event Action NoSave;

        /// <summary>The backend for this platform: localStorage on the web, a file elsewhere, memory if neither works.</summary>
        public static ISaveBackend DefaultBackend()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var web = new WebStorageBackend();
            return web.Persistent ? (ISaveBackend)web : new MemoryBackend();
#else
            return new FileBackend();
#endif
        }

        /// <summary>Today's local date, "yyyy-mm-dd". The one place the Game layer reads the date for saves.</summary>
        public static Func<string> TodayIso = () => WorldTour.Game.Util.LocalClock.TodayIso();   // LocalClock

        /// <summary>store.js load() with today from <see cref="TodayIso"/>.</summary>
        public static SaveStore Open(ISaveBackend backend = null)
        {
            return Open(TodayIso(), backend);
        }

        /// <summary>store.js load(): reads and migrates the stored save, or starts fresh.</summary>
        public static SaveStore Open(string today, ISaveBackend backend)
        {
            var st = new SaveStore { _backend = backend ?? DefaultBackend() };
            LoadResult r = SaveJson.Load(st._backend.Read(SaveBlob.Key), today);
            st.Blob = r.Blob;
            if (r.Unreadable)
            {
                st.LoadError = r.Error;
                st.Rescued = Rescue(st._backend, r.Text);
                if (!st.Rescued)
                {
                    // Keep the unreadable save where it is: play this session in memory, so nothing overwrites it.
                    st._backend = new MemoryBackend();
                    Debug.LogWarning("[save] the stored save can't be read and couldn't be copied to " + RescueKey + "; playing without saving this time so it isn't overwritten");
                }
            }
            return st;
        }

        /// <summary>
        /// Keeps unreadable stored text before anything overwrites it. Rule: if either rescue slot already holds exactly this
        /// text, it is kept; otherwise it goes in the first empty slot (RescueKey, then RescueKey2) and is read back to check.
        /// If both slots hold other texts, nothing is replaced and the result is false (so the main key is left alone too).
        /// </summary>
        private static bool Rescue(ISaveBackend backend, string text)
        {
            if (text == null) return false;
            string a = backend.Read(RescueKey), b = backend.Read(RescueKey2);
            if (a == text || b == text) return true;
            string slot = a == null ? RescueKey : b == null ? RescueKey2 : null;
            if (slot == null) return false;
            return backend.Write(slot, text) && backend.Read(slot) == text;
        }

        /// <summary>store.js saveNow(): stamps the time and writes the whole save. False when it wasn't kept.</summary>
        public bool Save()
        {
            Blob.Updated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (!_backend.Persistent) return false;
            if (_backend.Write(SaveBlob.Key, SaveJson.ToText(Blob))) return true;
            _backend = new MemoryBackend();
            var h = NoSave;
            if (h != null) h();
            return false;
        }

        /// <summary>Replaces the save after the player confirmed an import (ExportCode.Preview, then Apply).</summary>
        public bool Replace(SaveBlob incoming)
        {
            if (incoming == null) throw new ArgumentNullException(nameof(incoming));
            Blob = incoming;
            return Save();
        }

        /// <summary>The backend as Core's key-value slots (always the current backend, which can fall back to memory).</summary>
        public ISaveSlots Slots { get { return new SlotView(this); } }

        sealed class SlotView : ISaveSlots
        {
            readonly SaveStore _s;
            public SlotView(SaveStore s) { _s = s; }
            public string Read(string key) { return _s._backend.Read(key); }
            public bool Write(string key, string text) { return _s._backend.Write(key, text); }
        }

        /// <summary>
        /// The office's import, after the player confirmed: the current save is copied to SaveBackup.Key first
        /// (the older copy moves to SaveBackup.Key2), and only if that copy is safely kept is the save replaced.
        /// Logs "WT_SAVE_SWAP kind=import backup=Kept saved=true".
        /// </summary>
        public SwapResult ReplaceKeepingBackup(SaveBlob incoming)
        {
            if (incoming == null) throw new ArgumentNullException(nameof(incoming));
            return Swap("import", incoming);
        }

        /// <summary>
        /// The office's confirmed reset (store.js reset(): fresh defaults, saved at once), with the current save copied
        /// to SaveBackup.Key first, exactly as an import does. Logs "WT_SAVE_SWAP kind=reset ...".
        /// </summary>
        public SwapResult ResetKeepingBackup(string today)
        {
            return Swap("reset", SaveBlob.Defaults(today));
        }

        SwapResult Swap(string kind, SaveBlob next)
        {
            BackupOutcome b = SaveBackup.Keep(Slots, Blob);
            if (b == BackupOutcome.Failed)
            {
                Debug.LogWarning("[save] couldn't keep a copy of the current save before the " + kind + "; nothing was changed");
                Debug.Log("WT_SAVE_SWAP kind=" + kind + " backup=Failed saved=false");
                return new SwapResult { Changed = false, Backup = b, Saved = false };
            }
            Blob = next;
            bool saved = Save();
            Debug.Log("WT_SAVE_SWAP kind=" + kind + " backup=" + b + " saved=" + (saved ? "true" : "false"));
            return new SwapResult { Changed = true, Backup = b, Saved = saved };
        }
    }

    /// <summary>What <see cref="SaveStore.ReplaceKeepingBackup"/> or <see cref="SaveStore.ResetKeepingBackup"/> did.</summary>
    public struct SwapResult
    {
        /// <summary>False only when the backup failed: the save is exactly as it was.</summary>
        public bool Changed;
        public BackupOutcome Backup;
        /// <summary>True when the new save is written to storage (false while storage is blocked: this session only).</summary>
        public bool Saved;
    }
}
