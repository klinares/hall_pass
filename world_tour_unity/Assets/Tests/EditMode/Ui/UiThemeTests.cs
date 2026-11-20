using System;
using System.Globalization;
using System.Threading;
using NUnit.Framework;
using WorldTour.Game.Prefs;
using WorldTour.Game.Ui;

namespace WorldTour.Game.EditTests
{
    public class UiThemeTests
    {
        [Test]
        public void HexParses()
        {
            Assert.AreEqual(new Rgba32(253, 177, 203), Rgba32.Parse("#fdb1cb"));
            Assert.AreEqual(new Rgba32(253, 177, 203), Rgba32.Parse("FDB1CB"));
            Assert.AreEqual(new Rgba32(0, 0, 0, 128), Rgba32.Parse("#00000080"));
            Assert.AreEqual("#2f2fc1", Rgba32.Parse("#2F2FC1").ToHex());
        }

        [Test]
        public void BadHexThrows()
        {
            Assert.Throws<FormatException>(() => Rgba32.Parse(null));
            Assert.Throws<FormatException>(() => Rgba32.Parse("#fff"));
            Assert.Throws<FormatException>(() => Rgba32.Parse("#gggggg"));
        }

        [Test]
        public void TourPinkIsTheBriefPalette()
        {
            Palette p = Palettes.TourPink;
            Assert.AreEqual("#fdb1cb", p[ColorRole.Paper].ToHex());
            Assert.AreEqual("#f98bb0", p[ColorRole.PaperDeep].ToHex());
            Assert.AreEqual("#000000", p[ColorRole.Ink].ToHex());
            Assert.AreEqual("#ffffff", p[ColorRole.Card].ToHex());
            Assert.AreEqual("#a8a9fc", p[ColorRole.Accent].ToHex());
            Assert.AreEqual("#2f2fc1", p[ColorRole.Accent2].ToHex());
        }

        [Test]
        public void PeachNavyIsTokensCss()
        {
            Palette p = Palettes.PeachNavy;
            Assert.AreEqual("#ffcfb5", p[ColorRole.Paper].ToHex());
            Assert.AreEqual("#1c2747", p[ColorRole.Ink].ToHex());
            Assert.AreEqual("#9fb3ea", p[ColorRole.Accent].ToHex());
            Assert.AreEqual("#fffaf5", p[ColorRole.Card].ToHex());
        }

        [Test]
        public void UnknownThemeFallsBackToTourPink()
        {
            Assert.AreSame(Palettes.TourPink, Palettes.Get(null));
            Assert.AreSame(Palettes.TourPink, Palettes.Get("neon"));
            Assert.AreSame(Palettes.PeachNavy, Palettes.Get("peach-navy"));
        }

        [Test]
        public void LowerLowercases()
        {
            Assert.AreEqual("natalia's world tour", Lower.Of("Natalia's World TOUR"));
            Assert.AreEqual("", Lower.Of(null));
            Assert.AreEqual("", Lower.Of(""));
            Assert.AreEqual("12,345 fans", Lower.Of("12,345 Fans"));
        }

        [Test]
        public void LowerIgnoresTheMachineCulture()
        {
            CultureInfo saved = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
                Assert.AreEqual("title", Lower.Of("TITLE"));
            }
            finally { Thread.CurrentThread.CurrentCulture = saved; }
        }
    }

    public class UiClickGuardTests
    {
        [Test]
        public void SecondClickInsideTheWindowIsDropped()
        {
            var g = new ClickGuard(0.3f);
            Assert.IsTrue(g.TryAccept(10f));
            Assert.IsFalse(g.TryAccept(10.1f));
            Assert.IsFalse(g.TryAccept(10.29f));
            Assert.IsTrue(g.TryAccept(10.3f));
        }

        [Test]
        public void DroppedClicksAreNotQueuedAndDoNotExtendTheWindow()
        {
            var g = new ClickGuard(0.3f);
            Assert.IsTrue(g.TryAccept(0f));
            for (int i = 1; i < 10; i++) Assert.IsFalse(g.TryAccept(i * 0.025f));
            Assert.IsTrue(g.TryAccept(0.31f), "a click after the window counts, whatever was dropped before");
            Assert.IsFalse(g.TryAccept(0.32f));
        }

        [Test]
        public void BusyAndReset()
        {
            var g = new ClickGuard();
            Assert.IsFalse(g.IsBusy(0f));
            g.TryAccept(1f);
            Assert.IsTrue(g.IsBusy(1.1f));
            Assert.IsFalse(g.IsBusy(1f + ClickGuard.DefaultWindow));
            g.Reset();
            Assert.IsTrue(g.TryAccept(1.05f));
        }

        [Test]
        public void ClockGoingBackwardsNeverLocksTheButton()
        {
            var g = new ClickGuard(0.3f);
            Assert.IsTrue(g.TryAccept(100f));
            Assert.IsTrue(g.TryAccept(5f));
        }

        [Test]
        public void WindowCoversThePressAnimation()
        {
            Assert.GreaterOrEqual(ClickGuard.DefaultWindow, WorldTour.Game.Motion.Presets.Press.Duration + WorldTour.Game.Motion.Presets.Release.Duration - 1e-6f);
            Assert.Less(ClickGuard.DefaultWindow, Tokens.MaxAnim);
        }
    }

    public class UiStickerMathTests
    {
        [Test]
        public void SeedIsFnv1a()
        {
            Assert.AreEqual(unchecked((int)2166136261u), StickerMath.Seed(""));
            Assert.AreEqual(unchecked((int)0xe40c292cu), StickerMath.Seed("a"));
            Assert.AreEqual(StickerMath.Seed("start"), StickerMath.Seed("start"));
        }

        [Test]
        public void TiltIsSlightStableAndBothWays()
        {
            int left = 0, right = 0;
            for (int s = -500; s < 500; s++)
            {
                float t = StickerMath.Tilt(s);
                Assert.AreEqual(t, StickerMath.Tilt(s));
                Assert.GreaterOrEqual(Math.Abs(t), Tokens.TiltMin - 1e-5f);
                Assert.LessOrEqual(Math.Abs(t), Tokens.TiltMax + 1e-5f);
                if (t < 0) left++; else right++;
            }
            Assert.Greater(left, 300);
            Assert.Greater(right, 300);
        }
    }

    public class UiMotionModeTests
    {
        [Test]
        public void ParsesTheSaveStrings()
        {
            Assert.AreEqual(MotionMode.Auto, MotionModes.Parse("auto"));
            Assert.AreEqual(MotionMode.Reduce, MotionModes.Parse("reduced"));
            Assert.AreEqual(MotionMode.Full, MotionModes.Parse("full"));
            Assert.AreEqual(MotionMode.Auto, MotionModes.Parse(null));
            Assert.AreEqual(MotionMode.Auto, MotionModes.Parse("REDUCED"));
            foreach (MotionMode m in Enum.GetValues(typeof(MotionMode))) Assert.AreEqual(m, MotionModes.Parse(MotionModes.ToSave(m)));
        }

        [Test]
        public void AutoFollowsTheDevice()
        {
            Assert.IsTrue(MotionModes.IsReduced(MotionMode.Auto, true));
            Assert.IsFalse(MotionModes.IsReduced(MotionMode.Auto, false));
            Assert.IsFalse(MotionModes.IsReduced(MotionMode.Auto, null));
            Assert.IsTrue(MotionModes.IsReduced(MotionMode.Reduce, false));
            Assert.IsFalse(MotionModes.IsReduced(MotionMode.Full, true));
        }
    }
}
