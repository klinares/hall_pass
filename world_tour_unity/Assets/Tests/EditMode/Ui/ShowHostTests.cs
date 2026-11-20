using System.Linq;
using NUnit.Framework;
using WorldTour.Core.Maths;
using WorldTour.Game.Modes;
using WorldTour.Game.Save;

namespace WorldTour.Game.EditTests
{
    /// <summary>The show host's plain parts: the save throttle and the mode list.</summary>
    public class ShowHostTests
    {
        [Test]
        public void SaveThrottle_AtMostEveryHalfSecond_BurstsDontDeferIt()
        {
            var t = new SaveThrottle();
            Assert.IsFalse(t.Due(0), "nothing to save");
            t.MarkDirty(10f);
            Assert.IsFalse(t.Due(10.2f));
            t.MarkDirty(10.3f);                                   // a burst rides along
            t.MarkDirty(10.45f);
            Assert.IsFalse(t.Due(10.49f));
            Assert.IsTrue(t.Due(10.5f), "500 ms after the first change, not the last");
            Assert.IsFalse(t.Due(10.6f), "saved once");
            t.MarkDirty(11f);
            Assert.IsTrue(t.Flush(), "show end saves at once");
            Assert.IsFalse(t.Due(12f));
            Assert.IsFalse(t.Flush(), "nothing left");
        }

        [Test]
        public void ModeList_IsTheWebGameSetlist()
        {
            CollectionAssert.AreEqual(
                new[] { "slots", "vowels", "remix", "fanmail", "studio", "lyric", "prefix", "merch", "beat", "tickets", "stage", "truck", "budget", "soundcheck" },
                ModeList.All(null).Select(m => m.Id).ToArray());
            Assert.AreEqual("remix", ModeList.ById("remix", null).Name);
            Assert.AreEqual("frac", ModeList.ById("beat", null).Skill);
            Assert.AreEqual(7, ModeList.All(null).Count(m => m.Kind == "spelling"));
        }

        [Test]
        public void ModeList_LeavesOutSwitchedOffMath()
        {
            var config = MathConfig.WebDefaults();
            config.ENABLED_SKILLS.frac = false;
            var ids = ModeList.All(config).Select(m => m.Id).ToList();
            CollectionAssert.DoesNotContain(ids, "beat");
            Assert.AreEqual(13, ids.Count);
            Assert.IsNull(ModeList.ById("beat", config));
        }
    }
}
