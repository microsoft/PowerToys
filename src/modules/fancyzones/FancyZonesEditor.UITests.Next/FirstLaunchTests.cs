// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

[TestClass]
public class FirstLaunchTests : FancyZonesEditorTestBase
{
    public FirstLaunchTests()
    {
        EditorTestData.WriteForFirstLaunchTests(Files);
    }

    [TestMethod]
    [TestCategory("FancyZonesEditor")]
    public void FirstLaunch()
    {
        Assert.IsTrue(
            Session.WaitForElement(By.AccessibilityId("MainWindow1"), 30_000),
            "The editor process started but its main window was not ready for automation.");
        Assert.IsTrue(
            Session.WaitForElement(By.AccessibilityId("Monitors"), 30_000),
            "The editor opened but did not render its monitor list.");
        Assert.IsTrue(
            Session.WaitForElement(By.AccessibilityId("NewLayoutButton"), 30_000),
            "The editor opened but did not render the new-layout button.");
    }
}