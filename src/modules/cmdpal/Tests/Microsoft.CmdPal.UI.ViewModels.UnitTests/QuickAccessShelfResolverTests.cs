// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class QuickAccessShelfResolverTests
{
    [DataTestMethod]
    [DataRow(0, "1")]
    [DataRow(8, "9")]
    [DataRow(9, "")]
    [DataRow(10, "")]
    public void IndexToShortcutDigit_MapsFirstNineItems(int index, string expectedDigit)
    {
        Assert.AreEqual(expectedDigit, QuickAccessShelfResolver.IndexToShortcutDigit(index));
    }

    [TestMethod]
    public void RecentItemsDoNotReceiveMruDependentAccessKeys()
    {
        var item = new ListItem { Title = "Recent" };

        var pinned = new QuickAccessShelfItem(item, shortcutIndex: 2, startsRecentSection: false);
        var recent = new QuickAccessShelfItem(item, shortcutIndex: -1, startsRecentSection: true);

        Assert.AreEqual("3", pinned.ShortcutDigit);
        Assert.AreEqual(string.Empty, recent.ShortcutDigit);
    }

    [DataTestMethod]
    [DataRow(0, 300, 0)]
    [DataRow(1, 44, 1)]
    [DataRow(3, 140, 3)]
    [DataRow(4, 140, 2)]
    [DataRow(4, 188, 4)]
    [DataRow(3, 92, 1)]
    [DataRow(3, 44, 0)]
    public void CalculateVisibleCapacity_ReservesOverflowOnlyWhenNeeded(
        int itemCount,
        double availableWidth,
        int expectedCapacity)
    {
        Assert.AreEqual(
            expectedCapacity,
            QuickAccessShelfResolver.CalculateVisibleCapacity(itemCount, availableWidth, itemWidth: 44, spacing: 4));
    }
}
