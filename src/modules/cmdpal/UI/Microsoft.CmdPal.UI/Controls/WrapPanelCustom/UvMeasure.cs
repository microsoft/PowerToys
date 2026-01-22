// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Controls;

[DebuggerDisplay("U = {U} V = {V}")]
internal struct UvMeasure
{
    internal double U { get; set; }

    internal double V { get; set; }

    internal static UvMeasure Zero => default(UvMeasure);

    public UvMeasure(Orientation orientation, Size size)
    : this(orientation, size.Width, size.Height)
    {
    }

    public UvMeasure(Orientation orientation, double width, double height)
    {
        if (orientation == Orientation.Horizontal)
        {
            U = width;
            V = height;
        }
        else
        {
            U = height;
            V = width;
        }
    }

    public UvMeasure Add(double u, double v)
    {
        UvMeasure result = default(UvMeasure);
        result.U = U + u;
        result.V = V + v;
        return result;
    }

    public UvMeasure Add(UvMeasure measure)
    {
        return Add(measure.U, measure.V);
    }

    public Size ToSize(Orientation orientation)
    {
        if (orientation != Orientation.Horizontal)
        {
            return new Size(V, U);
        }

        return new Size(U, V);
    }

    public Point GetPoint(Orientation orientation)
    {
        return orientation is Orientation.Horizontal ? new Point(U, V) : new Point(V, U);
    }

    public Size GetSize(Orientation orientation)
    {
        return orientation is Orientation.Horizontal ? new Size(U, V) : new Size(V, U);
    }

    public static bool operator ==(UvMeasure measure1, UvMeasure measure2)
    {
        return measure1.U == measure2.U && measure1.V == measure2.V;
    }

    public static bool operator !=(UvMeasure measure1, UvMeasure measure2)
    {
        return !(measure1 == measure2);
    }

    public override bool Equals(object? obj)
    {
        return obj is UvMeasure measure && this == measure;
    }

    public bool Equals(UvMeasure value)
    {
        return this == value;
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }
}
