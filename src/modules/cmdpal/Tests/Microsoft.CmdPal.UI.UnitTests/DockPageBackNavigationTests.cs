// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.Dock;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class DockPageBackNavigationTests
{
    [TestMethod]
    [DataRow(SearchBarBackRequestKind.GoBack, false, true, false, DockPageBackAction.GoBack)]
    [DataRow(SearchBarBackRequestKind.GoBack, false, false, false, DockPageBackAction.Close)]
    [DataRow(SearchBarBackRequestKind.GoBack, true, true, true, DockPageBackAction.GoBack)]
    [DataRow(SearchBarBackRequestKind.GoBack, true, true, false, DockPageBackAction.None)]
    [DataRow(SearchBarBackRequestKind.GoBack, true, false, true, DockPageBackAction.None)]
    [DataRow(SearchBarBackRequestKind.Dismiss, false, true, false, DockPageBackAction.Close)]
    [DataRow(SearchBarBackRequestKind.Hide, false, true, false, DockPageBackAction.Close)]
    public void GetAction_MatchesThePaletteBackBehavior(
        SearchBarBackRequestKind kind,
        bool fromBackspace,
        bool canGoBack,
        bool backspaceGoesBack,
        object expected)
    {
        Assert.AreEqual(expected, DockPageBackNavigation.GetAction(kind, fromBackspace, canGoBack, backspaceGoesBack));
    }
}
