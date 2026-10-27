// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ShortcutGuide.UITests;

[TestClass]
public class ShortcutGuideActivationTests : UITestBase
{
    private bool? originalEnabled;

    public ShortcutGuideActivationTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: [TestHelper.ModuleSettingsKey])
    {
    }

    [TestMethod]
    [TestCategory("ShortcutGuide")]
    [TestCategory("Activation")]
    public void ActivationShortcutShowsAndDismissesOverlay()
    {
        TestHelper.NavigateToSettingsPage(this);
        originalEnabled = TestHelper.GetEnableToggle(this).IsOn;

        TestHelper.SetEnabled(this, enabled: true);
        var keys = TestHelper.ReadActivationShortcut(this);
        TestHelper.VerifyHiddenOverlay(this);

        var overlay = TestHelper.ActivateOverlay(this, keys);
        TestHelper.DismissOverlay(this, overlay);

        TestHelper.SetEnabled(this, enabled: false);
        TestHelper.VerifyDisabledShortcut(this, keys);

        TestHelper.SetEnabled(this, enabled: true);
        overlay = TestHelper.ActivateOverlay(this, keys);
        TestHelper.DismissOverlay(this, overlay);
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
