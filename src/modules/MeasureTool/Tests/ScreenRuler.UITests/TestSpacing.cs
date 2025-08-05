// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScreenRuler.UITests
{
    [TestClass]
    public class TestSpacing : UITestBase
    {
        public TestSpacing()
            : base(PowerToysModule.PowerToysSettings, WindowSize.Large)
        {
        }

        [TestMethod("ScreenRuler.SpacingTool")]
        [TestCategory("Spacing")]
        public void TestScreenRulerSpacingTool()
        {
            TestHelper.InitializeTest(this, "spacing test");
            TestHelper.PerformSpacingToolTest(this, TestHelper.SpacingButtonName, "Spacing");
            TestHelper.CleanupTest(this);
        }
    }
}
