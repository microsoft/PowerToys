// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
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

        private static int gridEditorUniqueIdCounter;

        private int gridEditorUniqueId;

        private GridData _data;

        public GridEditor()
        {
            InitializeComponent();
            Loaded += GridEditor_Loaded;
            Unloaded += GridEditor_Unloaded;
            ((App)Application.Current).MainWindowSettings.PropertyChanged += ZoneSettings_PropertyChanged;
            gridEditorUniqueId = ++gridEditorUniqueIdCounter;
        }

        private void GridEditor_Loaded(object sender, RoutedEventArgs e)
        {
            GridLayoutModel model = (GridLayoutModel)DataContext;
            if (model == null)
            {
                return;
            }

            _data = new GridData(model);

            Model = model;
            Model.PropertyChanged += OnGridDimensionsChanged;
            SetupUI();
        }

        private void PlaceResizer(GridResizer resizerThumb)
        {
            var leftZone = Preview.Children[resizerThumb.LeftReferenceZone];
            var rightZone = Preview.Children[resizerThumb.RightReferenceZone];
            var topZone = Preview.Children[resizerThumb.TopReferenceZone];
            var bottomZone = Preview.Children[resizerThumb.BottomReferenceZone];

            double left = Canvas.GetLeft(leftZone);
            double right = Canvas.GetLeft(rightZone) + (rightZone as GridZone).MinWidth;

            double top = Canvas.GetTop(topZone);
            double bottom = Canvas.GetTop(bottomZone) + (bottomZone as GridZone).MinHeight;

            double x = (left + right) / 2.0;
            double y = (top + bottom) / 2.0;

            Canvas.SetLeft(resizerThumb, x - 24);
            Canvas.SetTop(resizerThumb, y - 24);
        }

        private void SetupUI()
        {
            Size actualSize = WorkAreaSize();

            if (actualSize.Width < 1 || _data == null)
            {
                return;
            }

            Preview.Children.Clear();
            AdornerLayer.Children.Clear();

            Preview.Width = actualSize.Width;
            Preview.Height = actualSize.Height;

            foreach (var zone in _data.Zones)
            {
                var zonePanel = new GridZone(Model.ShowSpacing ? Model.Spacing : 0);
                Preview.Children.Add(zonePanel);
                zonePanel.Split += ZonePanel_Split;
                Canvas.SetTop(zonePanel, actualSize.Height * zone.Top / _data.Multiplier);
                Canvas.SetLeft(zonePanel, actualSize.Width * zone.Left / _data.Multiplier);
                zonePanel.MinWidth = actualSize.Width * (zone.Right - zone.Left) / _data.Multiplier;
                zonePanel.MinHeight = actualSize.Height * (zone.Bottom - zone.Top) / _data.Multiplier;
            }

            foreach (var resizer in _data.Resizers)
            {
                var resizerThumb = new GridResizer();
                resizerThumb.DragStarted += Resizer_DragStarted;
                resizerThumb.DragDelta += Resizer_DragDelta;
                resizerThumb.DragCompleted += Resizer_DragCompleted;
                resizerThumb.Orientation = resizer.Orientation;
                AdornerLayer.Children.Add(resizerThumb);

                if (resizer.Orientation == Orientation.Horizontal)
                {
                    resizerThumb.LeftReferenceZone = resizer.PositiveSideIndices[0];
                    resizerThumb.RightReferenceZone = resizer.PositiveSideIndices.Last();
                    resizerThumb.TopReferenceZone = resizer.PositiveSideIndices[0];
                    resizerThumb.BottomReferenceZone = resizer.NegativeSideIndices[0];
                }
                else
                {
                    resizerThumb.LeftReferenceZone = resizer.PositiveSideIndices[0];
                    resizerThumb.RightReferenceZone = resizer.NegativeSideIndices[0];
                    resizerThumb.TopReferenceZone = resizer.PositiveSideIndices[0];
                    resizerThumb.BottomReferenceZone = resizer.PositiveSideIndices.Last();
                }

                PlaceResizer(resizerThumb);
            }
        }

        private void ZonePanel_Split(object sender, SplitEventArgs args)
        {
            Size actualSize = WorkAreaSize();
            int zoneIndex = Preview.Children.IndexOf(sender as GridZone);

            int splitBase = args.Orientation == Orientation.Horizontal ? _data.Zones[zoneIndex].Top : _data.Zones[zoneIndex].Left;
            double screenSize = args.Orientation == Orientation.Horizontal ? actualSize.Height : actualSize.Width;

            int dataOffset = splitBase + Convert.ToInt32(args.Offset * _data.Multiplier / screenSize);

            if (_data.CanSplit(zoneIndex, dataOffset, args.Orientation))
            {
                _data.Split(zoneIndex, dataOffset, args.Orientation);
            }

            SetupUI();
        }

        private void GridEditor_Unloaded(object sender, RoutedEventArgs e)
        {
            gridEditorUniqueId = -1;
        }

        private Size WorkAreaSize()
        {
            Rect workingArea = App.Overlay.WorkArea;
            return new Size(workingArea.Width, workingArea.Height);
        }

        private void ZoneSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var actualSize = WorkAreaSize();

            // Only enter if this is the newest instance
            if (actualSize.Width > 0 && gridEditorUniqueId == gridEditorUniqueIdCounter)
            {
                SetupUI();
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

        private void OnSplit(object o, int position, Orientation orientation)
        {
            MergeCancelClick(null, null);

            UIElementCollection previewChildren = Preview.Children;
            GridZone splitee = (GridZone)o;

            _data.Split(previewChildren.IndexOf(splitee), position, orientation);
            SetupUI();
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
            ((GridEditor)d).SetupUI();
        }

        private void OnGridDimensionsChanged()
        {
            SetupUI();
        }

        private double _dragX = 0;
        private double _dragY = 0;

        private void Resizer_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            _dragX = 0;
            _dragY = 0;
        }

        private void Resizer_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            MergeCancelClick(null, null);

            _dragX += e.HorizontalChange;
            _dragY += e.VerticalChange;

            GridResizer resizer = (GridResizer)sender;
            int resizerIndex = AdornerLayer.Children.IndexOf(resizer);

            Size actualSize = WorkAreaSize();

            int delta;

            if (resizer.Orientation == Orientation.Vertical)
            {
                delta = Convert.ToInt32(_dragX / actualSize.Width * _data.Multiplier);
            }
            else
            {
                delta = Convert.ToInt32(_dragY / actualSize.Height * _data.Multiplier);
            }

            if (_data.CanDrag(resizerIndex, delta))
            {
                // Just update the UI, don't tell _data
                if (resizer.Orientation == Orientation.Vertical)
                {
                    _data.Resizers[resizerIndex].PositiveSideIndices.ForEach((zoneIndex) =>
                    {
                        var zone = Preview.Children[zoneIndex];
                        Canvas.SetLeft(zone, Canvas.GetLeft(zone) + e.HorizontalChange);
                        (zone as GridZone).MinWidth -= e.HorizontalChange;
                    });

                    _data.Resizers[resizerIndex].NegativeSideIndices.ForEach((zoneIndex) =>
                    {
                        var zone = Preview.Children[zoneIndex];
                        Canvas.SetRight(zone, Canvas.GetRight(zone) + e.HorizontalChange);
                        (zone as GridZone).MinWidth += e.HorizontalChange;
                    });

                    Canvas.SetLeft(resizer, Canvas.GetLeft(resizer) + e.HorizontalChange);
                }
                else
                {
                    _data.Resizers[resizerIndex].PositiveSideIndices.ForEach((zoneIndex) =>
                    {
                        var zone = Preview.Children[zoneIndex];
                        Canvas.SetTop(zone, Canvas.GetTop(zone) + e.VerticalChange);
                        (zone as GridZone).MinHeight -= e.VerticalChange;
                    });

                    _data.Resizers[resizerIndex].NegativeSideIndices.ForEach((zoneIndex) =>
                    {
                        var zone = Preview.Children[zoneIndex];
                        Canvas.SetBottom(zone, Canvas.GetBottom(zone) + e.VerticalChange);
                        (zone as GridZone).MinHeight += e.VerticalChange;
                    });

                    Canvas.SetTop(resizer, Canvas.GetTop(resizer) + e.VerticalChange);
                }

                foreach (var child in AdornerLayer.Children)
                {
                    GridResizer resizerThumb = child as GridResizer;
                    if (resizerThumb != resizer)
                    {
                        PlaceResizer(resizerThumb);
                    }
                }
            }
            else
            {
                // Undo changes
                _dragX -= e.HorizontalChange;
                _dragY -= e.VerticalChange;
            }
        }

        private void Resizer_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            GridResizer resizer = (GridResizer)sender;
            int resizerIndex = AdornerLayer.Children.IndexOf(resizer);
            Size actualSize = WorkAreaSize();

            double pixelDelta = resizer.Orientation == Orientation.Vertical ?
                _dragX / actualSize.Width * _data.Multiplier :
                _dragY / actualSize.Height * _data.Multiplier;

            _data.Drag(resizerIndex, Convert.ToInt32(pixelDelta));

            SetupUI();
        }

        private Point _startDragPos = new Point(-1, -1);

        private void OnMergeComplete(object o, MouseButtonEventArgs e)
        {
            // merge is only one zone. cancel merge;
            ClearSelection();
        }

        private void OnMergeDrag(object o, MouseEventArgs e)
        {
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
            SetupUI();

            return returnSize;
        }
    }
}
