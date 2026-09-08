// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

[TestClass]
public class NewFancyZonesEditorFirstLaunchTests : FancyZonesEditorTestBase
{
    public NewFancyZonesEditorFirstLaunchTests()
    {
        EditorTestData.WriteForFirstLaunchTests(Files);
    }

    [TestMethod]
    public void FirstLaunch()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        EditorUiTestHelper.Step(this, "Verifying the FancyZones Layout surface for the nested first-launch scenario");
        var layoutSurface = WindowsFinder.WaitForWindowByApp(
            "PowerToys.FancyZonesEditor",
            window => string.Equals(window.Title, "FancyZones Layout", StringComparison.Ordinal) && window.Width > 200 && window.Height > 200,
            timeoutMS: 10_000);
        Assert.IsNotNull(layoutSurface, "The editor did not initialize its FancyZones Layout surface window.");
    }
}
