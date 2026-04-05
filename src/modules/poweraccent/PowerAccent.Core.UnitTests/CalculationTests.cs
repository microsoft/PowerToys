// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        [TestMethod]
        public void GetRawCoordinatesFromCaret_NearLeftEdge_ShouldClampToScreenLeft()
        {
            // Caret near left edge - toolbar would extend past screen left
            var caret = new Point(50, 540);
            var result = Calculation.GetRawCoordinatesFromCaret(caret, StandardScreen, ToolbarWindow);

            // left = 50 - 150 = -100, which is < screen.X (0), so X should be clamped to 0
            Assert.AreEqual(0.0, result.X);
        }

        [TestMethod]
        public void GetRawCoordinatesFromCaret_NearRightEdge_ShouldClampToScreenRight()
        {
            // Caret near right edge - toolbar would extend past screen right
            var caret = new Point(1900, 540);
            var result = Calculation.GetRawCoordinatesFromCaret(caret, StandardScreen, ToolbarWindow);

            // left = 1900 - 150 = 1750, left + window.Width = 1750 + 300 = 2050 > 1920
            // So X should be clamped to screen.X + screen.Width - window.Width = 1920 - 300 = 1620
            Assert.AreEqual(1620.0, result.X);
        }

        [TestMethod]
        public void GetRawCoordinatesFromCaret_NearTopEdge_ShouldPlaceBelow()
        {
            // Caret near top edge - toolbar above would go off-screen
            var caret = new Point(960, 30);
            var result = Calculation.GetRawCoordinatesFromCaret(caret, StandardScreen, ToolbarWindow);

            // top = 30 - 50 - 20 = -40, which is < screen.Y (0)
            // So Y should be caret.Y + 20 = 50 (placed below caret)
            Assert.AreEqual(50.0, result.Y);
        }

        [TestMethod]
        public void GetRawCoordinatesFromCaret_SufficientSpaceAbove_ShouldPlaceAbove()
        {
            var caret = new Point(960, 200);
            var result = Calculation.GetRawCoordinatesFromCaret(caret, StandardScreen, ToolbarWindow);

            // top = 200 - 50 - 20 = 130, which is >= screen.Y (0)
            Assert.AreEqual(130.0, result.Y);
        }

        [TestMethod]
        public void GetRawCoordinatesFromCaret_OffsetScreen_ShouldRespectScreenOrigin()
        {
            // Simulate a second monitor offset at position (1920, 0)
            var screen = new Rect(1920, 0, 1920, 1080);
            var caret = new Point(1950, 540);
            var result = Calculation.GetRawCoordinatesFromCaret(caret, screen, ToolbarWindow);

            // left = 1950 - 150 = 1800, which is < screen.X (1920)
            // So X should be clamped to 1920
            Assert.AreEqual(1920.0, result.X);
        }

        [TestMethod]
        public void GetRawCoordinatesFromCaret_ExactBoundary_ShouldNotClamp()
        {
            // Caret positioned exactly so toolbar fits perfectly
            var caret = new Point(150, 100);
            var result = Calculation.GetRawCoordinatesFromCaret(caret, StandardScreen, ToolbarWindow);

            // left = 150 - 150 = 0, which == screen.X, so no left clamp needed
            // left + window.Width = 0 + 300 = 300, which <= 1920, so no right clamp
            Assert.AreEqual(0.0, result.X);

            // top = 100 - 50 - 20 = 30, which >= 0
            Assert.AreEqual(30.0, result.Y);
        }

        [TestMethod]
        [DataRow(Position.Top)]
        [DataRow(Position.Bottom)]
        [DataRow(Position.Left)]
        [DataRow(Position.Right)]
        [DataRow(Position.TopLeft)]
        [DataRow(Position.TopRight)]
        [DataRow(Position.BottomLeft)]
        [DataRow(Position.BottomRight)]
        [DataRow(Position.Center)]
        public void GetRawCoordinatesFromPosition_AllPositions_ShouldNotThrow(Position position)
        {
            var result = Calculation.GetRawCoordinatesFromPosition(position, StandardScreen, ToolbarWindow, 1.0);

            // Should return a valid point within reasonable range
            Assert.IsTrue(!double.IsNaN(result.X));
            Assert.IsTrue(!double.IsNaN(result.Y));
        }

        [TestMethod]
        public void GetRawCoordinatesFromPosition_TopCenter_ShouldBeCenteredAtTop()
        {
            var result = Calculation.GetRawCoordinatesFromPosition(Position.Top, StandardScreen, ToolbarWindow, 1.0);

            // X: screen.X + screen.Width/2 - window.Width*dpi/2 = 0 + 960 - 150 = 810
            Assert.AreEqual(810.0, result.X);

            // Y: screen.Y + offset = 0 + 24 = 24
            Assert.AreEqual(24.0, result.Y);
        }

        [TestMethod]
        public void GetRawCoordinatesFromPosition_BottomCenter_ShouldBeCenteredAtBottom()
        {
            var result = Calculation.GetRawCoordinatesFromPosition(Position.Bottom, StandardScreen, ToolbarWindow, 1.0);

            // X: centered = 810
            Assert.AreEqual(810.0, result.X);

            // Y: screen.Y + screen.Height - (window.Height*dpi + offset) = 0 + 1080 - (50 + 24) = 1006
            Assert.AreEqual(1006.0, result.Y);
        }

        [TestMethod]
        public void GetRawCoordinatesFromPosition_Center_ShouldBeTrulyCentered()
        {
            var result = Calculation.GetRawCoordinatesFromPosition(Position.Center, StandardScreen, ToolbarWindow, 1.0);

            // X: screen.X + screen.Width/2 - window.Width*dpi/2 = 0 + 960 - 150 = 810
            Assert.AreEqual(810.0, result.X);

            // Y: screen.Y + screen.Height/2 - window.Height*dpi/2 = 0 + 540 - 25 = 515
            Assert.AreEqual(515.0, result.Y);
        }

        [TestMethod]
        public void GetRawCoordinatesFromPosition_TopLeft_ShouldBeAtTopLeftCorner()
        {
            var result = Calculation.GetRawCoordinatesFromPosition(Position.TopLeft, StandardScreen, ToolbarWindow, 1.0);

            // X: screen.X + offset = 0 + 24 = 24
            Assert.AreEqual(24.0, result.X);

            // Y: screen.Y + offset = 0 + 24 = 24
            Assert.AreEqual(24.0, result.Y);
        }

        [TestMethod]
        public void GetRawCoordinatesFromPosition_BottomRight_ShouldBeAtBottomRightCorner()
        {
            var result = Calculation.GetRawCoordinatesFromPosition(Position.BottomRight, StandardScreen, ToolbarWindow, 1.0);

            // X: screen.X + screen.Width - (window.Width*dpi + offset) = 0 + 1920 - (300 + 24) = 1596
            Assert.AreEqual(1596.0, result.X);

            // Y: screen.Y + screen.Height - (window.Height*dpi + offset) = 0 + 1080 - (50 + 24) = 1006
            Assert.AreEqual(1006.0, result.Y);
        }

        [TestMethod]
        public void GetRawCoordinatesFromPosition_Left_ShouldBeAtLeftMiddle()
        {
            var result = Calculation.GetRawCoordinatesFromPosition(Position.Left, StandardScreen, ToolbarWindow, 1.0);

            // X: screen.X + offset = 0 + 24 = 24
            Assert.AreEqual(24.0, result.X);

            // Y: centered vertically = 0 + 540 - 25 = 515
            Assert.AreEqual(515.0, result.Y);
        }

        [TestMethod]
        public void GetRawCoordinatesFromPosition_Right_ShouldBeAtRightMiddle()
        {
            var result = Calculation.GetRawCoordinatesFromPosition(Position.Right, StandardScreen, ToolbarWindow, 1.0);

            // X: screen.X + screen.Width - (window.Width*dpi + offset) = 1920 - 324 = 1596
            Assert.AreEqual(1596.0, result.X);

            // Y: centered vertically = 515
            Assert.AreEqual(515.0, result.Y);
        }

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
