using NUnit.Framework;
using UnityEngine;
using WorldTour.Game.Save;
using WorldTour.Game.Ui;

namespace WorldTour.World.Tests
{
    /// <summary>Phase 10B.1 fix: "fans" and "coins" in the top-left card never touch, however many fans there are.</summary>
    public class CoinChipTests
    {
        GameObject _canvas;

        [TearDown]
        public void TearDown() { if (_canvas != null) Object.DestroyImmediate(_canvas); }

        [TestCase(0L)]
        [TestCase(99999L)]
        [TestCase(2023600L)]
        [TestCase(123456789L)]
        public void FansAndCoins_NeverTouch(long fans)
        {
            _canvas = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            SaveStore store = SaveStore.Open(new MemoryBackend());
            store.Blob.Fans.Lifetime = fans;
            store.Blob.Fans.Coins = 500;
            CoinChip chip = CoinChip.Create(_canvas.transform, store);
            var f = (RectTransform)chip.transform.Find("Face/Fans");
            var c = (RectTransform)chip.transform.Find("Face/Coins");
            Assert.AreEqual("fans " + fans.ToString("#,##0", System.Globalization.CultureInfo.InvariantCulture), chip.FansText);
            Assert.GreaterOrEqual(c.anchoredPosition.x - (f.anchoredPosition.x + f.sizeDelta.x), 12f, "gap between the two numbers for " + fans + " fans");
            Assert.LessOrEqual(c.anchoredPosition.x + c.sizeDelta.x + 8f, ((RectTransform)chip.transform).sizeDelta.x, "coins inside the card");
        }
    }
}
