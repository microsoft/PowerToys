// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

using FancyZonesEditor.Models;
using ManagedCommon;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;

namespace FancyZonesEditor
{
    /// <summary>
    /// GridEditor is how you tweak an initial GridLayoutModel before saving
    /// </summary>
    public sealed partial class GridEditor : UserControl
    {
        // Non-localizable strings
        private const string PropertyRowsChangedID = "Rows";
        private const string PropertyColumnsChangedID = "Columns";
        private const string ObjectDependencyID = "Model";
        private const string PropertyIsShiftKeyPressedID = "IsShiftKeyPressed";

        private const int MinZoneSize = 100;

        public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(ObjectDependencyID, typeof(GridLayoutModel), typeof(GridEditor), new PropertyMetadata(null, OnGridDimensionsChanged));

        private static int gridEditorUniqueIdCounter;

        private readonly GridData _data;

        private int gridEditorUniqueId;
        private double _dragX;
        private double _dragY;
        private bool _inMergeDrag;
        private Point _mergeDragStart;

        public GridEditor(GridLayoutModel layoutModel)
        {
            InitializeComponent();
            Loaded += GridEditor_Loaded;
            Unloaded += GridEditor_Unloaded;
            KeyDown += GridEditor_KeyDown;
            KeyUp += GridEditor_KeyUp;
            gridEditorUniqueId = ++gridEditorUniqueIdCounter;

            _data = new GridData(layoutModel);
            Model = layoutModel;
        }

        public GridLayoutModel Model
        {
            get { return (GridLayoutModel)GetValue(ModelProperty); }
            private set { SetValue(ModelProperty, value); }
        }

        public Panel PreviewPanel
        {
            get { return Preview; }
        }

        public void FocusZone()
        {
            if (Preview.Children.Count > 0 && Preview.Children[0] is GridZone zone)
            {
                zone.Focus(FocusState.Programmatic);
            }
        }

        private static bool IsCtrlKeyDown()
        {
            return InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
        }

        private static void OnGridDimensionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GridEditor)d).SetupUI();
        }

        private void GridEditor_Loaded(object sender, RoutedEventArgs e)
        {
            ((App)Application.Current).MainWindowSettings.PropertyChanged += ZoneSettings_PropertyChanged;
            Model.PropertyChanged += OnGridDimensionsChanged;
            SetupUI();
        }

        private void GridEditor_Unloaded(object sender, RoutedEventArgs e)
        {
            ((App)Application.Current).MainWindowSettings.PropertyChanged -= ZoneSettings_PropertyChanged;

            if (Model != null)
            {
                Model.PropertyChanged -= OnGridDimensionsChanged;
            }

            gridEditorUniqueId = -1;
        }

        private void HandleResizerKeyDown(GridResizer resizer, KeyRoutedEventArgs e)
        {
            double horizontalChange = 0;
            double verticalChange = 0;
            bool moved = false;

            if (resizer.Orientation == Orientation.Horizontal)
            {
                if (e.Key == VirtualKey.Up)
                {
                    verticalChange = -1;
                    moved = true;
                }
                else if (e.Key == VirtualKey.Down)
                {
                    verticalChange = 1;
                    moved = true;
                }
            }
            else
            {
                if (e.Key == VirtualKey.Left)
                {
                    horizontalChange = -1;
                    moved = true;
                }
                else if (e.Key == VirtualKey.Right)
                {
                    horizontalChange = 1;
                    moved = true;
                }
            }

            if (moved)
            {
                e.Handled = true;

                // WinUI's DragDeltaEventArgs cannot be constructed, so the keyboard path
                // calls the shared drag implementation directly.
                DragResizer(resizer, horizontalChange, verticalChange);
            }

            if (e.Key == VirtualKey.Delete)
            {
                int resizerIndex = AdornerLayer.Children.IndexOf(resizer);
                var resizerData = _data.Resizers[resizerIndex];

                var indices = new List<int>(resizerData.PositiveSideIndices);
                indices.AddRange(resizerData.NegativeSideIndices);
                _data.DoMerge(indices);
                SetupUI();
                e.Handled = true;
            }
        }

        private void HandleResizerKeyUp(GridResizer resizer, KeyRoutedEventArgs e)
        {
            if (resizer.Orientation == Orientation.Horizontal)
            {
                e.Handled = e.Key == VirtualKey.Up || e.Key == VirtualKey.Down;
            }
            else
            {
                e.Handled = e.Key == VirtualKey.Left || e.Key == VirtualKey.Right;
            }

            if (e.Handled)
            {
                int resizerIndex = AdornerLayer.Children.IndexOf(resizer);
                CompleteResizerDrag(resizer);
                Debug.Assert(AdornerLayer.Children.Count > resizerIndex, "Resizer index out of range");
                AdornerLayer.Children[resizerIndex].Focus(FocusState.Programmatic);
                _dragY = _dragX = 0;
            }
        }

        private void HandleGridZoneKeyUp(GridZone gridZone, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.S)
            {
                return;
            }

            Orientation orient = Orientation.Horizontal;

            Debug.Assert(Preview.Children.Count > Preview.Children.IndexOf(gridZone), "Zone index out of range");

            int offset;
            if (((App)Application.Current).MainWindowSettings.IsShiftKeyPressed)
            {
                orient = Orientation.Vertical;
                offset = gridZone.SnapAtHalfX();
            }
            else
            {
                offset = gridZone.SnapAtHalfY();
            }

            gridZone.DoSplit(orient, offset);
        }

        private void GridEditor_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Tab && IsCtrlKeyDown())
            {
                e.Handled = true;
                App.Overlay.FocusEditorWindow();
            }
            else if (FocusManager.GetFocusedElement(XamlRoot) is GridResizer resizer)
            {
                HandleResizerKeyDown(resizer, e);
            }
        }

        private void GridEditor_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            var focused = FocusManager.GetFocusedElement(XamlRoot);

            if (focused is GridResizer resizer)
            {
                HandleResizerKeyUp(resizer, e);
                return;
            }

            if (focused is GridZone gridZone)
            {
                HandleGridZoneKeyUp(gridZone, e);
            }
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

        private void SetZonePanelSize(GridZone panel, GridData.Zone zone)
        {
            Size actualSize = WorkAreaSize();
            double spacing = Model.ShowSpacing ? Model.Spacing : 0;

            double topSpacing = zone.Top == 0 ? spacing : spacing / 2;
            double bottomSpacing = zone.Bottom == GridData.Multiplier ? spacing : spacing / 2;
            double leftSpacing = zone.Left == 0 ? spacing : spacing / 2;
            double rightSpacing = zone.Right == GridData.Multiplier ? spacing : spacing / 2;

            Canvas.SetTop(panel, (actualSize.Height * zone.Top / GridData.Multiplier) + topSpacing);
            Canvas.SetLeft(panel, (actualSize.Width * zone.Left / GridData.Multiplier) + leftSpacing);
            panel.MinWidth = Math.Max(1, (actualSize.Width * (zone.Right - zone.Left) / GridData.Multiplier) - leftSpacing - rightSpacing);
            panel.MinHeight = Math.Max(1, (actualSize.Height * (zone.Bottom - zone.Top) / GridData.Multiplier) - topSpacing - bottomSpacing);
        }

        private void SetupUI()
        {
            Size actualSize = WorkAreaSize();

            if (actualSize.Width < 1 || _data == null || _data.Zones == null || Model == null)
            {
                return;
            }

            int spacing = Model.ShowSpacing ? Model.Spacing : 0;

            _data.MinZoneWidth = Convert.ToInt32(GridData.Multiplier / actualSize.Width * (MinZoneSize + (2 * spacing)));
            _data.MinZoneHeight = Convert.ToInt32(GridData.Multiplier / actualSize.Height * (MinZoneSize + (2 * spacing)));

            Preview.Children.Clear();
            AdornerLayer.Children.Clear();

            Preview.Width = actualSize.Width;
            Preview.Height = actualSize.Height;

            MagneticSnap snapX = new MagneticSnap(GridData.PrefixSum(Model.ColumnPercents).GetRange(1, Model.ColumnPercents.Count - 1), actualSize.Width);
            MagneticSnap snapY = new MagneticSnap(GridData.PrefixSum(Model.RowPercents).GetRange(1, Model.RowPercents.Count - 1), actualSize.Height);

            for (int zoneIndex = 0; zoneIndex < _data.Zones.Count; zoneIndex++)
            {
                // this is needed for the lambda
                int zoneIndexCopy = zoneIndex;

                var zone = _data.Zones[zoneIndex];
                var zonePanel = new GridZone(spacing, snapX, snapY, (orientation, offset) => _data.CanSplit(zoneIndexCopy, offset, orientation), zone);
                zonePanel.UpdateShiftState(((App)Application.Current).MainWindowSettings.IsShiftKeyPressed);
                Preview.Children.Add(zonePanel);
                zonePanel.Split += OnSplit;
                zonePanel.MergeDrag += OnMergeDrag;
                zonePanel.MergeComplete += OnMergeComplete;
                SetZonePanelSize(zonePanel, zone);
                zonePanel.LabelID.Text = (zoneIndex + 1).ToString(CultureInfo.CurrentCulture);
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

        private void OnSplit(object sender, SplitEventArgs args)
        {
            Logger.LogTrace();

            MergeCancelClick(null, null);

            var zonePanel = sender as GridZone;
            int zoneIndex = Preview.Children.IndexOf(zonePanel);

            if (_data.CanSplit(zoneIndex, args.Offset, args.Orientation))
            {
                _data.Split(zoneIndex, args.Offset, args.Orientation);
                SetupUI();
            }
        }

        private Size WorkAreaSize()
        {
            Rect workingArea = App.Overlay.WorkArea;
            return new Size(workingArea.Width, workingArea.Height);
        }

        private void OnGridDimensionsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Only enter if this is the newest instance
            if (((e.PropertyName == PropertyRowsChangedID) || (e.PropertyName == PropertyColumnsChangedID)) && gridEditorUniqueId == gridEditorUniqueIdCounter)
            {
                SetupUI();
            }
        }

        private void ZoneSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if ((e.PropertyName == PropertyIsShiftKeyPressedID) && gridEditorUniqueId == gridEditorUniqueIdCounter)
            {
                foreach (var child in Preview.Children)
                {
                    var zone = child as GridZone;
                    zone.UpdateShiftState(((App)Application.Current).MainWindowSettings.IsShiftKeyPressed);
                }
            }
        }

        private void Resizer_DragStarted(object sender, DragStartedEventArgs e)
        {
            _dragX = 0;
            _dragY = 0;
        }

        private void Resizer_DragDelta(object sender, DragDeltaEventArgs e)
        {
            DragResizer((GridResizer)sender, e.HorizontalChange, e.VerticalChange);
        }

        private void DragResizer(GridResizer resizer, double horizontalChange, double verticalChange)
        {
            MergeCancelClick(null, null);

            _dragX += horizontalChange;
            _dragY += verticalChange;

            int resizerIndex = AdornerLayer.Children.IndexOf(resizer);

            Size actualSize = WorkAreaSize();

            int delta;

            if (resizer.Orientation == Orientation.Vertical)
            {
                delta = Convert.ToInt32(_dragX / actualSize.Width * GridData.Multiplier);
            }
            else
            {
                delta = Convert.ToInt32(_dragY / actualSize.Height * GridData.Multiplier);
            }

            if (resizerIndex != -1 && _data.CanDrag(resizerIndex, delta))
            {
                // Just update the UI, don't tell _data.
                // Zones are laid out with Canvas.Left/Top only, so growing a zone on the
                // negative side is expressed purely through its MinWidth/MinHeight
                // (WinUI's Canvas has no Right/Bottom attached properties).
                if (resizer.Orientation == Orientation.Vertical)
                {
                    _data.Resizers[resizerIndex].PositiveSideIndices.ForEach((zoneIndex) =>
                    {
                        var zone = Preview.Children[zoneIndex];
                        Canvas.SetLeft(zone, Canvas.GetLeft(zone) + horizontalChange);
                        (zone as GridZone).MinWidth -= horizontalChange;
                    });

                    _data.Resizers[resizerIndex].NegativeSideIndices.ForEach((zoneIndex) =>
                    {
                        var zone = Preview.Children[zoneIndex];
                        (zone as GridZone).MinWidth += horizontalChange;
                    });

                    Canvas.SetLeft(resizer, Canvas.GetLeft(resizer) + horizontalChange);
                }
                else
                {
                    _data.Resizers[resizerIndex].PositiveSideIndices.ForEach((zoneIndex) =>
                    {
                        var zone = Preview.Children[zoneIndex];
                        Canvas.SetTop(zone, Canvas.GetTop(zone) + verticalChange);
                        (zone as GridZone).MinHeight -= verticalChange;
                    });

                    _data.Resizers[resizerIndex].NegativeSideIndices.ForEach((zoneIndex) =>
                    {
                        var zone = Preview.Children[zoneIndex];
                        (zone as GridZone).MinHeight += verticalChange;
                    });

                    Canvas.SetTop(resizer, Canvas.GetTop(resizer) + verticalChange);
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
                _dragX -= horizontalChange;
                _dragY -= verticalChange;
            }
        }

        private void Resizer_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            CompleteResizerDrag((GridResizer)sender);
        }

        private void CompleteResizerDrag(GridResizer resizer)
        {
            int resizerIndex = AdornerLayer.Children.IndexOf(resizer);
            if (resizerIndex == -1)
            {
                // Resizer was removed during drag
                return;
            }

            Size actualSize = WorkAreaSize();

            double pixelDelta = resizer.Orientation == Orientation.Vertical ?
                _dragX / actualSize.Width * GridData.Multiplier :
                _dragY / actualSize.Height * GridData.Multiplier;

            _data.Drag(resizerIndex, Convert.ToInt32(pixelDelta));

            SetupUI();
        }

        private void OnMergeComplete(object o, PointerRoutedEventArgs e)
        {
            Logger.LogTrace();
            _inMergeDrag = false;

            var selectedIndices = new List<int>();
            for (int zoneIndex = 0; zoneIndex < _data.Zones.Count; zoneIndex++)
            {
                if ((Preview.Children[zoneIndex] as GridZone).IsSelected)
                {
                    selectedIndices.Add(zoneIndex);
                }
            }

            if (selectedIndices.Count <= 1)
            {
                ClearSelection();
            }
            else
            {
                Point mousePoint = e.GetCurrentPoint(Preview).Position;
                MergePanel.Visibility = Visibility.Visible;
                Canvas.SetLeft(MergeButtons, mousePoint.X);
                Canvas.SetTop(MergeButtons, mousePoint.Y);
            }
        }

        private void OnMergeDrag(object o, PointerRoutedEventArgs e)
        {
            Point dragPosition = e.GetCurrentPoint(Preview).Position;
            Size actualSize = WorkAreaSize();

            if (!_inMergeDrag)
            {
                _inMergeDrag = true;
                _mergeDragStart = dragPosition;
            }

            // Find the new zone, if any
            int dataLowX = Convert.ToInt32(Math.Min(_mergeDragStart.X, dragPosition.X) / actualSize.Width * GridData.Multiplier);
            int dataHighX = Convert.ToInt32(Math.Max(_mergeDragStart.X, dragPosition.X) / actualSize.Width * GridData.Multiplier);
            int dataLowY = Convert.ToInt32(Math.Min(_mergeDragStart.Y, dragPosition.Y) / actualSize.Height * GridData.Multiplier);
            int dataHighY = Convert.ToInt32(Math.Max(_mergeDragStart.Y, dragPosition.Y) / actualSize.Height * GridData.Multiplier);

            var selectedIndices = new List<int>();

            for (int zoneIndex = 0; zoneIndex < _data.Zones.Count; zoneIndex++)
            {
                var zoneData = _data.Zones[zoneIndex];

                bool selected = Math.Max(zoneData.Left, dataLowX) <= Math.Min(zoneData.Right, dataHighX) &&
                                Math.Max(zoneData.Top, dataLowY) <= Math.Min(zoneData.Bottom, dataHighY);

                // Check whether the zone intersects the selected rectangle
                (Preview.Children[zoneIndex] as GridZone).IsSelected = selected;

                if (selected)
                {
                    selectedIndices.Add(zoneIndex);
                }
            }

            // Compute the closure
            _data.MergeClosureIndices(selectedIndices).ForEach((zoneIndex) =>
            {
                (Preview.Children[zoneIndex] as GridZone).IsSelected = true;
            });
        }

        private void ClearSelection()
        {
            foreach (UIElement zone in Preview.Children)
            {
                ((GridZone)zone).IsSelected = false;
            }

            _inMergeDrag = false;
        }

        private void MergeClick(object sender, RoutedEventArgs e)
        {
            MergePanel.Visibility = Visibility.Collapsed;

            var selectedIndices = new List<int>();

            for (int zoneIndex = 0; zoneIndex < _data.Zones.Count; zoneIndex++)
            {
                if ((Preview.Children[zoneIndex] as GridZone).IsSelected)
                {
                    selectedIndices.Add(zoneIndex);
                }
            }

            ClearSelection();
            _data.DoMerge(selectedIndices);
            SetupUI();
        }

        private void MergeCancelClick(object sender, RoutedEventArgs e)
        {
            MergePanel.Visibility = Visibility.Collapsed;
            ClearSelection();
        }

        private void MergePanelPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            MergeCancelClick(null, null);
        }
    }
}
