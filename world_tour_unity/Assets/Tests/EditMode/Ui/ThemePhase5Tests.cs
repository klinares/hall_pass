using System;
using NUnit.Framework;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;
using WorldTour.Game.Meta;
using WorldTour.Game.Ui;

namespace WorldTour.Game.EditTests
{
    /// <summary>
    /// The second theme (phase 5): both palettes are the tokens.css values, every text-on-background pair the screens use
    /// reads at WCAG AA (4.5:1) in both themes, the shop's swatches match the palettes, and the guitar skin lookups.
    /// </summary>
    public class ThemePhase5Tests
    {
        static double Lum(Rgba32 c)
        {
            Func<byte, double> ch = b => { double v = b / 255.0; return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); };
            return 0.2126 * ch(c.R) + 0.7152 * ch(c.G) + 0.0722 * ch(c.B);
        }

        static double Contrast(Rgba32 a, Rgba32 b)
        {
            double x = Lum(a), y = Lum(b);
            return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
        }

        // text colour on background colour, as the screens draw them
        static readonly ColorRole[][] TextPairs =
        {
            new[] { ColorRole.Ink, ColorRole.Paper },      // page text, headings
            new[] { ColorRole.Ink, ColorRole.Card },       // cards, scraps, ghost buttons, tags, empty slots
            new[] { ColorRole.OnInk, ColorRole.Ink },      // ink buttons, venue labels, the wearing tag
            new[] { ColorRole.OnInk, ColorRole.Accent2 },  // ink button hover, the fresh venue label
            new[] { ColorRole.Accent2, ColorRole.Card },   // "you're here", math level stars
            new[] { ColorRole.Accent2, ColorRole.Paper },
            new[] { ColorRole.Ink, ColorRole.Accent },     // the "yours" tag, sticky notes
            new[] { ColorRole.Ink, ColorRole.PaperDeep },
            new[] { ColorRole.Muted, ColorRole.Card },     // locked venue labels
        };

        [Test]
        public void BothThemes_TextPairsReadAtAA([Values(Palettes.TourPinkId, Palettes.PeachNavyId)] string id)
        {
            Palette p = Palettes.Get(id);
            Assert.AreEqual(id, p.Id);
            foreach (ColorRole[] pair in TextPairs)
            {
                double c = Contrast(p[pair[0]], p[pair[1]]);
                Assert.GreaterOrEqual(c, 4.5, id + ": " + pair[0] + " on " + pair[1] + " is " + c.ToString("0.00") + ":1");
            }
        }

        [Test]
        public void PeachNavy_IsTheTokensCssBlock()
        {
            Palette p = Palettes.PeachNavy;
            Assert.AreEqual("#ffcfb5", p[ColorRole.Paper].ToHex());
            Assert.AreEqual("#ffa47e", p[ColorRole.PaperDeep].ToHex());
            Assert.AreEqual("#1c2747", p[ColorRole.Ink].ToHex());
            Assert.AreEqual("#fffaf5", p[ColorRole.Card].ToHex());
            Assert.AreEqual("#fffaf5", p[ColorRole.OnInk].ToHex());
            Assert.AreEqual("#9fb3ea", p[ColorRole.Accent].ToHex());
            Assert.AreEqual("#1c2747", p[ColorRole.Accent2].ToHex());
            Assert.AreEqual("#6d6a78", p[ColorRole.Muted].ToHex());
        }

        [Test]
        public void ShopSwatches_UseThePaletteColours()
        {
            foreach (ShopItem it in Shop.All)
            {
                if (it.Slot != Shop.Theme) continue;
                Palette p = Palettes.Get(it.Id.Substring(6));
                Assert.AreEqual(it.Id.Substring(6), p.Id, "every shop theme has a palette");
                Assert.AreEqual(p[ColorRole.Paper].ToHex(), it.Swatch[0], it.Id + " paper");
                Assert.AreEqual(p[ColorRole.Ink].ToHex(), it.Swatch[1], it.Id + " ink");
            }
        }

        [Test]
        public void UnknownTheme_FallsBackToTourPink()
        {
            Assert.AreSame(Palettes.TourPink, Palettes.Get("sparkle"));
            Assert.AreSame(Palettes.TourPink, Palettes.Get(null));
        }

        [Test]
        public void GuitarSkin_FollowsTheEquippedOwnedSkin()
        {
            SaveBlob s = SaveBlob.Defaults("2026-11-20");
            LookArt.Apply(s);
            Assert.AreEqual("", LookArt.GuitarSkin);
            Assert.AreEqual("guitar_sm", LookArt.Skinned("guitar_sm"));
            s.Inventory.Equipped["guitar"] = "mint";                       // not owned: look() falls back to classic
            LookArt.Apply(s);
            Assert.AreEqual("", LookArt.GuitarSkin);
            s.Inventory.Owned.Add("guitar:mint");
            LookArt.Apply(s);
            Assert.AreEqual("mint", LookArt.GuitarSkin);
            Assert.AreEqual("guitar_mint_sm", LookArt.Skinned("guitar_sm"));
            Assert.AreEqual("guitar_mint", LookArt.Skinned("guitar"));
            Assert.AreEqual("star_sm", LookArt.Skinned("star_sm"));
            Assert.AreEqual("guitar_cherry_sm", LookArt.StickerName("guitar", true, "cherry"));
            Assert.AreEqual("tee", LookArt.StickerName("tee", false, "cherry"));
            LookArt.Apply(null);
            Assert.AreEqual("", LookArt.GuitarSkin);
        }

        [Test]
        public void JournalCopy_IsTheWebGames()
        {
            Assert.AreEqual("drag a sticker to move it, or tap it and then tap where it should go.", JournalText.Caption);
            Assert.AreEqual("stuck.", JournalText.Stuck);
            Assert.AreEqual("<b><noparse>ten shows</noparse></b>: <noparse>finish ten shows</noparse>. tap an empty spot to move it there.",
                JournalText.Selected("ten shows", "finish ten shows"));
            Assert.AreEqual("page 2 of 3", JournalText.PageOf(1, 3));
        }
    }
}
