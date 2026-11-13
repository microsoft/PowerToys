// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.System;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    /// <summary>
    /// A small map of the displays as a key to the edge editor: a tile per display with its number
    /// and name, a stroke per edge in the style of its rule, and a lettered badge per edge that
    /// selects its row. A direction that differs from the other is drawn on the side of the display
    /// it leaves. An edge walled in exactly one direction carries a caret toward the open way.
    /// </summary>
    public sealed partial class CursorBrakeEdgeMap : UserControl
    {
        public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(
            nameof(Editor),
            typeof(CursorBrakeEdgesViewModel),
            typeof(CursorBrakeEdgeMap),
            new PropertyMetadata(null, (d, e) => ((CursorBrakeEdgeMap)d).OnEditorChanged((CursorBrakeEdgesViewModel)e.OldValue, (CursorBrakeEdgesViewModel)e.NewValue)));

        // Sizes in effective pixels.
        private const double Pad = 14;
        private const double TileGap = 4;
        private const double StrokeInset = 8;
        private const double SplitOffset = 5;

        // The tile's number chip: a bordered numeral in the shape of a key, as tall as the name's line.
        // The tile already stands for a monitor, so the chip carries only the number.
        private const double ChipSize = 18;
        private const double ChipGap = 4;

        // Space between the tile's border and its label, on each side.
        private const double LabelInset = 4;

        // U+EDDA, the Segoe Fluent solid caret, pointing right before rotation.
        private const string CaretGlyph = "";

        private readonly List<Button> _badges = new List<Button>();

        public CursorBrakeEdgeMap()
        {
            InitializeComponent();
            AutomationProperties.SetName(MapCanvas, ResourceLoaderInstance.ResourceLoader.GetString("MouseUtils_CursorBrake_Edges_MapName"));

            // The brush helpers in the XAML take the new theme during the same change, so the redraw waits for it to finish.
            ActualThemeChanged += (s, e) => DispatcherQueue.TryEnqueue(Redraw);
        }

        public CursorBrakeEdgesViewModel Editor
        {
            get => (CursorBrakeEdgesViewModel)GetValue(EditorProperty);
            set => SetValue(EditorProperty, value);
        }

        private void OnEditorChanged(CursorBrakeEdgesViewModel oldEditor, CursorBrakeEdgesViewModel newEditor)
        {
            if (oldEditor != null)
            {
                oldEditor.PropertyChanged -= OnEditorPropertyChanged;
                oldEditor.Rows.CollectionChanged -= OnRowsChanged;
            }

            if (newEditor != null)
            {
                newEditor.PropertyChanged += OnEditorPropertyChanged;
                newEditor.Rows.CollectionChanged += OnRowsChanged;
            }

            Redraw();
        }

        private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(CursorBrakeEdgesViewModel.Detected) or nameof(CursorBrakeEdgesViewModel.SelectedRow) or nameof(CursorBrakeEdgesViewModel.TrapTitle))
            {
                Redraw();
            }
        }

        private void OnRowsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Redraw();
        }

        // Rows redraw the map when their values change, which every edit does through RefreshRows.
        private void Redraw()
        {
            MapCanvas.Children.Clear();
            _badges.Clear();
            var editor = Editor;
            var detected = editor?.Detected;
            if (detected == null || detected.Monitors.Count == 0 || detected.Monitors.Any(m => m.Rect.Count != 4))
            {
                return;
            }

            foreach (var row in editor.Rows)
            {
                row.PropertyChanged -= OnRowPropertyChanged;
                row.PropertyChanged += OnRowPropertyChanged;
            }

            double minX = detected.Monitors.Min(m => m.Rect[0]);
            double minY = detected.Monitors.Min(m => m.Rect[1]);
            double maxX = detected.Monitors.Max(m => m.Rect[2]);
            double maxY = detected.Monitors.Max(m => m.Rect[3]);
            double width = MapCanvas.Width;
            double height = MapCanvas.Height;
            double scale = Math.Min((width - (2 * Pad)) / Math.Max(1, maxX - minX), (height - (2 * Pad)) / Math.Max(1, maxY - minY));
            double offX = (width - ((maxX - minX) * scale)) / 2;
            double offY = (height - ((maxY - minY) * scale)) / 2;
            double Sx(double x) => offX + ((x - minX) * scale);
            double Sy(double y) => offY + ((y - minY) * scale);

            for (int i = 0; i < detected.Monitors.Count; i++)
            {
                DrawTile(detected.Monitors[i], i < editor.Numbers.Count ? editor.Numbers[i] : i + 1, Sx, Sy);
            }

            var badges = new List<(CursorBrakeEdgeRowViewModel Row, double X, double Y, string Caret)>();
            foreach (var row in editor.Rows)
            {
                var edge = detected.Edges[row.EdgeIndex];
                bool vertical = edge.IsSideBySide;
                double x1;
                double y1;
                double x2;
                double y2;
                if (vertical)
                {
                    x1 = x2 = Sx(edge.At);
                    y1 = Sy(edge.From) + StrokeInset;
                    y2 = Sy(edge.To) - StrokeInset;
                }
                else
                {
                    y1 = y2 = Sy(edge.At);
                    x1 = Sx(edge.From) + StrokeInset;
                    x2 = Sx(edge.To) - StrokeInset;
                }

                if (editor.SelectedRow == row)
                {
                    MapCanvas.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = AccentBrush.Foreground, StrokeThickness = 16, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Opacity = 0.35 });
                }

                var forward = row.IsSplit ? row.Forward.ShownMode : row.ShownMode;
                var backward = row.IsSplit ? row.Backward.ShownMode : row.ShownMode;
                if (forward == backward)
                {
                    DrawStroke(forward, x1, y1, x2, y2);
                }
                else
                {
                    // Each direction on the side of the display it leaves, where the cursor meets it.
                    DrawDirection(row.First, forward, vertical, x1, y1, x2, y2, detected, Sx, Sy);
                    DrawDirection(row.Second, backward, vertical, x1, y1, x2, y2, detected, Sx, Sy);
                }

                string caret = null;
                bool forwardWall = forward == CursorBrakeEdgeMode.Wall;
                bool backwardWall = backward == CursorBrakeEdgeMode.Wall;
                if (forwardWall != backwardWall)
                {
                    int from = forwardWall ? row.Second : row.First;
                    int to = forwardWall ? row.First : row.Second;
                    caret = OpenWay(detected.Monitors[from], detected.Monitors[to], vertical);
                }

                badges.Add((row, (x1 + x2) / 2, (y1 + y2) / 2, caret));
            }

            // Badges after every stroke so none is painted over.
            foreach (var (row, x, y, caret) in badges)
            {
                AddBadge(row, x, y, caret, editor.SelectedRow == row);
            }
        }

        private void OnRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(CursorBrakeEdgeRowViewModel.SelectedIndex) or nameof(CursorBrakeEdgeRowViewModel.ShownMode) or nameof(CursorBrakeEdgeRowViewModel.IsSplit) or nameof(CursorBrakeEdgeRowViewModel.Description))
            {
                Redraw();
            }
        }

        private void DrawTile(CursorBrakeDetectedMonitor monitor, int number, Func<double, double> sx, Func<double, double> sy)
        {
            double x = sx(monitor.Rect[0]) + TileGap;
            double y = sy(monitor.Rect[1]) + TileGap;
            double w = Math.Max(0, sx(monitor.Rect[2]) - sx(monitor.Rect[0]) - (2 * TileGap));
            double h = Math.Max(0, sy(monitor.Rect[3]) - sy(monitor.Rect[1]) - (2 * TileGap));
            var tile = new Rectangle { Width = w, Height = h, RadiusX = 4, RadiusY = 4, Fill = TileFillBrush.Foreground, Stroke = TileStrokeBrush.Foreground, StrokeThickness = 1 };
            Canvas.SetLeft(tile, x);
            Canvas.SetTop(tile, y);
            MapCanvas.Children.Add(tile);

            // One line for the number chip and the name, centered. The chip is decorative to screen
            // readers, so the name carries the number.
            var chip = new Border
            {
                MinWidth = ChipSize,
                Height = ChipSize,
                Padding = new Thickness(4, 0, 4, 0),
                CornerRadius = new CornerRadius(4),
                BorderBrush = StrongStrokeBrush.Foreground,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, string.IsNullOrEmpty(monitor.Name) ? 0 : ChipGap, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = number.ToString(System.Globalization.CultureInfo.CurrentCulture),
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = PrimaryTextBrush.Foreground,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            AutomationProperties.SetAccessibilityView(chip, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);

            // The name takes the rest of the tile's width. A name that still does not fit trims with an
            // ellipsis and shows in full in a tooltip.
            var name = new TextBlock { Text = monitor.Name, MaxWidth = Math.Max(0, w - (2 * LabelInset) - ChipSize - ChipGap), TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 12, Foreground = PrimaryTextBrush.Foreground, VerticalAlignment = VerticalAlignment.Center };
            name.IsTextTrimmedChanged += (sender, e) => ToolTipService.SetToolTip(sender, sender.IsTextTrimmed ? sender.Text : null);
            string labelFormat = ResourceLoaderInstance.ResourceLoader.GetString(string.IsNullOrEmpty(monitor.Name) ? "CursorBrake_Edges_DisplayFallback" : "CursorBrake_Edges_DisplayLabel");
            AutomationProperties.SetName(name, string.IsNullOrEmpty(monitor.Name) ? string.Format(System.Globalization.CultureInfo.CurrentCulture, labelFormat, number) : string.Format(System.Globalization.CultureInfo.CurrentCulture, labelFormat, monitor.Name, number));
            var line = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            line.Children.Add(chip);
            line.Children.Add(name);
            var label = new Grid { Width = w };
            label.Children.Add(line);

            var scaleText = new TextBlock { TextAlignment = TextAlignment.Center, Width = w, FontSize = 11, Foreground = TertiaryTextBrush.Foreground, Text = CursorBrakeEdgesViewModel.ScaleText(monitor) };
            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, y + (h / 2) - ChipSize);
            Canvas.SetLeft(scaleText, x);
            Canvas.SetTop(scaleText, y + (h / 2) + 4);
            MapCanvas.Children.Add(label);
            MapCanvas.Children.Add(scaleText);
        }

        // Pattern and width carry the rule as well as color, so the map reads in high contrast.
        private void DrawStroke(CursorBrakeEdgeMode mode, double x1, double y1, double x2, double y2)
        {
            Line Line(Brush brush, double thickness) => new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = brush, StrokeThickness = thickness };
            switch (mode)
            {
                case CursorBrakeEdgeMode.Custom:
                    var custom = Line(AccentBrush.Foreground, 5);
                    custom.StrokeStartLineCap = custom.StrokeEndLineCap = PenLineCap.Round;
                    MapCanvas.Children.Add(custom);
                    break;
                case CursorBrakeEdgeMode.Off:
                    var off = Line(StrongStrokeBrush.Foreground, 2);
                    off.StrokeDashArray = new DoubleCollection { 2, 2 };
                    MapCanvas.Children.Add(off);
                    break;
                case CursorBrakeEdgeMode.Wall:
                    MapCanvas.Children.Add(Line(PrimaryTextBrush.Foreground, 8));
                    var gaps = Line(BaseBackgroundBrush.Foreground, 8);
                    gaps.StrokeDashArray = new DoubleCollection { 0.25, 0.75 };
                    gaps.StrokeDashOffset = -0.375;
                    MapCanvas.Children.Add(gaps);
                    break;
                default:
                    var normal = Line(StrongStrokeBrush.Foreground, 4);
                    normal.StrokeStartLineCap = normal.StrokeEndLineCap = PenLineCap.Round;
                    MapCanvas.Children.Add(normal);
                    break;
            }
        }

        private void DrawDirection(int from, CursorBrakeEdgeMode mode, bool vertical, double x1, double y1, double x2, double y2, CursorBrakeDetectedEdges detected, Func<double, double> sx, Func<double, double> sy)
        {
            var rect = detected.Monitors[from].Rect;
            double sign = vertical
                ? Math.Sign(sx((rect[0] + rect[2]) / 2.0) - x1)
                : Math.Sign(sy((rect[1] + rect[3]) / 2.0) - y1);
            double dx = vertical ? SplitOffset * sign : 0;
            double dy = vertical ? 0 : SplitOffset * sign;
            DrawStroke(mode, x1 + dx, y1 + dy, x2 + dx, y2 + dy);
        }

        // Which way the open direction points, from the display it leaves to the one it enters.
        private static string OpenWay(CursorBrakeDetectedMonitor from, CursorBrakeDetectedMonitor to, bool vertical)
        {
            if (vertical)
            {
                return to.Rect[0] + to.Rect[2] < from.Rect[0] + from.Rect[2] ? "left" : "right";
            }

            return to.Rect[1] + to.Rect[3] < from.Rect[1] + from.Rect[3] ? "up" : "down";
        }

        private void AddBadge(CursorBrakeEdgeRowViewModel row, double x, double y, string caret, bool selected)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
            content.Children.Add(new TextBlock { Text = row.Letter, FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            if (caret != null)
            {
                double angle = caret switch { "down" => 90, "left" => 180, "up" => -90, _ => 0 };
                content.Children.Add(new FontIcon
                {
                    Glyph = CaretGlyph,
                    FontSize = 10,
                    VerticalAlignment = VerticalAlignment.Center,
                    RenderTransformOrigin = new global::Windows.Foundation.Point(0.5, 0.5),
                    RenderTransform = new RotateTransform { Angle = angle },
                });
            }

            var badge = new Button
            {
                Content = content,
                Padding = new Thickness(6, 0, 6, 0),
                MinHeight = 20,
                MinWidth = 22,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Tag = row,

                // Opaque, so the stroke under the badge does not show through its letter.
                Background = BadgeFillBrush.Foreground,
                BorderBrush = StrongStrokeBrush.Foreground,
                BorderThickness = new Thickness(1),
            };
            if (selected && Application.Current.Resources.TryGetValue("AccentButtonStyle", out var accent) && accent is Style style)
            {
                badge.Style = style;
                badge.Padding = new Thickness(6, 0, 6, 0);
                badge.MinHeight = 20;
                badge.Height = 20;
                badge.CornerRadius = new CornerRadius(10);
            }

            AutomationProperties.SetName(badge, row.AutomationName);
            AutomationProperties.SetAutomationId(badge, "MouseUtils_CursorBrakeEdgeBadge_" + row.Letter);
            badge.Click += (s, e) => Editor.SelectedRow = row;
            badge.KeyDown += OnBadgeKeyDown;
            badge.Loaded += (s, e) =>
            {
                Canvas.SetLeft(badge, x - (badge.ActualWidth / 2));
                Canvas.SetTop(badge, y - (badge.ActualHeight / 2));
            };
            Canvas.SetLeft(badge, x - 11);
            Canvas.SetTop(badge, y - 10);
            MapCanvas.Children.Add(badge);
            _badges.Add(badge);
        }

        // Arrow keys move between the edges in letter order.
        private void OnBadgeKeyDown(object sender, KeyRoutedEventArgs e)
        {
            int step = e.Key switch
            {
                VirtualKey.Left or VirtualKey.Up => -1,
                VirtualKey.Right or VirtualKey.Down => 1,
                _ => 0,
            };
            if (step == 0 || sender is not Button current)
            {
                return;
            }

            int index = _badges.IndexOf(current);
            int next = Math.Clamp(index + step, 0, _badges.Count - 1);
            if (next != index && _badges[next].Tag is CursorBrakeEdgeRowViewModel row)
            {
                Editor.SelectedRow = row;
                string letter = row.Letter;
                DispatcherQueue.TryEnqueue(() => _badges.FirstOrDefault(b => (b.Tag as CursorBrakeEdgeRowViewModel)?.Letter == letter)?.Focus(FocusState.Keyboard));
            }

            e.Handled = true;
        }
    }
}
