// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

[TestClass]
public class UIInitializeEditorParamsVerifyMonitorScalingTests : UIInitializeTestBase
{
    public UIInitializeEditorParamsVerifyMonitorScalingTests()
        : base(EditorTestData.WriteForUIInitializeEditorParamsVerifyMonitorScaling)
    {
    }

    [TestMethod]
    public void EditorParams_VerifyMonitorScaling()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        SelectAndAssertMonitor(this, Session, Monitor1);
        var monitor = FindMonitorByExactName(Session, Monitor1);
        var scaling = monitor.Find<TextBlock>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.ScalingText));

        Assert.AreEqual("200%", scaling.Text);
    }
}
