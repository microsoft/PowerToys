// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

[TestClass]
public class UIInitializeEditorParamsVerifyMonitorResolutionTests : UIInitializeTestBase
{
    public UIInitializeEditorParamsVerifyMonitorResolutionTests()
        : base(EditorTestData.WriteForUIInitializeEditorParamsVerifyMonitorResolution)
    {
    }

    [TestMethod]
    public void EditorParams_VerifyMonitorResolution()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        SelectAndAssertMonitor(this, Session, Monitor1);
        var monitor = FindMonitorByExactName(Session, Monitor1);
        var resolution = monitor.Find<TextBlock>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.ResolutionText));

        Assert.AreEqual("1920 × 1080", resolution.Text);
    }
}
