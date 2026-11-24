using System;
using NUnit.Framework;
using WorldTour.World.Audio;

namespace WorldTour.World.Tests
{
    /// <summary>Phase 8.3: the synthesised world sounds (birds, footsteps, applause, murmur) are made, sound sane, are repeatable, and the grounds differ.</summary>
    public class WorldAudioTests
    {
        static float Peak(float[] d) { float p = 0; foreach (float x in d) p = Math.Max(p, Math.Abs(x)); return p; }
        static float Rms(float[] d, int from, int to) { double s = 0; for (int i = from; i < to; i++) s += d[i] * d[i]; return (float)Math.Sqrt(s / Math.Max(1, to - from)); }
        static int ZeroCrossings(float[] d) { int z = 0; for (int i = 1; i < d.Length; i++) if ((d[i - 1] < 0) != (d[i] < 0)) z++; return z; }
        static bool Finite(float[] d) { foreach (float x in d) if (float.IsNaN(x) || float.IsInfinity(x)) return false; return true; }

        [Test]
        public void EverySound_IsFinite_NotClipped_AndNotSilent()
        {
            var all = new System.Collections.Generic.List<float[]> { WorldSynth.Birds(), WorldSynth.Applause(), WorldSynth.Murmur() };
            foreach (Surface s in Enum.GetValues(typeof(Surface))) for (int v = 0; v < 4; v++) all.Add(WorldSynth.Step(s, v));
            foreach (var d in all)
            {
                Assert.IsTrue(Finite(d));
                Assert.LessOrEqual(Peak(d), 0.9f);
                Assert.Greater(Peak(d), 0.5f);
            }
        }

        [Test]
        public void Lengths_AreAsPlanned()
        {
            Assert.AreEqual(12 * WorldSynth.SampleRate, WorldSynth.Birds().Length);
            Assert.AreEqual(14 * WorldSynth.SampleRate, WorldSynth.Murmur().Length);
            Assert.AreEqual(0.25f, WorldSynth.Step(Surface.Stone, 0).Length / (float)WorldSynth.SampleRate, 0.01f);
            Assert.AreEqual(2.4f, WorldSynth.Applause().Length / (float)WorldSynth.SampleRate, 0.01f);
        }

        [Test]
        public void SameCall_GivesTheSameSound()
        {
            CollectionAssert.AreEqual(WorldSynth.Applause(), WorldSynth.Applause());
            CollectionAssert.AreEqual(WorldSynth.Step(Surface.Wood, 2), WorldSynth.Step(Surface.Wood, 2));
        }

        [Test]
        public void FourVariationsOfAStep_Differ()
        {
            var a = WorldSynth.Step(Surface.Stone, 0);
            var b = WorldSynth.Step(Surface.Stone, 1);
            CollectionAssert.AreNotEqual(a, b);
        }

        [Test]
        public void BrightGrounds_AreBrighterThanSoftOnes()
        {
            int hard = ZeroCrossings(WorldSynth.Step(Surface.Hard, 0));
            int stone = ZeroCrossings(WorldSynth.Step(Surface.Stone, 0));
            int carpet = ZeroCrossings(WorldSynth.Step(Surface.Carpet, 0));
            Assert.Greater(hard, carpet, "a tap on the arena floor is brighter than a step on carpet");
            Assert.Greater(stone, carpet);
        }

        [Test]
        public void AStep_DiesAwayToSilenceByTheEnd()
        {
            var d = WorldSynth.Step(Surface.Stone, 0);
            Assert.Less(Rms(d, d.Length - 2000, d.Length), 0.01f);
        }

        [Test]
        public void TheLoops_JoinWithoutAClick()
        {
            // the step from the last sample to the first is no bigger than the largest step between neighbours elsewhere
            foreach (var d in new[] { WorldSynth.Murmur(), WorldSynth.Birds() })
            {
                float maxStep = 0;
                for (int i = 1; i < d.Length; i++) maxStep = Math.Max(maxStep, Math.Abs(d[i] - d[i - 1]));
                Assert.LessOrEqual(Math.Abs(d[0] - d[d.Length - 1]), maxStep + 1e-6f);
            }
        }

        [Test]
        public void Birds_AreMostlyQuietBetweenCalls_AndMurmurIsSteady()
        {
            var birds = WorldSynth.Birds();
            int quiet = 0, windows = birds.Length / 2205;
            for (int w = 0; w < windows; w++) if (Rms(birds, w * 2205, (w + 1) * 2205) < 0.02f) quiet++;
            Assert.Greater(quiet, windows / 3, "chirps with gaps, not a constant tone");
            Assert.Less(quiet, windows - 3, "but there are chirps");
            var m = WorldSynth.Murmur();
            float lo = 9, hi = 0;
            for (int w = 0; w < m.Length / 22050; w++) { float r = Rms(m, w * 22050, (w + 1) * 22050); lo = Math.Min(lo, r); hi = Math.Max(hi, r); }
            Assert.Greater(lo, 0.03f, "a murmur never drops to silence");
            Assert.Less(hi / lo, 3f, "slow swells only");
        }
    }
}
