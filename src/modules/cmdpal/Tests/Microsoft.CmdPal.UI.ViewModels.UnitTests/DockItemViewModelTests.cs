// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class DockItemViewModelTests
{
    [DataTestMethod]
    [DataRow("0.0 Kbps")]
    [DataRow("100 Mbps")]
    [DataRow("1.0 GB/s")]
    [DataRow("50.0 MiB/s")]
    public void GetTitleMinWidth_ReservesTransferRateWidth(string title)
    {
        Assert.AreEqual(64, DockItemViewModel.GetTitleMinWidth(title));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("42%")]
    [DataRow("1:57 PM")]
    public void GetTitleMinWidth_KeepsDefaultWidthForOtherTitles(string title)
    {
        Assert.AreEqual(24, DockItemViewModel.GetTitleMinWidth(title));
    }
}
