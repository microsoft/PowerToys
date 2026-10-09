// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FancyZone_HitTest
{
    public class VisualData
    {
        public Point RelativeMouseLocation { get; set; }

        public Point CenterMass { get; set; }

        public Point TopLeft { get; set; }

        public double MouseDistanceFromCenter { get; set; }

        public int Area { get; set; }

        public string Name { get; set; }

        public double DistanceFromEdge { get; set; }

        public double DistanceFromEdgePercentage { get; set; }

        public VisualData(Shape item, MouseEventArgs e, Visual root)
        {
            Name = item.Name;

            int width = (int)Math.Floor(item.ActualWidth);
            int height = (int)Math.Floor(item.ActualHeight);

            TopLeft = Utilities.GetPosition(item, root);
            CenterMass = new Point(width / 2, height / 2);
            Area = width * height;
            RelativeMouseLocation = e.GetPosition(item);
            MouseDistanceFromCenter = Point.Subtract(RelativeMouseLocation, CenterMass).Length;

            var mouseX = Math.Floor(RelativeMouseLocation.X);
            var mouseY = Math.Floor(RelativeMouseLocation.Y);

            var horDist = (RelativeMouseLocation.X < CenterMass.X) ? RelativeMouseLocation.X : width - mouseX;
            var vertDist = (RelativeMouseLocation.Y < CenterMass.Y) ? RelativeMouseLocation.Y : height - mouseY;

            var isHorCalc = horDist < vertDist;
            DistanceFromEdge = Math.Floor(isHorCalc ? horDist : vertDist);

            if (isHorCalc)
            {
                DistanceFromEdgePercentage = (width - DistanceFromEdge) / width;
            }
            else
            {
                DistanceFromEdgePercentage = (height - DistanceFromEdge) / height;
            }
        }
    }
}
