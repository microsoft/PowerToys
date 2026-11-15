// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

[TestClass]
public class UIInitializeEditorParamsVerifySelectedMonitorTests : UIInitializeTestBase
{
    public UIInitializeEditorParamsVerifySelectedMonitorTests()
        : base(EditorTestData.WriteForUIInitializeEditorParamsVerifySelectedMonitor)
    {
    }

    [TestMethod("FancyZonesEditor.Basic.EditorParams_VerifySelectedMonitor")]
    [TestCategory("FancyZones Editor #10")]
    public void EditorParams_VerifySelectedMonitor()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        EditorUiTestHelper.Step(this, "Clicking monitors in sequence to verify persisted selection");
        FindMonitorByExactName(Session, Monitor1).Click();
        FindMonitorByExactName(Session, Monitor2).Click();

        Assert.IsFalse(FindMonitorByExactName(Session, Monitor1).Selected);
        Assert.IsTrue(FindMonitorByExactName(Session, Monitor2).Selected);
    }
}
