using NUnit.Framework;

namespace WorldTour.Core.Tests
{
    public class GameInfoTests
    {
        [Test]
        public void TitleIsLowercaseWorldTour()
        {
            Assert.AreEqual("world tour", GameInfo.Title);
            Assert.AreEqual(GameInfo.Title.ToLowerInvariant(), GameInfo.Title);
        }
    }
}
