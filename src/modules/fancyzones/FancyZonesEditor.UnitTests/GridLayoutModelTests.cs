// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.Models;

namespace UnitTestsFancyZonesEditor;

[TestClass]
public class GridLayoutModelTests
{
    [TestMethod]
    public void EmptyGridLayoutModelIsNotValid()
    {
        GridLayoutModel gridLayoutModel = new GridLayoutModel();
        Assert.IsFalse(gridLayoutModel.IsModelValid());
    }

    [TestMethod]
    public void GridLayoutModelWithInvalidRowAndColumnCountsIsNotValid()
    {
        GridLayoutModel gridLayoutModel = new GridLayoutModel();
        gridLayoutModel.Rows = 0;
        gridLayoutModel.Columns = 0;
        Assert.IsFalse(gridLayoutModel.IsModelValid());
    }

    [TestMethod]
    public void GridLayoutModelWithInvalidRowPercentsIsNotValid()
    {
        GridLayoutModel gridLayoutModel = new GridLayoutModel();
        gridLayoutModel.Rows = 1;
        gridLayoutModel.Columns = 1;
        gridLayoutModel.RowPercents = new List<int> { 0 }; // Invalid percentage
        Assert.IsFalse(gridLayoutModel.IsModelValid());
    }

    [TestMethod]
    public void GridLayoutModelWithInvalidColumnPercentsIsNotValid()
    {
        GridLayoutModel gridLayoutModel = new GridLayoutModel();
        gridLayoutModel.Rows = 1;
        gridLayoutModel.Columns = 1;
        gridLayoutModel.ColumnPercents = new List<int> { 0 }; // Invalid percentage
        Assert.IsFalse(gridLayoutModel.IsModelValid());
    }

    [TestMethod]
    public void GridLayoutModelWithInvalidCellChildMapLengthIsNotValid()
    {
        GridLayoutModel gridLayoutModel = new GridLayoutModel();
        gridLayoutModel.Rows = 2;
        gridLayoutModel.Columns = 2;
        gridLayoutModel.CellChildMap = new int[2, 1]; // Invalid length
        Assert.IsFalse(gridLayoutModel.IsModelValid());
    }

    [TestMethod]
    public void GridLayoutModelWithInvalidZoneCountIsNotValid()
    {
        GridLayoutModel gridLayoutModel = new GridLayoutModel();
        gridLayoutModel.Rows = 2;
        gridLayoutModel.Columns = 2;
        gridLayoutModel.CellChildMap = new int[,]
        {
            { 1, 2 },
            { 3, 4 },
        }; // Invalid zone count
        Assert.IsFalse(gridLayoutModel.IsModelValid());
    }

    [TestMethod]
    public void GridLayoutModelWithValidPropertiesIsValid()
    {
        GridLayoutModel gridLayoutModel = new GridLayoutModel();

        // Set valid row and column counts
        gridLayoutModel.Rows = 2;
        gridLayoutModel.Columns = 2;

        // Set valid percentages for rows and columns
        // Should add up to 10000
        gridLayoutModel.RowPercents = new List<int> { 5000, 5000 };
        gridLayoutModel.ColumnPercents = new List<int> { 5000, 5000 };

        // Set a valid CellChildMap
        gridLayoutModel.CellChildMap = new int[,]
        {
            { 0, 1 },
            { 2, 3 },
        }; // corresponds to 4 zones

        Assert.IsTrue(gridLayoutModel.IsModelValid(), "GridLayoutModel with valid properties should be valid.");
    }
}
