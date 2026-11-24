using System.Collections.Generic;

namespace WorldTour.Core.Tour
{
    /// <summary>What a crew member says (one short lowercase line, 12 words at most) before offering a gig.</summary>
    public static class CrewLines
    {
        public const int MaxWords = 12;

        static readonly Dictionary<string, string> Pitch = new Dictionary<string, string>
        {
            { "slots", "someone smudged the setlist. can you fill in the letters?" },
            { "vowels", "the vowels fell off our posters. help me put them back?" },
            { "remix", "these words got scrambled. can you unscramble them with me?" },
            { "fanmail", "a fan wrote in. which spelling is the right one?" },
            { "studio", "ready to record? listen close and spell what you hear." },
            { "lyric", "this lyric is missing a word. which one fits?" },
            { "prefix", "prefix time. help me build the right word?" },
            { "merch", "the merch table needs help with prices. you in?" },
            { "beat", "this beat has fractions in it. want to build it?" },
            { "tickets", "the ticket count is a mess. help me with the math?" },
            { "stage", "i need the speakers placed just right. can you plot them?" },
            { "truck", "the truck is packed strangely. how many crates fit?" },
            { "budget", "the tour budget is a puzzle. want to solve it?" },
            { "soundcheck", "soundcheck. quick facts, no stress. you ready?" },
            { "snack", "the snack table needs fractions. can you slice it up?" },
        };

        public const string Generic = "hey. want to try a quick one together?";
        public const string Decline = "no rush. i will be here.";
        public const string Stage = "the crowd is waiting. ready to play the show?";

        /// <summary>The pitch for a mode id (a generic line for an unknown one).</summary>
        public static string For(string modeId)
        {
            string line;
            return modeId != null && Pitch.TryGetValue(modeId, out line) ? line : Generic;
        }

        public static IEnumerable<string> All()
        {
            foreach (var kv in Pitch) yield return kv.Value;
            yield return Generic;
            yield return Decline;
            yield return Stage;
        }

        /// <summary>Who the crew member is, by position in the place.</summary>
        public static string Role(int crewIndex)
        {
            switch (crewIndex) { case 0: return "the roadie"; case 1: return "the sound tech"; default: return "the manager"; }
        }
    }
}
