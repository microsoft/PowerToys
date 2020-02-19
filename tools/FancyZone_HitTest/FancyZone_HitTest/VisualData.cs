using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FancyZone_HitTest
{
    public class VisualData
    {
        public Point RelativeMouseLocation;
        public Point CenterMass;
        public Point TopLeft;
        public double MouseDistanceFromCenter;
        public int Area;
        public string Name;
        public double DistanceFromEdge;
        public double DistanceFromEdgePercentage;

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

            var isHorCalc = (horDist < vertDist);
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
