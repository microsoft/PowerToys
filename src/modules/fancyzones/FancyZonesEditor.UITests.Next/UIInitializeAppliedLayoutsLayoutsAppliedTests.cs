// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

[TestClass]
public class UIInitializeAppliedLayoutsLayoutsAppliedTests : UIInitializeTestBase
{
    private const string CustomLayoutName = "Custom layout 1";

    public UIInitializeAppliedLayoutsLayoutsAppliedTests()
        : base(EditorTestData.WriteForUIInitializeAppliedLayoutsLayoutsApplied)
    {
    }

    [TestMethod]
    public void AppliedLayouts_LayoutsApplied()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var layoutOnMonitor1 = FindLayoutByExactName(Session, EditorUiTestHelper.TemplateLayoutName.Columns);
        Assert.IsTrue(layoutOnMonitor1.Selected);

        SelectAndAssertMonitor(this, Session, Monitor2);
        var layoutOnMonitor2 = FindLayoutByExactName(Session, CustomLayoutName);
        Assert.IsTrue(layoutOnMonitor2.Selected);
    }
}
