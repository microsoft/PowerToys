// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

[TestClass]
public class UIInitializeAppliedLayoutsNoLayoutsAppliedCustomDefaultLayoutTests : UIInitializeTestBase
{
    private const string CustomLayoutName = "Custom layout 1";

    public UIInitializeAppliedLayoutsNoLayoutsAppliedCustomDefaultLayoutTests()
        : base(EditorTestData.WriteForUIInitializeAppliedLayoutsNoLayoutsAppliedCustomDefaultLayout)
    {
    }

    [TestMethod]
    public void AppliedLayouts_NoLayoutsApplied_CustomDefaultLayout()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var defaultLayout = FindLayoutByExactName(Session, CustomLayoutName);
        defaultLayout.Click();

        defaultLayout = FindLayoutByExactName(Session, CustomLayoutName);
        Assert.IsTrue(defaultLayout.Selected);
    }
}
