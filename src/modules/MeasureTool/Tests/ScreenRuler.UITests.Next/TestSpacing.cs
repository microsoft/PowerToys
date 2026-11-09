// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScreenRuler.UITests.Next;

[TestClass]
public class TestSpacing : UITestBase
{
    public TestSpacing()
        : base(PowerToysModule.PowerToysSettings, WindowSize.Large, enableModules: new[] { TestHelper.ModuleSettingsKey })
    {
    }

    [TestMethod]
    [TestCategory("Spacing")]
    public void TestScreenRulerSpacingTool()
    {
        TestHelper.InitializeTest(this, "spacing test");
        try
        {
            TestHelper.PerformSpacingToolTest(this, TestHelper.SpacingButtonName, "Spacing");
        }
        finally
        {
            TestHelper.CleanupTest(this);
        }
    }
}
