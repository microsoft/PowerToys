// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScreenRuler.UITests
{
    [OSCondition(OperatingSystems.Windows)]
    [TestClass]
    public class TestBounds : UITestBase
    {
        public TestBounds()
            : base(PowerToysModule.PowerToysSettings, WindowSize.Large)
        {
        }

        [TestMethod(DisplayName = "ScreenRuler.BoundsTool")]
        [TestCategory("Spacing")]
        public void TestScreenRulerBoundsTool()
        {
            TestHelper.InitializeTest(this, "bounds test");
            TestHelper.PerformBoundsToolTest(this);
            TestHelper.CleanupTest(this);
        }

        [TestMethod(DisplayName = "ScreenRuler.BoundsToolDipClipboard")]
        [TestCategory("Spacing")]
        public void TestScreenRulerBoundsToolDipClipboard()
        {
            TestHelper.InitializeTest(this, "bounds DIP test");
            TestHelper.SetExtraMeasurementUnit(this, "Display-independent pixels (DIP)");
            TestHelper.PerformBoundsToolTest(this, "DIP");
            TestHelper.SetExtraMeasurementUnit(this, "Show only pixels");
            TestHelper.CleanupTest(this);
        }
    }
}
