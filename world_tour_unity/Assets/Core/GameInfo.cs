namespace WorldTour.Core
{
    /// <summary>Fixed facts about the game that the UI shows. Plain C#, no UnityEngine.</summary>
    public static class GameInfo
    {
        /// <summary>The display title, lowercase like all display text.</summary>
        /// <remarks>
        /// static readonly, not const: a const is copied into callers at compile time, so the
        /// Boot label would no longer prove that WorldTour.Core loads in the stripped web player.
        /// </remarks>
        public static readonly string Title = "natalia" + (char)0x2019 + "s world tour";
    }
}
