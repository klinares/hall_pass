using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using WorldTour.Core.Progress;

namespace WorldTour.Game.Office
{
    /// <summary>Where a file pick stands (savefile.jslib WT_FilePickState).</summary>
    public enum PickState { Idle = 0, Waiting = 1, Ready = 2, Cancelled = 3, TooLarge = 4, Failed = 5 }

    /// <summary>
    /// The browser side of the office's export and import, through Plugins/WebGL/savefile.jslib: a file download (Blob
    /// plus an anchor), a file pick (input type=file read with FileReader), a clipboard copy and the paste event. No
    /// network. In the Editor and desktop players there is no browser: a download writes the file under
    /// persistentDataPath and logs where, copy and paste use the system clipboard, and a pick logs that it needs the
    /// browser. Every call is wrapped and reports failure instead of throwing.
    /// </summary>
    public static class SaveFiles
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int WT_FileDownload(string name, string text, string type);
        [DllImport("__Internal")] static extern int WT_FilePickOpen(int maxBytes);
        [DllImport("__Internal")] static extern int WT_FilePickState();
        [DllImport("__Internal")] static extern string WT_FilePickTake();
        [DllImport("__Internal")] static extern int WT_ClipboardCopy(string text);
        [DllImport("__Internal")] static extern int WT_PasteArm(int on, int maxChars);
        [DllImport("__Internal")] static extern int WT_PasteState();
        [DllImport("__Internal")] static extern string WT_PasteTake();
#endif
        public const string Log = "WT_SAVEFILE";

        /// <summary>
        /// Offers the text as a file download. In the browser, true when the download was started; elsewhere the file
        /// goes to persistentDataPath and <paramref name="where"/> says where (null in the browser).
        /// </summary>
        public static bool Download(string fileName, string text, out string where)
        {
            where = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            bool ok;
            try { ok = WT_FileDownload(fileName, text, "application/json") == 1; }
            catch (Exception) { ok = false; }
            Debug.Log(Log + " download ok=" + (ok ? "true" : "false") + " name=" + fileName + " chars=" + text.Length);
            return ok;
#else
            try
            {
                string path = Path.Combine(Application.persistentDataPath, fileName);
                File.WriteAllText(path, text);
                where = path;
                Debug.Log(Log + " download ok=true name=" + fileName + " chars=" + text.Length + " (editor: wrote " + path + ")");
                return true;
            }
            catch (Exception e)
            {
                Debug.Log(Log + " download ok=false name=" + fileName + " (editor: " + e.Message + ")");
                return false;
            }
#endif
        }

        /// <summary>Opens the browser's file picker. False when it couldn't (always false outside the browser, with a log line).</summary>
        public static bool OpenPicker()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            bool ok;
            try { ok = WT_FilePickOpen(SaveTransfer.MaxFileBytes) == 1; }
            catch (Exception) { ok = false; }
            Debug.Log(Log + " pick open=" + (ok ? "true" : "false"));
            return ok;
#else
            Debug.Log(Log + " pick open=false (editor: choosing a file needs the web build; paste a code or the file's text instead)");
            return false;
#endif
        }

        public static PickState PickStatus
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                try { return (PickState)WT_FilePickState(); }
                catch (Exception) { return PickState.Failed; }
#else
                return PickState.Idle;
#endif
            }
        }

        /// <summary>The picked file's text once it is <see cref="PickState.Ready"/>; the picker then goes back to Idle.</summary>
        public static string TakePicked()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { return WT_FilePickTake(); }
            catch (Exception) { return null; }
#else
            return null;
#endif
        }

        /// <summary>Puts text on the clipboard. True when the copy was made or started.</summary>
        public static bool Copy(string text)
        {
            bool ok;
#if UNITY_WEBGL && !UNITY_EDITOR
            try { ok = WT_ClipboardCopy(text) == 1; }
            catch (Exception) { ok = false; }
#else
            try { GUIUtility.systemCopyBuffer = text; ok = true; }
            catch (Exception) { ok = false; }
#endif
            Debug.Log(Log + " copy ok=" + (ok ? "true" : "false") + " chars=" + (text != null ? text.Length : 0));
            return ok;
        }

        /// <summary>While armed (the paste box has focus), the browser's next paste is kept for <see cref="TakePaste"/>.</summary>
        public static void ArmPaste(bool on)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { WT_PasteArm(on ? 1 : 0, 2 * SaveTransfer.MaxChars); }
            catch (Exception) { }
#endif
        }

        /// <summary>
        /// A paste waiting from the browser: the text, or null. <paramref name="tooLarge"/> is true when the paste was over
        /// the limit and was dropped. Outside the browser, <paramref name="ctrlV"/> (the key was pressed this frame) reads
        /// the system clipboard instead.
        /// </summary>
        public static string TakePaste(bool ctrlV, out bool tooLarge)
        {
            tooLarge = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            int st;
            try { st = WT_PasteState(); }
            catch (Exception) { st = 0; }
            if (st == 2) { tooLarge = true; return null; }
            if (st != 1) return null;
            try { return WT_PasteTake(); }
            catch (Exception) { return null; }
#else
            if (!ctrlV) return null;
            try
            {
                string t = GUIUtility.systemCopyBuffer;
                if (t != null && t.Length > 2 * SaveTransfer.MaxChars) { tooLarge = true; return null; }
                return t;
            }
            catch (Exception) { return null; }
#endif
        }
    }
}
