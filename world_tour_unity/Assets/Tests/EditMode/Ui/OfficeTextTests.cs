using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Game.Map;
using WorldTour.Game.Office;
using OfficeRules = WorldTour.Core.Meta.Office;

namespace WorldTour.Game.EditTests
{
    /// <summary>
    /// The manager's office screen text (task 5.2), checked against the web game's own office.js: office_vectors.txt is
    /// written by Tools/office/office_vectors.mjs, which runs screens/office.js mount() in node and reads its HTML back.
    /// Each seed's save is the one the web game held after mount, so Office.Build + OfficeText must show the same rows.
    /// </summary>
    public class OfficeTextTests
    {
        static string VectorPath
        {
            get
            {
                string assets;
                try { assets = UnityEngine.Application.dataPath; }
                catch (Exception) { assets = Environment.GetEnvironmentVariable("WT_ASSETS"); }
                return Path.Combine(assets, "Tests", "EditMode", "Ui", "office_vectors.txt");
            }
        }

        static string[] s_Lines;
        static string[] Lines { get { return s_Lines ?? (s_Lines = File.ReadAllLines(VectorPath)); } }

        static string Unesc(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 5 < s.Length + 0 && s[i + 1] == 'u')
                {
                    sb.Append((char)Convert.ToInt32(s.Substring(i + 2, 4), 16));
                    i += 5;
                }
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }

        [Test]
        public void Vectors_AreThere()
        {
            Assert.That(Lines.Count(l => l.Length > 0 && char.IsDigit(l[0])), Is.GreaterThanOrEqualTo(100));
        }

        [Test]
        public void EveryRender_MatchesTheWebOffice()
        {
            int n = 0, fails = 0;
            var report = new StringBuilder();
            foreach (string line in Lines)
            {
                if (line.Length == 0 || !char.IsDigit(line[0])) continue;
                string[] f = line.Split('\t');
                Assert.AreEqual(4, f.Length, "seed line has 4 fields");
                string today = f[1];
                LoadResult r = SaveJson.Load(f[2], today);
                Assert.IsFalse(r.Unreadable, "seed " + f[0] + ": " + r.Error);
                OfficeReport o = OfficeRules.Build(r.Blob, ModeCatalog.Infos, today);
                string got = OfficeText.Trace(o);
                n++;
                if (got != f[3])
                {
                    fails++;
                    if (fails <= 3)
                    {
                        string[] a = f[3].Split('|'), b = got.Split('|');
                        for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
                        {
                            string x = i < a.Length ? a[i] : "(none)", y = i < b.Length ? b[i] : "(none)";
                            if (x != y) { report.AppendLine("seed " + f[0] + " part " + i + "\n  web:   " + x + "\n  unity: " + y); break; }
                        }
                    }
                }
            }
            Assert.That(n, Is.GreaterThanOrEqualTo(100));
            Assert.AreEqual(0, fails, fails + " of " + n + " renders differ\n" + report);
        }

        [Test]
        public void Day_MatchesTheWebFormat()
        {
            string line = Lines.First(l => l.StartsWith("day\t", StringComparison.Ordinal));
            int n = 0;
            foreach (string pair in Unesc(line.Substring(4)).Split('|'))
            {
                string[] kv = pair.Split('=');
                Assert.AreEqual(kv[1], OfficeText.Day(kv[0]), kv[0]);
                n++;
            }
            Assert.That(n, Is.GreaterThan(150));
        }

        [Test]
        public void Copy_IsWordForWordTheWebs()
        {
            string line = Lines.First(l => l.StartsWith("copy\t", StringComparison.Ordinal));
            var web = new HashSet<string>(Unesc(line.Substring(5)).Split('|'));
            var mine = new List<string>
            {
                "back=" + OfficeText.Back, "h1=" + OfficeText.Title, "p=" + OfficeText.Sub,
                "h2=" + OfficeText.GlanceHead, "h2=" + OfficeText.TimeHead, "p=" + OfficeText.TimeFoot,
                "h2=" + OfficeText.AccuracyHead, "p=" + OfficeText.AccuracyFoot, "h3=" + OfficeText.Spelling, "h3=" + OfficeText.Math,
                "h2=" + OfficeText.WordsHead, "p=" + OfficeText.WordsEmpty, "h2=" + OfficeText.MistakesHead, "p=" + OfficeText.MistakesEmpty,
                "h2=" + OfficeText.SettingsHead, "label=" + OfficeText.ReduceMotion, "h2=" + OfficeText.ResetHead, "p=" + OfficeText.ResetBody,
                "button=" + OfficeText.ResetButton, "p=" + OfficeText.Sure + OfficeText.SureRest, "button=" + OfficeText.Cancel, "button=" + OfficeText.Erase,
            };
            foreach (string h in OfficeText.SkillHeads) mine.Add("th=" + h);
            foreach (string m in mine) Assert.IsTrue(web.Contains(m), "not in the web office: " + m);
            // and nothing the web shows (apart from the tables' rows) is missing here
            var have = new HashSet<string>(mine);
            foreach (string w in web)
            {
                if (w.StartsWith("th=", StringComparison.Ordinal)) continue;
                Assert.IsTrue(have.Contains(w), "the web office shows this and Unity doesn't: " + w);
            }
        }

        [Test]
        public void Helpers_FollowOfficeJs()
        {
            Assert.AreEqual("1 word", OfficeText.Plural(1, "word"));
            Assert.AreEqual("0 problems", OfficeText.Plural(0, "problem"));
            Assert.AreEqual("12 shows", OfficeText.Plural(12, "show"));
            Assert.AreEqual("86%", OfficeText.PctCell(86));
            Assert.AreEqual(OfficeText.None, OfficeText.PctCell(null));
            Assert.AreEqual(OfficeText.None, OfficeText.Dash(null));
            Assert.AreEqual("fri, nov 20", OfficeText.Day("2026-11-20"));
            Assert.AreEqual("nope", OfficeText.Day("nope"));
            Assert.AreEqual(60, OfficeText.Peak(new List<OfficeDay> { new OfficeDay { Secs = 10 } }));
            DayLine l = OfficeText.DayOf(new OfficeDay { D = "2026-11-20", Secs = 1234, Shows = 2 }, 4000);
            Assert.AreEqual("30.9", OfficeText.WidthText(l.Width));
            Assert.AreEqual("21 min, 2 shows", l.Value);
            Assert.AreEqual("1 min, 1 show", OfficeText.DayOf(new OfficeDay { D = "2026-11-20", Secs = 60, Shows = 1 }, 60).Value);
        }

        [Test]
        public void AllText_IsLowercase()
        {
            foreach (var p in typeof(OfficeText).GetFields())
            {
                object v = p.GetValue(null);
                var s = v as string;
                if (s != null) Assert.AreEqual(s.ToLowerInvariant(), s, p.Name);
                var arr = v as string[];
                if (arr != null) foreach (string x in arr) Assert.AreEqual(x.ToLowerInvariant(), x, p.Name);
            }
        }

        [Test]
        public void PreviewLines_SayWhatWouldBeLost()
        {
            SaveBlob here = SaveBlob.Defaults("2026-11-20");
            here.Fans.Lifetime = 12400; here.Fans.Balance = 3000;
            here.Venues.Unlocked.Add("records");
            here.Shows.Add(new ShowRecord { D = "2026-11-19", Mode = "remix", N = 10, First = 9 });
            here.Streak.Last = "2026-11-19";
            SaveBlob there = SaveBlob.Defaults("2026-11-20");
            there.Fans.Lifetime = 50;
            ImportPreview p = SaveTransfer.Check(SaveJson.ToText(there), here, "2026-11-20");
            Assert.IsTrue(p.Ok, p.Error);
            List<string> lines = OfficeText.PreviewLines(p, 8);
            Assert.AreEqual("this copy: 50 total fans, 1 of 8 venues, 0 shows, 0 journal stickers.", lines[0]);
            Assert.AreEqual("here now: 12,400 total fans, 2 of 8 venues, 1 show, 0 journal stickers, last played thu, nov 19.", lines[1]);
            Assert.IsTrue(lines.Any(x => x.StartsWith(OfficeText.HeadsUp + "total fans would go from 12400", StringComparison.Ordinal)), string.Join("\n", lines));
            Assert.IsTrue(lines.Any(x => x.Contains("venues would lock again")));
            Assert.IsTrue(lines.Any(x => x.Contains("show history")));
        }

        [Test]
        public void AfterImport_Messages()
        {
            Assert.AreEqual(OfficeText.BackupFailed, OfficeText.AfterImport(false, BackupOutcome.Failed, false));
            Assert.AreEqual(OfficeText.Loaded, OfficeText.AfterImport(true, BackupOutcome.Kept, true));
            Assert.AreEqual(OfficeText.LoadedNothingToKeep, OfficeText.AfterImport(true, BackupOutcome.NothingToKeep, true));
            StringAssert.EndsWith(OfficeText.NotSaving, OfficeText.AfterImport(true, BackupOutcome.Kept, false));
            Assert.AreEqual("world-tour-save-2026-11-20.json", OfficeText.FileName("2026-11-20"));
        }
    }
}
