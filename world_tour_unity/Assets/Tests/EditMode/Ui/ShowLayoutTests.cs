using NUnit.Framework;
using WorldTour.Game.Show;

namespace WorldTour.Game.EditTests
{
    /// <summary>Phase 8.1 stage show: the compact layout is off unless the world turns it on, and its panel leaves at least half the screen open.</summary>
    public class ShowLayoutTests
    {
        [Test]
        public void CompactIsOffByDefault_SoThe2DScreensKeepTheirLayout()
        {
            Assert.IsFalse(ShowLayout.Compact);
        }

        [Test]
        public void Panel_LeavesAtLeastHalfTheScreenOpen_AtEveryWindowShape()
        {
            foreach (float w in new[] { 1366f, 1280f, 1536f, 1920f, 2560f })
                Assert.LessOrEqual(ShowLayout.WidthFor(w) + ShowLayout.PanelMargin, w / 2f, "panel width at canvas " + w);
            Assert.AreEqual(660f, ShowLayout.WidthFor(1366f), "the 1366 canvas (every 16:9 window) gets the 660 panel");
        }

        [Test]
        public void PanelText_StaysAtSixteenPixelsOrMore_AtTheProse()
        {
            // the math prompt card's body is 18.4 px in the layout; drawn at the panel scale it must still be 16 px
            Assert.GreaterOrEqual(18.4f * ShowLayout.PanelScale, 16f);
            Assert.AreEqual(ShowLayout.WidthFor(1366f), ShowLayout.LayoutWidth(1366f) * ShowLayout.PanelScale, 0.01f);
        }
    }
}
