// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShowDesktop.UnitTests
{
    [TestClass]
    public class DesktopDetectorTests
    {
        [TestMethod]
        public void IsDesktopWindow_ZeroHandle_ReturnsFalse()
        {
            bool result = DesktopDetector.IsDesktopWindow(IntPtr.Zero);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsDesktopWindow_InvalidHandle_ReturnsFalse()
        {
            // An invalid, non-zero handle should not be detected as a desktop window
            // since GetClassName will return empty for invalid handles
            bool result = DesktopDetector.IsDesktopWindow(new IntPtr(0x7FFFFFFF));
            Assert.IsFalse(result);
        }
    }
}
