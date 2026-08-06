// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScreenRuler.UITests.Next;

[TestClass]
public class TestSpacingHorizontal : UITestBase
{
    public TestSpacingHorizontal()
        : base(PowerToysModule.PowerToysSettings, WindowSize.Large, enableModules: new[] { TestHelper.ModuleSettingsKey })
    {
    }

    [TestMethod]
    [TestCategory("Spacing")]
    public void TestScreenRulerHorizontalSpacingTool()
    {
        TestHelper.InitializeTest(this, "horizontal spacing test");
        try
        {
            TestHelper.PerformSpacingToolTest(this, TestHelper.HorizontalSpacingButtonName, "Horizontal Spacing");
        }
        finally
        {
            TestHelper.CleanupTest(this);
        }
    }
}
