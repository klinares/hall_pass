using System;
using NUnit.Framework;
using WorldTour.Game.Audio;

namespace WorldTour.Game.EditTests
{
    public class UiSynthTests
    {
        const int Sr = Synth.Rate;

        static float Peak(float[] b) { float m = 0f; foreach (float x in b) m = Math.Max(m, Math.Abs(x)); return m; }

        static double Rms(float[] b, int from, int to)
        {
            double s = 0; int n = 0;
            for (int i = from; i < to && i < b.Length; i++) { s += b[i] * (double)b[i]; n++; }
            return n == 0 ? 0 : Math.Sqrt(s / n);
        }

        [Test]
        public void EveryFixedSoundRendersAudibleAndInRange()
        {
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            {
                float[] b = Sfx.Render(id, Sr);
                Assert.Greater(b.Length, 0, id.ToString());
                float p = Peak(b);
                Assert.Greater(p, 0.005f, id + " should be audible");
                Assert.LessOrEqual(p, 1f, id + " must not clip past 1");
                foreach (float x in b) Assert.IsFalse(float.IsNaN(x) || float.IsInfinity(x), id + " has a bad sample");
            }
        }

        [Test]
        public void LengthsFollowSoundJs()
        {
            Assert.AreEqual((int)Math.Ceiling((0.04 + 0.02) * Sr), Synth.Tap(Sr).Length, 1);
            Assert.AreEqual((int)Math.Ceiling((0.11 + 0.02) * Sr), Synth.Pop(Sr).Length, 1);
            Assert.AreEqual((int)Math.Floor(Sr * (1.6 + 0.028 * 6)), Synth.Pluck(Synth.ChordWin, new Random(1), Sr).Length);
            Assert.AreEqual((int)Math.Ceiling((2.6 + 0.5 * 1.4) * Sr), Synth.Crowd(0.5f, new Random(1), Sr).Length, 1);
        }

        [Test]
        public void SameSeedSameSound()
        {
            CollectionAssert.AreEqual(Synth.Thud(new Random(7), Sr), Synth.Thud(new Random(7), Sr));
            CollectionAssert.AreEqual(Synth.Strum(Synth.ChordBig, new Random(7), Sr), Synth.Strum(Synth.ChordBig, new Random(7), Sr));
        }

        [Test]
        public void OscillatorStartsSilentAndDecays()
        {
            float[] b = new float[Synth.Samples(Sr, 0.2f)];
            Synth.Osc(b, Sr, 440f, Synth.Wave.Sine, 0.11f, 0.12f, 0f, 0f);
            Assert.AreEqual(0f, b[0]);
            double early = Rms(b, (int)(0.01 * Sr), (int)(0.03 * Sr));
            double late = Rms(b, (int)(0.09 * Sr), (int)(0.11 * Sr));
            Assert.Greater(early, late * 5);
            Assert.AreEqual(0f, b[(int)(0.15 * Sr)], "nothing after the stop at dur + 20 ms");
        }

        [Test]
        public void PitchGlideGoesUpForPop()
        {
            // pop: 420 Hz rising to 980 Hz; count zero crossings in the first and last fifth of the note.
            float[] b = new float[Synth.Samples(Sr, 0.13f)];
            Synth.Osc(b, Sr, 420f, Synth.Wave.Sine, 0.11f, 1f, 0f, 980f);
            int Cross(int from, int to) { int c = 0; for (int i = from + 1; i < to; i++) if ((b[i - 1] < 0) != (b[i] < 0)) c++; return c; }
            int span = (int)(0.022 * Sr);
            Assert.Greater(Cross((int)(0.088 * Sr) - span, (int)(0.088 * Sr)), Cross(0, span) * 1.6);
        }

        [Test]
        public void BeatReturnsTheMeasureLength()
        {
            var q = new BeatNote(0.25, 4);
            Synth.Beat(new[] { q, q, q, q }, out double seconds);
            Assert.AreEqual(1.6, seconds, 1e-9);
            float[] b = Synth.Beat(new[] { new BeatNote(0.5, 2), new BeatNote(0.5, 5) }, out seconds);
            Assert.AreEqual(1.6, seconds, 1e-9);
            Assert.Greater(Peak(b), 0.01f, "an unknown note size still plays (330 Hz, as NOTE_HZ[d] || 330)");
        }

        [Test]
        public void NoteTableMatchesSoundJs()
        {
            Assert.AreEqual(196f, Synth.NoteHz(2));
            Assert.AreEqual(233.1f, Synth.NoteHz(3));
            Assert.AreEqual(293.7f, Synth.NoteHz(4));
            Assert.AreEqual(349.2f, Synth.NoteHz(6));
            Assert.AreEqual(392f, Synth.NoteHz(8));
            Assert.AreEqual(466.2f, Synth.NoteHz(12));
            Assert.AreEqual(0f, Synth.NoteHz(5));
        }

        [Test]
        public void CompressorCurve()
        {
            Assert.AreEqual(-30.0, Synth.CurveDb(-30), 1e-9);
            Assert.AreEqual(-24.0, Synth.CurveDb(-24), 1e-9);
            Assert.AreEqual(0.0 + (1.0 / 12 - 1) * 24 * 24 / 60, Synth.CurveDb(0), 1e-9);
            Assert.Less(Synth.CurveDb(10), 0.0);
            Assert.AreEqual(0.6 * -Synth.CurveDb(0), Synth.MakeupDb, 1e-9);
        }

        [Test]
        public void MasterLimitsToOne()
        {
            float[] b = new float[2000];
            for (int i = 0; i < b.Length; i++) b[i] = (i % 2 == 0) ? 4f : -4f;
            Synth.Master(b, Sr);
            Assert.LessOrEqual(Peak(b), 1f);
        }

        [Test]
        public void FiltersBehave()
        {
            var lp = Biquad.LowPass(Sr, 380f, 1f);
            var hp = Biquad.HighPass(Sr, 1400f, 1f);
            float l = 0f, h = 0f;
            for (int i = 0; i < 5000; i++) { l = lp.Process(1f); h = hp.Process(1f); }
            Assert.AreEqual(1f, l, 1e-3f, "low-pass passes DC");
            Assert.AreEqual(0f, h, 1e-3f, "high-pass blocks DC");

            var bp = Biquad.BandPass(Sr, 1000f, 0.6f);
            double peakOut = 0;
            for (int i = 0; i < Sr / 2; i++)
            {
                float y = bp.Process((float)Math.Sin(2 * Math.PI * 1000 * i / Sr));
                if (i > Sr / 4) peakOut = Math.Max(peakOut, Math.Abs(y));
            }
            Assert.AreEqual(1.0, peakOut, 0.02, "band-pass has unity gain at its centre");
        }
    }
}
