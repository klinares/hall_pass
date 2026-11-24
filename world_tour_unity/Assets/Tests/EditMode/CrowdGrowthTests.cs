using NUnit.Framework;
using WorldTour.Core.Data;
using WorldTour.Core.Tour;

namespace WorldTour.Core.Tests
{
    /// <summary>Phase 8.3: every place's crowd grows with her lifetime fans, scaled by the venue's crowd factor.</summary>
    public class CrowdGrowthTests
    {
        [Test]
        public void EveryPlace_StartsWithAFew_AndNeverShrinks_AndStopsAtItsCapacity()
        {
            foreach (Venue v in Venues.All)
            {
                Assert.AreEqual(CrowdRules.VenueMin, CrowdRules.CountFor(0, v.Id), v.Id + " starts small");
                Assert.AreEqual(CrowdRules.VenueMin, CrowdRules.CountFor(-9, v.Id), v.Id);
                int cap = CrowdRules.Capacity(v.Id), last = 0;
                for (long f = 0; f <= 2000000; f += 2500)
                {
                    int c = CrowdRules.CountFor(f, v.Id);
                    Assert.GreaterOrEqual(c, last, v.Id + " never fewer as fans grow, at " + f);
                    Assert.LessOrEqual(c, cap, v.Id);
                    Assert.GreaterOrEqual(c, CrowdRules.VenueMin, v.Id);
                    last = c;
                }
                Assert.AreEqual(cap, CrowdRules.CountFor(CrowdRules.FullAtFans(v.Id), v.Id), v.Id + " is full at the next unlock");
                Assert.AreEqual(cap, CrowdRules.CountFor(5000000, v.Id), v.Id);
            }
        }

        [Test]
        public void Capacity_ScalesWithTheVenueCrowdFactor()
        {
            int prev = 0;
            foreach (Venue v in Venues.All)
            {
                int cap = CrowdRules.Capacity(v.Id);
                Assert.Greater(cap, prev, v.Id + " holds more than the one before it");
                Assert.AreEqual((int)System.Math.Round(10 + 34 * v.Crowd), cap, v.Id);
                prev = cap;
            }
            Assert.AreEqual(15, CrowdRules.Capacity("coffee"));
            Assert.AreEqual(44, CrowdRules.Capacity("arena"));
        }

        [Test]
        public void ArrivingAtTheNextPlace_ShowsARealCrowd_WithRoomToGrow()
        {
            // she arrives with exactly the fans that opened the place: each place greets her with a real crowd, bigger than the one before it
            int last = 0;
            for (int i = 1; i < Venues.All.Count; i++)
            {
                int atArrival = CrowdRules.CountFor(Venues.All[i].Fans, Venues.All[i].Id);
                Assert.Greater(atArrival, CrowdRules.VenueMin + 3, Venues.All[i].Id);
                Assert.Greater(atArrival, last, Venues.All[i].Id);
                Assert.Less(atArrival, CrowdRules.Capacity(Venues.All[i].Id), "and there is room to grow");
                last = atArrival;
            }
        }

        [Test]
        public void FullAtFans_IsTheNextUnlock_AndTheArenaFillsPastItsOwn()
        {
            Assert.AreEqual(Venues.All[1].Fans, CrowdRules.FullAtFans("coffee"));
            Assert.AreEqual(Venues.All[7].Fans, CrowdRules.FullAtFans("festival"));
            Assert.Greater(CrowdRules.FullAtFans("arena"), Venues.ById("arena").Fans);
        }
    }
}
