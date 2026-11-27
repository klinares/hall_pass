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
        public void BothSlidersAtZero_SurviveAReload_AndStayZero_NotTheDefaults()
        {
            var d = SaveBlob.Defaults("2026-11-20");
            ProgressRules.SetAmbience(d, 0);
            ProgressRules.SetEffects(d, 0);
            string text = SaveJson.ToText(d);
            StringAssert.Contains("\"ambience\":0", text);
            StringAssert.Contains("\"effects\":0", text);
            SaveBlob once = SaveJson.Load(text, "2026-11-21").Blob;
            Assert.AreEqual(0, once.Settings.Ambience);
            Assert.AreEqual(0, once.Settings.Effects);
            // and again: a second save and reload keeps them (zero is a value, never "not set")
            SaveBlob twice = SaveJson.Load(SaveJson.ToText(once), "2026-11-22").Blob;
            Assert.AreEqual(0, twice.Settings.Ambience);
            Assert.AreEqual(0, twice.Settings.Effects);
            Assert.AreEqual(SaveJson.ToCanonical(d), SaveJson.ToCanonical(twice));
        }

        [Test]
        public void AmbienceAtZero_WithEffectsUp_SurvivesAReload()
        {
            var d = SaveBlob.Defaults("2026-11-20");
            ProgressRules.SetAmbience(d, 0);
            ProgressRules.SetEffects(d, 70);
            SaveBlob back = SaveJson.Load(SaveJson.ToText(d), "2026-11-21").Blob;
            Assert.AreEqual(0, back.Settings.Ambience);
            Assert.AreEqual(70, back.Settings.Effects);
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
