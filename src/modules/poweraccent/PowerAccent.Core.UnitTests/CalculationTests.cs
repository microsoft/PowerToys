// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerAccent.Core.Services;
using PowerAccent.Core.Tools;

namespace PowerAccent.Core.UnitTests
{
    [TestClass]
    public class CalculationTests
    {
        // Screen representing a standard 1920x1080 monitor at position (0,0)
        private static readonly Rect StandardScreen = new Rect(0, 0, 1920, 1080);

        // A typical toolbar window size (300x50 in WPF DIPs)
        private static readonly Size ToolbarWindow = new Size(300, 50);

        /// <summary>
        /// Product code: Calculation.GetRawCoordinatesFromCaret
        /// What: Verifies toolbar is centered horizontally above the caret at screen center
        /// Why: The most common placement scenario — regression here affects every user
        /// </summary>
        [TestMethod]
        public void GetRawCoordinatesFromCaret_CenterOfScreen_ShouldCenterToolbar()
        {
            var caret = new Point(960, 540);
            var result = Calculation.GetRawCoordinatesFromCaret(caret, StandardScreen, ToolbarWindow);

            // X should be caret.X - window.Width/2 = 960 - 150 = 810
            Assert.AreEqual(810.0, result.X);

            // Y should be caret.Y - window.Height - 20 = 540 - 50 - 20 = 470
            Assert.AreEqual(470.0, result.Y);
        }

        /// <summary>
        /// Product code: Calculation.GetRawCoordinatesFromCaret — left edge clamping
        /// What: Verifies toolbar is clamped to screen left when caret is near left edge
        /// Why: Without clamping, toolbar would extend off-screen and be unusable
        /// </summary>
        [TestMethod]
        public void GetRawCoordinatesFromCaret_NearLeftEdge_ShouldClampToScreenLeft()
        {
            var caret = new Point(50, 540);
            var result = Calculation.GetRawCoordinatesFromCaret(caret, StandardScreen, ToolbarWindow);

            // left = 50 - 150 = -100, which is < screen.X (0), so X should be clamped to 0
            Assert.AreEqual(0.0, result.X);
        }

        /// <summary>
        /// Product code: Calculation.GetRawCoordinatesFromCaret — right edge clamping
        /// What: Verifies toolbar is clamped to screen right when caret is near right edge
        /// Why: Toolbar extending past screen right would be clipped by the display
        /// </summary>
        [TestMethod]
        public void GetRawCoordinatesFromCaret_NearRightEdge_ShouldClampToScreenRight()
        {
            var caret = new Point(1900, 540);
            var result = Calculation.GetRawCoordinatesFromCaret(caret, StandardScreen, ToolbarWindow);

            // left = 1900 - 150 = 1750, left + window.Width = 1750 + 300 = 2050 > 1920
            // So X should be clamped to screen.X + screen.Width - window.Width = 1920 - 300 = 1620
            Assert.AreEqual(1620.0, result.X);
        }

        /// <summary>
        /// Product code: Calculation.GetRawCoordinatesFromCaret — top edge flip
        /// What: Verifies toolbar is placed below the caret when there is no room above
        /// Why: When caret is near top of screen, toolbar must flip below to remain visible
        /// </summary>
        [TestMethod]
        public void GetRawCoordinatesFromCaret_NearTopEdge_ShouldPlaceBelow()
        {
            var caret = new Point(960, 30);
            var result = Calculation.GetRawCoordinatesFromCaret(caret, StandardScreen, ToolbarWindow);

            // top = 30 - 50 - 20 = -40, which is < screen.Y (0)
            // So Y should be caret.Y + 20 = 50 (placed below caret)
            Assert.AreEqual(50.0, result.Y);
        }

        /// <summary>
        /// Product code: Calculation.GetRawCoordinatesFromCaret — normal above placement
        /// What: Verifies toolbar is placed above the caret when there is sufficient space
        /// Why: Default behavior — toolbar should appear above to avoid occluding text below
        /// </summary>
        [TestMethod]
        public void GetRawCoordinatesFromCaret_SufficientSpaceAbove_ShouldPlaceAbove()
        {
            var caret = new Point(960, 200);
            var result = Calculation.GetRawCoordinatesFromCaret(caret, StandardScreen, ToolbarWindow);

            // top = 200 - 50 - 20 = 130, which is >= screen.Y (0)
            Assert.AreEqual(130.0, result.Y);
        }

        /// <summary>
        /// Product code: Calculation.GetRawCoordinatesFromCaret — multi-monitor offset
        /// What: Verifies toolbar respects non-zero screen origin (second monitor at X=1920)
        /// Why: Multi-monitor setups have offset screen coordinates — clamping must use screen.X
        /// </summary>
        [TestMethod]
        public void GetRawCoordinatesFromCaret_OffsetScreen_ShouldRespectScreenOrigin()
        {
            var screen = new Rect(1920, 0, 1920, 1080);
            var caret = new Point(1950, 540);
            var result = Calculation.GetRawCoordinatesFromCaret(caret, screen, ToolbarWindow);

            // left = 1950 - 150 = 1800, which is < screen.X (1920)
            // So X should be clamped to 1920
            Assert.AreEqual(1920.0, result.X);
        }

        /// <summary>
        /// Product code: Calculation.GetRawCoordinatesFromCaret — exact boundary case
        /// What: Verifies no clamping occurs when toolbar fits exactly at the boundary
        /// Why: Off-by-one errors in boundary checks are common — this catches ≤ vs &lt; bugs
        /// </summary>
        [TestMethod]
        public void GetRawCoordinatesFromCaret_ExactBoundary_ShouldNotClamp()
        {
            var caret = new Point(150, 100);
            var result = Calculation.GetRawCoordinatesFromCaret(caret, StandardScreen, ToolbarWindow);

            // left = 150 - 150 = 0, which == screen.X, so no left clamp needed
            Assert.AreEqual(0.0, result.X);

            // top = 100 - 50 - 20 = 30, which >= 0
            Assert.AreEqual(30.0, result.Y);
        }

        [TestMethod]
        [ExpectedException(typeof(NotImplementedException))]
        public void GetRawCoordinatesFromPosition_InvalidPosition_ShouldThrow()
        {
            Calculation.GetRawCoordinatesFromPosition((Position)999, StandardScreen, ToolbarWindow, 1.0);
        }

        /// <summary>
        /// Product code: Calculation.GetRawCoordinatesFromPosition with Position.Top
        /// What: Verifies toolbar is horizontally centered at the top of the screen with offset
        /// Why: Exact value test — catches formula regressions in the Top positioning branch
        /// </summary>
        [TestMethod]
        public void GetRawCoordinatesFromPosition_TopCenter_ShouldBeCenteredAtTop()
        {
            var result = Calculation.GetRawCoordinatesFromPosition(Position.Top, StandardScreen, ToolbarWindow, 1.0);

            // X: screen.X + screen.Width/2 - window.Width*dpi/2 = 0 + 960 - 150 = 810
            Assert.AreEqual(810.0, result.X);

            // Y: screen.Y + offset = 0 + 24 = 24
            Assert.AreEqual(24.0, result.Y);
        }

        /// <summary>
        /// Product code: Calculation.GetRawCoordinatesFromPosition with Position.Bottom
        /// What: Verifies toolbar is horizontally centered at the bottom of the screen
        /// Why: Bottom placement subtracts window height + offset — easy to get wrong
        /// </summary>
        [TestMethod]
        public void GetRawCoordinatesFromPosition_BottomCenter_ShouldBeCenteredAtBottom()
        {
            var result = Calculation.GetRawCoordinatesFromPosition(Position.Bottom, StandardScreen, ToolbarWindow, 1.0);

            // X: centered = 810
            Assert.AreEqual(810.0, result.X);

            // Y: screen.Y + screen.Height - (window.Height*dpi + offset) = 0 + 1080 - (50 + 24) = 1006
            Assert.AreEqual(1006.0, result.Y);
        }

        /// <summary>
        /// Product code: Calculation.GetRawCoordinatesFromPosition with Position.Center
        /// What: Verifies toolbar is centered both horizontally and vertically
        /// Why: Center is the simplest case — validates the baseline formula for both axes
        /// </summary>
        [TestMethod]
        public void GetRawCoordinatesFromPosition_Center_ShouldBeTrulyCentered()
        {
            var result = Calculation.GetRawCoordinatesFromPosition(Position.Center, StandardScreen, ToolbarWindow, 1.0);

            // X: screen.X + screen.Width/2 - window.Width*dpi/2 = 0 + 960 - 150 = 810
            Assert.AreEqual(810.0, result.X);

            // Y: screen.Y + screen.Height/2 - window.Height*dpi/2 = 0 + 540 - 25 = 515
            Assert.AreEqual(515.0, result.Y);
        }

        /// <summary>
        /// Product code: Calculation.GetRawCoordinatesFromPosition with Position.TopLeft
        /// What: Verifies toolbar is placed at top-left corner with offset margin
        /// Why: Corner placements use raw offset — validates the simplest X/Y path
        /// </summary>
        [TestMethod]
        public void GetRawCoordinatesFromPosition_TopLeft_ShouldBeAtTopLeftCorner()
        {
            var result = Calculation.GetRawCoordinatesFromPosition(Position.TopLeft, StandardScreen, ToolbarWindow, 1.0);

            // X: screen.X + offset = 0 + 24 = 24
            Assert.AreEqual(24.0, result.X);

            // Y: screen.Y + offset = 0 + 24 = 24
            Assert.AreEqual(24.0, result.Y);
        }

        /// <summary>
        /// Product code: Calculation.GetRawCoordinatesFromPosition with Position.TopRight
        /// What: Verifies exact coordinates for top-right corner placement
        /// Why: TopRight combines right-aligned X with top Y — validates both formula branches
        /// </summary>
        [TestMethod]
        public void GetRawCoordinatesFromPosition_TopRight_ShouldBeAtTopRightCorner()
        {
            var result = Calculation.GetRawCoordinatesFromPosition(Position.TopRight, StandardScreen, ToolbarWindow, 1.0);

            // X: screen.X + screen.Width - (window.Width*dpi + offset) = 0 + 1920 - (300 + 24) = 1596
            Assert.AreEqual(1596.0, result.X);

            // Y: screen.Y + offset = 0 + 24 = 24
            Assert.AreEqual(24.0, result.Y);
        }

        /// <summary>
        /// Product code: Calculation.GetRawCoordinatesFromPosition with Position.BottomLeft
        /// What: Verifies exact coordinates for bottom-left corner placement
        /// Why: BottomLeft combines left-aligned X with bottom Y — validates both formula branches
        /// </summary>
        [TestMethod]
        public void GetRawCoordinatesFromPosition_BottomLeft_ShouldBeAtBottomLeftCorner()
        {
            var result = Calculation.GetRawCoordinatesFromPosition(Position.BottomLeft, StandardScreen, ToolbarWindow, 1.0);

            // X: screen.X + offset = 0 + 24 = 24
            Assert.AreEqual(24.0, result.X);

            // Y: screen.Y + screen.Height - (window.Height*dpi + offset) = 0 + 1080 - (50 + 24) = 1006
            Assert.AreEqual(1006.0, result.Y);
        }

        /// <summary>
        /// Product code: Calculation.GetRawCoordinatesFromPosition with Position.BottomRight
        /// What: Verifies toolbar is placed at bottom-right corner with offset margin
        /// Why: BottomRight uses the most complex formula (subtracts from both edges)
        /// </summary>
        [TestMethod]
        public void GetRawCoordinatesFromPosition_BottomRight_ShouldBeAtBottomRightCorner()
        {
            var result = Calculation.GetRawCoordinatesFromPosition(Position.BottomRight, StandardScreen, ToolbarWindow, 1.0);

            // X: screen.X + screen.Width - (window.Width*dpi + offset) = 0 + 1920 - (300 + 24) = 1596
            Assert.AreEqual(1596.0, result.X);

            // Y: screen.Y + screen.Height - (window.Height*dpi + offset) = 0 + 1080 - (50 + 24) = 1006
            Assert.AreEqual(1006.0, result.Y);
        }

        /// <summary>
        /// Product code: Calculation.GetRawCoordinatesFromPosition with Position.Left
        /// What: Verifies toolbar is placed at left edge, vertically centered
        /// Why: Left position uses offset for X and centering for Y — tests the mix
        /// </summary>
        [TestMethod]
        public void GetRawCoordinatesFromPosition_Left_ShouldBeAtLeftMiddle()
        {
            var result = Calculation.GetRawCoordinatesFromPosition(Position.Left, StandardScreen, ToolbarWindow, 1.0);

            // X: screen.X + offset = 0 + 24 = 24
            Assert.AreEqual(24.0, result.X);

            // Y: centered vertically = 0 + 540 - 25 = 515
            Assert.AreEqual(515.0, result.Y);
        }

        /// <summary>
        /// Product code: Calculation.GetRawCoordinatesFromPosition with Position.Right
        /// What: Verifies toolbar is placed at right edge, vertically centered
        /// Why: Right position subtracts window width from screen edge — tests the right-align formula
        /// </summary>
        [TestMethod]
        public void GetRawCoordinatesFromPosition_Right_ShouldBeAtRightMiddle()
        {
            var result = Calculation.GetRawCoordinatesFromPosition(Position.Right, StandardScreen, ToolbarWindow, 1.0);

            // X: screen.X + screen.Width - (window.Width*dpi + offset) = 1920 - 324 = 1596
            Assert.AreEqual(1596.0, result.X);

            // Y: centered vertically = 515
            Assert.AreEqual(515.0, result.Y);
        }

        /// <summary>
        /// Product code: Calculation.GetRawCoordinatesFromPosition with DPI=1.5
        /// What: Verifies that DPI scaling is applied to window size in centering formula
        /// Why: High-DPI monitors are common — incorrect scaling makes toolbar appear off-center
        /// </summary>
        [TestMethod]
        public void GetRawCoordinatesFromPosition_WithHighDpi_ShouldScaleWindow()
        {
            double dpi = 1.5;
            var result = Calculation.GetRawCoordinatesFromPosition(Position.Center, StandardScreen, ToolbarWindow, dpi);

            // X: screen.X + screen.Width/2 - window.Width*dpi/2 = 0 + 960 - (300*1.5)/2 = 960 - 225 = 735
            Assert.AreEqual(735.0, result.X);

            // Y: screen.Y + screen.Height/2 - window.Height*dpi/2 = 0 + 540 - (50*1.5)/2 = 540 - 37.5 = 502.5
            Assert.AreEqual(502.5, result.Y);
        }

        /// <summary>
        /// Product code: Calculation.GetRawCoordinatesFromPosition with DPI=2.0, TopLeft
        /// What: Verifies that offset is NOT DPI-scaled for corner positions
        /// Why: The 24px offset is a fixed margin — DPI scaling only affects window size
        /// </summary>
        [TestMethod]
        public void GetRawCoordinatesFromPosition_WithDpi2_TopLeft_ShouldUseOffset()
        {
            double dpi = 2.0;
            var result = Calculation.GetRawCoordinatesFromPosition(Position.TopLeft, StandardScreen, ToolbarWindow, dpi);

            // X: screen.X + offset = 0 + 24 = 24 (offset is not DPI-scaled)
            Assert.AreEqual(24.0, result.X);

            // Y: screen.Y + offset = 0 + 24 = 24
            Assert.AreEqual(24.0, result.Y);
        }

        /// <summary>
        /// Product code: Calculation.GetRawCoordinatesFromPosition with DPI=2.0, BottomRight
        /// What: Verifies DPI scaling of window size in bottom-right corner formula
        /// Why: At 2x DPI, the window occupies 600×100 pixels — offset from edge must account for this
        /// </summary>
        [TestMethod]
        public void GetRawCoordinatesFromPosition_WithDpi2_BottomRight_ShouldScaleWindowSize()
        {
            double dpi = 2.0;
            var result = Calculation.GetRawCoordinatesFromPosition(Position.BottomRight, StandardScreen, ToolbarWindow, dpi);

            // X: screen.X + screen.Width - (window.Width*dpi + offset) = 0 + 1920 - (300*2 + 24) = 1920 - 624 = 1296
            Assert.AreEqual(1296.0, result.X);

            // Y: screen.Y + screen.Height - (window.Height*dpi + offset) = 0 + 1080 - (50*2 + 24) = 1080 - 124 = 956
            Assert.AreEqual(956.0, result.Y);
        }

        /// <summary>
        /// Product code: Calculation.GetRawCoordinatesFromPosition with offset screen origin
        /// What: Verifies screen.X is added as the base for TopLeft on a secondary monitor
        /// Why: Without adding screen origin, toolbar appears on the wrong monitor
        /// </summary>
        [TestMethod]
        public void GetRawCoordinatesFromPosition_OffsetScreen_ShouldRespectScreenOrigin()
        {
            var screen = new Rect(1920, 0, 1920, 1080);
            var result = Calculation.GetRawCoordinatesFromPosition(Position.TopLeft, screen, ToolbarWindow, 1.0);

            // X: screen.X + offset = 1920 + 24 = 1944
            Assert.AreEqual(1944.0, result.X);

            // Y: screen.Y + offset = 0 + 24 = 24
            Assert.AreEqual(24.0, result.Y);
        }
    }
}
