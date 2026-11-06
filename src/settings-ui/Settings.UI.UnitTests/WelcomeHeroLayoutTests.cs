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
        public void HitTestWithoutMotionMatchesTheFlatLayout()
        {
            var count = WelcomeHeroModules.All.Count;

            for (var y = -Layout.ApexY; y <= 60f; y += 3.7f)
            {
                for (var x = -500f; x <= 500f; x += 3.7f)
                {
                    var point = new Vector2(x, y);
                    Assert.AreEqual(Layout.HitTestSlot(point, count), Layout.HitTestSlot(point, count, WelcomeHeroMotion.None), $"Slot at {point}.");
                    Assert.AreEqual(WelcomeHeroLayout.HitTestLogo(point), Layout.HitTestLogo(point, WelcomeHeroMotion.None), $"Logo at {point}.");
                }
            }
        }

        [TestMethod]
        public void HitTestFollowsTheMagnifiedTile()
        {
            // Next to the tilt pivot, magnification is all that moves the tile: its edge grows past the flat hit area.
            var count = WelcomeHeroModules.All.Count;
            var center = Enumerable.Range(0, count).OrderBy(i => DistanceFromPivot(Layout.Slots[i].Offset)).First();
            var edge = Layout.Slots[center].Offset + new Vector2((WelcomeHeroLayout.TileSize / 2f) + 8f, 0f);
            var live = WelcomeHeroMotion.Live(1000f);

            Assert.AreEqual(-1, Layout.HitTestSlot(edge, count));
            Assert.AreEqual(center, Layout.HitTestSlot(edge, count, live));
        }

        [TestMethod]
        public void HitTestFollowsTheTiltedTile()
        {
            // At the far end of the stage the tilt moves the leftmost tool (MouseJump in the default order) noticeably.
            var count = WelcomeHeroModules.All.Count;
            var outer = Enumerable.Range(0, count).OrderBy(i => Layout.Slots[i].Offset.X).First();
            var live = WelcomeHeroMotion.Live(1000f);
            var flat = Layout.Slots[outer].Offset;
            var drawn = Layout.GetSlotCenter(outer, flat, live);

            Assert.IsTrue(Vector2.Distance(flat, drawn) > 4f, $"Expected the tilt to move the tile, it is drawn at {drawn} instead of {flat}.");

            // Probe just inside the drawn tile, on the side the tilt moved it to: only the tilt-aware test finds it.
            var shift = Vector2.Normalize(drawn - flat);
            var probe = drawn + (shift * ((WelcomeHeroLayout.TileSize / 2f) + 2f));
            Assert.AreEqual(outer, Layout.HitTestSlot(probe, count, live));
            Assert.AreNotEqual(outer, Layout.HitTestSlot(probe, count));
        }

        [TestMethod]
        public void HitTestCoversTheIdleBob()
        {
            var count = WelcomeHeroModules.All.Count;
            var bobOnly = new WelcomeHeroMotion(0f, 500f, WelcomeHeroPointerEffects.BobAmplitude);

            for (var i = 0; i < count; i++)
            {
                var top = Layout.Slots[i].Offset - new Vector2(0f, (WelcomeHeroLayout.TileSize / 2f) + 6f);
                Assert.AreEqual(-1, Layout.HitTestSlot(top, count), $"Slot {i} without bob.");
                Assert.AreEqual(i, Layout.HitTestSlot(top, count, bobOnly), $"Slot {i} with bob.");
            }
        }

        [TestMethod]
        public void HitTestPrefersTheTileClosestToThePointer()
        {
            // Two magnified neighbors next to the tilt pivot both reach the gap between them; the closer one wins.
            var count = WelcomeHeroModules.All.Count;
            var live = WelcomeHeroMotion.Live(1000f);
            var pair = (from i in Enumerable.Range(0, count)
                        from j in Enumerable.Range(0, count)
                        let delta = Layout.Slots[j].Offset - Layout.Slots[i].Offset
                        where Layout.Slots[i].Row == Layout.Slots[j].Row && Math.Abs(delta.X - WelcomeHeroLayout.Spacing) <= 1f
                        orderby DistanceFromPivot(Layout.Slots[i].Offset + (delta / 2f))
                        select (Left: i, Right: j, Delta: delta)).First();

            // Both probes sit in the gap that the flat hit areas leave between the tiles.
            var near = Layout.Slots[pair.Left].Offset + (pair.Delta * 0.47f);
            var far = Layout.Slots[pair.Left].Offset + (pair.Delta * 0.53f);

            Assert.AreEqual(-1, Layout.HitTestSlot(near, count));
            Assert.AreEqual(-1, Layout.HitTestSlot(far, count));
            Assert.AreEqual(pair.Left, Layout.HitTestSlot(near, count, live));
            Assert.AreEqual(pair.Right, Layout.HitTestSlot(far, count, live));
        }

        [TestMethod]
        public void LogoHitTestFollowsTheTilt()
        {
            var live = WelcomeHeroMotion.Live(1000f);
            var half = WelcomeHeroLayout.LogoSize / 2f;

            Assert.IsTrue(Layout.HitTestLogo(Vector2.Zero, live));
            Assert.IsFalse(Layout.HitTestLogo(new Vector2(half + 12f, 0f), live));
        }

        [TestMethod]
        public void MagnificationPeaksUnderThePointerAndFadesOut()
        {
            var slot = new Vector2(40f, -80f);

            Assert.AreEqual(WelcomeHeroPointerEffects.MaxMagnification, WelcomeHeroPointerEffects.GetMagnification(slot, slot, 1f), 0.0001f);
            Assert.AreEqual(1f, WelcomeHeroPointerEffects.GetMagnification(slot, slot, 0f), 0.0001f);
            Assert.AreEqual(1f, WelcomeHeroPointerEffects.GetMagnification(slot + new Vector2(WelcomeHeroPointerEffects.MagnifyRadius, 0f), slot, 1f), 0.0001f);

            var nearer = WelcomeHeroPointerEffects.GetMagnification(slot + new Vector2(30f, 0f), slot, 1f);
            var farther = WelcomeHeroPointerEffects.GetMagnification(slot + new Vector2(60f, 0f), slot, 1f);
            Assert.IsTrue(nearer > farther && farther > 1f);
        }

        [TestMethod]
        public void TiltLeansTowardsThePointer()
        {
            var funnelCenter = Layout.FunnelCenter;

            var (aroundX, aroundY) = WelcomeHeroPointerEffects.GetTilt(new Vector2(-500f, 0f), 500f, funnelCenter, 1f);
            Assert.AreEqual(-WelcomeHeroPointerEffects.MaxTiltDegrees, aroundY, 0.0001f);
            Assert.AreEqual(-WelcomeHeroPointerEffects.MaxTiltDegrees, aroundX, 0.0001f);

            var (restX, restY) = WelcomeHeroPointerEffects.GetTilt(new Vector2(0f, -funnelCenter), 500f, funnelCenter, 1f);
            Assert.AreEqual(0f, restX, 0.0001f);
            Assert.AreEqual(0f, restY, 0.0001f);

            // Without tilt, the flat tile plane is not distorted at all.
            var flat = WelcomeHeroPointerEffects.CreateStageProjection(0f, 0f, funnelCenter);
            var point = new Vector2(123f, -45f);
            var projected = WelcomeHeroPointerEffects.Project(point, flat);
            Assert.AreEqual(point.X, projected.X, 0.001f);
            Assert.AreEqual(point.Y, projected.Y, 0.001f);
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

        // The stage tilts around the middle of the funnel, so tiles there barely move.
        private static float DistanceFromPivot(Vector2 pointFromApex) => Vector2.Distance(pointFromApex, new Vector2(0f, -Layout.FunnelCenter));
    }
}
