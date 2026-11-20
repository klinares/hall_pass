using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Maths;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// Plays a scripted show on the engine the way a mode would. One action string per song played, in
    /// order (bonus replays included); a missing string means "w". Letters:
    /// h hint, f a hint with nothing new to show (free), m wrong try, w win, x win with 250 bonus fans
    /// (soundcheck), r reveal. After each song it logs "word,bonus,actions,fans,streak,level" and calls Next.
    /// </summary>
    internal sealed class ShowTestDriver
    {
        public readonly List<string> Log = new List<string>();
        public readonly List<ShowEvent> Events = new List<ShowEvent>();

        public static List<ShowItem> Words(int n)
        {
            return Enumerable.Range(0, n).Select(i => (ShowItem)new WordItem("w" + i)).ToList();
        }

        public static ShowEngine Spelling(IReadOnlyList<ShowItem> items, SkillRecord rec = null, bool encore = false)
        {
            return new ShowEngine(new ShowSetup
            {
                IsMath = false, ModeId = "remix", VenueId = "coffee", Items = items, Encore = encore,
                SkillRecord = rec ?? new SkillRecord(), ClockMs = () => 0
            });
        }

        public void Attach(ShowEngine e)
        {
            e.Raised += ev => Events.Add(ev);
        }

        /// <summary>Starts the engine (if needed) and plays it to the end.</summary>
        public void Play(ShowEngine e, IList<string> script, int hintsPerSong = 3)
        {
            Attach(e);
            if (!e.Started) e.Start();
            int j = 0;
            int guard = 0;
            while (!e.Finished)
            {
                if (++guard > 200) Assert.Fail("show never ended");
                e.SetupHints(hintsPerSong);
                string a = j < script.Count ? script[j] : "w";
                j++;
                foreach (char ch in a) Act(e, ch);
                var w = e.Current.Item;
                Log.Add(string.Join(",", w.Kid, e.Current.Bonus ? "1" : "0", a,
                    e.Fans.ToString(CultureInfo.InvariantCulture), e.Streak.ToString(CultureInfo.InvariantCulture),
                    e.SkillRecord.Level.ToString(CultureInfo.InvariantCulture)));
                e.Next();
            }
        }

        public static void Act(ShowEngine e, char ch)
        {
            switch (ch)
            {
                case 'h': e.Hint(); break;
                case 'f': e.Hint(level => false); break;
                case 'm': e.Miss(); break;
                case 'w': e.Win(); break;
                case 'x': e.Win(250); break;
                case 'r': e.Reveal(); break;
                default: throw new ArgumentException("unknown action " + ch);
            }
        }

        /// <summary>A seeded stand-in for Math.random (mulberry32, the same sequence as the JS rng(seed).next).</summary>
        public static Func<double> Random(long seed)
        {
            var r = new Rng(seed);
            return r.Next;
        }

        /// <summary>
        /// The summary in the shape the JS harness prints: queue; fans; first; hintOnly; best; hintTotal; hinted;
        /// missed; pct; n; sold; encore; finishShow.hinted. Queue entries are word[*]:state.
        /// </summary>
        public static string Describe(ShowEngine e)
        {
            var s = e.Summary;
            string q = string.Join(" ", e.Queue.Select(x => x.Item.Kid + (x.Bonus ? "*" : "") + ":" + State(x.State)));
            return string.Join(";", q, I(s.Fans), I(s.First), I(s.HintOnly), I(s.Best), I(s.HintTotal),
                string.Join(" ", e.HintedKids.OrderBy(k => k, StringComparer.Ordinal)),
                string.Join(" ", e.MissedKids.OrderBy(k => k, StringComparer.Ordinal)),
                "PCT", I(s.N), s.Sold ? "1" : "0", s.Encore ? "1" : "0", I(s.Hinted));
        }

        public static string DescribeSkill(SkillRecord k)
        {
            return string.Join(";", I(k.Level), I(k.Run), I(k.MissRun), I(k.A), I(k.C), I(k.H),
                string.Join(" ", new[] { 1, 2, 3 }.Select(i => I(k.ByLevel[i][0]) + "/" + I(k.ByLevel[i][1]))));
        }

        /// <summary>Compares a Describe() string with the JS one; pct (field 8) is compared as a number.</summary>
        public static void AssertSummary(string expectedJs, ShowEngine e, string label)
        {
            string[] want = expectedJs.Split(';');
            double pct = double.Parse(want[8], CultureInfo.InvariantCulture);
            Assert.AreEqual(pct, e.Summary.Pct, 0.0, label + " pct");
            want[8] = "PCT";
            Assert.AreEqual(string.Join(";", want), Describe(e), label + " summary");
        }

        private static string State(SlotState s)
        {
            return s == SlotState.Done ? "done" : s == SlotState.Miss ? "miss" : "";
        }

        private static string I(int v)
        {
            return v.ToString(CultureInfo.InvariantCulture);
        }
    }
}
