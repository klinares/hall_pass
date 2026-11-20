using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Core.Tests
{
    /// <summary>
    /// Checks the C# generators against problems made by the web game's own generators (all seven, in
    /// world_tour/math/gen/) (world_tour_unity/Tools/gen_vectors.mjs writes problem_vectors.json next to this file).
    /// The same (skill, level, seed) must give the identical problem: every field and every string.
    /// Problems are compared as sha256 of canonical JSON (see Problem.ToCanonicalJson); a sample is stored
    /// in full so a failure can be diffed. Regenerate with: node world_tour_unity/Tools/gen_vectors.mjs
    /// To see one JS problem: node world_tour_unity/Tools/gen_vectors.mjs --one dec 2 1234
    /// </summary>
    public class ProblemVectorTests
    {
        [Serializable] public class Vectors { public int version; public Group[] groups; public Sample[] samples; }
        [Serializable] public class Group { public string skill; public int level; public long[] seeds; public string[] hashes; public long allFirst; public long allStep; public int allCount; public string allDigest; }
        [Serializable] public class Sample { public string skill; public int level; public long seed; public string json; }

        private static Vectors _vectors;

        private static Vectors Load()
        {
            if (_vectors != null) return _vectors;
            string path = Path.Combine(Application.dataPath, "Tests", "EditMode", "problem_vectors.json");
            if (!File.Exists(path)) throw new FileNotFoundException("missing problem vectors; run node Tools/gen_vectors.mjs", path);
            var v = JsonUtility.FromJson<Vectors>(File.ReadAllText(path));
            if (v == null || v.version != 1 || v.groups == null || v.groups.Length == 0 || v.samples == null || v.samples.Length == 0)
                throw new InvalidDataException("problem_vectors.json is empty or malformed");
            _vectors = v;
            return v;
        }

        public static string Sha256Hex(string ascii)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.ASCII.GetBytes(ascii));
                var sb = new StringBuilder(64);
                foreach (byte b in h) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        /// <summary>
        /// The problem as the web game would have it: the C# problem with the web wording put back in the intended
        /// differences' changed steps only (IntendedDifferences.cs, QUESTIONS 8 and 9), so everything else is still
        /// compared exactly.
        /// </summary>
        private static string Canon(string skill, int level, long seed) => IntendedDifferences.JsCanon(skill, level, seed, out string _);

        /// <summary>
        /// The hash to compare with the JS. For the few tour budget seeds whose web story shows more than $150, the Unity
        /// story is a different one (QUESTIONS 9.4): the JS hash is used, after checking the C# story keeps the bound.
        /// </summary>
        private static string JsHash(string skill, int level, long seed)
        {
            if (skill == OrderOps.Skill && level == 3 && IntendedDifferences.OpsStoryJsHashes.TryGetValue(seed, out string js))
            {
                Problem p = Generators.Generate(skill, level, seed);
                Assert.IsTrue(OrderOps.StoryDollarsOk(p.Prompt), "seed " + seed + ": the Unity story keeps the $150 bound: " + p.Prompt);
                Assert.AreNotEqual(js, Sha256Hex(p.ToCanonicalJson()), "seed " + seed + " is listed as an intended difference but matches the JS");
                return js;
            }
            return Sha256Hex(Canon(skill, level, seed));
        }

        private static IEnumerable<TestCaseData> GroupCases() =>
            Load().groups.Select((g, i) => new TestCaseData(i).SetName("{m} " + g.skill + " level " + g.level));

        private static IEnumerable<TestCaseData> SampleCases() =>
            Load().samples.Select((s, i) => new TestCaseData(i).SetName("{m} " + (i + 1).ToString("00", CultureInfo.InvariantCulture) + " " + s.skill + " level " + s.level + " seed " + s.seed));

        [Test]
        public void VectorFileCoversEverySkillAndLevel()
        {
            Vectors v = Load();
            foreach (string skill in Generators.Skills)
                for (int level = 1; level <= 3; level++)
                    Assert.AreEqual(1, v.groups.Count(g => g.skill == skill && g.level == level), skill + " level " + level);
            Assert.AreEqual(Generators.Skills.Count * 3, v.groups.Length);
        }

        /// <summary>Per-problem hashes: 200 seeds per skill and level (150 from the volume set, 50 edge and spread seeds).</summary>
        [Test, TestCaseSource(nameof(GroupCases))]
        public void ProblemsMatchJs(int i)
        {
            Group g = Load().groups[i];
            Assert.AreEqual(g.seeds.Length, g.hashes.Length);
            Assert.That(g.seeds.Length, Is.GreaterThanOrEqualTo(200));
            var bad = new List<string>();
            for (int k = 0; k < g.seeds.Length; k++)
            {
                string json = Canon(g.skill, g.level, g.seeds[k]);
                if (JsHash(g.skill, g.level, g.seeds[k]) != g.hashes[k])
                    bad.Add("seed " + g.seeds[k] + (bad.Count == 0 ? ": C# gives " + json : ""));
            }
            Assert.IsEmpty(bad, bad.Count + " of " + g.seeds.Length + " " + g.skill + " level " + g.level +
                " problems differ from the JS (compare with node Tools/gen_vectors.mjs --one " + g.skill + " " + g.level + " <seed>):\n" +
                string.Join("\n", bad.Take(5)));
            TestContext.Out.WriteLine(g.skill + " level " + g.level + ": " + g.seeds.Length + " problems match the JS hash for hash");
        }

        /// <summary>The whole 3,000-seed volume set (helpers.mjs seeds(level)) as one digest of per-problem hashes.</summary>
        [Test, TestCaseSource(nameof(GroupCases))]
        public void VolumeSetMatchesJs(int i)
        {
            Group g = Load().groups[i];
            Assert.AreEqual(GeneratorContract.Runs, g.allCount);
            Assert.AreEqual(1000L * g.level + 1, g.allFirst, "seeds(level) starts at 1000 * level + 1");
            Assert.AreEqual(7919L, g.allStep);
            var hashes = new List<string>(g.allCount);
            int k = 0;
            foreach (long seed in GeneratorContract.Seeds(g.level, g.allCount))
            {
                Assert.AreEqual(g.allFirst + k * g.allStep, seed, "same seed list as the JS");
                hashes.Add(JsHash(g.skill, g.level, seed));
                k++;
            }
            Assert.AreEqual(g.allDigest, Sha256Hex(string.Join("\n", hashes)),
                g.skill + " level " + g.level + ": some of the 3,000 volume-set problems differ from the JS");
            TestContext.Out.WriteLine(g.skill + " level " + g.level + ": " + g.allCount + " volume-set problems match the JS digest");
        }

        /// <summary>Full problems, compared as text so a difference shows where it is.</summary>
        [Test, TestCaseSource(nameof(SampleCases))]
        public void SampleMatchesJs(int i)
        {
            Sample s = Load().samples[i];
            string got = Canon(s.skill, s.level, s.seed);
            if (got == s.json) return;
            int at = 0;
            while (at < got.Length && at < s.json.Length && got[at] == s.json[at]) at++;
            int from = Math.Max(0, at - 60);
            Assert.Fail("differs from the JS at character " + at + "\n  JS: ..." + Slice(s.json, from) + "\n  C#: ..." + Slice(got, from));
        }

        private static string Slice(string s, int from) => s.Substring(from, Math.Min(160, s.Length - from));
    }
}
