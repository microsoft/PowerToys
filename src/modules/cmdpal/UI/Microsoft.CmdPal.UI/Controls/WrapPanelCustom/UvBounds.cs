// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Controls;

internal sealed class UVBounds
{
    public double UMin { get; }

    public double UMax { get; }

    public double VMin { get; }

    public double VMax { get; }

    public UVBounds(Orientation orientation, Rect rect)
    {
        if (orientation == Orientation.Horizontal)
        {
            UMin = rect.Left;
            UMax = rect.Right;
            VMin = rect.Top;
            VMax = rect.Bottom;
        }
        else
        {
            UMin = rect.Top;
            UMax = rect.Bottom;
            VMin = rect.Left;
            VMax = rect.Right;
        }
    }
}
