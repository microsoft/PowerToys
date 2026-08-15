// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class QuickAccessShelfResolverTests
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
    public void ShelfItemsUseAssignedRowPositionForAccessKeys()
    {
        var item = new ListItem { Title = "Recent" };

        var first = QuickAccessShelfItem.CreateOrReuse([], item, shortcutIndex: 0, startsNewSection: true);
        var third = QuickAccessShelfItem.CreateOrReuse([], item, shortcutIndex: 2, startsNewSection: false);

        Assert.AreEqual("1", first.ShortcutDigit);
        Assert.AreEqual("3", third.ShortcutDigit);
    }

    [TestMethod]
    public void CreateOrReuse_UnchangedItemReusesInitializedIcon()
    {
        var icon = new CountingIconInfo();
        var item = new ListItem { Title = "Recent", Icon = icon };

        var original = QuickAccessShelfItem.CreateOrReuse([], item, shortcutIndex: -1, startsNewSection: false);
        var iconReadCount = icon.ReadCount;
        var reused = QuickAccessShelfItem.CreateOrReuse([original], item, shortcutIndex: -1, startsNewSection: false);

        Assert.IsTrue(iconReadCount > 0);
        Assert.AreSame(original, reused);
        Assert.AreEqual(iconReadCount, icon.ReadCount);
    }

    [TestMethod]
    public void ComposeSections_PinnedFirstAssignsRowShortcuts()
    {
        var result = QuickAccessShelfResolver.ComposeSections(
            ["pinned-1", "pinned-2"],
            ["recent-1", "recent-2"],
            RecentCommandsPlacement.AfterPinned);

        string[] expectedItems = ["pinned-1", "pinned-2", "recent-1", "recent-2"];
        int[] expectedShortcutIndexes = [0, 1, 2, 3];
        bool[] expectedSectionStarts = [false, false, true, false];
        bool[] expectedPinnedState = [true, true, false, false];
        CollectionAssert.AreEqual(expectedItems, result.Select(item => item.Item).ToArray());
        CollectionAssert.AreEqual(expectedShortcutIndexes, result.Select(item => item.ShortcutIndex).ToArray());
        CollectionAssert.AreEqual(expectedSectionStarts, result.Select(item => item.StartsNewSection).ToArray());
        CollectionAssert.AreEqual(expectedPinnedState, result.Select(item => item.IsPinned).ToArray());
    }

    [TestMethod]
    public void ComposeSections_RecentFirstAssignsRowShortcuts()
    {
        var result = QuickAccessShelfResolver.ComposeSections(
            ["pinned-1", "pinned-2"],
            ["recent-1", "recent-2"],
            RecentCommandsPlacement.BeforePinned);

        string[] expectedItems = ["recent-1", "recent-2", "pinned-1", "pinned-2"];
        int[] expectedShortcutIndexes = [0, 1, 2, 3];
        bool[] expectedSectionStarts = [false, false, true, false];
        bool[] expectedPinnedState = [false, false, true, true];
        CollectionAssert.AreEqual(expectedItems, result.Select(item => item.Item).ToArray());
        CollectionAssert.AreEqual(expectedShortcutIndexes, result.Select(item => item.ShortcutIndex).ToArray());
        CollectionAssert.AreEqual(expectedSectionStarts, result.Select(item => item.StartsNewSection).ToArray());
        CollectionAssert.AreEqual(expectedPinnedState, result.Select(item => item.IsPinned).ToArray());
    }

    [TestMethod]
    public void ComposeSections_HiddenRecentCommandsAreExcluded()
    {
        var result = QuickAccessShelfResolver.ComposeSections(
            ["pinned-1"],
            ["recent-1"],
            RecentCommandsPlacement.Hidden);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("pinned-1", result[0].Item);
        Assert.IsTrue(result[0].IsPinned);
        Assert.AreEqual(0, result[0].ShortcutIndex);
        Assert.IsFalse(result[0].StartsNewSection);
    }

    [TestMethod]
    public void ComposeSections_EmptyPinnedSupportsRecentOnlyShelf()
    {
        var result = QuickAccessShelfResolver.ComposeSections(
            [],
            ["recent-1", "recent-2"],
            RecentCommandsPlacement.AfterPinned);

        string[] expectedItems = ["recent-1", "recent-2"];
        int[] expectedShortcutIndexes = [0, 1];
        CollectionAssert.AreEqual(expectedItems, result.Select(item => item.Item).ToArray());
        CollectionAssert.AreEqual(expectedShortcutIndexes, result.Select(item => item.ShortcutIndex).ToArray());
        Assert.IsTrue(result.All(item => !item.IsPinned));
        Assert.IsTrue(result.All(item => !item.StartsNewSection));
    }

    [DataTestMethod]
    [DataRow(0, 300, 0)]
    [DataRow(1, 40, 1)]
    [DataRow(3, 128, 3)]
    [DataRow(4, 128, 2)]
    [DataRow(4, 172, 4)]
    [DataRow(3, 84, 1)]
    [DataRow(3, 40, 0)]
    public void CalculateVisibleCapacity_ReservesOverflowOnlyWhenNeeded(
        int itemCount,
        double availableWidth,
        int expectedCapacity)
    {
        Assert.AreEqual(
            expectedCapacity,
            QuickAccessShelfResolver.CalculateVisibleCapacity(itemCount, availableWidth, itemWidth: 40, spacing: 4));
    }

    private sealed partial class CountingIconInfo : IconInfo
    {
        public int ReadCount { get; private set; }

        public CountingIconInfo()
            : base("icon")
        {
        }

        public override IconData Light
        {
            get
            {
                ReadCount++;
                return base.Light;
            }

            set => base.Light = value;
        }

        public override IconData Dark
        {
            get
            {
                ReadCount++;
                return base.Dark;
            }

            set => base.Dark = value;
        }
    }
}
