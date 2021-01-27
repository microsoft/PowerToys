// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for LayoutPreview.xaml
    /// </summary>
    public partial class LayoutPreview : UserControl
    {
        // Non-localizable strings
        private const string PropertyZoneCountID = "ZoneCount";
        private const string PropertyShowSpacingID = "ShowSpacing";
        private const string PropertySpacingID = "Spacing";
        private const string PropertyZoneBackgroundID = "ZoneBackground";
        private const string PropertyZoneBorderID = "ZoneBorder";
        private const string ObjectDependencyID = "IsActualSize";

        public static readonly DependencyProperty IsActualSizeProperty = DependencyProperty.Register(ObjectDependencyID, typeof(bool), typeof(LayoutPreview), new PropertyMetadata(false));
        private LayoutModel _model;
        private List<Int32Rect> _zones = new List<Int32Rect>();

        public bool IsActualSize
        {
            get { return (bool)GetValue(IsActualSizeProperty); }
            set { SetValue(IsActualSizeProperty, value); }
        }

        public LayoutPreview()
        {
            InitializeComponent();
            DataContextChanged += LayoutPreview_DataContextChanged;
            ((App)Application.Current).MainWindowSettings.PropertyChanged += ZoneSettings_PropertyChanged;
        }

        public void UpdatePreview()
        {
            RenderPreview();
        }

        private void LayoutPreview_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_model != null)
            {
                _model.PropertyChanged -= LayoutModel_PropertyChanged;
            }

            _model = (LayoutModel)DataContext;
            if (_model != null)
            {
                _model.PropertyChanged += LayoutModel_PropertyChanged;
                RenderPreview();
            }
        }

        private void ZoneSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == PropertyZoneCountID)
            {
                RenderPreview();
            }
            else if ((e.PropertyName == PropertyShowSpacingID) || (e.PropertyName == PropertySpacingID))
            {
                if (_model is GridLayoutModel)
                {
                    RenderPreview();
                }
            }
        }

        private void LayoutModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            RenderPreview();
        }

        public Int32Rect[] GetZoneRects()
        {
            return _zones.ToArray();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _model = (LayoutModel)DataContext;

            RenderPreview();
        }

        private void RenderPreview()
        {
            if (_model == null)
            {
                return;
            }

            Body.Children.Clear();
            Body.RowDefinitions.Clear();
            Body.ColumnDefinitions.Clear();

            _zones.Clear();

            if (_model is GridLayoutModel gridModel)
            {
                RenderGridPreview(gridModel);
            }
            else if (_model is CanvasLayoutModel canvasModel)
            {
                RenderCanvasPreview(canvasModel);
            }
        }

        private void RenderActualScalePreview(GridLayoutModel grid)
        {
            int rows = grid.Rows;
            int cols = grid.Columns;

            RowColInfo[] rowInfo = (from percent in grid.RowPercents
                                    select new RowColInfo(percent)).ToArray();

            RowColInfo[] colInfo = (from percent in grid.ColumnPercents
                                    select new RowColInfo(percent)).ToArray();

            int spacing = grid.ShowSpacing ? grid.Spacing : 0;

            var workArea = App.Overlay.WorkArea;
            double width = workArea.Width - (spacing * (cols + 1));
            double height = workArea.Height - (spacing * (rows + 1));

            double top = spacing;
            for (int row = 0; row < rows; row++)
            {
                double cellHeight = rowInfo[row].Recalculate(top, height);
                top += cellHeight + spacing;
            }

            double left = spacing;
            for (int col = 0; col < cols; col++)
            {
                double cellWidth = colInfo[col].Recalculate(left, width);
                left += cellWidth + spacing;
            }

            Viewbox viewbox = new Viewbox
            {
                Stretch = Stretch.Uniform,
            };
            Body.Children.Add(viewbox);
            Canvas frame = new Canvas
            {
                Width = workArea.Width,
                Height = workArea.Height,
            };
            viewbox.Child = frame;

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int childIndex = grid.CellChildMap[row, col];
                    if (((row == 0) || (grid.CellChildMap[row - 1, col] != childIndex)) &&
                        ((col == 0) || (grid.CellChildMap[row, col - 1] != childIndex)))
                    {
                        // this is not a continuation of a span
                        Border rect = new Border();
                        left = colInfo[col].Start;
                        top = rowInfo[row].Start;
                        Canvas.SetTop(rect, top);
                        Canvas.SetLeft(rect, left);

                        int maxRow = row;
                        while (((maxRow + 1) < rows) && (grid.CellChildMap[maxRow + 1, col] == childIndex))
                        {
                            maxRow++;
                        }

                        int maxCol = col;
                        while (((maxCol + 1) < cols) && (grid.CellChildMap[row, maxCol + 1] == childIndex))
                        {
                            maxCol++;
                        }

                        rect.Width = Math.Max(0, colInfo[maxCol].End - left);
                        rect.Height = Math.Max(0, rowInfo[maxRow].End - top);
                        rect.Style = (Style)FindResource("GridLayoutPreviewActualSizeStyle");
                        frame.Children.Add(rect);
                        _zones.Add(new Int32Rect(
                            (int)left, (int)top, (int)rect.Width, (int)rect.Height));
                    }
                }
            }

            if (App.DebugMode)
            {
                TextBlock text = new TextBlock();
                text.Text = "(" + workArea.X + "," + workArea.Y + ")";
                text.FontSize = 42;
                frame.Children.Add(text);
            }
        }

        private void RenderSmallScalePreview(GridLayoutModel grid)
        {
            foreach (int percent in grid.RowPercents)
            {
                RowDefinition def = new RowDefinition
                {
                    Height = new GridLength(percent, GridUnitType.Star),
                };
                Body.RowDefinitions.Add(def);
            }

            foreach (int percent in grid.ColumnPercents)
            {
                ColumnDefinition def = new ColumnDefinition
                {
                    Width = new GridLength(percent, GridUnitType.Star),
                };
                Body.ColumnDefinitions.Add(def);
            }

            Thickness margin = new Thickness(grid.ShowSpacing ? grid.Spacing / 20 : 0);

            List<int> visited = new List<int>();

            for (int row = 0; row < grid.Rows; row++)
            {
                for (int col = 0; col < grid.Columns; col++)
                {
                    int childIndex = grid.CellChildMap[row, col];
                    if (!visited.Contains(childIndex))
                    {
                        visited.Add(childIndex);
                        Border rect = new Border();
                        Grid.SetRow(rect, row);
                        Grid.SetColumn(rect, col);
                        int span = 1;
                        int walk = row + 1;
                        while ((walk < grid.Rows) && grid.CellChildMap[walk, col] == childIndex)
                        {
                            span++;
                            walk++;
                        }

                        Grid.SetRowSpan(rect, span);

                        span = 1;
                        walk = col + 1;
                        while ((walk < grid.Columns) && grid.CellChildMap[row, walk] == childIndex)
                        {
                            span++;
                            walk++;
                        }

                        Grid.SetColumnSpan(rect, span);
                        rect.Margin = margin;
                        rect.Style = (Style)FindResource("GridLayoutPreviewStyle");
                        Body.Children.Add(rect);
                    }
                }
            }
        }

        private void RenderGridPreview(GridLayoutModel grid)
        {
            if (IsActualSize)
            {
                RenderActualScalePreview(grid);
            }
            else
            {
                RenderSmallScalePreview(grid);
            }
        }

        private void RenderCanvasPreview(CanvasLayoutModel canvas)
        {
            var workArea = canvas.CanvasRect;
            if (workArea.Width == 0 || workArea.Height == 0 || App.Overlay.SpanZonesAcrossMonitors)
            {
                workArea = App.Overlay.WorkArea;
            }

            Viewbox viewbox = new Viewbox
            {
                Stretch = Stretch.Uniform,
            };
            Body.Children.Add(viewbox);
            Canvas frame = new Canvas
            {
                Width = workArea.Width,
                Height = workArea.Height,
            };
            viewbox.Child = frame;

            foreach (Int32Rect zone in canvas.Zones)
            {
                Border rect = new Border();
                Canvas.SetTop(rect, zone.Y);
                Canvas.SetLeft(rect, zone.X);
                rect.MinWidth = zone.Width;
                rect.MinHeight = zone.Height;

                if (IsActualSize)
                {
                   rect.Style = (Style)FindResource("CanvasLayoutPreviewActualSizeStyle");
                }
                else
                {
                   rect.Style = (Style)FindResource("CanvasLayoutPreviewStyle");
                }

                frame.Children.Add(rect);
            }

            if (App.DebugMode)
            {
                TextBlock text = new TextBlock();
                text.Text = "(" + App.Overlay.WorkArea.X + "," + App.Overlay.WorkArea.Y + ")";
                text.FontSize = 42;
                frame.Children.Add(text);
            }
        }
    }
}
