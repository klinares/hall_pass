using System.Collections.Generic;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// The math modes' copy, word for word from world_tour/modes/math/parts.js, merch.js and beat.js. Non-ASCII characters
    /// are built from their codes, so the source stays ASCII.
    /// </summary>
    public static class MathLines
    {
        static readonly string A = ((char)0x2019).ToString();     // the curly apostrophe the web copy uses
        static readonly string Star = ((char)0x2736).ToString();
        static readonly string Play = ((char)0x25B6).ToString();
        static readonly string Box = ((char)0x25A2).ToString();
        static readonly string FracSlash = ((char)0x2044).ToString();

        /// <summary>parts.js:118-147 TRAP: the line for a wrong answer that matches a known mistake.</summary>
        public static readonly IReadOnlyDictionary<string, string> Trap = new Dictionary<string, string>
        {
            { "no-carry", "close. check your carrying: did a column go over 9?" },
            { "no-borrow", "close. when the top digit is smaller, you have to borrow." },
            { "gave-total", "that" + A + "s what it costs. the question wants the change back." },
            { "decimal-place", "right digits, but the decimal point landed in the wrong spot." },
            { "added-instead", "looks like you added. this one multiplies." },
            { "partial-only", "that" + A + "s only part of it. multiply by the tens too." },
            { "no-placeholder", "the tens digit is worth ten times as much. don" + A + "t forget the zero." },
            { "place-value", "check your place value: that" + A + "s ten times off." },
            { "subtracted-instead", "that subtracted once. how many times does it fit?" },
            { "rounded-up", "the last row isn" + A + "t full, so it doesn" + A + "t count." },
            { "gave-remainder", "that" + A + "s the leftover fans. how many full rows?" },
            { "added-denominators", "adding the bottoms changes the size of the pieces. make them match first." },
            { "subtracted-denominators", "subtracting the bottoms changes the size of the pieces. make them match first." },
            { "tops-not-rewritten", "the bottoms match now, but the tops have to change too." },
            { "swapped", "you flipped them. across comes first, then up." },
            { "moves-swapped", "right and left change the first number, up and down the second." },
            { "wrong-direction", "check the direction of each move." },
            { "added-sides", "that adds the sides. volume multiplies them." },
            { "one-layer", "that" + A + "s one layer. how many layers are there?" },
            { "visible-faces", "some crates are hiding behind the front ones." },
            { "surface-area", "that" + A + "s the outside. volume is what fits inside." },
            { "one-box", "that" + A + "s one of the blocks. add the other one too." },
            { "subtracted", "volume multiplies. try dividing instead." },
            { "divided-once", "you divided by one side. divide by the floor area." },
            { "too-short", "not full yet. there" + A + "s still a gap before the bar line." },
            { "too-long", "too much. notes are spilling past the bar line." },
            { "left-to-right", "that works left to right. parentheses first, then " + (char)0x00D7 + " and " + (char)0x00F7 + "." },
            { "ignored-parentheses", "the parentheses go first." },
        };

        /// <summary>parts.js:174 the first and later wrong-try leads.</summary>
        public const string NotQuite = "not quite. ";
        public const string KeepGoing = "keep going. ";
        /// <summary>parts.js:169 after the worked solution.</summary>
        public static readonly string ComeBack = " it" + A + "ll come back around.";

        /// <summary>
        /// The worked line and " it'll come back around.", with a full stop between them when the worked line doesn't end
        /// a sentence already ("so 9. it'll come back around."; the web ran them together). Intended difference 30.
        /// </summary>
        public static string ComeBackAfter(string worked)
        {
            string plain = System.Text.RegularExpressions.Regex.Replace(worked ?? "", "<[^>]+>", "").TrimEnd();
            char last = plain.Length > 0 ? plain[plain.Length - 1] : '.';
            bool ends = last == '.' || last == '?' || last == '!';
            return (worked ?? "") + (ends ? "" : ".") + ComeBack;
        }

        /// <summary>The worked line alone, ending a sentence (the snack table, whose reveal has no "it'll come back around").</summary>
        public static string WorkedOnly(string worked)
        {
            string plain = System.Text.RegularExpressions.Regex.Replace(worked ?? "", "<[^>]+>", "").TrimEnd();
            char last = plain.Length > 0 ? plain[plain.Length - 1] : '.';
            bool ends = last == '.' || last == '?' || last == '!' || last == ')';
            return (worked ?? "") + (ends ? "" : ".");
        }

        /// <summary>parts.js:39 the keypad's submit label.</summary>
        public const string LockItIn = "lock it in";
        /// <summary>parts.js:44 the mixed-number key.</summary>
        public const string And = "and";
        /// <summary>parts.js:43 the decimal point key face (a bullet).</summary>
        public static readonly string DotFace = ((char)0x2022).ToString();
        /// <summary>parts.js:213 the next button after a reveal.</summary>
        public static readonly string NextSong = "next song " + (char)0x2192;
        /// <summary>parts.js:17 the prompt card's default label.</summary>
        public const string Tonight = "tonight";

        // ---------- merch table (merch.js) ----------
        public const string MerchTag = "the merch table";
        public const string OrderUp = "order up";
        public const string RingItUp = "ring it up";
        public const string Paid = "paid";
        public const string MerchChange = "count back the change. take your time.";
        public static readonly string MerchTotal = "ring it up. the keypad" + A + "s right there.";

        // ---------- beat maker (beat.js) ----------
        public static readonly string BeatTag = "the beat " + Star + " ";
        public const string Measures = " measures";
        public const string OneMeasure = "one measure";
        public const string AddThem = "add them";
        public const string CutIt = "cut it";
        public const string TrimIt = "trim it";
        public const string FillIt = "fill it";
        public const string Add = "add";
        public const string Subtract = "subtract";
        public const string PutBack = "put back";
        public const string Undo = "undo";
        public static readonly string PlayIt = Play + " play it";
        public const string NoRoom = "no more room on the page. take something back first.";
        /// <summary>QUESTIONS 15 (new copy, after pack the truck's "load a crate or two first."): "play it" on an empty measure.</summary>
        public const string AddANoteFirst = "add a note or two first.";
        public const string TooMany = "too many notes. tap the ones that have to go.";
        public const string TapOrDrag = "tap a note block to add it, or drag it onto the measure.";
        public const string BassThenDrums = "bass, then drums";
        public const string FadedPartCut = "the faded part gets cut";
        public const string TapToTakeOut = "tap a note in the measure to take it out";
        public static readonly string HowLongTogether = "how long are they together? fractions use the " + Box + FracSlash + Box + " key.";
        public const string HowMuchLeft = "how much is left? simplest form, please.";
    }
}
