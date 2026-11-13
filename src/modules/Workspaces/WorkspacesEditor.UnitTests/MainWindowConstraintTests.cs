// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WorkspacesEditor.UnitTests
{
    /// <summary>
    /// Tests for MainWindow configuration constants and constraints.
    /// </summary>
    [TestClass]
    public class MainWindowConstraintTests
    {
        [TestMethod]
        [TestCategory("Window.Constraints")]
        public void MinWindowWidth_IsAtLeast750()
        {
            Assert.IsTrue(MainWindow.MinWindowWidth >= 750, "Min width must be at least 750 to fit all UI elements.");
        }

        [TestMethod]
        [TestCategory("Window.Constraints")]
        public void MinWindowHeight_IsAtLeast680()
        {
            Assert.IsTrue(MainWindow.MinWindowHeight >= 680, "Min height must be at least 680 to fit all UI elements.");
        }

        [TestMethod]
        [TestCategory("Window.Constraints")]
        public void MinWindowDimensions_AreReasonable()
        {
            // Ensure min size isn't accidentally set too large (e.g., exceeding common displays)
            Assert.IsTrue(MainWindow.MinWindowWidth <= 1024, "Min width should not exceed 1024.");
            Assert.IsTrue(MainWindow.MinWindowHeight <= 768, "Min height should not exceed 768.");
        }
    }
}
