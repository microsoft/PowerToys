// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using FancyZonesEditorCommon.Data;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

[TestClass]
public class UIInitializeAppliedLayoutsVerifyOtherVirtualDesktopsAreNotChangedTests : UIInitializeTestBase
{
    private const string VirtualDesktop1 = "{11111111-1111-1111-1111-111111111111}";
    private const string VirtualDesktop2 = "{22222222-2222-2222-2222-222222222222}";

    public UIInitializeAppliedLayoutsVerifyOtherVirtualDesktopsAreNotChangedTests()
        : base(EditorTestData.WriteForUIInitializeAppliedLayoutsVerifyOtherVirtualDesktopsAreNotChanged)
    {
    }

    [TestMethod]
    public void AppliedLayouts_VerifyOtherVirtualDesktopsAreNotChanged()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        FindLayoutByExactName(Session, EditorUiTestHelper.TemplateLayoutName.Rows).Click();

        var data = ReadAppliedLayoutsDurably(
            this,
            applied => applied.AppliedLayouts.Count == 2
                && applied.AppliedLayouts.Any(x => x.Device.VirtualDesktop == VirtualDesktop1)
                && applied.AppliedLayouts.Any(x => x.Device.VirtualDesktop == VirtualDesktop2),
            "updating only the current virtual desktop layout");

        var untouchedDesktopLayout = data.AppliedLayouts.Find(x => x.Device.VirtualDesktop == VirtualDesktop2);
        var currentDesktopLayout = data.AppliedLayouts.Find(x => x.Device.VirtualDesktop == VirtualDesktop1);

        Assert.AreEqual(Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Focus], untouchedDesktopLayout.AppliedLayout.Type);
        Assert.AreEqual(Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Rows], currentDesktopLayout.AppliedLayout.Type);
    }
}
