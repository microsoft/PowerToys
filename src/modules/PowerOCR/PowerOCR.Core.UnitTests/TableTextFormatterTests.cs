// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerOCR.Core.Formatting;
using PowerOCR.Core.Models;

namespace PowerOCR.Core.UnitTests;

[TestClass]
public sealed class TableTextFormatterTests
{
    [TestMethod]
    public void Format_TwoByTwoGrid_UsesTabsAndNewLines()
    {
        OcrLineData[] cells =
        [
            Cell("A1", 0, 0),
            Cell("B1", 100, 0),
            Cell("A2", 0, 40),
            Cell("B2", 100, 40),
        ];

        Assert.AreEqual(
            $"A1\tB1{Environment.NewLine}A2\tB2",
            TableTextFormatter.Format(cells, "en-US"));
    }

    [TestMethod]
    public void Format_SparseSecondRow_PreservesEmptyColumn()
    {
        OcrLineData[] cells =
        [
            Cell("A1", 0, 0),
            Cell("B1", 100, 0),
            Cell("B2", 100, 40),
        ];

        Assert.AreEqual(
            $"A1\tB1{Environment.NewLine}\tB2",
            TableTextFormatter.Format(cells, "en-US"));
    }

    private static OcrLineData Cell(string text, double x, double y)
        => new(text, new OcrRect(x, y, 40, 20), [new(text, new(x, y, 40, 20))]);
}
