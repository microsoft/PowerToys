// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FancyZonesEditor
{
    /// <summary>
    /// Once you've "Committ"ed the starter grid, then the Zones within the grid come to life for you to be able to further subdivide them
    /// using splitters
    /// </summary>
    public partial class GridZone : UserControl
    {
        public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register("IsSelected", typeof(bool), typeof(GridZone), new PropertyMetadata(false, OnSelectionChanged));

        private static void OnSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GridZone)d).OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            Background = IsSelected ? Brushes.SteelBlue : Brushes.LightGray;
        }

        public bool IsSelected
        {
            get { return (bool)GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }


        public double[] VerticalSnapPoints;
        public double[] HorizontalSnapPoints;



        public GridZone()
        {
            InitializeComponent();
            OnSelectionChanged();
            _splitter = new Rectangle();
            _splitter.Fill = Brushes.DarkGray;
            Body.Children.Add(_splitter);

            ((App)Application.Current).ZoneSettings.PropertyChanged += ZoneSettings_PropertyChanged;
        }

        private void ZoneSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsShiftKeyPressed")
            {
                _switchOrientation = ((App)Application.Current).ZoneSettings.IsShiftKeyPressed;
                if (_lastPos.X != -1)
                {
                    UpdateSplitter();
                }
            }
        }

        protected override Size ArrangeOverride(Size size)
        {
            _splitOrientation = (size.Width > size.Height) ? Orientation.Vertical : Orientation.Horizontal;
            return base.ArrangeOverride(size);
        }

        private bool IsVerticalSplit
        {
            get
            {
                bool isVertical = _splitOrientation == Orientation.Vertical;
                if (_switchOrientation)
                {
                    isVertical = !isVertical;
                }
                return isVertical;
            }
        }

        private int SplitterThickness {  get { return Math.Max(((App)Application.Current).ZoneSettings.Spacing, 5); } }

        private void UpdateSplitter()
        {
            if (IsVerticalSplit)
            {
                double bodyWidth = Body.ActualWidth;
                double pos = _lastPos.X - (SplitterThickness / 2);
                if (pos < 0)
                {
                    pos = 0;
                }
                else if (pos > (bodyWidth - SplitterThickness))
                {
                    pos = bodyWidth - SplitterThickness;
                }

                Canvas.SetLeft(_splitter, pos);
                Canvas.SetTop(_splitter, 0);
                _splitter.MinWidth = SplitterThickness;
                _splitter.MinHeight = Body.ActualHeight;
            }
            else
            {
                double bodyHeight = Body.ActualHeight;
                double pos = _lastPos.Y - (SplitterThickness / 2);
                if (pos < 0)
                {
                    pos = 0;
                }
                else if (pos > (bodyHeight - SplitterThickness))
                {
                    pos = bodyHeight - SplitterThickness;
                }
                Canvas.SetLeft(_splitter, 0);
                Canvas.SetTop(_splitter, pos);
                _splitter.MinWidth = Body.ActualWidth;
                _splitter.MinHeight = SplitterThickness;
            }
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            Frame.Visibility = Visibility.Visible;
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            Frame.Visibility = Visibility.Collapsed;
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

                if (IsVerticalSplit)
                {
                    if (VerticalSnapPoints != null)
                    {
                        int thickness = SplitterThickness;
                        foreach (double snapPoint in VerticalSnapPoints)
                        {
                            if (Math.Abs(_lastPos.X - snapPoint) <= (thickness * 2))
                            {
                                _lastPos.X = snapPoint;
                                break;
                            }
                        }
                    }
                }
                else // horizontal split
                {
                    if (HorizontalSnapPoints != null)
                    {
                        int thickness = SplitterThickness;
                        foreach (double snapPoint in HorizontalSnapPoints)
                        {
                            if (Math.Abs(_lastPos.Y - snapPoint) <= (thickness * 2))
                            {
                                _lastPos.Y = snapPoint;
                                break;
                            }
                        }
                    }
                }

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
                        DoSplit(Orientation.Vertical, _lastPos.X - (thickness / 2));
                    }
                    else
                    {
                        DoSplit(Orientation.Horizontal, _lastPos.Y - (thickness / 2));
                    }
                }
            }
            _mouseDownPos = new Point(-1, -1);
            base.OnMouseUp(e);
        }

        public event SplitEventHandler Split;
        public event SplitEventHandler FullSplit;
        public event MouseEventHandler MergeDrag;
        public event MouseButtonEventHandler MergeComplete;


        private Rectangle _splitter;
        private bool _switchOrientation = false;
        private Point _lastPos = new Point(-1,-1);
        private Point _mouseDownPos = new Point(-1,-1);
        private bool _inMergeDrag = false;
        private Orientation _splitOrientation;

        private void DoMergeDrag(MouseEventArgs e)
        {
            if (MergeDrag != null)
            {
                MergeDrag(this, e);
            }
        }

        private void DoMergeComplete(MouseButtonEventArgs e)
        {
            if (MergeComplete != null)
            {
                MergeComplete(this, e);
            }
        }

        private void DoSplit(Orientation orientation, double offset)
        {
            if (Split != null)
            {
                Split(this, new SplitEventArgs(orientation, offset));
            }
        }

        private void FullSplit_Click(object sender, RoutedEventArgs e)
        {
            DoFullSplit();
        }

        private void DoFullSplit()
        {
            if (FullSplit != null)
            {
                FullSplit(this, new SplitEventArgs());
            }
        }
    }

    public class SplitEventArgs : EventArgs
    {
        public SplitEventArgs() { }
        public SplitEventArgs(Orientation orientation, double offset)
        {
            _orientation = orientation;
            _offset = offset;
        }

        public Orientation Orientation { get { return _orientation; } }
        public double Offset { get { return _offset;  } }

        private Orientation _orientation;
        private double _offset;
    }

    public delegate void SplitEventHandler(object sender, SplitEventArgs args);
}
