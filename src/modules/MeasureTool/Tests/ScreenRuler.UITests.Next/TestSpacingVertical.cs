// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScreenRuler.UITests.Next;

[TestClass]
public class TestSpacingVertical : UITestBase
{
    public TestSpacingVertical()
        : base(PowerToysModule.PowerToysSettings, WindowSize.Large, enableModules: new[] { TestHelper.ModuleSettingsKey })
    {
    }

    [TestMethod]
    [TestCategory("Spacing")]
    public void TestScreenRulerVerticalSpacingTool()
    {
        TestHelper.InitializeTest(this, "vertical spacing test");
        try
        {
            TestHelper.PerformSpacingToolTest(this, TestHelper.VerticalSpacingButtonName, "Vertical Spacing");
        }
        finally
        {
            TestHelper.CleanupTest(this);
        }
    }
}
