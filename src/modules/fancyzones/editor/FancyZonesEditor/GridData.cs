// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    public class GridData
    {
        public class ResizeInfo
        {
            public ResizeInfo()
            {
            }

            public int NewPercent { get; set; }

            public int AdjacentPercent { get; private set; }

            public int TotalPercent { get; private set; }

            public int CurrentPercent { get; set; }

            public double CurrentExtent { get; set; }

            public bool IsResizeAllowed
            {
                get
                {
                    return (NewPercent > 0) && (NewPercent < TotalPercent);
                }
            }

            public void CalcNewPercent(double delta)
            {
                double newExtent = CurrentExtent + _adjacentExtent + delta;
                NewPercent = (int)((CurrentPercent + AdjacentPercent) * newExtent / (CurrentExtent + _adjacentExtent));
            }

            public void CalcAdjacentZones(int index, int spacing, int size, RowColInfo[] info, Func<int, bool> indexCmpr)
            {
                int ind = index;
                while (ind > 0 && indexCmpr(ind))
                {
                    ind--;
                    _adjacentExtent += info[ind].Extent;
                    AdjacentPercent += info[ind].Percent;
                }

                TotalPercent = CurrentPercent + AdjacentPercent + info[index + 1].Percent;

                ind = index + 2;
                while (ind < size && indexCmpr(ind))
                {
                    TotalPercent += info[ind].Percent;
                    ind++;
                }
            }

            public void FixAccuracyError(RowColInfo[] info, int[] percents, int index)
            {
                int total = 0;
                for (int i = 0; i < info.Length; i++)
                {
                    total += info[i].Percent;
                }

                int diff = total - 10000;
                if (diff != 0)
                {
                    TotalPercent -= diff;

                    while (index >= info.Length)
                    {
                        index--;
                    }

                    info[index].Percent -= diff;
                    percents[index] -= diff;
                }
            }

            private double _adjacentExtent;
        }

        public GridData(GridLayoutModel model)
        {
            _model = model;

            int rows = model.Rows;
            int cols = model.Columns;

            _rowInfo = new RowColInfo[rows];
            for (int row = 0; row < rows; row++)
            {
                _rowInfo[row] = new RowColInfo(model.RowPercents[row]);
            }

            _colInfo = new RowColInfo[cols];
            for (int col = 0; col < cols; col++)
            {
                _colInfo[col] = new RowColInfo(model.ColumnPercents[col]);
            }
        }

        public int ZoneCount
        {
            get
            {
                int maxIndex = 0;
                for (int row = 0; row < _model.Rows; row++)
                {
                    for (int col = 0; col < _model.Columns; col++)
                    {
                        maxIndex = Math.Max(maxIndex, _model.CellChildMap[row, col]);
                    }
                }

                return maxIndex;
            }
        }

        public Tuple<int, int> RowColByIndex(int index)
        {
            int foundRow = -1;
            int foundCol = -1;

            for (int row = 0; row < _model.Rows && foundRow == -1; row++)
            {
                for (int col = 0; col < _model.Columns && foundCol == -1; col++)
                {
                    if (_model.CellChildMap[row, col] == index)
                    {
                        foundRow = row;
                        foundCol = col;
                    }
                }
            }

            return new Tuple<int, int>(foundRow, foundCol);
        }

        public int GetIndex(int row, int col)
        {
            return _model.CellChildMap[row, col];
        }

        public double ColumnTop(int column)
        {
            return _colInfo[column].Start;
        }

        public double ColumnBottom(int column)
        {
            return _colInfo[column].End;
        }

        public double RowStart(int row)
        {
            return _rowInfo[row].Start;
        }

        public double RowEnd(int row)
        {
            return _rowInfo[row].End;
        }

        public void SetIndex(int row, int col, int index)
        {
            _model.CellChildMap[row, col] = index;
        }

        public void SplitColumn(int foundCol, int spliteeIndex, int newChildIndex, double space, double offset, double actualWidth)
        {
            int rows = _model.Rows;
            int cols = _model.Columns + 1;

            int[,] newCellChildMap = new int[rows, cols];
            int[] newColPercents = new int[cols];
            RowColInfo[] newColInfo = new RowColInfo[cols];

            int sourceCol = 0;
            for (int col = 0; col < cols; col++)
            {
                for (int row = 0; row < rows; row++)
                {
                    if ((col > foundCol) && (_model.CellChildMap[row, sourceCol] == spliteeIndex))
                    {
                        newCellChildMap[row, col] = newChildIndex;
                    }
                    else
                    {
                        newCellChildMap[row, col] = _model.CellChildMap[row, sourceCol];
                    }
                }

                if (col != foundCol)
                {
                    sourceCol++;
                }
            }

            sourceCol = 0;
            double newTotalExtent = actualWidth - (space * (cols + 1));
            for (int col = 0; col < cols; col++)
            {
                if (col == foundCol)
                {
                    RowColInfo[] split = _colInfo[col].Split(offset, space);
                    newColInfo[col] = split[0];
                    newColPercents[col] = split[0].Percent;
                    col++;

                    newColInfo[col] = split[1];
                    newColPercents[col] = split[1].Percent;
                }
                else
                {
                    newColInfo[col] = _colInfo[sourceCol];
                    newColInfo[col].RecalculatePercent(newTotalExtent);

                    newColPercents[col] = _model.ColumnPercents[sourceCol];
                }

                sourceCol++;
            }

            _model.CellChildMap = newCellChildMap;
            _model.ColumnPercents = newColPercents;
            _colInfo = newColInfo;

            _model.Columns++;
        }

        public void SplitRow(int foundRow, int spliteeIndex, int newChildIndex, double space, double offset, double actualHeight)
        {
            int rows = _model.Rows + 1;
            int cols = _model.Columns;

            int[,] newCellChildMap = new int[rows, cols];
            int[] newRowPercents = new int[rows];
            RowColInfo[] newRowInfo = new RowColInfo[rows];

            int sourceRow = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    if ((row > foundRow) && (_model.CellChildMap[sourceRow, col] == spliteeIndex))
                    {
                        newCellChildMap[row, col] = newChildIndex;
                    }
                    else
                    {
                        newCellChildMap[row, col] = _model.CellChildMap[sourceRow, col];
                    }
                }

                if (row != foundRow)
                {
                    sourceRow++;
                }
            }

            sourceRow = 0;
            double newTotalExtent = actualHeight - (space * (rows + 1));
            for (int row = 0; row < rows; row++)
            {
                if (row == foundRow)
                {
                    RowColInfo[] split = _rowInfo[row].Split(offset, space);
                    newRowInfo[row] = split[0];
                    newRowPercents[row] = split[0].Percent;
                    row++;

                    newRowInfo[row] = split[1];
                    newRowPercents[row] = split[1].Percent;
                }
                else
                {
                    newRowInfo[row] = _rowInfo[sourceRow];
                    newRowInfo[row].RecalculatePercent(newTotalExtent);

                    newRowPercents[row] = _model.RowPercents[sourceRow];
                }

                sourceRow++;
            }

            _rowInfo = newRowInfo;
            _model.CellChildMap = newCellChildMap;
            _model.RowPercents = newRowPercents;

            _model.Rows++;
        }

        public void RecalculateZones(int spacing, Size arrangeSize)
        {
            int rows = _model.Rows;
            int cols = _model.Columns;

            double totalWidth = arrangeSize.Width - (spacing * (cols + 1));
            double totalHeight = arrangeSize.Height - (spacing * (rows + 1));

            double top = spacing;
            for (int row = 0; row < rows; row++)
            {
                double cellHeight = _rowInfo[row].Recalculate(top, totalHeight);
                top += cellHeight + spacing;
            }

            double left = spacing;
            for (int col = 0; col < cols; col++)
            {
                double cellWidth = _colInfo[col].Recalculate(left, totalWidth);
                left += cellWidth + spacing;
            }
        }

        public void ManageZones(UIElementCollection zones, int spacing)
        {
            int rows = _model.Rows;
            int cols = _model.Columns;

            double left, top;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int i = _model.CellChildMap[row, col];
                    if (((row == 0) || (_model.CellChildMap[row - 1, col] != i)) &&
                        ((col == 0) || (_model.CellChildMap[row, col - 1] != i)))
                    {
                        // this is not a continuation of a span
                        GridZone zone = (GridZone)zones[i];
                        left = _colInfo[col].Start;
                        top = _rowInfo[row].Start;
                        Canvas.SetLeft(zone, left);
                        Canvas.SetTop(zone, top);
                        zone.LabelID.Content = i + 1;

                        int maxRow = row;
                        while (((maxRow + 1) < rows) && (_model.CellChildMap[maxRow + 1, col] == i))
                        {
                            maxRow++;
                        }

                        zone.HorizontalSnapPoints = null;
                        if (maxRow > row)
                        {
                            zone.HorizontalSnapPoints = new double[maxRow - row];
                            int pointsIndex = 0;
                            for (int walk = row; walk < maxRow; walk++)
                            {
                                zone.HorizontalSnapPoints[pointsIndex++] = _rowInfo[walk].End + (spacing / 2) - top;
                            }
                        }

                        int maxCol = col;
                        while (((maxCol + 1) < cols) && (_model.CellChildMap[row, maxCol + 1] == i))
                        {
                            maxCol++;
                        }

                        zone.VerticalSnapPoints = null;
                        if (maxCol > col)
                        {
                            zone.VerticalSnapPoints = new double[maxCol - col];
                            int pointsIndex = 0;
                            for (int walk = col; walk < maxCol; walk++)
                            {
                                zone.VerticalSnapPoints[pointsIndex++] = _colInfo[walk].End + (spacing / 2) - left;
                            }
                        }

                        zone.MinWidth = _colInfo[maxCol].End - left;
                        zone.MinHeight = _rowInfo[maxRow].End - top;
                    }
                }
            }
        }

        public void ManageResizers(UIElementCollection adornerChildren, int spacing)
        {
            int rows = _model.Rows;
            int cols = _model.Columns;

            foreach (GridResizer resizer in adornerChildren)
            {
                if (resizer.Orientation == Orientation.Horizontal)
                {
                    if (resizer.EndCol <= cols)
                    {
                        // hard coding this as (resizer.ActualHeight / 2) will still evaluate to 0 here ... a layout hasn't yet happened
                        Canvas.SetTop(resizer, _rowInfo[resizer.StartRow].End + (spacing / 2) - 24);
                        Canvas.SetLeft(resizer, (_colInfo[resizer.EndCol - 1].End + _colInfo[resizer.StartCol].Start) / 2);
                    }
                }
                else
                {
                    if (resizer.EndRow <= rows)
                    {
                        // hard coding this as (resizer.ActualWidth / 2) will still evaluate to 0 here ... a layout hasn't yet happened
                        Canvas.SetLeft(resizer, _colInfo[resizer.StartCol].End + (spacing / 2) - 24);
                        Canvas.SetTop(resizer, (_rowInfo[resizer.EndRow - 1].End + _rowInfo[resizer.StartRow].Start) / 2);
                    }
                }
            }
        }

        public ResizeInfo CalculateResizeInfo(GridResizer resizer, double delta, int spacing)
        {
            ResizeInfo res = new ResizeInfo();

            int rowIndex = resizer.StartRow;
            int colIndex = resizer.StartCol;
            int[,] indices = _model.CellChildMap;

            RowColInfo[] info;
            int[] percents;
            int index;

            if (resizer.Orientation == Orientation.Vertical)
            {
                res.CurrentExtent = _colInfo[colIndex].Extent;
                res.CurrentPercent = _colInfo[colIndex].Percent;

                info = _colInfo;
                percents = _model.ColumnPercents;
                index = colIndex;

                Func<int, bool> indexCmpr = i => indices[rowIndex, i] == indices[rowIndex, i - 1];
                res.CalcAdjacentZones(colIndex, spacing, _model.Columns, _colInfo, indexCmpr);
            }
            else
            {
                res.CurrentExtent = _rowInfo[rowIndex].Extent;
                res.CurrentPercent = _rowInfo[rowIndex].Percent;

                info = _rowInfo;
                percents = _model.RowPercents;
                index = rowIndex;

                Func<int, bool> indexCmpr = i => indices[i, colIndex] == indices[i - 1, colIndex];
                res.CalcAdjacentZones(rowIndex, spacing, _model.Rows, _rowInfo, indexCmpr);
            }

            res.FixAccuracyError(info, percents, delta > 0 ? index + 2 : index + 1);
            res.CalcNewPercent(delta);
            return res;
        }

        public void DragResizer(GridResizer resizer, ResizeInfo data)
        {
            RowColInfo[] info;
            int[] percents;
            int index;

            if (resizer.Orientation == Orientation.Vertical)
            {
                info = _colInfo;
                percents = _model.ColumnPercents;
                index = resizer.StartCol;
            }
            else
            {
                info = _rowInfo;
                percents = _model.RowPercents;
                index = resizer.StartRow;
            }

            int nextPercent = data.CurrentPercent + data.AdjacentPercent + info[index + 1].Percent;

            percents[index] = info[index].Percent = data.NewPercent - data.AdjacentPercent;
            percents[index + 1] = info[index + 1].Percent = nextPercent - data.NewPercent;
        }

        public bool SwapNegativePercents(Orientation orientation, int rowIndex, int colIndex)
        {
            int[] percents;
            int index;
            Action swapIndicesPrevLine, swapIndicesNextLine;

            if (orientation == Orientation.Vertical)
            {
                percents = _model.ColumnPercents;
                index = colIndex;

                swapIndicesPrevLine = () =>
                {
                    _model.CellChildMap[rowIndex, colIndex] = _model.CellChildMap[rowIndex, colIndex + 1];
                    for (int row = 0; row < _model.Rows; row++)
                    {
                        if (row != rowIndex)
                        {
                            _model.CellChildMap[row, colIndex] = _model.CellChildMap[row, colIndex - 1];
                        }
                    }
                };

                swapIndicesNextLine = () =>
                {
                    _model.CellChildMap[rowIndex, colIndex + 1] = _model.CellChildMap[rowIndex, colIndex];
                    for (int row = 0; row < _model.Rows; row++)
                    {
                        if (row != rowIndex)
                        {
                            _model.CellChildMap[row, colIndex + 1] = _model.CellChildMap[row, colIndex + 2];
                        }
                    }
                };
            }
            else
            {
                percents = _model.RowPercents;
                index = rowIndex;

                swapIndicesPrevLine = () =>
                {
                    _model.CellChildMap[rowIndex, colIndex] = _model.CellChildMap[rowIndex + 1, colIndex];
                    for (int col = 0; col < _model.Columns; col++)
                    {
                        if (col != colIndex)
                        {
                            _model.CellChildMap[rowIndex, col] = _model.CellChildMap[rowIndex - 1, col];
                        }
                    }
                };

                swapIndicesNextLine = () =>
                {
                    _model.CellChildMap[rowIndex + 1, colIndex] = _model.CellChildMap[rowIndex, colIndex];
                    for (int col = 0; col < _model.Columns; col++)
                    {
                        if (col != colIndex)
                        {
                            _model.CellChildMap[rowIndex + 1, col] = _model.CellChildMap[rowIndex + 2, col];
                        }
                    }
                };
            }

            if (percents[index] < 0)
            {
                swapIndicesPrevLine();

                percents[index] = -percents[index];
                percents[index - 1] = percents[index - 1] - percents[index];

                if (orientation == Orientation.Vertical)
                {
                    _colInfo[index].Percent = percents[index];
                    _colInfo[index - 1].Percent = percents[index - 1];
                }
                else
                {
                    _rowInfo[index].Percent = percents[index];
                    _rowInfo[index - 1].Percent = percents[index - 1];
                }

                return true;
            }

            if (percents[index + 1] < 0)
            {
                swapIndicesNextLine();

                percents[index + 1] = -percents[index + 1];
                percents[index] = percents[index] - percents[index + 1];

                if (orientation == Orientation.Vertical)
                {
                    _colInfo[index].Percent = percents[index];
                    _colInfo[index + 1].Percent = percents[index + 1];
                }
                else
                {
                    _rowInfo[index].Percent = percents[index];
                    _rowInfo[index + 1].Percent = percents[index + 1];
                }

                return true;
            }

            return false;
        }

        public int SwappedIndexAfterResize(GridResizer resizer)
        {
            if (resizer.Orientation == Orientation.Horizontal)
            {
                for (int i = 0; i < _model.Rows; i++)
                {
                    if (_rowInfo[i].Percent < 0)
                    {
                        _rowInfo[i].Percent = -_rowInfo[i].Percent;
                        _rowInfo[i - 1].Percent -= _rowInfo[i].Percent;
                        _rowInfo[i + 1].Percent -= _rowInfo[i].Percent;

                        _model.RowPercents[i - 1] = _rowInfo[i - 1].Percent;
                        _model.RowPercents[i] = _rowInfo[i].Percent;
                        _model.RowPercents[i + 1] = _rowInfo[i + 1].Percent;

                        return i;
                    }
                }
            }
            else
            {
                for (int i = 1; i < _model.Columns; i++)
                {
                    if (_colInfo[i].Percent < 0)
                    {
                        _colInfo[i - 1].Percent += _colInfo[i].Percent;
                        _colInfo[i].Percent = -_colInfo[i].Percent;

                        _model.ColumnPercents[i - 1] = _colInfo[i - 1].Percent;
                        _model.ColumnPercents[i] = _colInfo[i].Percent;

                        return i;
                    }
                }
            }

            return -1;
        }

        public void MergeZones(int startRow, int endRow, int startCol, int endCol, Action<int> deleteAction)
        {
            int mergedIndex = _model.CellChildMap[startRow, startCol];

            for (int row = startRow; row <= endRow; row++)
            {
                for (int col = startCol; col <= endCol; col++)
                {
                    int childIndex = _model.CellChildMap[row, col];
                    if (childIndex != mergedIndex)
                    {
                        _model.CellChildMap[row, col] = mergedIndex;
                        deleteAction(childIndex);
                    }
                }
            }

            CollapseIndices();
        }

        public void CollapseIndices()
        {
            List<int> rowsToRemove = new List<int>(), colsToRemove = new List<int>();
            int[,] cellChildMap = _model.CellChildMap;

            for (int row = 1; row < _model.Rows; row++)
            {
                bool couldBeRemoved = true;
                for (int col = 0; col < _model.Columns && couldBeRemoved; col++)
                {
                    if (cellChildMap[row, col] != cellChildMap[row - 1, col])
                    {
                        couldBeRemoved = false;
                    }
                }

                if (couldBeRemoved && row > 0)
                {
                    rowsToRemove.Add(row);
                }
            }

            for (int col = 1; col < _model.Columns; col++)
            {
                bool couldBeRemoved = true;
                for (int row = 0; row < _model.Rows && couldBeRemoved; row++)
                {
                    if (cellChildMap[row, col] != cellChildMap[row, col - 1])
                    {
                        couldBeRemoved = false;
                    }
                }

                if (couldBeRemoved && col > 0)
                {
                    colsToRemove.Add(col);
                }
            }

            int rows = _model.Rows - rowsToRemove.Count;
            int cols = _model.Columns - colsToRemove.Count;

            if (rowsToRemove.Count > 0)
            {
                int[] rowPercents = _model.RowPercents;
                int[] newRowPercents = new int[rows];
                RowColInfo[] newRowInfo = new RowColInfo[rows];

                int removableRowIndex = 0;
                int removableRow = rowsToRemove[removableRowIndex];

                int dstIndex = 0;
                for (int i = 0; i < _model.Rows; i++)
                {
                    if (i != removableRow)
                    {
                        newRowPercents[dstIndex] = rowPercents[i];
                        newRowInfo[dstIndex] = new RowColInfo(_rowInfo[i]);
                        dstIndex++;
                    }
                    else
                    {
                        newRowPercents[dstIndex - 1] += rowPercents[i];
                        newRowInfo[dstIndex - 1].Percent += rowPercents[i];

                        removableRowIndex++;
                        if (removableRowIndex < rowsToRemove.Count)
                        {
                            removableRow = rowsToRemove[removableRowIndex];
                        }
                    }
                }

                _model.RowPercents = newRowPercents;
                _rowInfo = newRowInfo;
            }

            if (colsToRemove.Count > 0)
            {
                int[] colPercents = _model.ColumnPercents;
                int[] newColPercents = new int[cols];
                RowColInfo[] newColInfo = new RowColInfo[cols];

                int removableColIndex = 0;
                int removableCol = colsToRemove[removableColIndex];

                int dstIndex = 0;
                for (int i = 0; i < _model.Columns; i++)
                {
                    if (i != removableCol)
                    {
                        newColPercents[dstIndex] = colPercents[i];
                        newColInfo[dstIndex] = new RowColInfo(_colInfo[i]);
                        dstIndex++;
                    }
                    else
                    {
                        newColPercents[dstIndex - 1] += colPercents[i];
                        newColInfo[dstIndex - 1].Percent += colPercents[i];

                        removableColIndex++;
                        if (removableColIndex < colsToRemove.Count)
                        {
                            removableCol = colsToRemove[removableColIndex];
                        }
                    }
                }

                _model.ColumnPercents = newColPercents;
                _colInfo = newColInfo;
            }

            if (rowsToRemove.Count > 0 || colsToRemove.Count > 0)
            {
                int[,] newCellChildMap = new int[rows, cols];
                int dstRow = 0, dstCol = 0;

                int removableRowIndex = 0;
                int removableRow = -1;
                if (rowsToRemove.Count > 0)
                {
                    removableRow = rowsToRemove[removableRowIndex];
                }

                for (int row = 0; row < _model.Rows; row++)
                {
                    if (row != removableRow)
                    {
                        int removableColIndex = 0;
                        int removableCol = -1;
                        if (colsToRemove.Count > 0)
                        {
                            removableCol = colsToRemove[removableColIndex];
                        }

                        for (int col = 0; col < _model.Columns; col++)
                        {
                            if (col != removableCol)
                            {
                                newCellChildMap[dstRow, dstCol] = cellChildMap[row, col];
                                dstCol++;
                            }
                            else
                            {
                                removableColIndex++;
                                if (removableColIndex < colsToRemove.Count)
                                {
                                    removableCol = colsToRemove[removableColIndex];
                                }
                            }
                        }

                        dstRow++;
                    }
                    else
                    {
                        removableRowIndex++;
                        if (removableRowIndex < rowsToRemove.Count)
                        {
                            removableRow = rowsToRemove[removableRowIndex];
                        }
                    }
                }

                _model.CellChildMap = newCellChildMap;
            }

            _model.Rows = rows;
            _model.Columns = cols;
        }

        private GridLayoutModel _model;
        private RowColInfo[] _rowInfo;
        private RowColInfo[] _colInfo;
    }
}
