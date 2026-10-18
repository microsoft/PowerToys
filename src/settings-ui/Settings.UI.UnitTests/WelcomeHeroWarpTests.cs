// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Numerics;

using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Settings.UI.UnitTests
{
    [TestClass]
    public class WelcomeHeroWarpTests
    {
        private static readonly WelcomeHeroLayout Layout = new(WelcomeHeroLayout.DefaultHeroHeight);
        private static readonly float FunnelCenter = Layout.ApexY / 2f;

        [TestMethod]
        public void RaysPointFromTheVanishingPointToTheirSlot()
        {
            var vanishingPoint = WelcomeHeroWarp.GetVanishingPoint(FunnelCenter);
            foreach (var slot in Layout.Slots)
            {
                var ray = WelcomeHeroWarp.GetRay(slot.Offset, FunnelCenter);

                // A visual rotated by the ray angle maps its x axis onto the ray.
                var end = vanishingPoint + Vector2.Transform(new Vector2(ray.Length, 0f), Matrix3x2.CreateRotation(ray.Angle));
                Assert.AreEqual(slot.Offset.X, end.X, 0.01f, $"Slot {slot.Rank}");
                Assert.AreEqual(slot.Offset.Y, end.Y, 0.01f, $"Slot {slot.Rank}");
            }
        }

        [TestMethod]
        [DataRow(0f)]
        [DataRow(-55f)]
        [DataRow(-900f)]
        [DataRow(-3600f)]
        [DataRow(-6000f)]
        [DataRow(400f)]
        public void StreaksLineUpWithTheStagePerspective(float z)
        {
            var stage = WelcomeHeroWarp.CreateStageTransform(FunnelCenter);
            foreach (var slot in Layout.Slots)
            {
                var projected = Vector4.Transform(new Vector4(slot.Offset, z, 1f), stage);
                var expected = new Vector2(projected.X, projected.Y) / projected.W;

                var actual = WelcomeHeroWarp.Project(WelcomeHeroWarp.GetRay(slot.Offset, FunnelCenter), z, FunnelCenter);
                Assert.AreEqual(expected.X, actual.X, 0.05f, $"Slot {slot.Rank} at z = {z}");
                Assert.AreEqual(expected.Y, actual.Y, 0.05f, $"Slot {slot.Rank} at z = {z}");
            }
        }

        [TestMethod]
        public void StreakColorsFollowTheLogoGradient()
        {
            Assert.AreEqual(WelcomeHeroPalette.BrandOrange, WelcomeHeroWarp.BrandColorAt(-1000f));
            Assert.AreEqual(WelcomeHeroPalette.BrandOrange, WelcomeHeroWarp.BrandColorAt(-420f));
            Assert.AreEqual(WelcomeHeroPalette.BrandBlue, WelcomeHeroWarp.BrandColorAt(420f));
            Assert.AreEqual(WelcomeHeroPalette.BrandBlue, WelcomeHeroWarp.BrandColorAt(1000f));

            // The logo center sits halfway between yellow and green.
            var center = WelcomeHeroWarp.BrandColorAt(0f);
            var yellow = WelcomeHeroPalette.BrandYellow;
            var green = WelcomeHeroPalette.BrandGreen;
            Assert.AreEqual((yellow.R + green.R) / 2f, center.R, 1f);
            Assert.AreEqual((yellow.G + green.G) / 2f, center.G, 1f);
            Assert.AreEqual((yellow.B + green.B) / 2f, center.B, 1f);
            Assert.AreEqual(255, center.A);
        }

        [TestMethod]
        public void StarFieldIsDeterministicAndStartsBehindTheLogo()
        {
            const float warpMs = 1634f;
            var stars = WelcomeHeroWarp.CreateStars(70, FunnelCenter, warpMs);

            Assert.AreEqual(70, stars.Length);
            CollectionAssert.AreEqual(stars, WelcomeHeroWarp.CreateStars(70, FunnelCenter, warpMs));
            Assert.IsTrue(stars.Select(s => s.Ray.Angle).Distinct().Count() > 60, "Stars should be spread around the vanishing point.");

            foreach (var star in stars)
            {
                Assert.IsTrue(star.StartZ is <= -600f and >= -6000f, $"StartZ {star.StartZ}");
                Assert.IsTrue(star.DelayMs is >= 0f and <= 250f, $"DelayMs {star.DelayMs}");
                Assert.IsTrue(star.DurationMs >= warpMs * 0.7f && star.DurationMs <= warpMs * 1.1f, $"DurationMs {star.DurationMs}");
                Assert.IsTrue(star.Thickness is >= 2.2f and <= 6.2f, $"Thickness {star.Thickness}");
                Assert.IsTrue(star.Ray.Length is >= 30f and <= 750f, $"Length {star.Ray.Length}");
                Assert.IsTrue(star.Color is null || star.Color.Value.A == 255, $"Color {star.Color}");
            }

            var neutral = stars.Count(s => s.Color is null);
            Assert.IsTrue(neutral > 0 && neutral < stars.Length, "Some, but not all, stars should use the neutral starlight.");
        }

        [TestMethod]
        public void EmptyStarFieldIsAllowed()
        {
            Assert.AreEqual(0, WelcomeHeroWarp.CreateStars(0, FunnelCenter, 1000f).Length);
            Assert.AreEqual(0, WelcomeHeroWarp.CreateStars(-5, FunnelCenter, 1000f).Length);
        }
    }
}
