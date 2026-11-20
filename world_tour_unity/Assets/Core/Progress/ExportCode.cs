using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using WorldTour.Core.Data;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Core.Progress
{
    /// <summary>The headline numbers of a save, for the import screen.</summary>
    public sealed class SaveSummary
    {
        public long Lifetime;
        public long Balance;
        public int Venues;
        public int Shows;
        public int Stickers;
        public int Owned;
        public string Created;
        public string LastPlayed;

        public static SaveSummary Of(SaveBlob s)
        {
            return new SaveSummary
            {
                Lifetime = s.Fans.Lifetime, Balance = s.Fans.Balance, Venues = s.Venues.Unlocked.Count, Shows = s.Shows.Count,
                Stickers = s.Journal.Stickers.Count, Owned = s.Inventory.Owned.Count, Created = s.Created, LastPlayed = s.Streak.Last
            };
        }
    }

    /// <summary>What an import would do. Nothing is changed until <see cref="ExportCode.Apply"/>.</summary>
    public sealed class ImportPreview
    {
        public bool Ok;
        /// <summary>A short, lowercase message for the player when not <see cref="Ok"/>.</summary>
        public string Error;
        /// <summary>The save in the code, migrated. Null when not Ok.</summary>
        public SaveBlob Incoming;
        public SaveSummary From;
        public SaveSummary Current;
        /// <summary>
        /// True when replacing would lose something the current save has earned (fewer lifetime fans, venues,
        /// stickers, items or shows). The UI must say so before asking to confirm.
        /// </summary>
        public bool LosesProgress;
        /// <summary>One plain line per thing that would be lost.</summary>
        public List<string> Losses = new List<string>();
    }

    /// <summary>
    /// Export and import of the whole save. The code is <c>WT1-</c> + base64url(raw DEFLATE of the save's JSON) +
    /// <c>-</c> + CRC-32 of that JSON (8 hex digits). Whitespace anywhere in a pasted code is ignored, so a
    /// wrapped code still works. The export file is the save's JSON itself; Preview takes either.
    /// Import replaces the save (no merge), and only when the UI calls Apply after the player confirms.
    /// </summary>
    public static class ExportCode
    {
        /// <summary>The code format's version prefix (not the save's version).</summary>
        public const string Prefix = "WT1-";

        /// <summary>The save as a copy-and-paste code.</summary>
        public static string Export(SaveBlob s)
        {
            byte[] json = Encoding.UTF8.GetBytes(SaveJson.ToText(s));
            return Prefix + B64Encode(Deflate.Compress(json)) + "-" + Deflate.Crc32(json).ToString("x8", CultureInfo.InvariantCulture);
        }

        /// <summary>The save as a file's text (its JSON).</summary>
        public static string ExportFile(SaveBlob s)
        {
            return SaveJson.ToText(s);
        }

        /// <summary>Reads a pasted code or a file's text and says what importing it would do. Changes nothing.</summary>
        public static ImportPreview Preview(string input, SaveBlob current, string today)
        {
            var p = new ImportPreview { Current = current != null ? SaveSummary.Of(current) : null };
            string text;
            string err = Unpack(input, out text);
            if (err != null) return Fail(p, err);

            JsonValue raw;
            StrictJsonException jerr;
            if (!StrictJson.TryParse(text, out raw, out jerr)) return Fail(p, "the save inside the code can't be read.");
            if (raw.Kind != JsonKind.Object || raw["fans"] == null || raw["venues"] == null) return Fail(p, "that isn't a world tour save.");
            JsonValue v = raw["v"];
            if (v != null && JsonTree.IsNum(v) && v.Number > SaveBlob.Version) return Fail(p, "that save is from a newer version of the game.");

            p.Ok = true;
            p.Incoming = SaveJson.Migrate(raw, today);
            p.From = SaveSummary.Of(p.Incoming);
            if (current != null)
            {
                SaveSummary a = p.Current, b = p.From;
                if (b.Lifetime < a.Lifetime) p.Losses.Add("total fans would go from " + a.Lifetime.ToString(CultureInfo.InvariantCulture) + " to " + b.Lifetime.ToString(CultureInfo.InvariantCulture));
                if (b.Balance < a.Balance) p.Losses.Add("fans to spend would go from " + a.Balance.ToString(CultureInfo.InvariantCulture) + " to " + b.Balance.ToString(CultureInfo.InvariantCulture));
                if (current.Venues.Unlocked.Any(id => !p.Incoming.Venues.Unlocked.Contains(id))) p.Losses.Add("some unlocked venues would lock again");
                if (current.Journal.Stickers.Keys.Any(k => !p.Incoming.Journal.Stickers.ContainsKey(k))) p.Losses.Add("some journal stickers would go");
                if (current.Inventory.Owned.Any(id => !p.Incoming.Inventory.Owned.Contains(id))) p.Losses.Add("some backstage items would go");
                if (b.Shows < a.Shows) p.Losses.Add("the show history would be shorter");
                p.LosesProgress = p.Losses.Count > 0;
            }
            return p;
        }

        /// <summary>
        /// The save to store after the player confirmed the preview: the incoming save, replacing the current one.
        /// Throws if the preview wasn't OK.
        /// </summary>
        public static SaveBlob Apply(ImportPreview preview)
        {
            if (preview == null || !preview.Ok || preview.Incoming == null) throw new InvalidOperationException("nothing to import");
            return preview.Incoming;
        }

        private static ImportPreview Fail(ImportPreview p, string error)
        {
            p.Ok = false;
            p.Error = error;
            return p;
        }

        /// <summary>The save JSON inside a code or file text, or an error message.</summary>
        private static string Unpack(string input, out string text)
        {
            text = null;
            string s = (input ?? "").Trim();
            if (s.Length > 0 && s[0] == (char)0xFEFF) s = s.Substring(1).Trim();
            if (s.Length == 0) return "paste your code first.";
            if (s[0] == '{') { text = s; return null; }
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) if (!char.IsWhiteSpace(c)) sb.Append(c);
            s = sb.ToString();
            if (s.Length >= 4 && s.StartsWith("WT", StringComparison.OrdinalIgnoreCase) && s[3] == '-' && char.IsDigit(s[2]) && s[2] != '1')
                return "that code is from a newer version of the game.";
            if (!s.StartsWith(Prefix, StringComparison.Ordinal)) return "that doesn't look like a world tour code. it starts with " + Prefix;
            string body = s.Substring(Prefix.Length);
            int dash = body.LastIndexOf('-');
            if (dash < 0 || body.Length - dash - 1 != 8) return "the end of the code is missing.";
            string data = body.Substring(0, dash), check = body.Substring(dash + 1);
            uint want;
            if (!uint.TryParse(check, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out want)) return "the end of the code is mistyped.";
            byte[] packed = B64Decode(data);
            if (packed == null) return "part of the code is mistyped.";
            byte[] json;
            try { json = Deflate.Inflate(packed); }
            catch (System.IO.InvalidDataException) { return "part of the code is missing or mistyped."; }
            if (Deflate.Crc32(json) != want) return "the code doesn't check out. copy it again, all of it.";
            try { text = new UTF8Encoding(false, true).GetString(json); }
            catch (ArgumentException) { return "part of the code is missing or mistyped."; }
            return null;
        }

        private static string B64Encode(byte[] b)
        {
            return Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>base64url without padding; null when it isn't valid.</summary>
        private static byte[] B64Decode(string s)
        {
            foreach (char c in s)
                if (!(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-' || c == '_')) return null;
            if (s.Length % 4 == 1) return null;
            string t = s.Replace('-', '+').Replace('_', '/');
            t += new string('=', (4 - t.Length % 4) % 4);
            try { return Convert.FromBase64String(t); }
            catch (FormatException) { return null; }
        }
    }
}
