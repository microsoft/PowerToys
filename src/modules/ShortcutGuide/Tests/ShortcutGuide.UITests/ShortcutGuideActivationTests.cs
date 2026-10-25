// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ShortcutGuide.UITests;

[TestClass]
public class ShortcutGuideActivationTests : UITestBase
{
    public ShortcutGuideActivationTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: [TestHelper.ModuleSettingsKey])
    {
    }

    [TestMethod]
    [TestCategory("ShortcutGuide")]
    [TestCategory("Activation")]
    public async Task ActivationShortcutShowsAndDismissesOverlay()
    {
        TestHelper.NavigateToSettingsPage(this);
        var originalEnabled = TestHelper.GetEnableToggle(this).IsOn;

        try
        {
            TestHelper.SetEnabled(this, enabled: true);
            var keys = TestHelper.ReadActivationShortcut(this);
            Assert.IsTrue(
                TestHelper.WaitForOverlay(visible: false),
                "Enabling Shortcut Guide must not display the overlay before its shortcut is pressed.");

            var overlay = TestHelper.ActivateOverlay(this, keys);
            TestHelper.DismissOverlay(this, overlay);

            TestHelper.SetEnabled(this, enabled: false);
            TestHelper.VerifyDisabledShortcut(this, keys);

            TestHelper.SetEnabled(this, enabled: true);
            overlay = TestHelper.ActivateOverlay(this, keys);
            TestHelper.DismissOverlay(this, overlay);
        }
        finally
        {
            await CaptureFailureArtifactsBeforeCleanupAsync();
            TestHelper.CleanupTest(this, originalEnabled);
        }
    }
}
