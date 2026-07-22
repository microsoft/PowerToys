// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ColorPicker.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace ColorPicker.UnitTests.Views
{
    [TestClass]
    public class ZoomViewTests
    {
        [TestMethod]
        public void Cursor_sample_patch_is_centered_and_survives_the_largest_grow_animation()
        {
            Rect bounds = ZoomView.GetCursorSamplePatchBounds(200, 160);

            Assert.AreEqual(98.0, bounds.X, 0.001);
            Assert.AreEqual(78.0, bounds.Y, 0.001);
            Assert.AreEqual(4.0, bounds.Width, 0.001);
            Assert.AreEqual(4.0, bounds.Height, 0.001);

            // The largest grow is from the factor-4 card (208 DIPs) to factor 8 (408 DIPs).
            // Even at its first animation frame and 96 DPI, the patch must remain at least
            // two physical pixels wide around the screen sample point.
            const double minimumAnimationScale = 208.0 / 408.0;
            Assert.IsTrue(bounds.Width * minimumAnimationScale >= 2.0);
        }
    }
}
