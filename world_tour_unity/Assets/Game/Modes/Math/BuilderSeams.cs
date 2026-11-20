using System;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Game.Modes
{
    /// <summary>
    /// What the hands-on math builders (stage designer, pack the truck) need from the shared hint ladder
    /// (world_tour/modes/math/parts.js ladder): the bulb gives steps 1 and 2, a wrong try climbs the ladder, the third
    /// miss shows the worked solution. Plain C#, so the builders' rules run in edit-mode tests; the real ladder (task
    /// 4.1's shared math parts) is wrapped to this shape by the mode.
    /// </summary>
    public interface IMathLadder
    {
        /// <summary>parts.js <c>L.over</c>: answered, or the worked solution has been shown.</summary>
        bool Over { get; }
        /// <summary>parts.js <c>L.shown</c>: the highest step shown so far (0 to 3).</summary>
        int Shown { get; }
        /// <summary>
        /// parts.js <c>L.wrong(text, why)</c> with a picked value (a point) or <see cref="MathValue.Null"/> (the truck,
        /// which has no value to match against the traps). True once it was the third miss and the answer is shown.
        /// </summary>
        bool Wrong(MathValue picked, string why = null);
        /// <summary>parts.js <c>L.right(extra)</c>.</summary>
        void Right(int extra = 0);
    }

    /// <summary>The song, as the builders' rules see it (a slice of the show's ctx).</summary>
    public interface IBuilderHost
    {
        /// <summary>ctx.say. pose: "idle", "think", "oops", "cheer".</summary>
        void Say(string line, string pose);
        /// <summary>ctx.mistake: a tagged mistake for the parent view.</summary>
        void Mistake(string tag);
        /// <summary>ctx.lockHints.</summary>
        void LockHints();
        /// <summary>ctx.next(ms), in seconds.</summary>
        void Next(float seconds);
        /// <summary>parts.js showNext: after the worked solution, the "next song" button, so she can read it first.</summary>
        void ShowNext();
    }

    /// <summary>
    /// The shared <see cref="MathLadder"/> as an <see cref="IMathLadder"/>: a picked point is matched against the traps as
    /// the text "(x, y)", which is how the web passes it (parts.js matches() takes the point object; answer.js parsePoint
    /// reads the same pair).
    /// </summary>
    public sealed class BuilderLadder : IMathLadder
    {
        private readonly MathLadder _l;

        public BuilderLadder(IMathSong song, Problem prob, Action<int> onStep) { _l = new MathLadder(song, prob, onStep); }

        public bool Over { get { return _l.Over; } }
        public int Shown { get { return _l.Shown; } }
        public bool Wrong(MathValue picked, string why = null) { return _l.Wrong(PointText(picked), why); }
        public void Right(int extra = 0) { _l.Right(); }

        public static string PointText(MathValue v)
        {
            return v.Kind == ValueKind.Point ? Answers.Show(Answer.Point(v.X, v.Y)) : null;
        }
    }
}
