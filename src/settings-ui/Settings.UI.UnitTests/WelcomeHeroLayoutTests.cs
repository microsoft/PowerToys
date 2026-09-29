// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Numerics;

using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Settings.UI.UnitTests
{
    [TestClass]
    public class WelcomeHeroLayoutTests
    {
        private static readonly WelcomeHeroLayout Layout = new(WelcomeHeroLayout.DefaultHeroHeight);

        [TestMethod]
        public void SlotsAreRankedVisibleFirstThenByDistance()
        {
            var slots = Layout.Slots;
            var seenFaded = false;

            for (var i = 0; i < slots.Count; i++)
            {
                Assert.AreEqual(i, slots[i].Rank);

                var faded = slots[i].Opacity < WelcomeHeroLayout.VisibleOpacityThreshold;
                Assert.IsFalse(seenFaded && !faded, $"Visible slot {i} is ranked after a faded slot.");
                seenFaded |= faded;

                if (i > 0 && (slots[i - 1].Opacity < WelcomeHeroLayout.VisibleOpacityThreshold) == faded)
                {
                    Assert.IsTrue(slots[i].Distance >= slots[i - 1].Distance - 0.01f, $"Slot {i} is closer to the logo than slot {i - 1}.");
                }
            }
        }

        [TestMethod]
        public void TilesNeverOverlapEachOtherOrTheLogo()
        {
            var slots = Layout.Slots;
            var logoClearance = (WelcomeHeroLayout.TileSize + WelcomeHeroLayout.LogoSize) / 2f;

            for (var i = 0; i < slots.Count; i++)
            {
                var offset = slots[i].Offset;
                Assert.IsFalse(
                    Math.Abs(offset.X) < logoClearance && Math.Abs(offset.Y) < logoClearance,
                    $"Slot {i} overlaps the logo.");

                for (var j = i + 1; j < slots.Count; j++)
                {
                    var delta = offset - slots[j].Offset;
                    Assert.IsFalse(
                        Math.Abs(delta.X) < WelcomeHeroLayout.TileSize && Math.Abs(delta.Y) < WelcomeHeroLayout.TileSize,
                        $"Slots {i} and {j} overlap.");
                }
            }
        }

        [TestMethod]
        public void EveryModuleLandsInAClearlyVisibleSlot()
        {
            var visible = Layout.Slots.Count(s => s.Opacity >= WelcomeHeroLayout.VisibleOpacityThreshold);

            Assert.IsTrue(visible >= WelcomeHeroModules.All.Count, $"Only {visible} visible slots for {WelcomeHeroModules.All.Count} modules.");
        }

        [TestMethod]
        public void SlotsFanOutSymmetricallyAboveTheLogo()
        {
            foreach (var slot in Layout.Slots)
            {
                Assert.IsTrue(slot.Offset.Y < 0f, "Tiles must sit above the logo.");
                Assert.IsTrue(slot.Opacity > 0.05f && slot.Opacity <= 1f);
                Assert.IsTrue(Layout.ApexY + slot.Offset.Y + (WelcomeHeroLayout.TileSize / 2f) <= Layout.HeroHeight);
                Assert.IsTrue(
                    Layout.Slots.Any(s => s.Column == -slot.Column && s.Row == slot.Row),
                    $"Slot ({slot.Column}, {slot.Row}) has no mirror.");
            }
        }

        [TestMethod]
        public void LayoutIsDeterministic()
        {
            var other = new WelcomeHeroLayout(WelcomeHeroLayout.DefaultHeroHeight);

            CollectionAssert.AreEqual(Layout.Slots.ToList(), other.Slots.ToList());
        }

        [TestMethod]
        public void ShortHeroStillHasRoomForTheLogo()
        {
            var layout = new WelcomeHeroLayout(10f);

            Assert.AreEqual(WelcomeHeroLayout.LogoSize + WelcomeHeroLayout.ApexBottomInset, layout.HeroHeight);
            Assert.IsTrue(layout.ApexY - (WelcomeHeroLayout.LogoSize / 2f) >= 0f);
        }

        [TestMethod]
        public void HitTestSlotFindsTheTileUnderThePointer()
        {
            var count = WelcomeHeroModules.All.Count;
            var reach = (WelcomeHeroLayout.TileSize / 2f) - 1f;

            for (var i = 0; i < count; i++)
            {
                var center = Layout.Slots[i].Offset;
                Assert.AreEqual(i, Layout.HitTestSlot(center, count));
                Assert.AreEqual(i, Layout.HitTestSlot(center + new Vector2(reach, -reach), count));
            }

            Assert.AreEqual(-1, Layout.HitTestSlot(Vector2.Zero, count));
            Assert.AreEqual(-1, Layout.HitTestSlot(new Vector2(0f, 200f), count));
            Assert.AreEqual(-1, Layout.HitTestSlot(Layout.Slots[1].Offset, 1));
        }

        [TestMethod]
        public void HitTestLogoCoversOnlyTheLogo()
        {
            var half = WelcomeHeroLayout.LogoSize / 2f;

            Assert.IsTrue(WelcomeHeroLayout.HitTestLogo(Vector2.Zero));
            Assert.IsTrue(WelcomeHeroLayout.HitTestLogo(new Vector2(half - 1f, 1f - half)));
            Assert.IsFalse(WelcomeHeroLayout.HitTestLogo(new Vector2(half + 1f, 0f)));
            Assert.IsFalse(WelcomeHeroLayout.HitTestLogo(Layout.Slots[0].Offset));
        }

        [TestMethod]
        public void EdgeOpacityDissolvesTowardsTheTop()
        {
            Assert.AreEqual(0f, WelcomeHeroLayout.EdgeOpacity(-WelcomeHeroLayout.TileSize));
            Assert.AreEqual(1f, WelcomeHeroLayout.EdgeOpacity(WelcomeHeroLayout.TileSize));

            var previous = 0f;
            for (var y = -WelcomeHeroLayout.TileSize; y <= WelcomeHeroLayout.TileSize; y += 2f)
            {
                var opacity = WelcomeHeroLayout.EdgeOpacity(y);
                Assert.IsTrue(opacity >= previous);
                previous = opacity;
            }
        }

        [TestMethod]
        public void ModulesHaveUniqueIconsAndNavigateToAnOobePage()
        {
            var modules = WelcomeHeroModules.All;

            Assert.AreEqual(modules.Count, modules.Select(m => m.AssetName).Distinct(StringComparer.OrdinalIgnoreCase).Count());

            foreach (var module in modules)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(module.NameResourceKey));
                Assert.IsTrue(
                    Enum.TryParse<PowerToysModules>(module.NavigationTag, out var page) && page != PowerToysModules.Overview,
                    $"'{module.NavigationTag}' is not an OOBE page.");
            }
        }
    }
}
