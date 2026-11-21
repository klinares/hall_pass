using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Maths;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Core.Tests
{
    /// <summary>Phase 7.4 kid-fit, round 1: ticket sales' warm-up level (a grade-5 child can do most of it in her head).</summary>
    public class KidFitCoreTests
    {
        private static long P(Problem p, string key) { return p.Params[key].Long; }

        [Test]
        public void TicketSalesWarmUp_IsFactsAndNoRegroupingSums()
        {
            int facts = 0, total = 0, mul = 0, div = 0;
            foreach (long seed in GeneratorContract.Seeds(0, 3000))
            {
                Problem p = MultDiv.Generate(0, seed);
                Assert.AreEqual(0, p.Level);
                total++;
                if (p.Kind == "mul")
                {
                    mul++;
                    long a = P(p, "a"), b = P(p, "b");
                    Assert.AreEqual(a * b, (long)p.Answer.Value.Number);
                    bool fact = a <= 9 && b <= 9;
                    if (fact) { facts++; Assert.GreaterOrEqual(a, 2); Assert.GreaterOrEqual(b, 3); }
                    else
                    {
                        Assert.That(a, Is.InRange(10, 99), "2-digit x 1-digit: " + a + " x " + b);
                        Assert.That(b, Is.InRange(2, 9));
                        if (a % 10 != 0)
                        {
                            Assert.LessOrEqual((a % 10) * b, 9, "no carry in the ones: " + a + " x " + b);
                            Assert.LessOrEqual((a / 10) * b, 9, "no carry in the tens: " + a + " x " + b);
                        }
                    }
                }
                else
                {
                    Assert.AreEqual("div", p.Kind, "no 'full rows' at the warm-up");
                    div++;
                    long n = P(p, "N"), d = P(p, "d");
                    Assert.LessOrEqual(n, 99, "no 3-digit dividends at level 0: " + n + " / " + d);
                    Assert.AreEqual(0, n % d, "no remainder: " + n + " / " + d);
                    Assert.AreEqual(n / d, (long)p.Answer.Value.Number);
                    bool fact = d <= 9 && n / d <= 9;
                    if (fact) facts++;
                    else
                    {
                        Assert.That(d, Is.InRange(2, 4));
                        Assert.AreEqual(0, (n / 10) % d, "no regrouping, tens: " + n + " / " + d);
                        Assert.AreEqual(0, (n % 10) % d, "no regrouping, ones: " + n + " / " + d);
                    }
                }
            }
            Assert.Greater(mul, 500); Assert.Greater(div, 500);
            Assert.GreaterOrEqual(facts / (double)total, .6, "mostly facts");
        }

        [Test]
        public void TicketSalesWarmUp_Sample()
        {
            var lines = new List<string>();
            for (long seed = 101; lines.Count < 10; seed += 7919) { Problem p = MultDiv.Generate(0, seed); lines.Add(p.Label + "  | " + p.Prompt); }
            TestContext.Out.WriteLine("KIDFIT_SAMPLE\n" + string.Join("\n", lines));
        }

        [Test]
        public void TicketSalesWarmUp_StepLineIsShort()
        {
            var re = new System.Text.RegularExpressions.Regex("^(\\d+) × (\\d+) = (\\d+)\\. that leaves (\\d+)\\. how many \\1s fit in \\4\\?$");
            int seen = 0;
            foreach (long seed in GeneratorContract.Seeds(0, 600))
            {
                Problem p = MultDiv.Generate(0, seed);
                if (p.Kind != "div") continue;
                seen++;
                Assert.IsTrue(re.IsMatch(p.Steps[1]), p.Steps[1]);
            }
            Assert.Greater(seen, 100);
            // levels 1 to 3 keep the web's text (the JS vectors pin it)
            int web = 0;
            foreach (long seed in GeneratorContract.Seeds(1, 300))
            {
                Problem p = MultDiv.Generate(1, seed);
                if (p.Kind == "div" && p.Steps[1].Contains("running total")) web++;
            }
            Assert.Greater(web, 0, "level 1 keeps the web's step line with its running total");
        }
    }
}
