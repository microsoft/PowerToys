// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FancyZone_HitTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private static ArrayList _hitResultsList = new ArrayList();
        private static ArrayList _visualCalculationList = new ArrayList();

        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            // Retrieve the coordinate of the mouse position.
            Point gridMouseLocation = e.GetPosition((UIElement)sender);

            // Clear the contents of the list used for hit test results.
            _hitResultsList.Clear();
            _visualCalculationList.Clear();

            // Set up a callback to receive the hit test result enumeration.
            VisualTreeHelper.HitTest(hitTestGrid, null, new HitTestResultCallback(MyHitTestResult), new PointHitTestParameters(gridMouseLocation));

            // Perform actions on the hit test results list.
            if (_hitResultsList.Count > 0)
            {
                foreach (Rectangle item in hitTestGrid.Children.OfType<Rectangle>())
                {
                    item.Opacity = 0.25;
                    item.StrokeThickness = 0;
                }

                itemsHit.Text = string.Format("Number of Visuals Hit: {0}{1}", _hitResultsList.Count, Environment.NewLine);
                itemsHit.Text += string.Format("Grid mouse: {0}{1}", gridMouseLocation, Environment.NewLine);
                itemsHit.Text += Environment.NewLine;

                foreach (Shape item in _hitResultsList)
                {
                    _visualCalculationList.Add(new VisualData(item, e, hitTestGrid));
                }

                var reorderedVisualData = _visualCalculationList.Cast<VisualData>().OrderBy(x => x, new VisualDataComparer<VisualData>());

                foreach (VisualData item in reorderedVisualData)
                {
                    itemsHit.Text += string.Format(
                        "Name: {7}{0}" +
                        "C Mouse: {4}{0}" +
                        "Rel Mouse: {3}{0}" +
                        "Edge Mouse: {8}{0}" +
                        "Abs TL: {1}{0}" +
                        "Center: {2}{0}" +
                        "Area: {5}{0}" +
                        "Edge %: {9}{0}" +
                        "a/d: {6}{0}",
                        Environment.NewLine,
                        item.TopLeft, // 1
                        item.CenterMass, // 2
                        item.RelativeMouseLocation, // 3
                        item.MouseDistanceFromCenter, // 4
                        item.Area, // 5
                        item.Area / item.MouseDistanceFromCenter, // 6
                        item.Name, // 7
                        item.DistanceFromEdge, // 8
                        item.DistanceFromEdgePercentage); // 9
                    itemsHit.Text += Environment.NewLine;
                }

                if (reorderedVisualData.Count() > 0)
                {
                    var rect = hitTestGrid.FindName(reorderedVisualData.First().Name) as Rectangle;
                    rect.Opacity = .75;
                    rect.Stroke = Brushes.Black;
                    rect.StrokeThickness = 5;
                }
            }
            else
            {
                itemsHit.Text = string.Empty;
            }
        }

        public static HitTestResultBehavior MyHitTestResult(HitTestResult result)
        {
            // Add the hit test result to the list that will be processed after the enumeration.
            _hitResultsList.Add(result.VisualHit);

            // Set the behavior to return visuals at all z-order levels.
            return HitTestResultBehavior.Continue;
        }
    }
}
