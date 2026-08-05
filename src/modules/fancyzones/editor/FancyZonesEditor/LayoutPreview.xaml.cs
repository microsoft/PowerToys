// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using FancyZonesEditor.Models;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for LayoutPreview.xaml
    /// </summary>
    public sealed partial class LayoutPreview : UserControl
    {
        // Non-localizable strings
        private const string PropertyZoneCountID = "ZoneCount";
        private const string PropertyShowSpacingID = "ShowSpacing";
        private const string PropertySpacingID = "Spacing";
        private const string ObjectDependencyID = "IsActualSize";

        public static readonly DependencyProperty IsActualSizeProperty = DependencyProperty.Register(ObjectDependencyID, typeof(bool), typeof(LayoutPreview), new PropertyMetadata(false));

        private LayoutModel _model;

        public LayoutPreview()
        {
            InitializeComponent();
            DataContextChanged += LayoutPreview_DataContextChanged;
        }

        public bool IsActualSize
        {
            get { return (bool)GetValue(IsActualSizeProperty); }
            set { SetValue(IsActualSizeProperty, value); }
        }

        public void UpdatePreview()
        {
            RenderPreview();
        }

        /// <summary>
        /// Detaches the preview from the layout model before the preview leaves the overlay tree.
        /// Removing a WinUI element from its parent does not guarantee a DataContext change, so the
        /// model subscription must be released explicitly.
        /// </summary>
        internal void DetachModel()
        {
            SetModel(null);
        }

        internal void AttachModel(LayoutModel model)
        {
            SetModel(model);
        }

        public void ZoneSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
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

        // WinUI has no FrameworkElement.FindResource; the preview styles live in App.Resources.
        private static Style FindStyle(string key)
        {
            return Application.Current.Resources[key] as Style;
        }

        private void LayoutPreview_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            SetModel(args.NewValue as LayoutModel);
        }

        private void LayoutModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            RenderPreview();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SetModel(DataContext as LayoutModel);

            if (_model != null)
            {
                Logger.LogInfo("Loaded " + _model.Name);
            }

            RenderPreview();
        }

        private void SetModel(LayoutModel model)
        {
            if (ReferenceEquals(_model, model))
            {
                return;
            }

            if (_model != null)
            {
                _model.PropertyChanged -= LayoutModel_PropertyChanged;
            }

            _model = model;
            if (_model != null)
            {
                _model.PropertyChanged += LayoutModel_PropertyChanged;
            }

            RenderPreview();
        }

        private void RenderPreview()
        {
            Body.Children.Clear();
            Body.RowDefinitions.Clear();
            Body.ColumnDefinitions.Clear();

            if (_model == null)
            {
                return;
            }

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
            double spacing = grid.ShowSpacing ? grid.Spacing : 0;

            var rowData = GridData.PrefixSum(grid.RowPercents);
            var columnData = GridData.PrefixSum(grid.ColumnPercents);

            var workArea = App.Overlay.WorkArea;

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
                        double left = columnData[col] * workArea.Width / GridData.Multiplier;
                        double top = rowData[row] * workArea.Height / GridData.Multiplier;

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

                        double right = columnData[maxCol + 1] * workArea.Width / GridData.Multiplier;
                        double bottom = rowData[maxRow + 1] * workArea.Height / GridData.Multiplier;

                        left += col == 0 ? spacing : spacing / 2;
                        right -= maxCol == cols - 1 ? spacing : spacing / 2;
                        top += row == 0 ? spacing : spacing / 2;
                        bottom -= maxRow == rows - 1 ? spacing : spacing / 2;

                        Canvas.SetTop(rect, top);
                        Canvas.SetLeft(rect, left);
                        rect.Width = Math.Max(1, right - left);
                        rect.Height = Math.Max(1, bottom - top);

                        rect.Style = FindStyle("GridLayoutActualScalePreviewStyle");
                        frame.Children.Add(rect);
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
                        rect.Style = FindStyle("GridLayoutSmallScalePreviewStyle");
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
            var screenWorkArea = App.Overlay.WorkArea;

            using var renderLayout = (CanvasLayoutModel)canvas.Clone();
            renderLayout.ScaleLayout(workAreaWidth: screenWorkArea.Width, workAreaHeight: screenWorkArea.Height);

            Viewbox viewbox = new Viewbox
            {
                Stretch = Stretch.Uniform,
            };
            Body.Children.Add(viewbox);
            Canvas frame = new Canvas
            {
                Width = screenWorkArea.Width,
                Height = screenWorkArea.Height,
            };
            viewbox.Child = frame;

            foreach (RectInt32 zone in renderLayout.Zones)
            {
                Border rect = new Border();
                Canvas.SetTop(rect, zone.Y);
                Canvas.SetLeft(rect, zone.X);
                rect.MinWidth = zone.Width;
                rect.MinHeight = zone.Height;

                rect.Style = FindStyle(IsActualSize ? "CanvasLayoutActualScalePreviewStyle" : "CanvasLayoutSmallScalePreviewStyle");

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
