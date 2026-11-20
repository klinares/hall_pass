using System;
using NUnit.Framework;
using WorldTour.Game.Motion;
using WorldTour.Game.Ui;

namespace WorldTour.Game.EditTests
{
    public class UiEasingTests
    {
        [Test]
        public void EndpointsAreExact()
        {
            foreach (CubicBezier e in new[] { Ease.Spring, Ease.Out, Ease.CssEase, Ease.CssEaseIn, Ease.CssEaseOut, Ease.Linear })
            {
                Assert.AreEqual(0f, e.Evaluate(0f));
                Assert.AreEqual(1f, e.Evaluate(1f));
                Assert.AreEqual(0f, e.Evaluate(-0.5f));
                Assert.AreEqual(1f, e.Evaluate(1.5f));
            }
        }

        [Test]
        public void LinearIsIdentity()
        {
            for (float t = 0f; t <= 1f; t += 0.05f) Assert.AreEqual(t, Ease.Linear.Evaluate(t), 1e-6f);
            Assert.IsTrue(default(CubicBezier).IsLinear);
        }

        [Test]
        public void CssEaseMatchesTheBrowserAtHalfway()
        {
            // cubic-bezier(.25,.1,.25,1) at x = .5 is 0.8024033877 in every browser engine.
            Assert.AreEqual(0.8024034f, Ease.CssEase.Evaluate(0.5f), 1e-4f);
        }

        [Test]
        public void SpringOvershootsThenSettles()
        {
            float max = 0f;
            for (int i = 1; i < 100; i++) max = Math.Max(max, Ease.Spring.Evaluate(i / 100f));
            Assert.Greater(max, 1.05f, "the spring curve should overshoot past 1");
            Assert.Less(max, 1.2f);
            Assert.AreEqual(1f, Ease.Spring.Evaluate(0.999f), 0.01f);
        }

        [Test]
        public void EaseOutIsMonotonic()
        {
            float prev = 0f;
            for (int i = 1; i <= 200; i++)
            {
                float v = Ease.Out.Evaluate(i / 200f);
                Assert.GreaterOrEqual(v, prev - 1e-6f);
                prev = v;
            }
        }
    }

    public class UiMotionPresetTests
    {
        [Test]
        public void EveryMotionIsUnder400ms()
        {
            foreach (MotionPreset p in Presets.All)
            {
                Assert.Greater(p.Duration, 0f, p.Name);
                Assert.Less(p.Duration, Tokens.MaxAnim, p.Name);
            }
        }

        [Test]
        public void DurationsMatchMotionJs()
        {
            Assert.AreEqual(0.34f, Presets.Pop.Duration);
            Assert.AreEqual(0.34f, Presets.Wobble.Duration);
            Assert.AreEqual(0.26f, Presets.Slap.Duration);
            Assert.AreEqual(0.38f, Presets.PeelOn.Duration);
            Assert.AreEqual(0.16f, Presets.Shrink.Duration);
            Assert.AreEqual(0.28f, Presets.SlideIn.Duration);
            Assert.AreEqual(0.38f, Presets.Hop.Duration);
            Assert.AreEqual(0.12f, Presets.ReducedFade.Duration);
        }

        [Test]
        public void PopPeaksAt122AndReturns()
        {
            Pose peak = Presets.Pop.SampleProgress(0.45f);
            Assert.AreEqual(1.22f, peak.Sx, 1e-5f);
            Assert.AreEqual(1.22f, peak.Sy, 1e-5f);
            Pose end = Presets.Pop.Sample(Presets.Pop.Duration);
            Assert.AreEqual(1f, end.Sx, 1e-5f);
            Assert.AreEqual(Channel.Scale, Presets.Pop.Channels);
        }

        [Test]
        public void OvershootExtrapolatesTheLastSegmentLikeWebAnimations()
        {
            // progress 1.1 in the .45..1 segment: 1.22 + (1 - 1.22) * (0.65 / 0.55)
            Pose p = Presets.Pop.SampleProgress(1.1f);
            Assert.AreEqual(1.22f + (1f - 1.22f) * (0.65f / 0.55f), p.Sx, 1e-5f);
            Assert.Less(p.Sx, 1f);
        }

        [Test]
        public void WobbleKeyframesAreEvenlySpacedAndNeverTouchColour()
        {
            Pose[] k = Presets.Wobble.Keys;
            Assert.AreEqual(5, k.Length);
            for (int i = 0; i < k.Length; i++) Assert.AreEqual(i * 0.25f, k[i].Offset, 1e-6f);
            Assert.AreEqual(-7f, Presets.Wobble.SampleProgress(0.25f).X, 1e-5f);
            Assert.AreEqual(6f, Presets.Wobble.SampleProgress(0.5f).X, 1e-5f);
            Assert.AreEqual(Channel.None, Presets.Wobble.Channels & Channel.Alpha, "wobble must not fade or tint");
        }

        [Test]
        public void SlideInComesFromTheRightAndFadesIn()
        {
            Pose start = Presets.SlideIn.Sample(0f);
            Assert.AreEqual(28f, start.X);
            Assert.AreEqual(0f, start.Alpha);
            Pose end = Presets.SlideIn.Sample(1f);
            Assert.AreEqual(0f, end.X, 1e-5f);
            Assert.AreEqual(1f, end.Alpha, 1e-5f);
        }

        [Test]
        public void ReducedFadeOnlyFades()
        {
            Assert.AreEqual(Channel.Alpha, Presets.ReducedFade.Channels);
            Assert.AreEqual(0.6f, Presets.ReducedFade.Sample(0f).Alpha, 1e-6f);
            Assert.AreEqual(0.8f, Presets.ReducedFade.Sample(0.06f).Alpha, 1e-5f);
        }

        [Test]
        public void PressAndReleaseMeet()
        {
            Pose pressed = Presets.Press.Sample(1f);
            Pose releaseStart = Presets.Release.Sample(0f);
            Assert.AreEqual(pressed.X, releaseStart.X);
            Assert.AreEqual(pressed.Y, releaseStart.Y);
            Assert.AreEqual(pressed.Sx, releaseStart.Sx);
            Assert.AreEqual(pressed.Sy, releaseStart.Sy);
            Assert.Less(Presets.Press.Duration + Presets.Release.Duration, Tokens.MaxAnim);
        }

        [Test]
        public void BadKeyframesAreRefused()
        {
            Assert.Throws<ArgumentException>(() => new MotionPreset("one", 0.1f, Ease.Linear, Channel.Scale, Pose.At(0f)));
            Assert.Throws<ArgumentException>(() => new MotionPreset("back", 0.1f, Ease.Linear, Channel.Scale, Pose.At(0.5f), Pose.At(0.2f)));
        }
    }
}
