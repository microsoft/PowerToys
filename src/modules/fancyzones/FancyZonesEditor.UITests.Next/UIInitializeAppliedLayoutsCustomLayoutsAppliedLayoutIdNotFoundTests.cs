// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

[TestClass]
public class UIInitializeAppliedLayoutsCustomLayoutsAppliedLayoutIdNotFoundTests : UIInitializeTestBase
{
    public UIInitializeAppliedLayoutsCustomLayoutsAppliedLayoutIdNotFoundTests()
        : base(EditorTestData.WriteForUIInitializeAppliedLayoutsCustomLayoutsAppliedLayoutIdNotFound)
    {
    }

    [TestMethod]
    public void AppliedLayouts_CustomLayoutsApplied_LayoutIdNotFound()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var emptyLayout = FindLayoutByExactName(Session, EditorUiTestHelper.TemplateLayoutName.Blank);
        Assert.IsTrue(emptyLayout.Selected);
    }
}
