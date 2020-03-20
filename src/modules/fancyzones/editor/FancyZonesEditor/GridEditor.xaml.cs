// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    /// <summary>
    /// GridEditor is how you tweak an initial GridLayoutModel before saving
    /// </summary>
    public partial class GridEditor : UserControl
    {
        public static readonly DependencyProperty ModelProperty = DependencyProperty.Register("Model", typeof(GridLayoutModel), typeof(GridEditor), new PropertyMetadata(null, OnGridDimensionsChanged));

        private static int gridEditorUniqueIdCounter = 0;

        private int gridEditorUniqueId;

        public GridEditor()
        {
            InitializeComponent();
            Loaded += GridEditor_Loaded;
            ((App)Application.Current).ZoneSettings.PropertyChanged += ZoneSettings_PropertyChanged;
            gridEditorUniqueId = ++gridEditorUniqueIdCounter;
        }

        private void GridEditor_Loaded(object sender, RoutedEventArgs e)
        {
            GridLayoutModel model = (GridLayoutModel)DataContext;
            if (model != null)
            {
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

                int maxIndex = 0;
                for (int row = 0; row < rows; row++)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        maxIndex = Math.Max(maxIndex, model.CellChildMap[row, col]);
                    }
                }

                for (int i = 0; i <= maxIndex; i++)
                {
                    AddZone();
                }
            }

            Model = model;
            if (Model == null)
            {
                Model = new GridLayoutModel();
                DataContext = Model;
            }

            Model.PropertyChanged += OnGridDimensionsChanged;
            AddDragHandles();
        }

        private void ZoneSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Size actualSize = new Size(ActualWidth, ActualHeight);

            // Only enter if this is the newest instance
            if (actualSize.Width > 0 && gridEditorUniqueId == gridEditorUniqueIdCounter)
            {
                ArrangeGridRects(actualSize);
            }
        }

        public GridLayoutModel Model
        {
            get { return (GridLayoutModel)GetValue(ModelProperty); }
            set { SetValue(ModelProperty, value); }
        }

        public Panel PreviewPanel
        {
            get { return Preview; }
        }

        private void OnFullSplit(object o, SplitEventArgs e)
        {
            UIElementCollection previewChildren = Preview.Children;
            UIElement splitee = (UIElement)o;

            GridLayoutModel model = Model;
            int spliteeIndex = previewChildren.IndexOf(splitee);

            int rows = model.Rows;
            int cols = model.Columns;
            _startRow = -1;
            _startCol = -1;

            for (int row = rows - 1; row >= 0; row--)
            {
                for (int col = cols - 1; col >= 0; col--)
                {
                    if (model.CellChildMap[row, col] == spliteeIndex)
                    {
                        RemoveDragHandles();
                        _startRow = _endRow = row;
                        _startCol = _endCol = col;
                        ExtendRangeToHaveEvenCellEdges();

                        for (row = _startRow; row <= _endRow; row++)
                        {
                            for (col = _startCol; col <= _endCol; col++)
                            {
                                if ((row != _startRow) || (col != _startCol))
                                {
                                    model.CellChildMap[row, col] = AddZone();
                                }
                            }
                        }

                        OnGridDimensionsChanged();
                        return;
                    }
                }
            }
        }

        private void ExtendRangeToHaveEvenCellEdges()
        {
            // As long as there is an edge of the 2D range such that some zone crosses its boundary, extend
            // that boundary. A single pass is not enough, a while loop is needed. This results in the unique
            // smallest rectangle containing the initial range such that no zone is "broken", meaning that
            // some part of it is inside the 2D range, and some part is outside.
            GridLayoutModel model = Model;
            bool possiblyBroken = true;

            while (possiblyBroken)
            {
                possiblyBroken = false;

                for (int col = _startCol; col <= _endCol; col++)
                {
                    if (_startRow > 0 && model.CellChildMap[_startRow - 1, col] == model.CellChildMap[_startRow, col])
                    {
                        _startRow--;
                        possiblyBroken = true;
                        break;
                    }

                    if (_endRow < model.Rows - 1 && model.CellChildMap[_endRow + 1, col] == model.CellChildMap[_endRow, col])
                    {
                        _endRow++;
                        possiblyBroken = true;
                        break;
                    }
                }

                for (int row = _startRow; row <= _endRow; row++)
                {
                    if (_startCol > 0 && model.CellChildMap[row, _startCol - 1] == model.CellChildMap[row, _startCol])
                    {
                        _startCol--;
                        possiblyBroken = true;
                        break;
                    }

                    if (_endCol < model.Columns - 1 && model.CellChildMap[row, _endCol + 1] == model.CellChildMap[row, _endCol])
                    {
                        _endCol++;
                        possiblyBroken = true;
                        break;
                    }
                }
            }
        }

        private void OnSplit(object o, SplitEventArgs e)
        {
            UIElementCollection previewChildren = Preview.Children;
            GridZone splitee = (GridZone)o;

            int spliteeIndex = previewChildren.IndexOf(splitee);
            GridLayoutModel model = Model;

            int rows = model.Rows;
            int cols = model.Columns;
            int foundRow = -1;
            int foundCol = -1;

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    if (model.CellChildMap[row, col] == spliteeIndex)
                    {
                        foundRow = row;
                        foundCol = col;
                        break;
                    }
                }

                if (foundRow != -1)
                {
                    break;
                }
            }

            int newChildIndex = AddZone();

            double offset = e.Offset;

            if (e.Orientation == Orientation.Vertical)
            {
                if (splitee.VerticalSnapPoints != null)
                {
                    offset += Canvas.GetLeft(splitee);
                    int count = splitee.VerticalSnapPoints.Length;
                    bool foundExistingSplit = false;

                    for (int i = 0; i <= count; i++)
                    {
                        if (foundExistingSplit)
                        {
                            int walkRow = foundRow;
                            while ((walkRow < rows) && (model.CellChildMap[walkRow, foundCol + i] == spliteeIndex))
                            {
                                model.CellChildMap[walkRow++, foundCol + i] = newChildIndex;
                            }
                        }

                        if (_colInfo[foundCol + i].End == offset)
                        {
                            foundExistingSplit = true;

                            // use existing division
                        }
                    }

                    if (foundExistingSplit)
                    {
                        OnGridDimensionsChanged();
                        return;
                    }

                    while (_colInfo[foundCol].End < offset)
                    {
                        foundCol++;
                    }

                    offset -= _colInfo[foundCol].Start;
                }

                AddDragHandle(Orientation.Vertical, cols - 1);
                cols++;
                int[,] newCellChildMap = new int[rows, cols];
                int[] newColPercents = new int[cols];
                RowColInfo[] newColInfo = new RowColInfo[cols];

                int sourceCol = 0;
                for (int col = 0; col < cols; col++)
                {
                    for (int row = 0; row < rows; row++)
                    {
                        if ((col > foundCol) && (model.CellChildMap[row, sourceCol] == spliteeIndex))
                        {
                            newCellChildMap[row, col] = newChildIndex;
                        }
                        else
                        {
                            newCellChildMap[row, col] = model.CellChildMap[row, sourceCol];
                        }
                    }

                    if (col != foundCol)
                    {
                        sourceCol++;
                    }
                }

                model.CellChildMap = newCellChildMap;

                sourceCol = 0;
                for (int col = 0; col < cols; col++)
                {
                    if (col == foundCol)
                    {
                        RowColInfo[] split = _colInfo[col].Split(offset);
                        newColPercents[col] = split[0].Percent;
                        newColInfo[col++] = split[0];
                        newColPercents[col] = split[1].Percent;
                        newColInfo[col] = split[1];
                        sourceCol++;
                    }
                    else
                    {
                        newColPercents[col] = model.ColumnPercents[sourceCol];
                        newColInfo[col] = _colInfo[sourceCol++];
                    }
                }

                _colInfo = newColInfo;
                model.ColumnPercents = newColPercents;

                model.Columns++;
            }
            else
            {
                // Horizontal
                if (splitee.HorizontalSnapPoints != null)
                {
                    offset += Canvas.GetTop(splitee);
                    int count = splitee.HorizontalSnapPoints.Length;
                    bool foundExistingSplit = false;

                    for (int i = 0; i <= count; i++)
                    {
                        if (foundExistingSplit)
                        {
                            int walkCol = foundCol;
                            while ((walkCol < cols) && (model.CellChildMap[foundRow + i, walkCol] == spliteeIndex))
                            {
                                model.CellChildMap[foundRow + i, walkCol] = newChildIndex;
                            }
                        }

                        if (_rowInfo[foundRow + i].End == offset)
                        {
                            foundExistingSplit = true;

                            // use existing division
                        }
                    }

                    if (foundExistingSplit)
                    {
                        OnGridDimensionsChanged();
                        return;
                    }

                    while (_rowInfo[foundRow].End < offset)
                    {
                        foundRow++;
                    }

                    offset -= _rowInfo[foundRow].Start;
                }

                AddDragHandle(Orientation.Horizontal, rows - 1);
                rows++;
                int[,] newCellChildMap = new int[rows, cols];
                int[] newRowPercents = new int[rows];
                RowColInfo[] newRowInfo = new RowColInfo[rows];

                int sourceRow = 0;
                for (int row = 0; row < rows; row++)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        if ((row > foundRow) && (model.CellChildMap[sourceRow, col] == spliteeIndex))
                        {
                            newCellChildMap[row, col] = newChildIndex;
                        }
                        else
                        {
                            newCellChildMap[row, col] = model.CellChildMap[sourceRow, col];
                        }
                    }

                    if (row != foundRow)
                    {
                        sourceRow++;
                    }
                }

                model.CellChildMap = newCellChildMap;

                sourceRow = 0;
                for (int row = 0; row < rows; row++)
                {
                    if (row == foundRow)
                    {
                        RowColInfo[] split = _rowInfo[row].Split(offset);
                        newRowPercents[row] = split[0].Percent;
                        newRowInfo[row++] = split[0];
                        newRowPercents[row] = split[1].Percent;
                        newRowInfo[row] = split[1];
                        sourceRow++;
                    }
                    else
                    {
                        newRowPercents[row] = model.RowPercents[sourceRow];
                        newRowInfo[row] = _rowInfo[sourceRow++];
                    }
                }

                _rowInfo = newRowInfo;
                model.RowPercents = newRowPercents;

                model.Rows++;
            }
        }

        private void RemoveDragHandles()
        {
            AdornerLayer.Children.Clear();
        }

        private void AddDragHandles()
        {
            if (AdornerLayer.Children.Count == 0)
            {
                int interiorRows = Model.Rows - 1;
                int interiorCols = Model.Columns - 1;

                for (int row = 0; row < interiorRows; row++)
                {
                    AddDragHandle(Orientation.Horizontal, row);
                }

                for (int col = 0; col < interiorCols; col++)
                {
                    AddDragHandle(Orientation.Vertical, col);
                }
            }
        }

        private void AddDragHandle(Orientation orientation, int index)
        {
            GridResizer resizer = new GridResizer
            {
                Orientation = orientation,
                Index = index,
                Model = Model,
            };
            resizer.DragDelta += Resizer_DragDelta;

            if (orientation == Orientation.Vertical)
            {
                index += Model.Rows - 1;
            }

            AdornerLayer.Children.Insert(index, resizer);
        }

        private void DeleteZone(int index)
        {
            IList<int> freeZones = Model.FreeZones;

            if (freeZones.Contains(index))
            {
                return;
            }

            freeZones.Add(index);

            GridZone zone = (GridZone)Preview.Children[index];
            zone.Visibility = Visibility.Hidden;
            zone.MinHeight = 0;
            zone.MinWidth = 0;
        }

        private int AddZone()
        {
            GridZone zone;
            if (Model != null)
            {
                IList<int> freeZones = Model.FreeZones;

                // first check free list
                if (freeZones.Count > 0)
                {
                    int freeIndex = freeZones[0];
                    freeZones.RemoveAt(0);
                    zone = (GridZone)Preview.Children[freeIndex];
                    zone.Visibility = Visibility.Visible;
                    return freeIndex;
                }
            }

            zone = new GridZone();
            zone.Split += OnSplit;
            zone.MergeDrag += OnMergeDrag;
            zone.MergeComplete += OnMergeComplete;
            zone.FullSplit += OnFullSplit;
            Preview.Children.Add(zone);
            return Preview.Children.Count - 1;
        }

        private void OnGridDimensionsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Only enter if this is the newest instance
            if (((e.PropertyName == "Rows") || (e.PropertyName == "Columns")) && gridEditorUniqueId == gridEditorUniqueIdCounter)
            {
                OnGridDimensionsChanged();
            }
        }

        private static void OnGridDimensionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GridEditor)d).OnGridDimensionsChanged();
        }

        private void OnGridDimensionsChanged()
        {
            Size actualSize = new Size(ActualWidth, ActualHeight);
            if (actualSize.Width > 0)
            {
                ArrangeGridRects(actualSize);
            }
        }

        private void ArrangeGridRects(Size arrangeSize)
        {
            GridLayoutModel model = Model;
            if (model == null)
            {
                return;
            }

            Settings settings = ((App)Application.Current).ZoneSettings;

            int spacing = settings.ShowSpacing ? settings.Spacing : 0;

            int cols = model.Columns;
            int rows = model.Rows;

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

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int i = model.CellChildMap[row, col];
                    if (((row == 0) || (model.CellChildMap[row - 1, col] != i)) &&
                        ((col == 0) || (model.CellChildMap[row, col - 1] != i)))
                    {
                        // this is not a continuation of a span
                        GridZone zone = (GridZone)Preview.Children[i];
                        left = _colInfo[col].Start;
                        top = _rowInfo[row].Start;
                        Canvas.SetLeft(zone, left);
                        Canvas.SetTop(zone, top);

                        int maxRow = row;
                        while (((maxRow + 1) < rows) && (model.CellChildMap[maxRow + 1, col] == i))
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
                        while (((maxCol + 1) < cols) && (model.CellChildMap[row, maxCol + 1] == i))
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

            AddDragHandles();
            int childIndex = 0;
            UIElementCollection adornerChildren = AdornerLayer.Children;
            for (int row = 0; row < rows - 1; row++)
            {
                GridResizer resizer = (GridResizer)adornerChildren[childIndex++];
                int startCol = -1;
                int endCol = cols - 1;
                for (int col = 0; col < cols; col++)
                {
                    if ((startCol == -1) && (model.CellChildMap[row, col] != model.CellChildMap[row + 1, col]))
                    {
                        startCol = col;
                    }
                    else if ((startCol != -1) && (model.CellChildMap[row, col] == model.CellChildMap[row + 1, col]))
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
                    if ((startRow == -1) && (model.CellChildMap[row, col] != model.CellChildMap[row, col + 1]))
                    {
                        startRow = row;
                    }
                    else if ((startRow != -1) && (model.CellChildMap[row, col] == model.CellChildMap[row, col + 1]))
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

        private void Resizer_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            GridResizer resizer = (GridResizer)sender;
            int[] percents;
            RowColInfo[] info;
            int index = resizer.Index;
            double delta;

            if (resizer.Orientation == Orientation.Vertical)
            {
                percents = Model.ColumnPercents;
                info = _colInfo;
                delta = e.HorizontalChange;
            }
            else
            {
                percents = Model.RowPercents;
                info = _rowInfo;
                delta = e.VerticalChange;
            }

            double currentExtent = info[index].Extent;
            double newExtent = currentExtent + delta;
            int currentPercent = info[index].Percent;
            int totalPercent = currentPercent + info[index + 1].Percent;

            int newPercent = (int)(currentPercent * newExtent / currentExtent);

            if ((newPercent > 0) && (newPercent < totalPercent))
            {
                percents[index] = info[index].Percent = newPercent;
                percents[index + 1] = info[index + 1].Percent = totalPercent - newPercent;

                Size actualSize = new Size(ActualWidth, ActualHeight);
                ArrangeGridRects(actualSize);
            }
        }

        private Point _startDragPos = new Point(-1, -1);

        private void OnMergeComplete(object o, MouseButtonEventArgs e)
        {
            Point mousePoint = e.GetPosition(Preview);
            _startDragPos = new Point(-1, -1);

            int mergedIndex = Model.CellChildMap[_startRow, _startCol];

            for (int row = _startRow; row <= _endRow; row++)
            {
                for (int col = _startCol; col <= _endCol; col++)
                {
                    if (Model.CellChildMap[row, col] != mergedIndex)
                    {
                        // selection is more than one cell, merge is valid
                        MergePanel.Visibility = Visibility.Visible;
                        Canvas.SetTop(MergeButtons, mousePoint.Y);
                        Canvas.SetLeft(MergeButtons, mousePoint.X);
                        return;
                    }
                }
            }

            // merge is only one zone. cancel merge;
            ClearSelection();
        }

        private void OnMergeDrag(object o, MouseEventArgs e)
        {
            if (_startDragPos.X == -1)
            {
                _startDragPos = e.GetPosition(Preview);
            }

            GridLayoutModel model = Model;

            if (_startDragPos.X != -1)
            {
                Point dragPos = e.GetPosition(Preview);

                _startRow = -1;
                _endRow = -1;
                _startCol = -1;
                _endCol = -1;

                int rows = model.Rows;
                int cols = model.Columns;

                double minX, maxX;
                if (dragPos.X < _startDragPos.X)
                {
                    minX = dragPos.X;
                    maxX = _startDragPos.X;
                }
                else
                {
                    minX = _startDragPos.X;
                    maxX = dragPos.X;
                }

                double minY, maxY;
                if (dragPos.Y < _startDragPos.Y)
                {
                    minY = dragPos.Y;
                    maxY = _startDragPos.Y;
                }
                else
                {
                    minY = _startDragPos.Y;
                    maxY = dragPos.Y;
                }

                for (int row = 0; row < rows; row++)
                {
                    if (_startRow == -1)
                    {
                        if (_rowInfo[row].End > minY)
                        {
                            _startRow = row;
                        }
                    }
                    else if (_rowInfo[row].Start > maxY)
                    {
                        _endRow = row - 1;
                        break;
                    }
                }

                if ((_startRow >= 0) && (_endRow == -1))
                {
                    _endRow = rows - 1;
                }

                for (int col = 0; col < cols; col++)
                {
                    if (_startCol == -1)
                    {
                        if (_colInfo[col].End > minX)
                        {
                            _startCol = col;
                        }
                    }
                    else if (_colInfo[col].Start > maxX)
                    {
                        _endCol = col - 1;
                        break;
                    }
                }

                if ((_startCol >= 0) && (_endCol == -1))
                {
                    _endCol = cols - 1;
                }

                ExtendRangeToHaveEvenCellEdges();

                for (int row = 0; row < rows; row++)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        ((GridZone)Preview.Children[model.CellChildMap[row, col]]).IsSelected = (row >= _startRow) && (row <= _endRow) && (col >= _startCol) && (col <= _endCol);
                    }
                }

                e.Handled = true;
            }

            OnPreviewMouseMove(e);
        }

        private void ClearSelection()
        {
            foreach (UIElement zone in Preview.Children)
            {
                ((GridZone)zone).IsSelected = false;
            }
        }

        private void MergeClick(object sender, RoutedEventArgs e)
        {
            GridLayoutModel model = Model;

            MergePanel.Visibility = Visibility.Collapsed;
            int mergedIndex = model.CellChildMap[_startRow, _startCol];

            for (int row = _startRow; row <= _endRow; row++)
            {
                for (int col = _startCol; col <= _endCol; col++)
                {
                    int childIndex = model.CellChildMap[row, col];
                    if (childIndex != mergedIndex)
                    {
                        model.CellChildMap[row, col] = mergedIndex;
                        DeleteZone(childIndex);
                    }
                }
            }

            OnGridDimensionsChanged();
            ClearSelection();
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            MergePanel.Visibility = Visibility.Collapsed;
            ClearSelection();
        }

        private void MergePanelMouseUp(object sender, MouseButtonEventArgs e)
        {
            CancelClick(null, null);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            Size returnSize = base.ArrangeOverride(arrangeBounds);
            ArrangeGridRects(arrangeBounds);

            return returnSize;
        }

        private RowColInfo[] _rowInfo;
        private RowColInfo[] _colInfo;

        private int _startRow = -1;
        private int _endRow = -1;
        private int _startCol = -1;
        private int _endCol = -1;
    }
}
