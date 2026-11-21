using WorldTour.Core.Meta;
using WorldTour.Core.Progress;

namespace WorldTour.Game.Ui
{
    /// <summary>
    /// The art half of look.js: which guitar skin she has on, so every guitar sticker wears it (shop.js "every guitar
    /// sticker"; look.js sets --gtr). The skins are exported sprites (guitar_&lt;skin&gt; and guitar_&lt;skin&gt;_sm), so the
    /// sprite lookups (MapArt.Get, TicketArt.Get) send a plain "guitar" or "guitar_sm" to the skinned one. Classic is the
    /// plain sprite. Set by GameSettings.Apply from the save. Plain C#.
    /// </summary>
    public static class LookArt
    {
        /// <summary>The equipped skin without the slot ("mint"), or "" for classic.</summary>
        public static string GuitarSkin { get; private set; } = "";

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { GuitarSkin = ""; }

        /// <summary>Reads the equipped skin from the save (look() rules: unknown or not owned is classic).</summary>
        public static void Apply(SaveBlob s)
        {
            GuitarSkin = s == null ? "" : SkinOf(new Backstage(s).EquippedGuitarId());
        }

        /// <summary>"guitar:mint" to "mint"; classic, unknown or null to "".</summary>
        public static string SkinOf(string guitarId)
        {
            if (string.IsNullOrEmpty(guitarId) || guitarId == "guitar:classic" || !guitarId.StartsWith("guitar:")) return "";
            return guitarId.Substring(7);
        }

        /// <summary>The sprite name for a sticker art id, small or not, with a guitar wearing <paramref name="skin"/> ("" = classic).</summary>
        public static string StickerName(string art, bool small, string skin)
        {
            if (art == "guitar" && !string.IsNullOrEmpty(skin)) return "guitar_" + skin + (small ? "_sm" : "");
            return art + (small ? "_sm" : "");
        }

        /// <summary>What a lookup by manifest name should return: the equipped skin for a plain guitar sticker, else the name.</summary>
        public static string Skinned(string spriteName)
        {
            if (GuitarSkin.Length == 0) return spriteName;
            if (spriteName == "guitar") return "guitar_" + GuitarSkin;
            if (spriteName == "guitar_sm") return "guitar_" + GuitarSkin + "_sm";
            return spriteName;
        }
    }
}
