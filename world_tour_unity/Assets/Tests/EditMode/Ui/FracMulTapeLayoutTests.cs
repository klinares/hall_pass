using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using WorldTour.Core.Maths.Gen;
using WorldTour.Game.Modes;
using WorldTour.Game.Show;

namespace WorldTour.Game.EditTests
{
    /// <summary>Phase 8.4: the snack table's uncut "n in all" bar grows to fill the sheet in the world-hosted layout only; the 2D screens keep the 60 unit bar.</summary>
    public class FracMulTapeLayoutTests
    {
        static float BarHeight(bool compact)
        {
            var go = new GameObject("paper", typeof(RectTransform));
            bool before = ShowLayout.Compact;
            try
            {
                ShowLayout.Compact = compact;
                FracMulDraw.Draw((RectTransform)go.transform, new FmPicture { Kind = "tape", Wholes = 1, Parts = 2, Total = "14" });
                foreach (var img in go.GetComponentsInChildren<Image>(true))
                    if (img.name == "Bar") return img.rectTransform.sizeDelta.y;
                return -1f;
            }
            finally { ShowLayout.Compact = before; Object.DestroyImmediate(go); }
        }

        [Test]
        public void OneWholeBar_IsTallerInTheWorldLayout_AndUnchangedIn2D()
        {
            Assert.AreEqual(60f, BarHeight(false), 0.01f, "the 2D layout keeps its 60 unit bar");
            Assert.AreEqual(150f, BarHeight(true), 0.01f, "the world layout fills the sheet (no caption below it)");
        }
    }
}
