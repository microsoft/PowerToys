// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class FlyoutPlacementHelperTests
{
    [TestMethod]
    [DataRow(DockSide.Top, FlyoutPlacementMode.BottomEdgeAlignedLeft)]
    [DataRow(DockSide.Bottom, FlyoutPlacementMode.TopEdgeAlignedLeft)]
    [DataRow(DockSide.Left, FlyoutPlacementMode.BottomEdgeAlignedLeft)]
    [DataRow(DockSide.Right, FlyoutPlacementMode.BottomEdgeAlignedLeft)]
    [DataRow((DockSide)(-1), FlyoutPlacementMode.Auto)]
    [DataRow((DockSide)int.MaxValue, FlyoutPlacementMode.Auto)]
    public void DockPlacement_OpensUpwardOnlyForBottomDock(DockSide side, FlyoutPlacementMode expected)
    {
        Assert.AreEqual(expected, FlyoutPlacementHelper.ForDockSide(side));
    }
}
