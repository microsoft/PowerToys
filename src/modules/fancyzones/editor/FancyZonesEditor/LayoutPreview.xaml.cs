// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for LayoutPreview.xaml
    /// </summary>
    public partial class LayoutPreview : UserControl
    {
        public static readonly DependencyProperty IsActualSizeProperty = DependencyProperty.Register("IsActualSize", typeof(bool), typeof(LayoutPreview), new PropertyMetadata(false));

        private LayoutModel _model;

        public LayoutPreview()
        {
            InitializeComponent();
            DataContextChanged += LayoutPreview_DataContextChanged;
            ((App)Application.Current).ZoneSettings.PropertyChanged += ZoneSettings_PropertyChanged;
        }

        private void LayoutPreview_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _model = (LayoutModel)DataContext;
            RenderPreview();
        }

        public bool IsActualSize
        {
            get { return (bool)GetValue(IsActualSizeProperty); }
            set { SetValue(IsActualSizeProperty, value); }
        }

        private void ZoneSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ZoneCount")
            {
                RenderPreview();
            }
            else if ((e.PropertyName == "ShowSpacing") || (e.PropertyName == "Spacing"))
            {
                if (IsActualSize)
                {
                    Settings settings = ((App)Application.Current).ZoneSettings;
                    Body.Margin = new Thickness(settings.ShowSpacing ? settings.Spacing / 2 : 0);
                }
                else
                {
                    Body.Margin = new Thickness(0);
                }

                if (_model is GridLayoutModel)
                {
                    RenderPreview();
                }
            }
        }

        public Panel PreviewPanel
        {
            get { return Body; }
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

            if (_model is GridLayoutModel gridModel)
            {
                RenderGridPreview(gridModel);
            }
            else
            {
                if (_model is CanvasLayoutModel canvasModel)
                {
                    RenderCanvasPreview(canvasModel);
                }
            }
        }

        private void RenderGridPreview(GridLayoutModel grid)
        {
            Body.RowDefinitions.Clear();
            foreach (int percent in grid.RowPercents)
            {
                RowDefinition def = new RowDefinition
                {
                    Height = new GridLength(percent, GridUnitType.Star),
                };
                Body.RowDefinitions.Add(def);
            }

            Body.ColumnDefinitions.Clear();
            foreach (int percent in grid.ColumnPercents)
            {
                ColumnDefinition def = new ColumnDefinition
                {
                    Width = new GridLength(percent, GridUnitType.Star),
                };
                Body.ColumnDefinitions.Add(def);
            }

            Settings settings = ((App)Application.Current).ZoneSettings;
            int divisor = IsActualSize ? 2 : 20;
            Thickness margin = new Thickness(settings.ShowSpacing ? settings.Spacing / divisor : 0);

            List<int> visited = new List<int>();

            for (int row = 0; row < grid.Rows; row++)
            {
                for (int col = 0; col < grid.Columns; col++)
                {
                    int childIndex = grid.CellChildMap[row, col];
                    if (!visited.Contains(childIndex))
                    {
                        visited.Add(childIndex);
                        Rectangle rect = new Rectangle();
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
                        rect.StrokeThickness = 1;
                        rect.Stroke = Brushes.DarkGray;
                        rect.Fill = Brushes.LightGray;
                        Body.Children.Add(rect);
                    }
                }
            }
        }

        private void RenderCanvasPreview(CanvasLayoutModel canvas)
        {
            Body.RowDefinitions.Clear();
            Body.ColumnDefinitions.Clear();

            Viewbox viewbox = new Viewbox
            {
                Stretch = Stretch.Uniform,
            };
            Body.Children.Add(viewbox);
            Canvas frame = new Canvas();
            viewbox.Child = frame;
            frame.Width = canvas.ReferenceWidth;
            frame.Height = canvas.ReferenceHeight;
            foreach (Int32Rect zone in canvas.Zones)
            {
                Rectangle rect = new Rectangle();
                Canvas.SetTop(rect, zone.Y);
                Canvas.SetLeft(rect, zone.X);
                rect.MinWidth = zone.Width;
                rect.MinHeight = zone.Height;
                rect.StrokeThickness = 5;
                rect.Stroke = Brushes.DarkGray;
                rect.Fill = Brushes.LightGray;
                frame.Children.Add(rect);
            }
        }
    }
}
