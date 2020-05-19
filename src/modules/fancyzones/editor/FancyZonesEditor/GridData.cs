using System;
using System.Windows;
using System.Windows.Controls;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    class GridData
    {
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

            int childIndex = 0;
            for (int row = 0; row < rows - 1; row++)
            {
                GridResizer resizer = (GridResizer)adornerChildren[childIndex++];
                int startCol = -1;
                int endCol = cols - 1;
                for (int col = 0; col < cols; col++)
                {
                    if ((startCol == -1) && (_model.CellChildMap[row, col] != _model.CellChildMap[row + 1, col]))
                    {
                        startCol = col;
                    }
                    else if ((startCol != -1) && (_model.CellChildMap[row, col] == _model.CellChildMap[row + 1, col]))
                    {
                        endCol = col - 1;
                        break;
                    }
                }

                if (startCol != -1)
                {
                    // hard coding this as (resizer.ActualHeight / 2) will still evaluate to 0 here ... a layout hasn't yet happened
                    Canvas.SetTop(resizer, _rowInfo[row].End + (spacing / 2) - 24);
                    Canvas.SetLeft(resizer, (_colInfo[endCol].End + _colInfo[startCol].Start) / 2);
                }
                else
                {
                    resizer.Visibility = Visibility.Collapsed;
                }
            }

            for (int col = 0; col < cols - 1; col++)
            {
                GridResizer resizer = (GridResizer)adornerChildren[childIndex++];
                int startRow = -1;
                int endRow = rows - 1;
                for (int row = 0; row < rows; row++)
                {
                    if ((startRow == -1) && (_model.CellChildMap[row, col] != _model.CellChildMap[row, col + 1]))
                    {
                        startRow = row;
                    }
                    else if ((startRow != -1) && (_model.CellChildMap[row, col] == _model.CellChildMap[row, col + 1]))
                    {
                        endRow = row - 1;
                        break;
                    }
                }

                if (startRow != -1)
                {
                    Canvas.SetLeft(resizer, _colInfo[col].End + (spacing / 2) - 24); // hard coding this as (resizer.ActualWidth / 2) will still evaluate to 0 here ... a layout hasn't yet happened
                    Canvas.SetTop(resizer, (_rowInfo[endRow].End + _rowInfo[startRow].Start) / 2);
                    resizer.Visibility = Visibility.Visible;
                }
                else
                {
                    resizer.Visibility = Visibility.Collapsed;
                }
            }
        }

        public bool DragResizer(GridResizer resizer, double delta)
        {
            RowColInfo[] info;
            int rowIndex = resizer.RowIndex;
            int colIndex = resizer.ColIndex;
            int[,] indices = _model.CellChildMap;
            int[] percents = (resizer.Orientation == Orientation.Vertical) ? _model.ColumnPercents : _model.RowPercents;

            double currentExtent, adjacentExtent = 0;
            int currentPercent, adjacentPercent = 0, totalPercent, nextPercent;
            int index;

            if (resizer.Orientation == Orientation.Vertical)
            {
                info = _colInfo;
                index = colIndex;

                currentExtent = _colInfo[colIndex].Extent;
                currentPercent = _colInfo[colIndex].Percent;

                int col = colIndex;
                while (col > 0 && indices[rowIndex, col] == indices[rowIndex, col - 1])
                {
                    col--;
                    adjacentExtent += _colInfo[col].Extent;
                    adjacentPercent += _colInfo[col].Percent;
                }

                nextPercent = currentPercent + adjacentPercent + info[index + 1].Percent;
                totalPercent = nextPercent;

                col = colIndex;
                while (col < _model.Columns - 1 && indices[rowIndex, col] == indices[rowIndex, col + 1])
                {
                    col++;
                    totalPercent += _colInfo[col].Percent;
                }
            }
            else
            {
                info = _rowInfo;
                index = rowIndex;

                currentExtent = _rowInfo[rowIndex].Extent;
                currentPercent = _rowInfo[rowIndex].Percent;

                int row = rowIndex;
                while (row > 0 && indices[row, colIndex] == indices[row - 1, colIndex])
                {
                    row--;
                    adjacentExtent += _rowInfo[row].Extent;
                    adjacentPercent += _rowInfo[row].Percent;
                }

                nextPercent = currentPercent + adjacentPercent + info[index + 1].Percent;
                totalPercent = nextPercent;

                row = rowIndex + 1;
                while (row < _model.Rows - 1 && indices[row, colIndex] == indices[row + 1, colIndex])
                {
                    row++;
                    totalPercent += _rowInfo[row].Percent;
                }
            }

            double newExtent = currentExtent + adjacentExtent + delta;
            int newPercent = (int)((currentPercent + adjacentPercent) * newExtent / (currentExtent + adjacentExtent));

            if ((newPercent > 0) && (newPercent < totalPercent))
            {
                percents[rowIndex] = info[index].Percent = newPercent - adjacentPercent;
                percents[rowIndex + 1] = info[index + 1].Percent = nextPercent - newPercent;

                // TODO cols
                if (percents[rowIndex] < 0)
                {
                    _model.CellChildMap[rowIndex, colIndex] = _model.CellChildMap[rowIndex + 1, colIndex];
                    for (int col = 0; col < _model.Columns; col++)
                    {
                        if (col != colIndex)
                        {
                            _model.CellChildMap[rowIndex, col] = _model.CellChildMap[rowIndex - 1, col];
                        }
                    }

                    percents[rowIndex] = _rowInfo[rowIndex].Percent = -percents[rowIndex];
                    percents[rowIndex - 1] = _rowInfo[rowIndex - 1].Percent = percents[rowIndex - 1] - percents[rowIndex];
                }

                if (percents[rowIndex + 1] < 0)
                {
                    _model.CellChildMap[rowIndex + 1, colIndex] = _model.CellChildMap[rowIndex, colIndex];
                    for (int col = 0; col < _model.Columns; col++)
                    {
                        if (col != colIndex)
                        {
                            _model.CellChildMap[rowIndex, col] = _model.CellChildMap[rowIndex + 2, col];
                        }
                    }

                    percents[rowIndex + 1] = _rowInfo[rowIndex + 1].Percent = -percents[rowIndex + 1];
                    percents[rowIndex] = _rowInfo[rowIndex].Percent = percents[rowIndex] - percents[rowIndex + 1];
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

        private GridLayoutModel _model;
        private RowColInfo[] _rowInfo;
        private RowColInfo[] _colInfo;
    }
}
