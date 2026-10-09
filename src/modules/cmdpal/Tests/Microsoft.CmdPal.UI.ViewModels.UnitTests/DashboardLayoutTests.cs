// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.UI.ViewModels.Dashboard;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class DashboardLayoutTests
{
    private static DashboardTileRect Unplaced(int columnSpan = DashboardLayout.DefaultColumnSpan, int rowSpan = DashboardLayout.DefaultRowSpan) =>
        new(-1, -1, columnSpan, rowSpan);

    private static void AssertNoOverlaps(IReadOnlyList<DashboardTileRect> rects)
    {
        for (var i = 0; i < rects.Count; i++)
        {
            for (var j = i + 1; j < rects.Count; j++)
            {
                Assert.IsFalse(rects[i].IntersectsWith(rects[j]), $"Tiles {i} {rects[i]} and {j} {rects[j]} overlap.");
            }
        }
    }

    private static void AssertInsideColumns(IReadOnlyList<DashboardTileRect> rects, int columns)
    {
        foreach (var rect in rects)
        {
            Assert.IsTrue(rect.Column >= 0 && rect.Row >= 0, $"{rect} isn't placed.");
            Assert.IsTrue(rect.Right <= columns, $"{rect} extends past column {columns}.");
        }
    }

    [TestMethod]
    public void ColumnsFor_NeverReturnsLessThanOneMinimumTile()
    {
        Assert.AreEqual(DashboardLayout.MinColumnSpan, DashboardLayout.ColumnsFor(0));
        Assert.AreEqual(DashboardLayout.MinColumnSpan, DashboardLayout.ColumnsFor(double.NaN));
        Assert.AreEqual(DashboardLayout.MinRowSpan, DashboardLayout.RowsFor(-50));
        Assert.AreEqual(96, DashboardLayout.ColumnsFor(1920));
        Assert.AreEqual(54, DashboardLayout.RowsFor(1080));
    }

    [TestMethod]
    public void Arrange_UnplacedTiles_FlowLeftToRightThenWrap()
    {
        var arranged = DashboardLayout.Arrange([Unplaced(), Unplaced(), Unplaced()], columns: 40, rows: 60);

        Assert.AreEqual(new DashboardTileRect(0, 0, 18, 24), arranged[0]);
        Assert.AreEqual(new DashboardTileRect(18, 0, 18, 24), arranged[1]);
        Assert.AreEqual(new DashboardTileRect(0, 24, 18, 24), arranged[2]);
    }

    [TestMethod]
    public void Arrange_PlacedTilesThatFit_AreUnchanged()
    {
        DashboardTileRect[] desired = [new(2, 3, 20, 10), new(30, 0, 12, 30)];

        var arranged = DashboardLayout.Arrange(desired, columns: 60, rows: 40, designColumns: 60, designRows: 40);

        CollectionAssert.AreEqual(desired, arranged);
    }

    [TestMethod]
    public void Arrange_OverlappingTile_MovesToNearestFreeSpot()
    {
        DashboardTileRect[] desired = [new(0, 0, 20, 20), new(5, 5, 20, 20)];

        var arranged = DashboardLayout.Arrange(desired, columns: 60, rows: 40);

        Assert.AreEqual(desired[0], arranged[0]);
        Assert.AreEqual(new DashboardTileRect(20, 5, 20, 20), arranged[1]);
        AssertNoOverlaps(arranged);
    }

    [TestMethod]
    public void Arrange_PriorityTile_KeepsItsSpotAndPushesOthers()
    {
        DashboardTileRect[] desired = [new(0, 0, 20, 20), new(5, 5, 20, 20)];

        var arranged = DashboardLayout.Arrange(desired, columns: 60, rows: 40, priorityIndex: 1);

        Assert.AreEqual(desired[1], arranged[1]);
        Assert.AreNotEqual(desired[0], arranged[0]);
        AssertNoOverlaps(arranged);
    }

    [TestMethod]
    public void Arrange_SmallerDisplay_ScalesPositionsButKeepsSizes()
    {
        // A tile hugging the right edge of a 100-column grid stays at the right edge of a 50-column grid.
        DashboardTileRect[] desired = [new(80, 10, 20, 20)];

        var arranged = DashboardLayout.Arrange(desired, columns: 50, rows: 30, designColumns: 100, designRows: 60);

        Assert.AreEqual(new DashboardTileRect(30, 5, 20, 20), arranged[0]);
    }

    [TestMethod]
    public void Arrange_ReturningToTheDesignGrid_RestoresTheOriginalLayout()
    {
        DashboardTileRect[] desired = [new(0, 0, 30, 20), new(40, 0, 30, 20), new(70, 25, 30, 20)];

        var small = DashboardLayout.Arrange(desired, columns: 40, rows: 30, designColumns: 100, designRows: 50);
        var restored = DashboardLayout.Arrange(desired, columns: 100, rows: 50, designColumns: 100, designRows: 50);

        AssertNoOverlaps(small);
        AssertInsideColumns(small, 40);
        CollectionAssert.AreEqual(desired, restored);
    }

    [TestMethod]
    public void Arrange_TilesWiderThanTheGrid_AreClampedToFit()
    {
        var arranged = DashboardLayout.Arrange([new DashboardTileRect(0, 0, 80, 20)], columns: 30, rows: 30);

        Assert.AreEqual(new DashboardTileRect(0, 0, 30, 20), arranged[0]);
    }

    [TestMethod]
    public void Arrange_TilesTooSmall_AreGrownToTheMinimum()
    {
        var arranged = DashboardLayout.Arrange([new DashboardTileRect(0, 0, 1, 1)], columns: 30, rows: 30);

        Assert.AreEqual(new DashboardTileRect(0, 0, DashboardLayout.MinColumnSpan, DashboardLayout.MinRowSpan), arranged[0]);
    }

    [TestMethod]
    public void Arrange_WhenTheGridIsFull_OverflowsBelowWithoutOverlapping()
    {
        var desired = Enumerable.Range(0, 6).Select(_ => Unplaced(20, 20)).ToArray();

        var arranged = DashboardLayout.Arrange(desired, columns: 20, rows: 20);

        AssertNoOverlaps(arranged);
        AssertInsideColumns(arranged, 20);
        Assert.AreEqual(new DashboardTileRect(0, 0, 20, 20), arranged[0]);
        Assert.IsTrue(DashboardLayout.Extent(arranged) > 20);
    }

    [TestMethod]
    public void Arrange_ManyTilesOnManyGridSizes_NeverOverlap()
    {
        DashboardTileRect[] desired =
        [
            new(0, 0, 18, 24), new(18, 0, 18, 24), new(36, 0, 30, 12), new(36, 12, 30, 12),
            new(66, 0, 30, 40), Unplaced(), Unplaced(12, 10), new(5, 30, 40, 20),
        ];

        foreach (var (columns, rows) in new[] { (96, 54), (64, 36), (40, 30), (10, 8), (200, 100) })
        {
            var arranged = DashboardLayout.Arrange(desired, columns, rows, designColumns: 96, designRows: 54);
            AssertNoOverlaps(arranged);
            AssertInsideColumns(arranged, columns);
            Assert.AreEqual(desired.Length, arranged.Length);
        }
    }
}
