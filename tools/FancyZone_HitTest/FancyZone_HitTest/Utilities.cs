// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
