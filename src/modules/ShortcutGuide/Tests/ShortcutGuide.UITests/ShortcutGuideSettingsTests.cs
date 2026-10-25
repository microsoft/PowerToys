// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ShortcutGuide.UITests;

[TestClass]
public class ShortcutGuideSettingsTests : UITestBase
{
    public ShortcutGuideSettingsTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: [TestHelper.ModuleSettingsKey])
    {
    }

    [TestMethod]
    [TestCategory("ShortcutGuide")]
    [TestCategory("Settings")]
    public void NavigateToPageAndReadShortcut()
    {
        TestHelper.NavigateToSettingsPage(this);
        Assert.IsTrue(TestHelper.GetEnableToggle(this).IsOn, "Shortcut Guide should start enabled.");
        TestHelper.ReadActivationShortcut(this);
    }

    [TestMethod]
    [TestCategory("ShortcutGuide")]
    [TestCategory("Settings")]
    public async Task ToggleEnablesAndDisablesProcess()
    {
        TestHelper.NavigateToSettingsPage(this);
        var originalEnabled = TestHelper.GetEnableToggle(this).IsOn;

        try
        {
            TestHelper.SetEnabled(this, enabled: true);
            TestHelper.SetEnabled(this, enabled: false);
            TestHelper.SetEnabled(this, enabled: true);
            Assert.IsTrue(
                TestHelper.WaitForOverlay(visible: false),
                "Re-enabling Shortcut Guide should start its listener without displaying the overlay.");
        }
        finally
        {
            await CaptureFailureArtifactsBeforeCleanupAsync();
            TestHelper.CleanupTest(this, originalEnabled);
        }
    }
}
