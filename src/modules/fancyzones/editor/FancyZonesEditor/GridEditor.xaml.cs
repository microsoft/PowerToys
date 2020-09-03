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
        // Non-localizable strings
        private const string PropertyRowsChangedID = "Rows";
        private const string PropertyColumnsChangedID = "Columns";
        private const string ObjectDependencyID = "Model";

        public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(ObjectDependencyID, typeof(GridLayoutModel), typeof(GridEditor), new PropertyMetadata(null, OnGridDimensionsChanged));

        private static int gridEditorUniqueIdCounter = 0;

        private int gridEditorUniqueId;

        public GridEditor()
        {
            InitializeComponent();
            Loaded += GridEditor_Loaded;
            Unloaded += GridEditor_Unloaded;
            ((App)Application.Current).ZoneSettings.PropertyChanged += ZoneSettings_PropertyChanged;
            gridEditorUniqueId = ++gridEditorUniqueIdCounter;
        }

        private void GridEditor_Loaded(object sender, RoutedEventArgs e)
        {
            GridLayoutModel model = (GridLayoutModel)DataContext;
            if (model != null)
            {
                _data = new GridData(model);
                _dragHandles = new GridDragHandles(AdornerLayer.Children, Resizer_DragDelta, Resizer_DragCompleted);

                int zoneCount = _data.ZoneCount;
                for (int i = 0; i <= zoneCount; i++)
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
            _dragHandles.InitDragHandles(model);
        }

        private void GridEditor_Unloaded(object sender, RoutedEventArgs e)
        {
            gridEditorUniqueId = -1;
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
                        _dragHandles.RemoveDragHandles();
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
            MergeCancelClick(null, null);

            UIElementCollection previewChildren = Preview.Children;
            GridZone splitee = (GridZone)o;

            int spliteeIndex = previewChildren.IndexOf(splitee);
            GridLayoutModel model = Model;

            int rows = model.Rows;
            int cols = model.Columns;

            Tuple<int, int> rowCol = _data.RowColByIndex(spliteeIndex);
            int foundRow = rowCol.Item1;
            int foundCol = rowCol.Item2;

            int newChildIndex = AddZone();

            double offset = e.Offset;
            double space = e.Space;

            if (e.Orientation == Orientation.Vertical)
            {
                if (splitee.VerticalSnapPoints != null)
                {
                    offset += Canvas.GetLeft(splitee);
                    int count = splitee.VerticalSnapPoints.Length;
                    bool foundExistingSplit = false;
                    int splitCol = foundCol;

                    for (int i = 0; i <= count; i++)
                    {
                        if (foundExistingSplit)
                        {
                            int walkRow = foundRow;
                            while ((walkRow < rows) && (_data.GetIndex(walkRow, foundCol + i) == spliteeIndex))
                            {
                                _data.SetIndex(walkRow++, foundCol + i, newChildIndex);
                            }
                        }

                        if (_data.ColumnBottom(foundCol + i) == offset)
                        {
                            foundExistingSplit = true;
                            splitCol = foundCol + i;

                            // use existing division
                        }
                    }

                    if (foundExistingSplit)
                    {
                        _data.ReplaceIndicesToMaintainOrder(Preview.Children.Count);
                        _dragHandles.UpdateForExistingVerticalSplit(model, foundRow, splitCol);
                        OnGridDimensionsChanged();
                        return;
                    }

                    while (_data.ColumnBottom(foundCol) < offset)
                    {
                        foundCol++;
                    }

                    offset -= _data.ColumnTop(foundCol);
                }

                _dragHandles.UpdateAfterVerticalSplit(foundCol);
                _data.SplitColumn(foundCol, spliteeIndex, newChildIndex, space, offset, ActualWidth);
                _dragHandles.AddDragHandle(Orientation.Vertical, foundRow, foundCol, model);
            }
            else
            {
                // Horizontal
                if (splitee.HorizontalSnapPoints != null)
                {
                    offset += Canvas.GetTop(splitee);
                    int count = splitee.HorizontalSnapPoints.Length;
                    bool foundExistingSplit = false;
                    int splitRow = foundRow;

                    for (int i = 0; i <= count; i++)
                    {
                        if (foundExistingSplit)
                        {
                            int walkCol = foundCol;
                            while ((walkCol < cols) && (_data.GetIndex(foundRow + i, walkCol) == spliteeIndex))
                            {
                                _data.SetIndex(foundRow + i, walkCol++, newChildIndex);
                            }
                        }

                        if (_data.RowEnd(foundRow + i) == offset)
                        {
                            foundExistingSplit = true;
                            splitRow = foundRow + i;

                            // use existing division
                        }
                    }

                    if (foundExistingSplit)
                    {
                        _data.ReplaceIndicesToMaintainOrder(Preview.Children.Count);
                        _dragHandles.UpdateForExistingHorizontalSplit(model, splitRow, foundCol);
                        OnGridDimensionsChanged();
                        return;
                    }

                    while (_data.RowEnd(foundRow) < offset)
                    {
                        foundRow++;
                    }

                    offset -= _data.RowStart(foundRow);
                }

                _dragHandles.UpdateAfterHorizontalSplit(foundRow);
                _data.SplitRow(foundRow, spliteeIndex, newChildIndex, space, offset, ActualHeight);
                _dragHandles.AddDragHandle(Orientation.Horizontal, foundRow, foundCol, model);
            }

            Size actualSize = new Size(ActualWidth, ActualHeight);
            ArrangeGridRects(actualSize);
        }

        private void DeleteZone(int index)
        {
            Preview.Children.RemoveAt(index);
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
            if (((e.PropertyName == PropertyRowsChangedID) || (e.PropertyName == PropertyColumnsChangedID)) && gridEditorUniqueId == gridEditorUniqueIdCounter)
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

            if (model.Rows != model.RowPercents.Count || model.Columns != model.ColumnPercents.Count)
            {
                // Merge was not finished
                return;
            }

            Settings settings = ((App)Application.Current).ZoneSettings;

            int spacing = settings.ShowSpacing ? settings.Spacing : 0;

            _data.RecalculateZones(spacing, arrangeSize);
            _data.ArrangeZones(Preview.Children, spacing);
            _dragHandles.InitDragHandles(model);
            _data.ArrangeResizers(AdornerLayer.Children, spacing);
        }

        private void Resizer_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            MergeCancelClick(null, null);

            GridResizer resizer = (GridResizer)sender;

            double delta = (resizer.Orientation == Orientation.Vertical) ? e.HorizontalChange : e.VerticalChange;
            if (delta == 0)
            {
                return;
            }

            GridData.ResizeInfo resizeInfo = _data.CalculateResizeInfo(resizer, delta);
            if (resizeInfo.IsResizeAllowed)
            {
                if (_dragHandles.HasSnappedNonAdjascentResizers(resizer))
                {
                    double spacing = 0;
                    Settings settings = ((App)Application.Current).ZoneSettings;
                    if (settings.ShowSpacing)
                    {
                        spacing = settings.Spacing;
                    }

                    _data.SplitOnDrag(resizer, delta, spacing);
                    _dragHandles.UpdateAfterDetach(resizer, delta);
                }
                else
                {
                    _data.DragResizer(resizer, resizeInfo);
                    if (_data.SwapNegativePercents(resizer.Orientation, resizer.StartRow, resizer.EndRow, resizer.StartCol, resizer.EndCol))
                    {
                        _dragHandles.UpdateAfterSwap(resizer, delta);
                    }
                }
            }

            Size actualSize = new Size(ActualWidth, ActualHeight);
            ArrangeGridRects(actualSize);
            AdornerLayer.UpdateLayout();
        }

        private void Resizer_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            GridResizer resizer = (GridResizer)sender;
            int index = _data.SwappedIndexAfterResize(resizer);
            if (index != -1)
            {
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
                        if (_data.RowEnd(row) > minY)
                        {
                            _startRow = row;
                        }
                    }
                    else if (_data.RowStart(row) > maxY)
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
                        if (_data.ColumnBottom(col) > minX)
                        {
                            _startCol = col;
                        }
                    }
                    else if (_data.ColumnTop(col) > maxX)
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
            MergePanel.Visibility = Visibility.Collapsed;

            Action<int> deleteAction = (index) =>
            {
                DeleteZone(index);
            };
            _data.MergeZones(_startRow, _endRow, _startCol, _endCol, deleteAction, Preview.Children.Count);
            _dragHandles.RemoveDragHandles();
            _dragHandles.InitDragHandles(Model);

            OnGridDimensionsChanged();
            ClearSelection();
        }

        private void MergeCancelClick(object sender, RoutedEventArgs e)
        {
            MergePanel.Visibility = Visibility.Collapsed;
            ClearSelection();
        }

        private void MergePanelMouseUp(object sender, MouseButtonEventArgs e)
        {
            MergeCancelClick(null, null);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            Size returnSize = base.ArrangeOverride(arrangeBounds);
            ArrangeGridRects(arrangeBounds);

            return returnSize;
        }

        private GridData _data;
        private GridDragHandles _dragHandles;

        private int _startRow = -1;
        private int _endRow = -1;
        private int _startCol = -1;
        private int _endCol = -1;
    }
}
