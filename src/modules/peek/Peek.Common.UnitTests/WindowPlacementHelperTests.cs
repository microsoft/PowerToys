// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Peek.Common.Constants;
using Peek.UI.Helpers;

namespace Peek.Common.UnitTests
{
    [TestClass]
    public class WindowPlacementHelperTests
    {
        [TestMethod]
        [DataRow(120, 168, 3840, 2160)]
        [DataRow(168, 120, 1920, 1080)]
        [DataRow(120, 120, 1920, 1080)]
        public void Apply_DpiTransition_PreservesFinalPhysicalBounds(int initialDpi, int targetDpi, int monitorWidth, int monitorHeight)
        {
            var window = new DpiChangingWindow(initialDpi, targetDpi);
            int width = (int)Math.Round(monitorWidth * WindowConstants.MaxWindowToMonitorRatio);
            int height = (int)Math.Round(monitorHeight * WindowConstants.MaxWindowToMonitorRatio);

            WindowPlacementHelper.Apply(
                initialDpi != targetDpi,
                window.Move,
                () => window.Resize(width, height));

            Assert.AreEqual(targetDpi, window.Dpi);
            Assert.AreEqual(width, window.Width);
            Assert.AreEqual(height, window.Height);
            Assert.AreEqual(initialDpi != targetDpi ? 1 : 0, window.MoveCount);
            Assert.AreEqual(1, window.ResizeCount);
        }

        [TestMethod]
        public void Apply_LargerTargetDpi_DoesNotRepeatCombinedMoveResizeOversizing()
        {
            var combinedMoveResize = new DpiChangingWindow(120, 168);
            combinedMoveResize.Resize(3072, 1728);
            Assert.IsTrue(combinedMoveResize.Width > 3840, "The simulation must reproduce double-scaling of the intended 4K bounds.");

            var separateMoveResize = new DpiChangingWindow(120, 168);
            WindowPlacementHelper.Apply(true, separateMoveResize.Move, () => separateMoveResize.Resize(3072, 1728));

            Assert.AreEqual(3072, separateMoveResize.Width);
            Assert.AreEqual(1728, separateMoveResize.Height);
        }

        private sealed class DpiChangingWindow(int initialDpi, int targetDpi)
        {
            public int Dpi { get; private set; } = initialDpi;

            public int Width { get; private set; } = 500;

            public int Height { get; private set; } = 500;

            public int MoveCount { get; private set; }

            public int ResizeCount { get; private set; }

            public void Move()
            {
                MoveCount++;
                ApplyDpiChange();
            }

            public void Resize(int width, int height)
            {
                ResizeCount++;
                Width = width;
                Height = height;
                ApplyDpiChange();
            }

            private void ApplyDpiChange()
            {
                // Model WinUI applying its DPI-change bounds adjustment to the current
                // window size, including when relocation and sizing happen together.
                double ratio = (double)targetDpi / Dpi;
                Width = (int)Math.Round(Width * ratio, MidpointRounding.AwayFromZero);
                Height = (int)Math.Round(Height * ratio, MidpointRounding.AwayFromZero);
                Dpi = targetDpi;
            }
        }
    }
}
