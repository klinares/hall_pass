using System;
using System.Collections.Generic;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Game.Modes
{
    /// <summary>The snack table's copy (Unity only, no web twin): lowercase, short, a little dry.</summary>
    public static class FracMulLines
    {
        /// <summary>The stage desk's tag.</summary>
        public const string DeskTag = "the snack table";
        /// <summary>The keypad's check key.</summary>
        public const string ServeIt = "serve it";
        public const string Name = "snack table";
        public const string Sub = "parts of parts of a tray";

        /// <summary>The lines for the mistakes the snack table tags (kept apart from the web game's <see cref="MathLines.Trap"/>).</summary>
        public static readonly IReadOnlyDictionary<string, string> Trap = new Dictionary<string, string>
        {
            { "multiplied-instead", "that multiplied. a part of a number is smaller than the number." },
            { "took-some-away", "that took some away. a fraction of a number means splitting it up." },
            { "gave-the-bottom", "that's how many groups there are. how many go in one group?" },
            { "added-the-bottoms", "adding the bottoms isn't it. a part of a part makes smaller pieces: the bottoms multiply." },
            { "kept-one-part", "that's just one of the parts. the second fraction cuts it up again." },
            { "added-across", "that added the tops and the bottoms. times means tops times tops and bottoms times bottoms." },
            { "multiplied-only-the-tops", "the tops multiplied, but the bottoms change too. the pieces got smaller." },
            { "forgot-a-top", "the bottoms multiplied, but the tops do too. multiply both." },
            { "multiplied-the-share", "that put the pieces together. a share is smaller than the part you started with." },
            { "divided-wrong-way", "that divided the wrong way round. check which number is being split up." },
            { "divided-by-the-bottom", "that divided by the bottom. dividing by a fraction asks how many of them fit." },
            { "added-them-up", "that added them up. look again at what the question wants." },
            { "one-tray-only", "that's one tray. how many trays are there?" },
            { "forgot-the-whole", "that leaves the whole number out. a mixed number has wholes and a fraction part." },
            { "multiplied-only-the-whole", "the whole number got multiplied, but the fraction part has to be multiplied too." },
            { "multiplied-the-known", "that multiplied again. working backwards undoes the multiplying." },
            { "subtracted-the-known", "subtracting won't undo a multiply." },
            { "counted-the-unshaded", "that counts the part that isn't shaded. look at what is." },
            { "one-cell-only", "that's the size of one little piece. how many pieces are shaded?" },
            { "ignored-the-size", "check the size of the other factor. is it more than 1, less than 1, or exactly 1?" },
            { "multiplying-makes-bigger", "multiplying doesn't always make it bigger. what happens when you take a part of something?" },
            { "multiplying-makes-smaller", "multiplying doesn't always make it smaller. what if you take more than the whole?" },
        };

        public static string PromptLabel(string kind)
        {
            switch (kind)
            {
                case "unitof": return "one share";
                case "part": return "a part of a part";
                case "unitnon":
                case "nonnon": return "parts of a tray";
                case "shaded": return "the shaded part";
                case "share": return "sharing a share";
                case "howmany": return "how many fit";
                case "mixed": return "the recipe";
                case "missing": return "the smudge";
                default: return "no multiplying";
            }
        }

        /// <summary>The dog's first line.</summary>
        /// <summary>7.4: the dog's first line for a problem; for a part of a part it names the numbers ("you want 1/2 of a 1/6 piece. picture the tray.").</summary>
        public static string Intro(Problem p)
        {
            if (p.Kind == "part" && p.Params != null && p.Params.Has("factors"))
            {
                var f = p.Params["factors"].Items;
                if (f.Count == 2)
                    return "you want [[1/" + f[1]["d"].Long + "]] of a [[1/" + f[0]["d"].Long + "]] piece. picture the tray.";
            }
            return Intro(p.Kind);
        }

        public static string Intro(string kind)
        {
            switch (kind)
            {
                case "unitof": return "cut it into equal groups and take one.";
                case "part": return "a part of a part. picture the tray.";   // the problem's own line is Intro(Problem)
                case "unitnon":
                case "nonnon": return "work it out on paper if you want. i" + Glyph.Apos + "ll wait.";
                case "shaded": return "read the picture. which expression says it?";
                case "share": return "sharing a part makes smaller parts.";
                case "howmany": return "how many of the small ones fit?";
                case "mixed": return "turn the mixed number into one fraction first.";
                case "missing": return "work backwards from the product.";
                default: return "you don" + Glyph.Apos + "t need to multiply. just think about sizes.";
            }
        }

        /// <summary>Kinds answered by picking an expression or a sign, not on the keypad.</summary>
        public static bool Picks(Problem p) { return p.Choices != null; }

        /// <summary>Kinds with a diagram on the desk; the others show only the expression.</summary>
        public static bool HasPicture(Problem p) { return FracMulPicture.For(p, 0).Kind != "none"; }

        /// <summary>The picture is the question itself (the shaded region): no expression line over it.</summary>
        public static bool PictureIsTheQuestion(Problem p) { return p.Kind == "shaded"; }

        /// <summary>A choice's text as the button shows it: stacked fractions, and a "&lt;" TMP can't take for a tag.</summary>
        public static string ChoiceLabel(Problem p, Choice c)
        {
            if (p.Kind == "compare") return c.Text.Replace("<", "<noparse><</noparse>");
            return MathText.ToTmp(c.Text, MathText.SetlistScale);
        }
    }

    /// <summary>What a press on a choice did.</summary>
    public enum FmPick { Ignored, Right, Wrong, WrongShown }

    /// <summary>The choices' side of a snack table problem.</summary>
    public interface IFmChoiceView
    {
        /// <summary>A wrong pick: struck through, off, a wobble.</summary>
        void Gone(int i);
        /// <summary>Every choice off (the look kept).</summary>
        void FreezeChoices();
        /// <summary>The right choice in ink, with a pop when she picked it.</summary>
        void Good(int i, bool pop);
        /// <summary>The next-song button, so she can read the worked solution first.</summary>
        void ShowNext();
    }

    /// <summary>
    /// The rules of a picked snack table problem (which expression matches the shaded part; less, equal or greater): the
    /// shared hint ladder, a wrong pick is a try with its mistake tag, the third try shows the right one. A right pick wins
    /// and moves on after 1.5 s, as the typed round does.
    /// </summary>
    public sealed class FmChoiceRound
    {
        private readonly Problem _p;
        private readonly IFmChoiceView _view;
        private readonly Action<float> _next;

        public readonly MathLadder Ladder;
        public readonly int RightIndex;

        public FmChoiceRound(Problem p, IMathSong song, IFmChoiceView view, Action<float> next, Action<int> onStep)
        {
            _p = p; _view = view; _next = next;
            RightIndex = p.Choices.FindIndex(c => c.Correct);
            Ladder = new MathLadder(song, p, onStep);
        }

        public FmPick Pick(int i)
        {
            if (Ladder.Over || i < 0 || i >= _p.Choices.Count) return FmPick.Ignored;
            Choice c = _p.Choices[i];
            if (c.Correct)
            {
                _view.FreezeChoices();
                _view.Good(i, true);
                Ladder.Right();
                _next(TypedMathRound.NextAfterRight);
                return FmPick.Right;
            }
            _view.Gone(i);
            if (Ladder.Wrong(null, c.Why))
            {
                _view.FreezeChoices();
                _view.Good(RightIndex, false);
                _view.ShowNext();
                return FmPick.WrongShown;
            }
            return FmPick.Wrong;
        }
    }
}
