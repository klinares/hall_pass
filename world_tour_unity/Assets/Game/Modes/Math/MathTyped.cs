using System;
using System.Text.RegularExpressions;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Game.Modes
{
    /// <summary>How the readout draws what she has typed (parts.js:52-59 renderReadout).</summary>
    public enum ReadoutKind { Empty, Plain, Fraction }

    /// <summary>The readout's shape: plain text, or a stacked fraction (an empty part is a dashed box).</summary>
    public struct ReadoutShape
    {
        public ReadoutKind Kind;
        public string Text;
        /// <summary>The whole number of a mixed number, or null.</summary>
        public string Whole;
        /// <summary>Numerator and denominator; "" is drawn as an empty box.</summary>
        public string Num, Den;
    }

    /// <summary>
    /// parts.js:61-90 wireKeypad's text rules, with no keys and no drawing: digits up to 9 characters, one decimal point
    /// ("0." when it comes first), one fraction bar after a digit (fractions only), "and" (a space) after a whole number
    /// (fractions only), at most two digits after a money point, backspace. <see cref="Press"/> returns whether the key
    /// was taken (the web plays its tap then), which is every key except a third money decimal and anything once locked.
    /// </summary>
    public sealed class KeypadText
    {
        public const int Max = 9;
        public readonly AnswerType Type;
        public string Text { get; private set; } = "";
        public bool Locked { get; private set; }

        public KeypadText(AnswerType type) { Type = type; }

        /// <summary>parts.js:30-35: the decimal point key for money and decimals, the fraction bar and "and" for fractions.</summary>
        public bool HasDot { get { return Type == AnswerType.Money || Type == AnswerType.Decimal; } }
        public bool HasFraction { get { return Type == AnswerType.Fraction; } }

        static readonly Regex EndsDigit = new Regex(@"\d$"), AllDigits = new Regex(@"^\d+$"), TwoDecimals = new Regex(@"\.\d\d$");

        /// <summary>One key: "0".."9", ".", "/", " ", "back". ("go" is the caller's.)</summary>
        public bool Press(string k)
        {
            if (Locked || k == null) return false;
            if (k == "back") { if (Text.Length > 0) Text = Text.Substring(0, Text.Length - 1); }
            else if (k == ".") { if (Text.IndexOf('.') < 0) Text += Text.Length > 0 ? "." : "0."; }
            else if (k == "/") { if (Type == AnswerType.Fraction && Text.IndexOf('/') < 0 && EndsDigit.IsMatch(Text)) Text += "/"; }
            else if (k == " ") { if (Type == AnswerType.Fraction && AllDigits.IsMatch(Text)) Text += " "; }
            else if (k.Length == 1 && k[0] >= '0' && k[0] <= '9')
            {
                if (Text.Length < Max)
                {
                    if (Type == AnswerType.Money && TwoDecimals.IsMatch(Text)) return false;
                    Text += k;
                }
            }
            else return false;
            return true;
        }

        public void Set(string text) { Text = text ?? ""; }
        public void Clear() { Text = ""; }
        public void Lock() { Locked = true; }

        static readonly Regex FracRe = new Regex(@"^(?:(\d+) )?(\d*)\/(\d*)$"), MixedRe = new Regex(@"^(\d+) (\d*)$");

        /// <summary>parts.js:52-59 renderReadout: 1 3/4 drawn stacked as she types it.</summary>
        public static ReadoutShape Shape(string text)
        {
            if (string.IsNullOrEmpty(text)) return new ReadoutShape { Kind = ReadoutKind.Empty, Text = "" };
            Match m = FracRe.Match(text);
            if (m.Success) return new ReadoutShape { Kind = ReadoutKind.Fraction, Whole = m.Groups[1].Success && m.Groups[1].Length > 0 ? m.Groups[1].Value : null, Num = m.Groups[2].Value, Den = m.Groups[3].Value, Text = text };
            Match w = MixedRe.Match(text);
            if (w.Success) return new ReadoutShape { Kind = ReadoutKind.Fraction, Whole = w.Groups[1].Value, Num = w.Groups[2].Value, Den = "", Text = text };
            return new ReadoutShape { Kind = ReadoutKind.Plain, Text = text };
        }
    }

    /// <summary>What a typed submit did.</summary>
    public enum TypedMathResult { Ignored, Nudge, Right, Wrong, Revealed }

    /// <summary>The keypad's side of a typed round: what the readout shows.</summary>
    public interface IMathPadView
    {
        /// <summary>pad.flash('nudge'): the readout rings for 650 ms. Never a miss.</summary>
        void Nudge();
        /// <summary>pad.lock('right') + pop.</summary>
        void LockRight();
        /// <summary>wobble the readout (a wrong answer; nothing turns red).</summary>
        void Wobble();
        /// <summary>pad.set(text); pad.lock('shown').</summary>
        void ShowAnswer(string text);
        /// <summary>pad.clear().</summary>
        void Clear();
        /// <summary>showNext(ctx): the next-song button, so she can read the worked solution first.</summary>
        void ShowNext();
    }

    /// <summary>
    /// parts.js:195-230 <c>typedAnswer</c>: the submit rules for "work it out, type it in". Empty or unreadable input, and a
    /// right value not in simplest form when the problem asks for it, only ask her to retype: no miss, no fans lost, no
    /// streak broken (QUESTIONS 7, <see cref="Answers.IsRetype"/>). A right answer wins and moves on after 1.5 s; a wrong
    /// one goes up the ladder, and the third shows the answer on the readout and the next-song button.
    /// </summary>
    public sealed class TypedMathRound
    {
        public const float NextAfterRight = 1.5f;

        private readonly IMathSong _ctx;
        private readonly Problem _prob;
        private readonly IMathPadView _view;
        private readonly Action _onRight;
        private readonly Action<float> _next;

        public readonly MathLadder Ladder;

        public TypedMathRound(IMathSong ctx, Problem prob, IMathPadView view, Action<float> next, Action<int> onStep = null, Action onRight = null)
        {
            _ctx = ctx; _prob = prob; _view = view; _next = next; _onRight = onRight;
            Ladder = new MathLadder(ctx, prob, onStep);
        }

        public Problem Problem { get { return _prob; } }

        /// <summary>parts.js:226 what the readout shows after the reveal: show(answer) without "$", the [[ ]] or commas.</summary>
        public static string ShownText(Answer answer)
        {
            string s = Answers.Show(answer);
            if (s.StartsWith("$", StringComparison.Ordinal)) s = s.Substring(1);
            return s.Replace("[[", "").Replace("]]", "").Replace(",", "");
        }

        /// <summary>The typed answer that would be right, as she would key it (for tests and the browser playthrough).</summary>
        public static string KeyedAnswer(Answer answer) { return ShownText(answer); }

        public TypedMathResult Submit(string text)
        {
            if (Ladder.Over) return TypedMathResult.Ignored;
            CheckResult res = Answers.Check(text ?? "", _prob.Answer, _prob.Simplest);
            if (Answers.IsRetype(res))
            {
                _view.Nudge();
                _ctx.Say(Answers.Nudge(res, _prob.Answer.Type), "think");
                return TypedMathResult.Nudge;
            }
            if (res.Ok)
            {
                _view.LockRight();
                if (_onRight != null) _onRight();
                Ladder.Right();
                _next(NextAfterRight);
                return TypedMathResult.Right;
            }
            _view.Wobble();
            if (Ladder.Wrong(text))
            {
                _view.ShowAnswer(ShownText(_prob.Answer));
                _view.ShowNext();
                return TypedMathResult.Revealed;
            }
            _view.Clear();
            return TypedMathResult.Wrong;
        }
    }
}
