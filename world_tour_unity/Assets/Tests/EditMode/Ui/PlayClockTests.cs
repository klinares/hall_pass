using NUnit.Framework;
using WorldTour.Game.Save;

namespace WorldTour.Game.EditTests
{
    /// <summary>The play-time timer's timing (progress.js:107-117) on a fake clock: 15 s ticks, visible, input in the last minute.</summary>
    public class PlayClockTests
    {
        [Test]
        public void NothingBeforeStart()
        {
            var c = new PlayClock();
            Assert.AreEqual(0, c.Advance(1000f, true));
        }

        [Test]
        public void TheLoadCountsAsInput_ForAMinute()
        {
            var c = new PlayClock();
            c.Start(100f);
            Assert.AreEqual(0, c.Advance(114.9f, true), "the first tick is 15 s after the load");
            Assert.AreEqual(1, c.Advance(115f, true));
            // ticks at 130, 145, 160 are within a minute of the load; 175 and 190 are not
            Assert.AreEqual(3, c.Advance(190f, true));
            Assert.AreEqual(0, c.Advance(300f, true), "idle for over a minute: nothing");
        }

        [Test]
        public void AnyPress_StartsAnotherMinute()
        {
            var c = new PlayClock();
            c.Start(0f);
            Assert.AreEqual(4, c.Advance(200f, true));      // 15, 30, 45, 60
            c.Input(200f);
            Assert.AreEqual(4, c.Advance(260f, true));      // 210, 225, 240, 255 (270 would be 70 s after the press)
            Assert.AreEqual(0, c.Advance(400f, true));
        }

        [Test]
        public void Hidden_DoesNotCount()
        {
            var c = new PlayClock();
            c.Start(0f);
            Assert.AreEqual(0, c.Advance(45f, false));
            c.Input(45f);
            Assert.AreEqual(1, c.Advance(60f, true), "visible again, and pressed at 45");
        }

        [Test]
        public void ALongPause_AddsAtMostTheMinuteAfterTheLastPress()
        {
            var c = new PlayClock();
            c.Start(0f);
            c.Input(10f);
            Assert.AreEqual(4, c.Advance(3600f, true), "15, 30, 45, 60 (70 is past the minute)");
        }
    }
}
