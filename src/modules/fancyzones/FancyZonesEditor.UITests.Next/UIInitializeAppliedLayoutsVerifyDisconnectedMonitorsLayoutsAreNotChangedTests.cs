// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using FancyZonesEditorCommon.Data;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

[TestClass]
public class UIInitializeAppliedLayoutsVerifyDisconnectedMonitorsLayoutsAreNotChangedTests : UIInitializeTestBase
{
    public UIInitializeAppliedLayoutsVerifyDisconnectedMonitorsLayoutsAreNotChangedTests()
        : base(EditorTestData.WriteForUIInitializeAppliedLayoutsVerifyDisconnectedMonitorsLayoutsAreNotChanged)
    {
    }

    [TestMethod]
    public void AppliedLayouts_VerifyDisconnectedMonitorsLayoutsAreNotChanged()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        FindLayoutByExactName(Session, EditorUiTestHelper.TemplateLayoutName.Rows).Click();

        var data = ReadAppliedLayoutsDurably(
            this,
            applied => applied.AppliedLayouts.Count == 3,
            "updating connected monitor while preserving disconnected monitor layouts");

        Assert.IsNotNull(data.AppliedLayouts.Find(x => x.Device.Monitor == "monitor-1"));
        Assert.IsNotNull(data.AppliedLayouts.Find(x => x.Device.Monitor == "monitor-2"));
        Assert.IsNotNull(data.AppliedLayouts.Find(x => x.Device.Monitor == "monitor-3"));
    }
}
