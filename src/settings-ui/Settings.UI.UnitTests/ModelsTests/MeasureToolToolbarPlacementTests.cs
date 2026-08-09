// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Settings.UI.Library.Enumerations;

namespace CommonLibTest
{
    /// <summary>
    /// Exercises the pure toolbar-anchor geometry in <see cref="MeasureToolToolbarPlacement"/>.
    /// This is the math that resolves the Screen Ruler toolbar's summon position, so a regression
    /// here silently mis-places the toolbar (the classic high-DPI / multi-monitor / negative-origin
    /// "double scaling" or "wrong edge" failure mode) without any UI ever needing to run.
    /// </summary>
    [TestClass]
    public sealed class MeasureToolToolbarPlacementTests
    {
        private const double InsetDip = 24;

        // Arbitrary horizontal-bar-shaped toolbar size, in DIP, used across the anchor tests below.
        private const double HorizontalWidthDip = 200;
        private const double HorizontalHeightDip = 50;

        [DataTestMethod]
        [DataRow(MeasureToolToolbarPosition.TopLeft, 24, 24)]
        [DataRow(MeasureToolToolbarPosition.TopCenter, 860, 24)]
        [DataRow(MeasureToolToolbarPosition.TopRight, 1696, 24)]
        [DataRow(MeasureToolToolbarPosition.BottomLeft, 24, 1006)]
        [DataRow(MeasureToolToolbarPosition.BottomCenter, 860, 1006)]
        [DataRow(MeasureToolToolbarPosition.BottomRight, 1696, 1006)]
        public void GetAnchorPosition_AtDpi100Percent_PlacesEachOfTheSixAnchors(MeasureToolToolbarPosition position, int expectedX, int expectedY)
        {
            var (x, y) = MeasureToolToolbarPlacement.GetAnchorPosition(
                position,
                workAreaX: 0,
                workAreaY: 0,
                workAreaWidth: 1920,
                workAreaHeight: 1080,
                toolbarWidthDip: HorizontalWidthDip,
                toolbarHeightDip: HorizontalHeightDip,
                insetDip: InsetDip,
                dpiScale: 1.0);

            Assert.AreEqual(expectedX, x, "X for " + position);
            Assert.AreEqual(expectedY, y, "Y for " + position);
        }

        // At 150% scaling the physical surface is 300x75 and the inset scales to 36px. Center must
        // subtract HALF the *scaled* size (not the DIP size), and the far edge anchors must
        // subtract the full scaled size plus the scaled inset - exactly where a missing/extra DPI
        // factor would show up as a silent regression.
        [DataTestMethod]
        [DataRow(MeasureToolToolbarPosition.TopLeft, 36, 36)]
        [DataRow(MeasureToolToolbarPosition.TopCenter, 810, 36)]
        [DataRow(MeasureToolToolbarPosition.TopRight, 1584, 36)]
        [DataRow(MeasureToolToolbarPosition.BottomLeft, 36, 969)]
        [DataRow(MeasureToolToolbarPosition.BottomCenter, 810, 969)]
        [DataRow(MeasureToolToolbarPosition.BottomRight, 1584, 969)]
        public void GetAnchorPosition_AtDpi150Percent_ScalesSurfaceAndInset(MeasureToolToolbarPosition position, int expectedX, int expectedY)
        {
            var (x, y) = MeasureToolToolbarPlacement.GetAnchorPosition(
                position,
                workAreaX: 0,
                workAreaY: 0,
                workAreaWidth: 1920,
                workAreaHeight: 1080,
                toolbarWidthDip: HorizontalWidthDip,
                toolbarHeightDip: HorizontalHeightDip,
                insetDip: InsetDip,
                dpiScale: 1.5);

            Assert.AreEqual(expectedX, x, "X for " + position);
            Assert.AreEqual(expectedY, y, "Y for " + position);
        }

        [TestMethod]
        public void GetAnchorPosition_AtDpi125Percent_RoundsSurfaceAndInsetOutward()
        {
            var (x, y) = MeasureToolToolbarPlacement.GetAnchorPosition(
                MeasureToolToolbarPosition.TopRight,
                workAreaX: 0,
                workAreaY: 0,
                workAreaWidth: 1920,
                workAreaHeight: 1080,
                toolbarWidthDip: 274,
                toolbarHeightDip: 50,
                insetDip: InsetDip,
                dpiScale: 1.25);

            Assert.AreEqual(1547, x);
            Assert.AreEqual(30, y);
        }

        // A 4K secondary monitor at 200% scaling, positioned to the right of the primary (positive
        // origin). Verifies the work-area origin is honored for every anchor, not just (0,0).
        [DataTestMethod]
        [DataRow(MeasureToolToolbarPosition.TopLeft, 1968, 48)]
        [DataRow(MeasureToolToolbarPosition.TopCenter, 3640, 48)]
        [DataRow(MeasureToolToolbarPosition.TopRight, 5312, 48)]
        [DataRow(MeasureToolToolbarPosition.BottomLeft, 1968, 2012)]
        [DataRow(MeasureToolToolbarPosition.BottomCenter, 3640, 2012)]
        [DataRow(MeasureToolToolbarPosition.BottomRight, 5312, 2012)]
        public void GetAnchorPosition_OnOffsetSecondaryMonitor_HonorsWorkAreaOrigin(MeasureToolToolbarPosition position, int expectedX, int expectedY)
        {
            var (x, y) = MeasureToolToolbarPlacement.GetAnchorPosition(
                position,
                workAreaX: 1920,
                workAreaY: 0,
                workAreaWidth: 3840,
                workAreaHeight: 2160,
                toolbarWidthDip: HorizontalWidthDip,
                toolbarHeightDip: HorizontalHeightDip,
                insetDip: InsetDip,
                dpiScale: 2.0);

            Assert.AreEqual(expectedX, x, "X for " + position);
            Assert.AreEqual(expectedY, y, "Y for " + position);
        }

        // A monitor to the LEFT of the primary has a negative virtual-desktop X origin. Edge
        // anchors must still offset relative to that negative origin, not clamp to zero.
        [TestMethod]
        public void GetAnchorPosition_OnNegativeOriginMonitor_OffsetsFromWorkAreaEdge()
        {
            const int WorkAreaX = -1920;
            const int WorkAreaY = -1080;
            const int WorkAreaWidth = 1920;
            const int WorkAreaHeight = 1080;

            var topLeft = MeasureToolToolbarPlacement.GetAnchorPosition(
                MeasureToolToolbarPosition.TopLeft,
                WorkAreaX,
                WorkAreaY,
                WorkAreaWidth,
                WorkAreaHeight,
                HorizontalWidthDip,
                HorizontalHeightDip,
                InsetDip,
                dpiScale: 1.0);
            Assert.AreEqual(WorkAreaX + 24, topLeft.X);
            Assert.AreEqual(WorkAreaY + 24, topLeft.Y);

            var bottomRight = MeasureToolToolbarPlacement.GetAnchorPosition(
                MeasureToolToolbarPosition.BottomRight,
                WorkAreaX,
                WorkAreaY,
                WorkAreaWidth,
                WorkAreaHeight,
                HorizontalWidthDip,
                HorizontalHeightDip,
                InsetDip,
                dpiScale: 1.0);
            Assert.AreEqual(WorkAreaX + WorkAreaWidth - (int)HorizontalWidthDip - 24, bottomRight.X);
            Assert.AreEqual(WorkAreaY + WorkAreaHeight - (int)HorizontalHeightDip - 24, bottomRight.Y);
        }

        // When the toolbar (plus inset) is wider than the work area - e.g. a narrow secondary
        // monitor - every anchor must clamp flush to the work area's leading edge instead of
        // spilling past the trailing edge or going negative.
        [DataTestMethod]
        [DataRow(MeasureToolToolbarPosition.TopLeft)]
        [DataRow(MeasureToolToolbarPosition.TopCenter)]
        [DataRow(MeasureToolToolbarPosition.TopRight)]
        [DataRow(MeasureToolToolbarPosition.BottomLeft)]
        [DataRow(MeasureToolToolbarPosition.BottomCenter)]
        [DataRow(MeasureToolToolbarPosition.BottomRight)]
        public void GetAnchorPosition_WhenToolbarIsLargerThanWorkArea_ClampsToLeadingEdges(MeasureToolToolbarPosition position)
        {
            var (x, y) = MeasureToolToolbarPlacement.GetAnchorPosition(
                position,
                workAreaX: 100,
                workAreaY: -40,
                workAreaWidth: 150, // narrower than the 200 DIP-wide toolbar even at 100% scaling
                workAreaHeight: 40, // shorter than the 50 DIP-high toolbar
                toolbarWidthDip: HorizontalWidthDip,
                toolbarHeightDip: HorizontalHeightDip,
                insetDip: InsetDip,
                dpiScale: 1.0);

            Assert.AreEqual(100, x, "Expected clamp to the work area's leading (left) edge for " + position);
            Assert.AreEqual(-40, y, "Expected clamp to the work area's leading (top) edge for " + position);
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        [DataRow(9)]
        [DataRow(int.MaxValue)]
        public void Normalize_WithUnknownValue_ReturnsTopCenter(int value)
        {
            Assert.AreEqual(MeasureToolToolbarPosition.TopCenter, MeasureToolToolbarPlacement.Normalize(value));
        }
    }
}
