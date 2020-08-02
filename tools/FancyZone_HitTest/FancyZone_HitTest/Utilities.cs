using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace FancyZone_HitTest
{
    public static class Utilities
    {
        public static Point GetPosition(Visual element, Visual root)
        {
            var positionTransform = element.TransformToAncestor(root);
            var areaPosition = positionTransform.Transform(new Point(0, 0));

            return areaPosition;
        }
    }
}
