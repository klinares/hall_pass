using System;
using NUnit.Framework;
using WorldTour.Game.Audio;
using WorldTour.World.Audio;

namespace WorldTour.World.Tests
{
    /// <summary>Phase 8.3 and 10A: the synthesised world sounds (bird chirps, footsteps, applause) are made, sound sane, are repeatable, the grounds differ, and the plan keeps the mix quiet and static-free.</summary>
    public class WorldAudioTests
    {
        static float Peak(float[] d) { float p = 0; foreach (float x in d) p = Math.Max(p, Math.Abs(x)); return p; }
        static float Rms(float[] d, int from, int to) { double s = 0; for (int i = from; i < to; i++) s += d[i] * d[i]; return (float)Math.Sqrt(s / Math.Max(1, to - from)); }
        static int ZeroCrossings(float[] d) { int z = 0; for (int i = 1; i < d.Length; i++) if ((d[i - 1] < 0) != (d[i] < 0)) z++; return z; }
        static bool Finite(float[] d) { foreach (float x in d) if (float.IsNaN(x) || float.IsInfinity(x)) return false; return true; }
        static double Db(double lin) { return 20 * Math.Log10(Math.Max(lin, 1e-9)); }

        static readonly string[] Places = { "coffee", "records", "rec", "rooftop", "club", "theater", "festival", "arena" };

        [Test]
        public void EverySound_IsFinite_NotClipped_AndNotSilent()
        {
            var all = new System.Collections.Generic.List<float[]> { WorldSynth.Applause() };
            for (int v = 0; v < WorldSynth.ChirpCount; v++) all.Add(WorldSynth.BirdChirp(v));
            foreach (Surface s in Enum.GetValues(typeof(Surface))) for (int v = 0; v < 4; v++) all.Add(WorldSynth.Step(s, v));
            foreach (var d in all)
            {
                Assert.IsTrue(Finite(d));
                Assert.LessOrEqual(Peak(d), 1.0001f);
                Assert.Greater(Peak(d), 0.5f);
            }
        }

        [Test]
        public void Lengths_AreAsPlanned()
        {
            Assert.AreEqual(0.25f, WorldSynth.Step(Surface.Stone, 0).Length / (float)WorldSynth.SampleRate, 0.01f);
            Assert.AreEqual(2.4f, WorldSynth.Applause().Length / (float)WorldSynth.SampleRate, 0.01f);
            for (int v = 0; v < WorldSynth.ChirpCount; v++) Assert.LessOrEqual(WorldSynth.BirdChirp(v).Length / (float)WorldSynth.SampleRate, 0.75f, "a chirp is short");
        }

        [Test]
        public void SameCall_GivesTheSameSound()
        {
            CollectionAssert.AreEqual(WorldSynth.Applause(), WorldSynth.Applause());
            CollectionAssert.AreEqual(WorldSynth.Step(Surface.Wood, 2), WorldSynth.Step(Surface.Wood, 2));
            CollectionAssert.AreEqual(WorldSynth.BirdChirp(1), WorldSynth.BirdChirp(1));
        }

        [Test]
        public void FourVariationsOfAStep_Differ()
        {
            var a = WorldSynth.Step(Surface.Stone, 0);
            var b = WorldSynth.Step(Surface.Stone, 1);
            CollectionAssert.AreNotEqual(a, b);
        }

        [Test]
        public void TheChirps_AreDifferentFromEachOther()
        {
            for (int a = 0; a < WorldSynth.ChirpCount; a++)
                for (int b = a + 1; b < WorldSynth.ChirpCount; b++)
                    CollectionAssert.AreNotEqual(WorldSynth.BirdChirp(a), WorldSynth.BirdChirp(b));
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
        public void AChirp_IsASineWithSoftEdges_NotNoise()
        {
            // a tone, not hiss: it starts and ends at silence
            for (int v = 0; v < WorldSynth.ChirpCount; v++)
            {
                var d = WorldSynth.BirdChirp(v);
                Assert.Less(Math.Abs(d[0]), 0.01f);
                Assert.Less(Math.Abs(d[d.Length - 1]), 0.01f);
                Assert.Less(Rms(d, d.Length - 2205, d.Length), 0.01f, "the chirp has died away before the clip ends");
            }
        }

        [Test]
        public void NoBroadbandNoiseBed_IsLeft()
        {
            // the loops that were noise (city hum, hall murmur, crowd murmur) are gone: the only bed there is, is birds
            Assert.AreEqual(2, Enum.GetValues(typeof(Bed)).Length, "None and Birds only");
            foreach (string id in Places) Assert.IsTrue(AudioPlan.BedFor(id) == Bed.None || AudioPlan.BedFor(id) == Bed.Birds);
        }

        [Test]
        public void Birds_OnlyInDaytimeOutdoorPlaces()
        {
            CollectionAssert.AreEquivalent(new[] { "coffee", "festival" }, AudioPlan.BirdPlaces);
            foreach (string id in Places) Assert.AreEqual(Array.IndexOf(AudioPlan.BirdPlaces, id) >= 0, AudioPlan.BedFor(id) == Bed.Birds, id);
            foreach (string quiet in new[] { "rooftop", "club", "theater", "arena", "records", "rec" }) Assert.AreEqual(Bed.None, AudioPlan.BedFor(quiet), quiet);
        }

        [Test]
        public void ABird_CallsEveryTwentyToFortySeconds_AndIsAboutAQuarterAsLoudAsBefore()
        {
            Assert.AreEqual(20f, AudioPlan.BirdGap(0f), 1e-4f);
            Assert.AreEqual(40f, AudioPlan.BirdGap(1f), 1e-4f);
            Assert.AreEqual(20f, AudioPlan.BirdGap(-3f), 1e-4f);
            Assert.AreEqual(40f, AudioPlan.BirdGap(9f), 1e-4f);
            // the old bed peaked at 0.3 x 0.8 = 0.24; the chirp at the default 40 percent ambience peaks at a quarter of that
            Assert.AreEqual(0.24f * 0.25f, AudioPlan.BirdPeak * Sfx.DefaultAmbience, 0.005f);
        }

        [Test]
        public void EveryPlannedLevel_IsUnderMinus12_ExceptApplause_AndTheMixHasRoom()
        {
            double ceiling = Math.Pow(10, -12 / 20.0);   // 0.251
            foreach (Surface s in Enum.GetValues(typeof(Surface)))
                Assert.Less(AudioPlan.StepVolume(s) * Peak(WorldSynth.Step(s, 0)), Math.Pow(10, -20 / 20.0) + 1e-4, "step on " + s);
            Assert.Less(AudioPlan.BirdPeak, ceiling);
            Assert.Less(AudioPlan.CueCard, ceiling);
            Assert.Less(AudioPlan.CueWhoosh, ceiling);
            Assert.LessOrEqual(AudioPlan.CueJingle, ceiling);
            Assert.Less(Sfx.GuitarPeak, ceiling + 1e-4);
            Assert.Less(Sfx.EffectPeak, ceiling);
            Assert.Less(Sfx.BeatPeak, ceiling);
            // the loudest the sources can be together (a cheer, a jingle, a step, a guitar phrase, a bird, with the master at its headroom) is still under full scale
            double worst = (AudioPlan.CheerPeak(1f) + AudioPlan.CueJingle + AudioPlan.StepVolume(Surface.Wood) * 0.85f + Sfx.GuitarPeak + AudioPlan.BirdPeak) * WorldAudio.Headroom;
            Assert.Less(worst, 1.0, "even all at once, the sources never reach full scale");
        }

        [Test]
        public void Applause_IsTheLoudestThing_ButNotPastMinus6()
        {
            Assert.Less(Db(AudioPlan.CheerPeak(1f)), -6.0);
            Assert.Greater(AudioPlan.CheerPeak(1f), AudioPlan.CheerPeak(0f));
        }

        [Test]
        public void GuitarPhrases_AreFiveDifferentOnes_QuietAndShort()
        {
            var set = new System.Collections.Generic.List<float[]>();
            for (int p = 0; p < Synth.PhraseCount; p++)
            {
                var d = Synth.Phrase(false, p, new Random(300 + p));
                Assert.IsTrue(Finite(d));
                Assert.AreEqual(1f, Peak(d), 1e-3f, "scaled to 1 so one number sets the loudness");
                Assert.LessOrEqual(d.Length / (float)Synth.Rate, 1.6f, "short enough to be over before the next question");
                Assert.Less(Math.Abs(d[d.Length - 1]), 0.01f, "fades out");
                set.Add(d);
            }
            Assert.GreaterOrEqual(Synth.PhraseCount, 4);
            for (int a = 0; a < set.Count; a++) for (int b = a + 1; b < set.Count; b++) CollectionAssert.AreNotEqual(set[a], set[b]);
            Assert.Less(Sfx.GuitarPeak * Sfx.DefaultAmbience, Math.Pow(10, -18 / 20.0), "at the default ambience the guitar peaks under -18 dBFS");
        }

        [Test]
        public void GuitarPlanner_NeverRepeatsAPhrase_AndLeavesSilenceBetween()
        {
            var g = new GuitarPlanner();
            var rng = new Random(5);
            int last = -1, played = 0;
            double t = 0;
            var seen = new bool[Synth.PhraseCount];
            for (int i = 0; i < 4000; i++)
            {
                t += 0.5 + rng.NextDouble() * 4;            // answers come every half second to four and a half
                int p = g.Request(t, rng);
                if (p < 0) continue;
                Assert.AreNotEqual(last, p, "no immediate repeat");
                Assert.GreaterOrEqual(p, 0);
                Assert.Less(p, Synth.PhraseCount);
                last = p; seen[p] = true; played++;
                Assert.AreEqual(-1, g.Request(t + GuitarPlanner.Gap - 0.01, rng), "silent inside the gap");
            }
            Assert.Greater(played, 100);
            foreach (bool s in seen) Assert.IsTrue(s, "every phrase gets played");
        }
    }
}
