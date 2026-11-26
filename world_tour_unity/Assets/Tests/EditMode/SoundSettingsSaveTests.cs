using NUnit.Framework;
using WorldTour.Core.Progress;

namespace WorldTour.Core.Tests
{
    /// <summary>Phase 10A: the two sound sliders are new optional settings. A save without them is byte-for-byte what it was, a save with them round-trips, bad values are clamped.</summary>
    public class SoundSettingsSaveTests
    {
        [Test]
        public void NoSliders_WritesNothingNew()
        {
            var d = SaveBlob.Defaults("2026-11-20");
            Assert.IsNull(d.Settings.Ambience);
            Assert.IsNull(d.Settings.Effects);
            string text = SaveJson.ToCanonical(d);
            StringAssert.DoesNotContain("ambience", text);
            StringAssert.DoesNotContain("effects", text);
        }

        [Test]
        public void Sliders_RoundTrip_AndOtherSettingsStay()
        {
            var d = SaveBlob.Defaults("2026-11-20");
            ProgressRules.SetAmbience(d, 30);
            ProgressRules.SetEffects(d, 0);
            d.Settings.GentleMath = true;
            SaveBlob back = SaveJson.Load(SaveJson.ToText(d), "2026-11-21").Blob;
            Assert.AreEqual(30, back.Settings.Ambience);
            Assert.AreEqual(0, back.Settings.Effects);
            Assert.IsTrue(back.Settings.GentleMath);
            Assert.IsTrue(back.Settings.Sound);
            Assert.AreEqual("tour-pink", back.Settings.Theme);
            Assert.AreEqual(SaveJson.ToCanonical(d), SaveJson.ToCanonical(back));
        }

        [Test]
        public void Sliders_AreClamped_OnSetAndOnLoad()
        {
            var d = SaveBlob.Defaults("2026-11-20");
            ProgressRules.SetAmbience(d, 250);
            ProgressRules.SetEffects(d, -5);
            Assert.AreEqual(100, d.Settings.Ambience);
            Assert.AreEqual(0, d.Settings.Effects);
            string text = "{\"v\":2,\"settings\":{\"sound\":true,\"ambience\":900,\"effects\":-4}}";
            SaveBlob back = SaveJson.Load(text, "2026-11-21").Blob;
            Assert.AreEqual(100, back.Settings.Ambience);
            Assert.AreEqual(0, back.Settings.Effects);
        }

        [Test]
        public void AnOldSave_HasNoSliders_SoTheDefaultsApply()
        {
            SaveBlob back = SaveJson.Load("{\"v\":2,\"settings\":{\"sound\":false,\"theme\":\"tour-pink\",\"motion\":\"auto\"}}", "2026-11-21").Blob;
            Assert.IsNull(back.Settings.Ambience);
            Assert.IsNull(back.Settings.Effects);
            Assert.IsFalse(back.Settings.Sound);
        }
    }
}
