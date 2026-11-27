using NUnit.Framework;
using UnityEngine;
using WorldTour.Core.Meta;
using WorldTour.World.Wardrobe;

namespace WorldTour.World.Tests
{
    /// <summary>Phase 10B.1 fix: a burst of look requests is one apply per frame, and the latest look wins.</summary>
    public class AvatarCoalesceTests
    {
        [Test]
        public void ABurstOfApplies_IsOneApplyPerFrame_AndTheLatestLookWins()
        {
            var holder = new GameObject("RigHolder");
            try
            {
                var rig = new WardrobeRig(holder) { Coalesce = true };
                AvatarLook last = null;
                for (int i = 0; i < 40; i++)
                {
                    last = AvatarLook.Default();
                    last.Top = new AvatarPick("top:hoodie", i % AvatarCatalogue.PaletteHex.Count, i % AvatarCatalogue.PatternNames.Count);
                    rig.Apply(last);
                }
                Assert.AreEqual(0, rig.AppliedCount, "nothing is applied until the frame's late update");
                rig.Flush();                                   // the frame ends
                Assert.AreEqual(1, rig.AppliedCount, "forty requests, one apply");
                Assert.AreEqual(last.Top.Color, rig.CurrentAvatar.Top.Color, "the latest request wins");
                rig.Flush();
                Assert.AreEqual(1, rig.AppliedCount, "an idle frame applies nothing");
                rig.Apply(last); rig.Apply(last); rig.Flush();
                Assert.AreEqual(2, rig.AppliedCount, "the next frame applies once more");
            }
            finally { Object.DestroyImmediate(holder); }
        }
    }
}
