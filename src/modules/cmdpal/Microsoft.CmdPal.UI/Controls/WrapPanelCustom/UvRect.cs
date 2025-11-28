// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Controls;

internal sealed class UvRect
{
    public UvMeasure Position { get; set; }

    public UvMeasure Size { get; set; }

    public Rect ToRect(Orientation orientation)
    {
        return orientation switch
        {
            Orientation.Vertical => new Rect(Position.V, Position.U, Size.V, Size.U),
            Orientation.Horizontal => new Rect(Position.U, Position.V, Size.U, Size.V),
            _ => ThrowArgumentException(),
        };
    }

    private static Rect ThrowArgumentException()
    {
        throw new ArgumentException("The input orientation is not valid.");
    }
}
