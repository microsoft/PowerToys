// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace FancyZone_HitTest
{
    public class VisualDataComparer<T> : IComparer<VisualData>
    {
        int IComparer<VisualData>.Compare(VisualData x, VisualData y)
        {
            // has quirks but workable
            if (x.DistanceFromEdge == y.DistanceFromEdge)
            {
                return y.Area.CompareTo(x.Area);
            }
            else
            {
                return x.DistanceFromEdge.CompareTo(y.DistanceFromEdge);
            }

            // entire screen won't work
            /*
            if (x.MouseDistanceFromCenter == y.MouseDistanceFromCenter)
            {
                return y.Area.CompareTo(x.Area);
            }
            else
            {
                return x.MouseDistanceFromCenter.CompareTo(y.MouseDistanceFromCenter);
            }

            if (x.DistanceFromEdgePercentage == y.DistanceFromEdgePercentage)
            {
                return y.Area.CompareTo(x.Area);
            }
            else
            {
                return x.DistanceFromEdgePercentage.CompareTo(y.DistanceFromEdgePercentage);
            }*/
        }
    }
}
