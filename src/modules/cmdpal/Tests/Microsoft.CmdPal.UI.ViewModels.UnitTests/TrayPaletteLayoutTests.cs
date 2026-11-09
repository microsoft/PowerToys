// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class TrayPaletteLayoutTests
{
    [TestMethod]
    public void ButtonsKeepThreeColumns()
    {
        CollectionAssert.AreEqual(
            new Rect[] { new(0, 0, 112, 100), new(112, 0, 112, 100), new(224, 0, 112, 100), new(0, 100, 112, 100) },
            TrayPaletteLayout.GetItemBounds([false, false, false, false]));
        Assert.IsEmpty(TrayPaletteLayout.GetItemBounds([]));
    }

    [TestMethod]
    public void WidgetsGetWholeRowsWithoutBackfillingEarlierButtonRows()
    {
        var smallContent = new[] { false, true, false, false, true, true, false };
        var bounds = TrayPaletteLayout.GetItemBounds(smallContent);
        CollectionAssert.AreEqual(
            new Rect[]
            {
                new(0, 0, 112, 100),
                new(0, 100, 336, 100),
                new(0, 200, 112, 100),
                new(112, 200, 112, 100),
                new(0, 300, 336, 100),
                new(0, 400, 336, 100),
                new(0, 500, 112, 100),
            },
            bounds);
        for (var i = 0; i < bounds.Length; i++)
        {
            if (smallContent[i])
            {
                Assert.AreEqual(TrayPaletteLayout.Width, bounds[i].Width);
                Assert.IsTrue(bounds.Where((_, index) => index != i).All(other => other.Bottom <= bounds[i].Top || other.Top >= bounds[i].Bottom));
            }
        }
    }

    [TestMethod]
    [DataRow(10, -1, 0)]
    [DataRow(10, 20, 0)]
    [DataRow(90, 20, 1)]
    [DataRow(250, 50, 1)]
    [DataRow(300, 110, 1)]
    [DataRow(300, 149, 1)]
    [DataRow(20, 150, 2)]
    [DataRow(20, 180, 2)]
    [DataRow(10, 220, 2)]
    [DataRow(90, 220, 3)]
    [DataRow(10, 300, 3)]
    public void DragInsertionUsesTheSameMixedRowGeometry(int x, int y, int expected)
    {
        var bounds = TrayPaletteLayout.GetItemBounds([false, true, false]);
        Assert.AreEqual(expected, TrayPaletteLayout.GetInsertionIndex(bounds, new(x, y)));
    }
}
