using System;
using System.Globalization;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// An evaluator that shares nothing with the generators, standing in for the JS test's
    /// <c>Function('return (' + expr + ')')</c>: ordinary precedence (parentheses, then x and /, then + and -,
    /// left to right), in doubles. Reads whole and decimal numbers, + - ( ), the minus sign U+2212,
    /// times U+00D7 and divide U+00F7. Spaces are ignored. Throws FormatException on anything else.
    /// </summary>
    public static class ExprEval
    {
        private const char MinusSign = (char)0x2212, TimesSign = (char)0x00D7, DivideSign = (char)0x00F7;

        public static double Eval(string text)
        {
            int i = 0;
            double v = Sum(text, ref i);
            Skip(text, ref i);
            if (i != text.Length) throw new FormatException("unexpected '" + text[i] + "' in " + text);
            return v;
        }

        private static void Skip(string s, ref int i) { while (i < s.Length && s[i] == ' ') i++; }

        private static double Sum(string s, ref int i)
        {
            double v = Product(s, ref i);
            for (;;)
            {
                Skip(s, ref i);
                if (i < s.Length && s[i] == '+') { i++; v += Product(s, ref i); }
                else if (i < s.Length && (s[i] == '-' || s[i] == MinusSign)) { i++; v -= Product(s, ref i); }
                else return v;
            }
        }

        private static double Product(string s, ref int i)
        {
            double v = Atom(s, ref i);
            for (;;)
            {
                Skip(s, ref i);
                if (i < s.Length && (s[i] == TimesSign || s[i] == '*')) { i++; v *= Atom(s, ref i); }
                else if (i < s.Length && (s[i] == DivideSign || s[i] == '/')) { i++; v /= Atom(s, ref i); }
                else return v;
            }
        }

        private static double Atom(string s, ref int i)
        {
            Skip(s, ref i);
            if (i < s.Length && s[i] == '(')
            {
                i++;
                double v = Sum(s, ref i);
                Skip(s, ref i);
                if (i >= s.Length || s[i] != ')') throw new FormatException("missing ) in " + s);
                i++;
                return v;
            }
            int start = i;
            while (i < s.Length && ((s[i] >= '0' && s[i] <= '9') || s[i] == '.')) i++;
            if (i == start) throw new FormatException("expected a number at " + start + " in " + s);
            return double.Parse(s.Substring(start, i - start), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        }
    }
}
