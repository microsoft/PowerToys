// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ShortcutGuide.UITests;

[TestClass]
public class ShortcutGuideSettingsTests : UITestBase
{
    private bool? originalEnabled;

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
    public void ToggleEnablesAndDisablesProcess()
    {
        TestHelper.NavigateToSettingsPage(this);
        originalEnabled = TestHelper.GetEnableToggle(this).IsOn;

        TestHelper.SetEnabled(this, enabled: true);
        TestHelper.SetEnabled(this, enabled: false);
        TestHelper.SetEnabled(this, enabled: true);
        TestHelper.VerifyHiddenOverlay(this);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();
        if (originalEnabled.HasValue)
        {
            TestHelper.CleanupTest(this, originalEnabled.Value);
        }
    }
}
