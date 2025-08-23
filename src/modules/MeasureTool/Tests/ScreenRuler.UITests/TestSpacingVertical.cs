// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScreenRuler.UITests
{
    [TestClass]
    public class TestSpacingVertical : UITestBase
    {
        public TestSpacingVertical()
            : base(PowerToysModule.PowerToysSettings, WindowSize.Large)
        {
        }

        [TestMethod("ScreenRuler.VerticalSpacingTool")]
        [TestCategory("Spacing")]
        public void TestScreenRulerVerticalSpacingTool()
        {
            TestHelper.InitializeTest(this, "vertical spacing test");
            TestHelper.PerformSpacingToolTest(this, TestHelper.VerticalSpacingButtonName, "Vertical Spacing");
            TestHelper.CleanupTest(this);
        }
    }
}
