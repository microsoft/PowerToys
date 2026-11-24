// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Graphics;
using WorkspacesLauncherUI.Helpers;

namespace WorkspacesLauncherUI.UnitTests
{
    [TestClass]
    public sealed class WindowPlacementTests
    {
        [TestMethod]
        public void WarningCentersOnOwnerMovedToSecondaryDisplay()
        {
            var bounds = WindowPlacement.GetCenteredBounds(
                new RectInt32(1920, 0, 1920, 1080),
                new SizeInt32(600, 700),
                new RectInt32(2200, 280, 360, 360));

            AssertBounds(bounds, 2080, 110, 600, 700);
        }

        [TestMethod]
        public void WarningFollowsOwnerOnNegativeCoordinateDisplay()
        {
            var bounds = WindowPlacement.GetCenteredBounds(
                new RectInt32(-1920, -600, 1920, 1080),
                new SizeInt32(600, 700),
                new RectInt32(-1500, -200, 360, 360));

            AssertBounds(bounds, -1620, -370, 600, 700);
        }

        [DataTestMethod]
        [DataRow(1920, 400, 1920, 230)]
        [DataRow(2840, 400, 2600, 230)]
        [DataRow(2300, 48, 2180, 48)]
        [DataRow(2300, 672, 2180, 332)]
        public void WarningRemainsVisibleWhenOwnerIsNearWorkAreaEdge(int ownerX, int ownerY, int expectedX, int expectedY)
        {
            var bounds = WindowPlacement.GetCenteredBounds(
                new RectInt32(1920, 48, 1280, 984),
                new SizeInt32(600, 700),
                new RectInt32(ownerX, ownerY, 360, 360));

            AssertBounds(bounds, expectedX, expectedY, 600, 700);
        }

        [DataTestMethod]
        [DataRow(1920, 48, 2580, 238)]
        [DataRow(-1920, -200, -1260, -10)]
        public void ConfirmationOnlyWarningCentersWithinWorkArea(int workAreaX, int workAreaY, int expectedX, int expectedY)
        {
            var bounds = WindowPlacement.GetCenteredBounds(
                new RectInt32(workAreaX, workAreaY, 1920, 1080),
                new SizeInt32(600, 700));

            AssertBounds(bounds, expectedX, expectedY, 600, 700);
        }

        [TestMethod]
        public void HighDpiWarningIsRestrictedToSmallWorkArea()
        {
            var bounds = WindowPlacement.GetCenteredBounds(
                new RectInt32(1920, -600, 800, 600),
                new SizeInt32(1200, 1400),
                new RectInt32(2100, -500, 360, 360));

            AssertBounds(bounds, 1920, -600, 800, 600);
        }

        private static void AssertBounds(RectInt32 bounds, int x, int y, int width, int height)
        {
            Assert.AreEqual(x, bounds.X);
            Assert.AreEqual(y, bounds.Y);
            Assert.AreEqual(width, bounds.Width);
            Assert.AreEqual(height, bounds.Height);
        }
    }
}
