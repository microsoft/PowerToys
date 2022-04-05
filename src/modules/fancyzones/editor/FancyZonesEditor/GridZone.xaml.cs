// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FancyZonesEditor
{
    /// <summary>
    /// Once you've "Commit"ted the starter grid, then the Zones within the grid come to life for you to be able to further subdivide them
    /// using splitters
    /// </summary>
    public partial class GridZone : UserControl
    {
        // Non-localizable strings
        private const string ObjectDependencyID = "IsSelected";
        private const string GridZoneBackgroundBrushID = "GridZoneBackgroundBrush";
        private const string SecondaryForegroundBrushID = "SecondaryForegroundBrush";
        private const string AccentColorBrushID = "SystemControlBackgroundAccentBrush";
        public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(ObjectDependencyID, typeof(bool), typeof(GridZone), new PropertyMetadata(false, OnSelectionChanged));

        public event SplitEventHandler Split;

        public event MouseEventHandler MergeDrag;

        public event MouseButtonEventHandler MergeComplete;

        private readonly Rectangle _splitter;
        private bool _switchOrientation;
        private Point _lastPos = new Point(-1, -1);
        private int _snappedPositionX;
        private int _snappedPositionY;
        private Point _mouseDownPos = new Point(-1, -1);
        private bool _inMergeDrag;
        private MagneticSnap _snapX;
        private MagneticSnap _snapY;
        private Func<Orientation, int, bool> _canSplit;
        private bool _hovering;
        private GridData.Zone _zone;

        private static void OnSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GridZone)d).OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            Background = IsSelected ? Application.Current.Resources[AccentColorBrushID] as SolidColorBrush : Application.Current.Resources[GridZoneBackgroundBrushID] as SolidColorBrush;
        }

        public bool IsSelected
        {
            get { return (bool)GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }

        public GridZone(int spacing, MagneticSnap snapX, MagneticSnap snapY, Func<Orientation, int, bool> canSplit, GridData.Zone zone)
        {
            InitializeComponent();
            OnSelectionChanged();
            _splitter = new Rectangle
            {
                Fill = Application.Current.Resources[AccentColorBrushID] as SolidColorBrush,
            };
            Body.Children.Add(_splitter);

            SplitterThickness = Math.Max(spacing, 1);

            SizeChanged += GridZone_SizeChanged;

            GotKeyboardFocus += GridZone_GotKeyboardFocus;
            LostKeyboardFocus += GridZone_LostKeyboardFocus;

            _snapX = snapX;
            _snapY = snapY;
            _canSplit = canSplit;
            _zone = zone;
        }

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

        private void GridZone_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Opacity = 1;
        }

        private void GridZone_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Opacity = 0.5;
        }

        private void GridZone_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // using current culture as this is end user facing
            WidthLabel.Text = Math.Round(ActualWidth).ToString(CultureInfo.CurrentCulture);
            HeightLabel.Text = Math.Round(ActualHeight).ToString(CultureInfo.CurrentCulture);
            System.Windows.Automation.AutomationProperties.SetName(
                this,
#pragma warning disable SA1118 // Parameter should not span multiple lines
                Properties.Resources.Zone_Name + " " + (_zone.Index + 1).ToString(CultureInfo.CurrentCulture) + ". " +
                Properties.Resources.Width_Name + ": " + WidthLabel.Text + ", " +
                Properties.Resources.Height_Name + ": " + HeightLabel.Text);
#pragma warning restore SA1118 // Parameter should not span multiple lines
        }

        public void UpdateShiftState(bool shiftState)
        {
            _switchOrientation = shiftState;

            if (_lastPos.X != -1)
            {
                UpdateSplitter();
            }
        }

        private bool IsVerticalSplit
        {
            get => (ActualWidth > ActualHeight) ^ _switchOrientation;
        }

        private int SplitterThickness { get; set; }

        private void UpdateSplitter()
        {
            if (!_hovering)
            {
                _splitter.Fill = Brushes.Transparent;
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

            Brush disabledBrush = Application.Current.Resources[SecondaryForegroundBrushID] as SolidColorBrush;
            Brush enabledBrush = Application.Current.Resources[AccentColorBrushID] as SolidColorBrush;
            _splitter.Fill = enabled ? enabledBrush : disabledBrush;
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            _hovering = true;
            UpdateSplitter();
            _splitter.Fill = Application.Current.Resources[AccentColorBrushID] as SolidColorBrush;
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            _hovering = false;
            UpdateSplitter();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            _mouseDownPos = _lastPos;
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_inMergeDrag)
            {
                DoMergeDrag(e);
            }
            else
            {
                _lastPos = e.GetPosition(Body);
                _snappedPositionX = _snapX.PixelToDataWithSnapping(e.GetPosition(Parent as GridEditor).X, _zone.Left, _zone.Right);
                _snappedPositionY = _snapY.PixelToDataWithSnapping(e.GetPosition(Parent as GridEditor).Y, _zone.Top, _zone.Bottom);

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
                        Mouse.Capture(this, CaptureMode.Element);
                        DoMergeDrag(e);
                        _splitter.Visibility = Visibility.Hidden;
                    }
                }
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (_inMergeDrag)
            {
                Mouse.Capture(this, CaptureMode.None);
                DoMergeComplete(e);
                _inMergeDrag = false;
                _splitter.Visibility = Visibility.Visible;
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
            base.OnMouseUp(e);
        }

        private void DoMergeDrag(MouseEventArgs e)
        {
            MergeDrag?.Invoke(this, e);
        }

        private void DoMergeComplete(MouseButtonEventArgs e)
        {
            MergeComplete?.Invoke(this, e);
        }

        public void DoSplit(Orientation orientation, int offset)
        {
            Split?.Invoke(this, new SplitEventArgs(orientation, offset));
        }
    }
}
