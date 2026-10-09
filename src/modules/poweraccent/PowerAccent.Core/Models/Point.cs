// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerAccent.Core;

public struct Point
{
    public Point(double x, double y)
    {
        X = x;
        Y = y;
    }

    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }

    public double X { get; init; }

    public double Y { get; init; }
}
