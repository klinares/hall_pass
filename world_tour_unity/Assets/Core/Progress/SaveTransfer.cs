using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WorldTour.Core.Progress
{
    /// <summary>Somewhere save texts can be kept by key (the Game layer's storage backend). Never throws.</summary>
    public interface ISaveSlots
    {
        /// <summary>The text under <paramref name="key"/>, or null.</summary>
        string Read(string key);
        /// <summary>False when the text wasn't kept.</summary>
        bool Write(string key, string text);
    }

    /// <summary>
    /// The import gate of the manager's office, in front of <see cref="ExportCode"/>: a size limit, a tidy-up of
    /// pasted codes, and the code-version check, so a bad, truncated, wrong-version or too-large input never gets
    /// as far as the save. Nothing here changes anything: replacing the save is <see cref="SaveBackup"/> plus the
    /// store, after the player confirmed. The web game has no export or import; see DECISIONS.md for the format.
    /// </summary>
    public static class SaveTransfer
    {
        /// <summary>
        /// The longest save text taken in, in characters (the stored JSON is plain ASCII, so also bytes). A year of
        /// daily play is well under a fifth of this, and the store keeps up to four more copies (backups and
        /// rescues) under the browser's few-megabyte storage quota, so a bigger save couldn't be kept safely anyway.
        /// </summary>
        public const int MaxChars = 1000000;
        /// <summary>The largest file the picker reads, in bytes (a byte-order mark and a trailing line break on top).</summary>
        public const int MaxFileBytes = MaxChars + 16;

        public const string TooBig = "that's too big to be a save from this game.";
        public const string Empty = "paste your code first.";
        public const string NotACode = "that doesn't look like a code from this game. it starts with " + ExportCode.Prefix;
        public const string NewerCode = "that code is from a newer version of the game.";

        /// <summary>
        /// Reads a pasted code or a file's text and says what importing it would do. Changes nothing.
        /// <paramref name="current"/> is the live save (for the losses list) and may be null.
        /// </summary>
        public static ImportPreview Check(string input, SaveBlob current, string today)
        {
            string s = input ?? "";
            if (s.Length > 2 * MaxChars) return Fail(current, TooBig);
            s = s.Trim();
            if (s.Length > 0 && s[0] == (char)0xFEFF) s = s.Substring(1).Trim();
            if (s.Length == 0) return Fail(current, Empty);
            if (s[0] == '{')
            {
                if (s.Length > MaxChars) return Fail(current, TooBig);
            }
            else
            {
                string err = TidyCode(ref s);
                if (err != null) return Fail(current, err);
                if (s.Length > MaxChars) return Fail(current, TooBig);
            }

            ImportPreview p = ExportCode.Preview(s, current, today);
            if (!p.Ok) return p;
            // A code can unpack to more than it looks (DEFLATE's inflate cap is 8 MB), so the save itself is measured too.
            if (SaveJson.ToText(p.Incoming).Length > MaxChars) return Fail(current, TooBig);
            return p;
        }

        /// <summary>
        /// A pasted code without the things a chat app or document adds: every space and line break, and the
        /// invisible zero-width characters. Then the version in front: "WT1-" is this format, "WT2-" and up come from a
        /// newer game, anything else (WT0-, WT10x, a missing dash) isn't a code. Returns an error, or null with
        /// <paramref name="code"/> tidied.
        /// </summary>
        public static string TidyCode(ref string code)
        {
            var sb = new StringBuilder(code.Length);
            foreach (char c in code)
            {
                if (char.IsWhiteSpace(c) || IsInvisible(c)) continue;
                sb.Append(c);
            }
            string s = sb.ToString();
            if (s.Length == 0) return Empty;
            int dash = s.IndexOf('-');
            if (s.Length < 4 || dash < 3 || !(s[0] == 'W' || s[0] == 'w') || !(s[1] == 'T' || s[1] == 't')) return NotACode;
            string digits = s.Substring(2, dash - 2);
            if (digits.Length > 6 || !digits.All(ch => ch >= '0' && ch <= '9')) return NotACode;
            int n = int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
            if (digits.Length > 1 && digits[0] == '0') return NotACode;
            if (n > 1) return NewerCode;
            if (n != 1) return NotACode;
            code = ExportCode.Prefix + s.Substring(dash + 1);
            return null;
        }

        /// <summary>Zero-width space, non-joiner and joiner, word joiner, the byte-order mark and the soft hyphen.</summary>
        public static bool IsInvisible(char c)
        {
            return c == (char)0x200B || c == (char)0x200C || c == (char)0x200D || c == (char)0x2060 || c == (char)0xFEFF || c == (char)0x00AD;
        }

        /// <summary>
        /// True when the save holds anything earned or played: fans, venues past the first, shows, stickers, items,
        /// skill or word records, the review queue, play time or a streak. Settings alone don't count.
        /// </summary>
        public static bool HasProgress(SaveBlob s)
        {
            if (s == null) return false;
            var fresh = new InventoryData();
            return s.Fans.Lifetime != 0 || s.Fans.Balance != 0
                || s.Venues.Unlocked.Any(v => v != Data.Venues.First)
                || s.Shows.Count > 0 || s.Journal.Stickers.Count > 0 || s.Journal.Layout.Count > 0
                || s.Inventory.Owned.Any(id => !fresh.Owned.Contains(id))
                || s.Skills.Count > 0 || s.Words.Count > 0
                || s.Review.Words.Count > 0 || s.Review.Math.Count > 0
                || s.Time.Count > 0 || s.Streak.Best != 0 || s.Streak.Days != 0;
        }

        private static ImportPreview Fail(SaveBlob current, string error)
        {
            return new ImportPreview { Ok = false, Error = error, Current = current != null ? SaveSummary.Of(current) : null };
        }
    }

    /// <summary>What happened to the copy of the current save before a replace or a reset.</summary>
    public enum BackupOutcome
    {
        /// <summary>Written to <see cref="SaveBackup.Key"/> and read back (the copy before it moved to <see cref="SaveBackup.Key2"/>).</summary>
        Kept,
        /// <summary><see cref="SaveBackup.Key"/> already holds exactly this save.</summary>
        AlreadyKept,
        /// <summary>The save has nothing earned in it, so the backups are left as they are.</summary>
        NothingToKeep,
        /// <summary>A write failed or didn't read back: the caller must not replace the save.</summary>
        Failed,
    }

    /// <summary>
    /// The backup taken before the office replaces or wipes the save ("nothing earned is ever taken away except by the
    /// parent's confirmed reset", and even then a copy stays on the device). Two slots, newest first: the copy already
    /// in <see cref="Key"/> moves to <see cref="Key2"/>, then the current save goes in <see cref="Key"/>; each write is
    /// read back. A save with nothing earned is never backed up, so resetting a fresh save twice can't push the
    /// real copy out. These keys are separate from SaveStore's two unreadable-save rescue slots.
    /// </summary>
    public static class SaveBackup
    {
        public const string Key = SaveBlob.Key + ".backup";
        public const string Key2 = Key + ".2";

        /// <summary>Keeps <paramref name="current"/> before it is replaced. Only a non-Failed result allows the replace.</summary>
        public static BackupOutcome Keep(ISaveSlots slots, SaveBlob current)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (!SaveTransfer.HasProgress(current)) return BackupOutcome.NothingToKeep;
            string text = SaveJson.ToText(current);
            string a = slots.Read(Key);
            if (a == text) return BackupOutcome.AlreadyKept;
            if (a != null && slots.Read(Key2) != a)
            {
                if (!slots.Write(Key2, a) || slots.Read(Key2) != a) return BackupOutcome.Failed;
            }
            if (!slots.Write(Key, text) || slots.Read(Key) != text) return BackupOutcome.Failed;
            return BackupOutcome.Kept;
        }
    }

    /// <summary>
    /// The reset's deliberate two steps (office.js:89-104): "reset progress…" opens the question with cancel focused,
    /// and only "yes, erase everything" while the question is open erases. Leaving the office closes the question.
    /// </summary>
    public sealed class ResetConfirm
    {
        /// <summary>True while "are you sure?" is showing.</summary>
        public bool Asking { get; private set; }

        public void Ask() { Asking = true; }

        public void Cancel() { Asking = false; }

        /// <summary>True exactly once per question: the caller erases. False when the question isn't open.</summary>
        public bool TryErase()
        {
            if (!Asking) return false;
            Asking = false;
            return true;
        }
    }

    /// <summary>
    /// The import's two steps: a checked input waits as <see cref="Pending"/>, and only "yes, load this copy" while one
    /// is pending hands it over. A new check, cancel, or leaving the office drops it.
    /// </summary>
    public sealed class ImportConfirm
    {
        /// <summary>The last check, good or bad; null before any.</summary>
        public ImportPreview Last { get; private set; }

        /// <summary>A good check waiting for the yes, or null.</summary>
        public ImportPreview Pending { get { return Last != null && Last.Ok ? Last : null; } }

        public ImportPreview Check(string input, SaveBlob current, string today)
        {
            Last = SaveTransfer.Check(input, current, today);
            return Last;
        }

        public void Cancel() { Last = null; }

        /// <summary>The save to put in place, exactly once per good check; null when nothing is pending.</summary>
        public SaveBlob TakeConfirmed()
        {
            ImportPreview p = Pending;
            if (p == null) return null;
            Last = null;
            return ExportCode.Apply(p);
        }
    }
}
