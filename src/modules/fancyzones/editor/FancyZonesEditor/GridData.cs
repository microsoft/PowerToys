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
        // The sum of row/column percents should be equal to this number
        private const int _multiplier = 10000;

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

            public void CalcAdjacentZones(int index, int size, List<RowColInfo> info, Func<int, bool> indexCmpr)
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

            public void FixAccuracyError(List<RowColInfo> info, List<int> percents, int index)
            {
                int total = 0;
                for (int i = 0; i < info.Count; i++)
                {
                    total += info[i].Percent;
                }

                int diff = total - _multiplier;
                if (diff != 0)
                {
                    TotalPercent -= diff;

                    while (index >= info.Count)
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

            _rowInfo = new List<RowColInfo>(rows);
            for (int row = 0; row < rows; row++)
            {
                _rowInfo.Add(new RowColInfo(model.RowPercents[row]));
            }

            _colInfo = new List<RowColInfo>(cols);
            for (int col = 0; col < cols; col++)
            {
                _colInfo.Add(new RowColInfo(model.ColumnPercents[col]));
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

            int[,] cellChildMap = _model.CellChildMap;
            int[,] newCellChildMap = new int[rows, cols];

            int sourceCol = 0;
            for (int col = 0; col < cols; col++)
            {
                for (int row = 0; row < rows; row++)
                {
                    if (cellChildMap[row, sourceCol] > spliteeIndex || ((col > foundCol) && (cellChildMap[row, sourceCol] == spliteeIndex)))
                    {
                        newCellChildMap[row, col] = cellChildMap[row, sourceCol] + 1;
                    }
                    else
                    {
                        newCellChildMap[row, col] = cellChildMap[row, sourceCol];
                    }
                }

                if (col != foundCol)
                {
                    sourceCol++;
                }
            }

            RowColInfo[] split = _colInfo[foundCol].Split(offset, space);

            _colInfo[foundCol] = split[0];
            _colInfo.Insert(foundCol + 1, split[1]);

            _model.ColumnPercents[foundCol] = split[0].Percent;
            _model.ColumnPercents.Insert(foundCol + 1, split[1].Percent);

            double newTotalExtent = actualWidth - (space * (cols + 1));
            for (int col = 0; col < cols; col++)
            {
                if (col != foundCol && col != foundCol + 1)
                {
                    _colInfo[col].RecalculatePercent(newTotalExtent);
                }
            }

            FixAccuracyError(_colInfo, _model.ColumnPercents);
            _model.CellChildMap = newCellChildMap;
            _model.Columns++;
        }

        public void SplitRow(int foundRow, int spliteeIndex, int newChildIndex, double space, double offset, double actualHeight)
        {
            int rows = _model.Rows + 1;
            int cols = _model.Columns;

            int[,] cellChildMap = _model.CellChildMap;
            int[,] newCellChildMap = new int[rows, cols];

            int sourceRow = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    if (cellChildMap[sourceRow, col] > spliteeIndex || ((row > foundRow) && (cellChildMap[sourceRow, col] == spliteeIndex)))
                    {
                        newCellChildMap[row, col] = cellChildMap[sourceRow, col] + 1;
                    }
                    else
                    {
                        newCellChildMap[row, col] = cellChildMap[sourceRow, col];
                    }
                }

                if (row != foundRow)
                {
                    sourceRow++;
                }
            }

            RowColInfo[] split = _rowInfo[foundRow].Split(offset, space);

            _rowInfo[foundRow] = split[0];
            _rowInfo.Insert(foundRow + 1, split[1]);

            _model.RowPercents[foundRow] = split[0].Percent;
            _model.RowPercents.Insert(foundRow + 1, split[1].Percent);

            double newTotalExtent = actualHeight - (space * (rows + 1));
            for (int row = 0; row < rows; row++)
            {
                if (row != foundRow && row != foundRow + 1)
                {
                    _rowInfo[row].RecalculatePercent(newTotalExtent);
                }
            }

            FixAccuracyError(_rowInfo, _model.RowPercents);
            _model.CellChildMap = newCellChildMap;
            _model.Rows++;
        }

        public void SplitOnDrag(GridResizer resizer, double delta, double space)
        {
            if (resizer.Orientation == Orientation.Vertical)
            {
                int rows = _model.Rows;
                int cols = _model.Columns + 1;
                int[,] cellChildMap = _model.CellChildMap;
                int[,] newCellChildMap = new int[rows, cols];

                int draggedResizerStartCol = resizer.StartCol;

                if (delta > 0)
                {
                    int sourceCol = 0;
                    for (int col = 0; col < cols; col++)
                    {
                        for (int row = 0; row < rows; row++)
                        {
                            if (col == draggedResizerStartCol + 1 && (row < resizer.StartRow || row >= resizer.EndRow))
                            {
                                newCellChildMap[row, col] = cellChildMap[row, sourceCol + 1];
                            }
                            else
                            {
                                newCellChildMap[row, col] = cellChildMap[row, sourceCol];
                            }
                        }

                        if (col != draggedResizerStartCol)
                        {
                            sourceCol++;
                        }
                    }

                    RowColInfo[] split = _colInfo[draggedResizerStartCol + 1].Split(delta, space);

                    _colInfo[draggedResizerStartCol + 1] = split[0];
                    _colInfo.Insert(draggedResizerStartCol + 2, split[1]);

                    _model.ColumnPercents[draggedResizerStartCol + 1] = split[0].Percent;
                    _model.ColumnPercents.Insert(draggedResizerStartCol + 2, split[1].Percent);
                }
                else
                {
                    int sourceCol = 0;
                    for (int col = 0; col < cols; col++)
                    {
                        for (int row = 0; row < rows; row++)
                        {
                            if (col == draggedResizerStartCol + 1 && (row >= resizer.StartRow && row < resizer.EndRow))
                            {
                                newCellChildMap[row, col] = cellChildMap[row, sourceCol + 1];
                            }
                            else
                            {
                                newCellChildMap[row, col] = cellChildMap[row, sourceCol];
                            }
                        }

                        if (col != draggedResizerStartCol)
                        {
                            sourceCol++;
                        }
                    }

                    double offset = _colInfo[draggedResizerStartCol].End - _colInfo[draggedResizerStartCol].Start + delta;
                    RowColInfo[] split = _colInfo[draggedResizerStartCol].Split(offset - space, space);

                    _colInfo[draggedResizerStartCol] = split[1];
                    _colInfo.Insert(draggedResizerStartCol + 1, split[0]);

                    _model.ColumnPercents[draggedResizerStartCol] = split[1].Percent;
                    _model.ColumnPercents.Insert(draggedResizerStartCol + 1, split[0].Percent);
                }

                FixAccuracyError(_colInfo, _model.ColumnPercents);
                _model.CellChildMap = newCellChildMap;
                _model.Columns++;
            }
            else
            {
                int rows = _model.Rows + 1;
                int cols = _model.Columns;
                int[,] cellChildMap = _model.CellChildMap;
                int[,] newCellChildMap = new int[rows, cols];

                int draggedResizerStartRow = resizer.StartRow;

                if (delta > 0)
                {
                    int sourcRow = 0;
                    for (int row = 0; row < rows; row++)
                    {
                        for (int col = 0; col < cols; col++)
                        {
                            if (row == draggedResizerStartRow + 1 && (col < resizer.StartCol || col >= resizer.EndCol))
                            {
                                newCellChildMap[row, col] = cellChildMap[sourcRow + 1, col];
                            }
                            else
                            {
                                newCellChildMap[row, col] = cellChildMap[sourcRow, col];
                            }
                        }

                        if (row != draggedResizerStartRow)
                        {
                            sourcRow++;
                        }
                    }

                    RowColInfo[] split = _rowInfo[draggedResizerStartRow + 1].Split(delta, space);

                    _rowInfo[draggedResizerStartRow + 1] = split[0];
                    _rowInfo.Insert(draggedResizerStartRow + 2, split[1]);

                    _model.RowPercents[draggedResizerStartRow + 1] = split[0].Percent;
                    _model.RowPercents.Insert(draggedResizerStartRow + 2, split[1].Percent);
                }
                else
                {
                    int sourceRow = 0;
                    for (int row = 0; row < rows; row++)
                    {
                        for (int col = 0; col < cols; col++)
                        {
                            if (row == draggedResizerStartRow + 1 && (col >= resizer.StartCol && col < resizer.EndCol))
                            {
                                newCellChildMap[row, col] = cellChildMap[sourceRow + 1, col];
                            }
                            else
                            {
                                newCellChildMap[row, col] = cellChildMap[sourceRow, col];
                            }
                        }

                        if (row != draggedResizerStartRow)
                        {
                            sourceRow++;
                        }
                    }

                    double offset = _rowInfo[draggedResizerStartRow].End - _rowInfo[draggedResizerStartRow].Start + delta;
                    RowColInfo[] split = _rowInfo[draggedResizerStartRow].Split(offset - space, space);

                    _rowInfo[draggedResizerStartRow] = split[0];
                    _rowInfo.Insert(draggedResizerStartRow + 1, split[1]);

                    _model.RowPercents[draggedResizerStartRow] = split[0].Percent;
                    _model.RowPercents.Insert(draggedResizerStartRow + 1, split[1].Percent);
                }

                FixAccuracyError(_rowInfo, _model.RowPercents);
                _model.CellChildMap = newCellChildMap;
                _model.Rows++;
            }
        }

        public void RecalculateZones(int spacing, Size arrangeSize)
        {
            int rows = _model.Rows;
            int cols = _model.Columns;

            double totalWidth = arrangeSize.Width - (spacing * (cols + 1));
            double totalHeight = arrangeSize.Height - (spacing * (rows + 1));

            double top = spacing;
            for (int row = 0; row < _rowInfo.Count; row++)
            {
                double cellHeight = _rowInfo[row].Recalculate(top, totalHeight);
                top += cellHeight + spacing;
            }

            double left = spacing;
            for (int col = 0; col < _colInfo.Count; col++)
            {
                double cellWidth = _colInfo[col].Recalculate(left, totalWidth);
                left += cellWidth + spacing;
            }
        }

        public void ArrangeZones(UIElementCollection zones, int spacing)
        {
            int rows = _model.Rows;
            int cols = _model.Columns;
            int[,] cells = _model.CellChildMap;

            if (cells.Length < rows * cols)
            {
                // Merge was not finished yet, rows and cols values are invalid
                return;
            }

            double left, top;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int i = cells[row, col];
                    if (((row == 0) || (cells[row - 1, col] != i)) &&
                        ((col == 0) || (cells[row, col - 1] != i)))
                    {
                        // this is not a continuation of a span
                        GridZone zone = (GridZone)zones[i];
                        left = _colInfo[col].Start;
                        top = _rowInfo[row].Start;
                        Canvas.SetLeft(zone, left);
                        Canvas.SetTop(zone, top);
                        zone.LabelID.Content = i + 1;

                        int maxRow = row;
                        while (((maxRow + 1) < rows) && (cells[maxRow + 1, col] == i))
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
                        while (((maxCol + 1) < cols) && (cells[row, maxCol + 1] == i))
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

        public void ArrangeResizers(UIElementCollection adornerChildren, int spacing)
        {
            int rows = _model.Rows;
            int cols = _model.Columns;

            foreach (GridResizer resizer in adornerChildren)
            {
                if (resizer.Orientation == Orientation.Horizontal)
                {
                    if (resizer.EndCol <= cols && resizer.StartRow < rows)
                    {
                        // hard coding this as (resizer.ActualHeight / 2) will still evaluate to 0 here ... a layout hasn't yet happened
                        Canvas.SetTop(resizer, _rowInfo[resizer.StartRow].End + (spacing / 2) - 24);
                        Canvas.SetLeft(resizer, (_colInfo[resizer.EndCol - 1].End + _colInfo[resizer.StartCol].Start) / 2);
                    }
                }
                else
                {
                    if (resizer.EndRow <= rows && resizer.StartCol < cols)
                    {
                        // hard coding this as (resizer.ActualWidth / 2) will still evaluate to 0 here ... a layout hasn't yet happened
                        Canvas.SetLeft(resizer, _colInfo[resizer.StartCol].End + (spacing / 2) - 24);
                        Canvas.SetTop(resizer, (_rowInfo[resizer.EndRow - 1].End + _rowInfo[resizer.StartRow].Start) / 2);
                    }
                }
            }
        }

        public ResizeInfo CalculateResizeInfo(GridResizer resizer, double delta)
        {
            ResizeInfo res = new ResizeInfo();

            int rowIndex = resizer.StartRow;
            int colIndex = resizer.StartCol;
            int[,] indices = _model.CellChildMap;

            List<RowColInfo> info;
            List<int> percents;
            int index;

            if (resizer.Orientation == Orientation.Vertical)
            {
                res.CurrentExtent = _colInfo[colIndex].Extent;
                res.CurrentPercent = _colInfo[colIndex].Percent;

                info = _colInfo;
                percents = _model.ColumnPercents;
                index = colIndex;

                Func<int, bool> indexCmpr = (ind) =>
                {
                    bool sameIndices = true;
                    for (int i = resizer.StartRow; i < resizer.EndRow && sameIndices; i++)
                    {
                        sameIndices &= indices[i, ind] == indices[i, ind - 1];
                    }

                    return sameIndices;
                };

                res.CalcAdjacentZones(colIndex, _model.Columns, _colInfo, indexCmpr);
            }
            else
            {
                res.CurrentExtent = _rowInfo[rowIndex].Extent;
                res.CurrentPercent = _rowInfo[rowIndex].Percent;

                info = _rowInfo;
                percents = _model.RowPercents;
                index = rowIndex;

                Func<int, bool> indexCmpr = (ind) =>
                {
                    bool sameIndices = true;
                    for (int i = resizer.StartCol; i < resizer.EndCol && sameIndices; i++)
                    {
                        sameIndices &= indices[ind, i] == indices[ind - 1, i];
                    }

                    return sameIndices;
                };

                res.CalcAdjacentZones(rowIndex, _model.Rows, _rowInfo, indexCmpr);
            }

            res.FixAccuracyError(info, percents, delta > 0 ? index + 2 : index + 1);
            res.CalcNewPercent(delta);
            return res;
        }

        public void DragResizer(GridResizer resizer, ResizeInfo data)
        {
            List<RowColInfo> info;
            List<int> percents;
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

        public bool SwapNegativePercents(Orientation orientation, int startRow, int endRow, int startCol, int endCol)
        {
            List<int> percents;
            int index;
            Action swapIndicesPrevLine, swapIndicesNextLine;

            if (orientation == Orientation.Vertical)
            {
                percents = _model.ColumnPercents;
                index = startCol;

                swapIndicesPrevLine = () =>
                {
                    for (int row = startRow; row < endRow; row++)
                    {
                        _model.CellChildMap[row, startCol] = _model.CellChildMap[row, startCol + 1];
                    }

                    for (int row = 0; row < _model.Rows; row++)
                    {
                        if (row < startRow || row >= endRow)
                        {
                            _model.CellChildMap[row, startCol] = _model.CellChildMap[row, startCol - 1];
                        }
                    }
                };

                swapIndicesNextLine = () =>
                {
                    for (int row = startRow; row < endRow; row++)
                    {
                        _model.CellChildMap[row, startCol + 1] = _model.CellChildMap[row, startCol];
                    }

                    for (int row = 0; row < _model.Rows; row++)
                    {
                        if (row < startRow || row >= endRow)
                        {
                            _model.CellChildMap[row, startCol + 1] = _model.CellChildMap[row, startCol + 2];
                        }
                    }
                };
            }
            else
            {
                percents = _model.RowPercents;
                index = startRow;

                swapIndicesPrevLine = () =>
                {
                    for (int col = startCol; col < endCol; col++)
                    {
                        _model.CellChildMap[startRow, col] = _model.CellChildMap[startRow + 1, col];
                    }

                    for (int col = 0; col < _model.Columns; col++)
                    {
                        if (col < startCol || col >= endCol)
                        {
                            _model.CellChildMap[startRow, col] = _model.CellChildMap[startRow - 1, col];
                        }
                    }
                };

                swapIndicesNextLine = () =>
                {
                    for (int col = startCol; col < endCol; col++)
                    {
                        _model.CellChildMap[startRow + 1, col] = _model.CellChildMap[startRow, col];
                    }

                    for (int col = 0; col < _model.Columns; col++)
                    {
                        if (col < startCol || col >= endCol)
                        {
                            _model.CellChildMap[startRow + 1, col] = _model.CellChildMap[startRow + 2, col];
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

        public void MergeZones(int startRow, int endRow, int startCol, int endCol, Action<int> deleteAction, int zoneCount)
        {
            int[,] cells = _model.CellChildMap;
            int mergedIndex = cells[startRow, startCol];

            // maintain indices order after merge
            Dictionary<int, int> indexReplacement = new Dictionary<int, int>();
            List<int> zoneIndices = new List<int>(zoneCount);
            for (int i = 0; i < zoneCount; i++)
            {
                zoneIndices.Add(i);
            }

            for (int row = startRow; row <= endRow; row++)
            {
                for (int col = startCol; col <= endCol; col++)
                {
                    int childIndex = cells[row, col];
                    if (childIndex != mergedIndex)
                    {
                        indexReplacement[childIndex] = mergedIndex;
                        zoneIndices[childIndex] = -1;
                    }
                }
            }

            for (int i = zoneIndices.Count - 1; i >= 0; i--)
            {
                int index = zoneIndices[i];
                if (index == -1)
                {
                    deleteAction(i);
                    zoneIndices.RemoveAt(i);
                }
            }

            for (int i = zoneIndices.Count - 1; i >= 0; i--)
            {
                indexReplacement[zoneIndices[i]] = i;
            }

            ReplaceIndicesToMaintainOrder(indexReplacement);
            CollapseIndices();
            FixAccuracyError(_rowInfo, _model.RowPercents);
            FixAccuracyError(_colInfo, _model.ColumnPercents);
        }

        public void ReplaceIndicesToMaintainOrder(int zoneCount)
        {
            int[,] cells = _model.CellChildMap;
            Dictionary<int, int> indexReplacement = new Dictionary<int, int>();
            List<int> zoneIndices = new List<int>(zoneCount);
            HashSet<int> zoneIndexSet = new HashSet<int>(zoneCount);

            for (int i = 0; i < zoneCount; i++)
            {
                zoneIndices.Add(i);
            }

            for (int row = 0; row < _model.Rows; row++)
            {
                for (int col = 0; col < _model.Columns; col++)
                {
                    zoneIndexSet.Add(cells[row, col]);
                }
            }

            int j = 0;
            foreach (int index in zoneIndexSet)
            {
                indexReplacement[index] = zoneIndices[j];
                j++;
            }

            ReplaceIndicesToMaintainOrder(indexReplacement);
        }

        private void ReplaceIndicesToMaintainOrder(Dictionary<int, int> indexReplacement)
        {
            int[,] cells = _model.CellChildMap;

            for (int row = 0; row < _model.Rows; row++)
            {
                for (int col = 0; col < _model.Columns; col++)
                {
                    cells[row, col] = indexReplacement[cells[row, col]];
                }
            }
        }

        private void CollapseIndices()
        {
            List<int> rowsToRemove = new List<int>(), colsToRemove = new List<int>();
            int[,] cellChildMap = _model.CellChildMap;

            int arrayShift = 0;
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

                if (couldBeRemoved)
                {
                    _rowInfo[row - 1 - arrayShift].Percent += _rowInfo[row - arrayShift].Percent;
                    _rowInfo.RemoveAt(row - arrayShift);

                    _model.RowPercents[row - 1 - arrayShift] += _model.RowPercents[row - arrayShift];
                    _model.RowPercents.RemoveAt(row - arrayShift);

                    rowsToRemove.Add(row);
                    arrayShift++;
                }
            }

            arrayShift = 0;
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

                if (couldBeRemoved)
                {
                    _colInfo[col - 1 - arrayShift].Percent += _colInfo[col - arrayShift].Percent;
                    _colInfo.RemoveAt(col - arrayShift);

                    _model.ColumnPercents[col - 1 - arrayShift] += _model.ColumnPercents[col - arrayShift];
                    _model.ColumnPercents.RemoveAt(col - arrayShift);

                    colsToRemove.Add(col);
                    arrayShift++;
                }
            }

            int rows = _model.Rows - rowsToRemove.Count;
            int cols = _model.Columns - colsToRemove.Count;

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

                        dstCol = 0;
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

        private void FixAccuracyError(List<RowColInfo> info, List<int> percents)
        {
            int total = 0;
            for (int i = 0; i < info.Count; i++)
            {
                total += info[i].Percent;
            }

            int prefixTotal = 0;
            for (int i = 0; i < percents.Count; i++)
            {
                int first = prefixTotal * _multiplier / total;
                prefixTotal += info[i].Percent;
                int last = prefixTotal * _multiplier / total;
                percents[i] = info[i].Percent = last - first;
            }
        }

        private GridLayoutModel _model;
        private List<RowColInfo> _rowInfo;
        private List<RowColInfo> _colInfo;
    }
}
