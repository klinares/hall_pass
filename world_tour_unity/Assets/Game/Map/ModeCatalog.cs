using System.Collections.Generic;
using WorldTour.Core.Meta;

namespace WorldTour.Game.Map
{
    /// <summary>A playable mode as the venue card lists it (modes/index.js entries: id, name, kind, skill, sub, sticker).</summary>
    public sealed class ModeEntry
    {
        public readonly ModeInfo Info;
        /// <summary>The one-line description under the name (each mode file's <c>sub</c>).</summary>
        public readonly string Sub;
        /// <summary>The sticker art id (each mode file's <c>sticker</c>); the sprite is "&lt;id&gt;_sm".</summary>
        public readonly string Sticker;
        /// <summary>True once the Unity build has the mode. Others are left off the venue card.</summary>
        public readonly bool Playable;

        public ModeEntry(ModeInfo info, string sub, string sticker, bool playable)
        {
            Info = info; Sub = sub; Sticker = sticker; Playable = playable;
        }

        public string Id { get { return Info.Id; } }
        public string Name { get { return Info.Name; } }
        public bool IsSpelling { get { return Info.Kind == ModeInfo.Spelling; } }
    }

    /// <summary>
    /// The web game's mode registry (world_tour/modes/index.js MODES, setlist order), with the copy from each mode file.
    /// Every mode is listed so milestones (every-mode, master-*) and the office see the whole set, as progress.js
    /// <c>setModes(MODES)</c> does; <see cref="ModeEntry.Playable"/> marks the ones the Unity build can play so far.
    /// Math modes a math_config switch turns off are not filtered yet (no math mode is playable yet).
    /// </summary>
    public static class ModeCatalog
    {
        static string A { get { return MapText.Apos; } }

        public static readonly IReadOnlyList<ModeEntry> All = new[]
        {
            new ModeEntry(new ModeInfo("slots", "missing letters", ModeInfo.Spelling), "some letters skipped town. fill them in", "mic", true),
            new ModeEntry(new ModeInfo("vowels", "missing vowels", ModeInfo.Spelling), "every vowel is gone. put them back", "phones", true),
            new ModeEntry(new ModeInfo("remix", "remix", ModeInfo.Spelling), "tap the letter stickers in order", "cassette", true),
            new ModeEntry(new ModeInfo("fanmail", "fan mail", ModeInfo.Spelling), "match the word to the fan" + A + "s message", "heart", true),
            new ModeEntry(new ModeInfo("studio", "studio session", ModeInfo.Spelling), "hear it, spell it. test-day practice", "vinyl", true),
            new ModeEntry(new ModeInfo("lyric", "lyric writer", ModeInfo.Spelling), "finish the lyric with this week" + A + "s word", "setlist", true),
            new ModeEntry(new ModeInfo("prefix", "prefix mixer", ModeInfo.Spelling), "build the word, then crack the prefix", "pick", true),
            new ModeEntry(new ModeInfo("merch", "merch table", ModeInfo.Math, "dec"), "ring up fans and count their change", "tee", false),
            new ModeEntry(new ModeInfo("beat", "beat maker", ModeInfo.Math, "frac"), "fill the measure with fraction notes", "note", false),
            new ModeEntry(new ModeInfo("tickets", "ticket sales", ModeInfo.Math, "md"), "multiply and divide the crowd", "ticket", false),
            new ModeEntry(new ModeInfo("stage", "stage designer", ModeInfo.Math, "xy"), "place the gear on the grid", "lights", false),
            new ModeEntry(new ModeInfo("truck", "pack the truck", ModeInfo.Math, "vol"), "volume: fill the trailer", "bus", false),
            new ModeEntry(new ModeInfo("budget", "tour budget", ModeInfo.Math, "ops"), "order of operations on the books", "setlist", false),
            new ModeEntry(new ModeInfo("soundcheck", "soundcheck", ModeInfo.Math, "flu"), "fast facts and powers of ten", "amp", false),
        };

        static List<ModeInfo> s_Infos;

        /// <summary>The list progress.js <c>setModes</c> receives, for AwardStickers and the office.</summary>
        public static IReadOnlyList<ModeInfo> Infos
        {
            get
            {
                if (s_Infos == null) { s_Infos = new List<ModeInfo>(); foreach (ModeEntry m in All) s_Infos.Add(m.Info); }
                return s_Infos;
            }
        }

        public static ModeEntry ById(string id)
        {
            foreach (ModeEntry m in All) if (m.Id == id) return m;
            return null;
        }
    }
}
