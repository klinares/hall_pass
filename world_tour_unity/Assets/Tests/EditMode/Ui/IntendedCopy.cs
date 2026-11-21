namespace WorldTour.Game.EditTests
{
    /// <summary>
    /// Phase 7.4 (kid-fit): game copy that now differs on purpose from the web game's, in plainer words. The web-vector tests
    /// keep comparing against the web's text: <see cref="ToWeb"/> puts the web's wording back for exactly these lines.
    /// The list is IntendedDifferences.md items 39 to 43.
    /// </summary>
    internal static class IntendedCopy
    {
        private static readonly string[][] Pairs =
        {
            new[] { "what does it come to?", "what is the answer?" },
            new[] { "quick as you can. the meter only changes bonus fans.", "quick as you can. the bar only changes bonus fans. you can't lose fans." },
            new[] { "every star rehearses. run it back.", "every star practices. try it again." },
        };

        public static string ToWeb(string unity)
        {
            foreach (string[] p in Pairs) if (unity == p[1]) return p[0];
            return unity;
        }
    }
}
