// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

using FancyZonesEditor.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace FancyZonesEditor
{
    /// <summary>
    /// Once you've "Commit"ted the starter grid, then the Zones within the grid come to life for you to be able to further subdivide them
    /// using splitters
    /// </summary>
    public sealed partial class GridZone : UserControl
    {
        // Non-localizable strings
        private const string ObjectDependencyID = "IsSelected";
        private const string GridZoneBackgroundBrushID = "GridZoneBackgroundBrush";
        private const string SecondaryForegroundBrushID = "SecondaryForegroundBrush";
        private const string AccentColorBrushID = "AccentFillColorDefaultBrush";

        public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(ObjectDependencyID, typeof(bool), typeof(GridZone), new PropertyMetadata(false, OnSelectionChanged));

        public event SplitEventHandler Split;

        public event PointerEventHandler MergeDrag;

        public event PointerEventHandler MergeComplete;

        private static readonly SolidColorBrush TransparentBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        private readonly Rectangle _splitter;
        private readonly MagneticSnap _snapX;
        private readonly MagneticSnap _snapY;
        private readonly Func<Orientation, int, bool> _canSplit;
        private readonly GridData.Zone _zone;

        private bool _switchOrientation;
        private Point _lastPos = new Point(-1, -1);
        private int _snappedPositionX;
        private int _snappedPositionY;
        private Point _mouseDownPos = new Point(-1, -1);
        private bool _inMergeDrag;
        private bool _hovering;

        public GridZone(int spacing, MagneticSnap snapX, MagneticSnap snapY, Func<Orientation, int, bool> canSplit, GridData.Zone zone)
        {
            InitializeComponent();
            OnSelectionChanged();

            _splitter = new Rectangle
            {
                Fill = GetBrush(AccentColorBrushID),
            };
            Body.Children.Add(_splitter);

            SplitterThickness = Math.Max(spacing, 1);

            SizeChanged += GridZone_SizeChanged;

            GotFocus += GridZone_GotFocus;
            LostFocus += GridZone_LostFocus;

            _snapX = snapX;
            _snapY = snapY;
            _canSplit = canSplit;
            _zone = zone;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new GridZoneAutomationPeer(this);
        }

        private sealed partial class GridZoneAutomationPeer : FrameworkElementAutomationPeer
        {
            public GridZoneAutomationPeer(GridZone owner)
                : base(owner)
            {
            }

            protected override string GetClassNameCore() => nameof(GridZone);

            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
        }

        public bool IsSelected
        {
            get { return (bool)GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }

        private bool IsVerticalSplit
        {
            get => (ActualWidth > ActualHeight) ^ _switchOrientation;
        }

        private int SplitterThickness { get; set; }

        public int SnapAtHalfX()
        {
            var half = (_zone.Right - _zone.Left) / 2;
            var pixelX = _snapX.DataToPixelWithoutSnapping(_zone.Left + half);
            return _snapX.PixelToDataWithSnapping(pixelX, _zone.Left, _zone.Right);
        }

        public int SnapAtHalfY()
        {
            var half = (_zone.Bottom - _zone.Top) / 2;
            var pixelY = _snapY.DataToPixelWithoutSnapping(_zone.Top + half);
            return _snapY.PixelToDataWithSnapping(pixelY, _zone.Top, _zone.Bottom);
        }

        public void UpdateShiftState(bool shiftState)
        {
            _switchOrientation = shiftState;

            if (_lastPos.X != -1)
            {
                UpdateSplitter();
            }
        }

        public void DoSplit(Orientation orientation, int offset)
        {
            Split?.Invoke(this, new SplitEventArgs(orientation, offset));
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            _hovering = true;
            UpdateSplitter();
            _splitter.Fill = GetBrush(AccentColorBrushID);
            base.OnPointerEntered(e);
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            _hovering = false;
            UpdateSplitter();
            base.OnPointerExited(e);
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            _mouseDownPos = _lastPos;
            base.OnPointerPressed(e);
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            if (_inMergeDrag)
            {
                MergeDrag?.Invoke(this, e);
            }
            else
            {
                _lastPos = e.GetCurrentPoint(Body).Position;

                var editor = Parent as GridEditor;
                var editorPos = e.GetCurrentPoint(editor).Position;
                _snappedPositionX = _snapX.PixelToDataWithSnapping(editorPos.X, _zone.Left, _zone.Right);
                _snappedPositionY = _snapY.PixelToDataWithSnapping(editorPos.Y, _zone.Top, _zone.Bottom);

                if (_mouseDownPos.X == -1)
                {
                    UpdateSplitter();
                }
                else
                {
                    double threshold = SplitterThickness / 2;
                    if ((Math.Abs(_mouseDownPos.X - _lastPos.X) > threshold) || (Math.Abs(_mouseDownPos.Y - _lastPos.Y) > threshold))
                    {
                        // switch to merge (which is handled by parent GridEditor)
                        _inMergeDrag = true;
                        CapturePointer(e.Pointer);
                        MergeDrag?.Invoke(this, e);

                        // WinUI has no Visibility.Hidden - Opacity keeps the layout slot.
                        _splitter.Opacity = 0;
                    }
                }
            }

            base.OnPointerMoved(e);
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            if (_inMergeDrag)
            {
                ReleasePointerCapture(e.Pointer);
                MergeComplete?.Invoke(this, e);
                _inMergeDrag = false;
                _splitter.Opacity = 1;
            }
            else
            {
                int thickness = SplitterThickness;

                double delta = IsVerticalSplit ? _mouseDownPos.X - _lastPos.X : _mouseDownPos.Y - _lastPos.Y;
                if (Math.Abs(delta) <= thickness / 2)
                {
                    if (IsVerticalSplit)
                    {
                        DoSplit(Orientation.Vertical, _snappedPositionX);
                    }
                    else
                    {
                        DoSplit(Orientation.Horizontal, _snappedPositionY);
                    }
                }
            }

            _mouseDownPos = new Point(-1, -1);
            base.OnPointerReleased(e);
        }

        private static Brush GetBrush(string key)
        {
            return Application.Current.Resources[key] as Brush;
        }

        private static void OnSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GridZone)d).OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            ZoneBorder.Background = GetBrush(IsSelected ? AccentColorBrushID : GridZoneBackgroundBrushID);
        }

        private void GridZone_LostFocus(object sender, RoutedEventArgs e)
        {
            Opacity = 1;
        }

        private void GridZone_GotFocus(object sender, RoutedEventArgs e)
        {
            Opacity = 0.5;
        }

        private void GridZone_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // using current culture as this is end user facing
            WidthLabel.Text = Math.Round(ActualWidth).ToString(CultureInfo.CurrentCulture);
            HeightLabel.Text = Math.Round(ActualHeight).ToString(CultureInfo.CurrentCulture);
            AutomationProperties.SetName(
                this,
#pragma warning disable SA1118 // Parameter should not span multiple lines
                ResourceLoaderInstance.GetString("Zone_Name") + " " + (_zone.Index + 1).ToString(CultureInfo.CurrentCulture) + ". " +
                ResourceLoaderInstance.GetString("Width_Name") + ": " + WidthLabel.Text + ", " +
                ResourceLoaderInstance.GetString("Height_Name") + ": " + HeightLabel.Text);
#pragma warning restore SA1118 // Parameter should not span multiple lines
        }

        private void UpdateSplitter()
        {
            if (!_hovering)
            {
                _splitter.Fill = TransparentBrush;
                return;
            }

            bool enabled;

            if (IsVerticalSplit)
            {
                double bodyWidth = Body.ActualWidth;
                double pos = _snapX.DataToPixelWithoutSnapping(_snappedPositionX) - Canvas.GetLeft(this) - (SplitterThickness / 2);
                pos = Math.Clamp(pos, 0, bodyWidth - SplitterThickness);

                Canvas.SetLeft(_splitter, pos);
                Canvas.SetTop(_splitter, 0);
                _splitter.MinWidth = SplitterThickness;
                _splitter.MinHeight = Body.ActualHeight;

                enabled = _canSplit(Orientation.Vertical, _snappedPositionX);
            }
            else
            {
                double bodyHeight = Body.ActualHeight;
                double pos = _snapY.DataToPixelWithoutSnapping(_snappedPositionY) - Canvas.GetTop(this) - (SplitterThickness / 2);
                pos = Math.Clamp(pos, 0, bodyHeight - SplitterThickness);

                Canvas.SetLeft(_splitter, 0);
                Canvas.SetTop(_splitter, pos);
                _splitter.MinWidth = Body.ActualWidth;
                _splitter.MinHeight = SplitterThickness;

                enabled = _canSplit(Orientation.Horizontal, _snappedPositionY);
            }

            _splitter.Fill = GetBrush(enabled ? AccentColorBrushID : SecondaryForegroundBrushID);
        }
    }
}
