// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScreenRuler.UITests
{
    [TestClass]
    public class TestSpacingHorizontal : UITestBase
    {
        public TestSpacingHorizontal()
            : base(PowerToysModule.PowerToysSettings, WindowSize.Large)
        {
        }

        [TestMethod("ScreenRuler.HorizontalSpacingTool")]
        [TestCategory("Spacing")]
        public void TestScreenRulerHorizontalSpacingTool()
        {
            TestHelper.InitializeTest(this, "horizontal spacing test");
            TestHelper.PerformSpacingToolTest(this, TestHelper.HorizontalSpacingButtonName, "Horizontal Spacing");
            TestHelper.CleanupTest(this);
        }
    }
}
