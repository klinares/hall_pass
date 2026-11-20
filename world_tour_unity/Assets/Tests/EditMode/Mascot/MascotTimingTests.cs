using System.Linq;
using NUnit.Framework;
using WorldTour.Game.Mascot;

namespace WorldTour.Mascot.Tests
{
    public class MascotTimingTests
    {
        const double Eps = 1e-6;

        [Test]
        public void CssTimingFunctions()
        {
            Assert.AreEqual(.5, CubicBezier.EaseInOut.Ease(.5), 1e-6);
            Assert.AreEqual(0.8024033877399112, CubicBezier.Ease_.Ease(.5), 1e-4);     // the CSS 'ease' value at 50%
            Assert.AreEqual(0, CubicBezier.Spring.Ease(0)); Assert.AreEqual(1, CubicBezier.Spring.Ease(1));
            Assert.Greater(Enumerable.Range(1, 99).Max(i => CubicBezier.Spring.Ease(i / 100.0)), 1.0, "--spring overshoots");
        }

        [Test]
        public void ReactionsAreUnder400msAndEndAtRest()
        {
            foreach (var m in new[] { DogMotion.Hop, DogMotion.Wobble })
                foreach (var reduced in new[] { false, true })
                {
                    double d = DogTiming.Duration(m, reduced);
                    Assert.Less(d, .4, m + " " + reduced);
                    var start = DogTiming.Reaction(m, 0, reduced);
                    var end = DogTiming.Reaction(m, d, reduced);
                    Assert.AreEqual(0, end.X, Eps); Assert.AreEqual(0, end.Y, Eps); Assert.AreEqual(1, end.Alpha, Eps); Assert.AreEqual(1, end.ScaleY, Eps);
                    if (!reduced) { Assert.AreEqual(0, start.Y, Eps); Assert.AreEqual(0, start.X, Eps); }
                }
            Assert.AreEqual(0, DogTiming.Duration(DogMotion.None, false));
        }

        [Test]
        public void HopAndWobbleFollowMotionJs()
        {
            var ys = Enumerable.Range(0, 381).Select(i => DogTiming.Reaction(DogMotion.Hop, i / 1000.0, false)).ToArray();
            double top = ys.Min(o => o.Y);
            Assert.Less(top, -15.5); Assert.Greater(top, -22);      // the -16 keyframe falls between 1 ms samples
            Assert.AreEqual(1.04, ys.Max(o => o.ScaleX), .02);
            Assert.AreEqual(.96, ys.Min(o => o.ScaleY), .02);
            // wobble: the eased progress hits .25 where x = -7 and rotation = -2
            var w = Enumerable.Range(0, 341).Select(i => DogTiming.Reaction(DogMotion.Wobble, i / 1000.0, false)).ToArray();
            Assert.AreEqual(-7, w.Min(o => o.X), .3); Assert.AreEqual(6, w.Max(o => o.X), .3);      // 1 ms samples move x up to about .2 px
            Assert.AreEqual(-2, w.Min(o => o.Rotation), .1);
            var r = DogTiming.Reaction(DogMotion.Hop, 0, true);
            Assert.AreEqual(.6, r.Alpha, Eps); Assert.AreEqual(0, r.Y, Eps);
            Assert.AreEqual(0.8, DogTiming.Reaction(DogMotion.Wobble, .06, true).Alpha, 1e-6);
        }

        [Test]
        public void KeyframesExtendTheEndSegments()
        {
            var at = new double[] { 0, .4, 1 }; var v = new double[] { 0, -16, 0 };
            Assert.AreEqual(-16, DogTiming.Keyframes(at, v, .4), Eps);
            Assert.AreEqual(-8, DogTiming.Keyframes(at, v, .7), Eps);
            Assert.AreEqual(16.0 / 6, DogTiming.Keyframes(at, v, 1.1), 1e-9);      // past the end: last segment continued
            Assert.AreEqual(4, DogTiming.Keyframes(at, v, -.1), 1e-9);
        }

        [Test]
        public void IdleLoopsMatchScreensCss()
        {
            Assert.AreEqual(0, DogTiming.TailDegrees(0, false), Eps);
            Assert.AreEqual(14, DogTiming.TailDegrees(.55, false), Eps);
            Assert.AreEqual(7, DogTiming.TailDegrees(.275, false), 1e-6);
            Assert.AreEqual(0, DogTiming.TailDegrees(1.1, false), 1e-6);
            Assert.AreEqual(0, DogTiming.LidScale(1, false)); Assert.AreEqual(0, DogTiming.LidScale(3.8, false));
            Assert.AreEqual(1, DogTiming.LidScale(.95 * 4.2, false), 1e-6);
            Assert.AreEqual(0, DogTiming.LidScale(4.2, false), 1e-6);
            var z0 = DogTiming.Zzz(1.2, 0, false);
            Assert.AreEqual(3, z0.X, Eps); Assert.AreEqual(-5, z0.Y, Eps); Assert.AreEqual(.55, z0.Alpha, Eps);
            var z1Early = DogTiming.Zzz(.3, 1, false);
            Assert.AreEqual(0, z1Early.X); Assert.AreEqual(1, z1Early.Alpha);
            var z2 = DogTiming.Zzz(.8 + 1.2, 2, false);
            Assert.AreEqual(3, z2.X, Eps);
            Assert.AreEqual(0, DogTiming.BobY(0, false), Eps); Assert.AreEqual(-3, DogTiming.BobY(1.2, false), Eps);
        }

        [Test]
        public void ReduceMotionStopsEveryLoop()
        {
            foreach (var t in new[] { .3, .55, 3.99, 1.2, 7.7 })
            {
                Assert.AreEqual(0, DogTiming.TailDegrees(t, true));
                Assert.AreEqual(0, DogTiming.LidScale(t, true));
                Assert.AreEqual(0, DogTiming.BobY(t, true));
                for (int i = 0; i < 3; i++) { var z = DogTiming.Zzz(t, i, true); Assert.AreEqual(0, z.X); Assert.AreEqual(1, z.Alpha); }
            }
        }

        [Test]
        public void PosesAndOutfits()
        {
            foreach (DogPose p in System.Enum.GetValues(typeof(DogPose)))
            {
                Assert.IsTrue(DogPoses.TryParse(DogPoses.Id(p), out var back)); Assert.AreEqual(p, back);
            }
            Assert.IsFalse(DogPoses.TryParse("dance", out _));
            Assert.AreEqual("beanie", DogPoses.StackOutfit(DogPose.Think, "beanie"));
            Assert.AreEqual("none", DogPoses.StackOutfit(DogPose.Sleepy, "beanie"));
            Assert.AreEqual("none", DogPoses.StackOutfit(DogPose.Idle, ""));
            Assert.AreEqual("none", DogPoses.StackOutfit(DogPose.Idle, "tophat"));
            Assert.AreEqual(DogPose.Sleepy, DogPoses.HomePose(3));
            Assert.AreEqual(DogPose.Idle, DogPoses.HomePose(2));
            Assert.AreEqual(DogPose.Idle, DogPoses.HomePose(null));
            Assert.AreEqual("", DogPoses.OutfitOf(null));
        }
    }
}
