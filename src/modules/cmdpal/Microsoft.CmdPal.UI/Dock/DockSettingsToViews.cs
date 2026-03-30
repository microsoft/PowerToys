// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Settings;
using Windows.Win32;

namespace Microsoft.CmdPal.UI.Dock;

internal static class DockSettingsToViews
{
    public static double WidthForSize(DockSize size)
    {
        return size switch
        {
            DockSize.Default => 128,
            DockSize.Compact => 100,
            _ => throw new NotImplementedException(),
        };
    }

    public static double HeightForSize(DockSize size)
    {
        return size switch
        {
            DockSize.Default => 38,
            DockSize.Compact => 24,
            _ => throw new NotImplementedException(),
        };
    }

    public static uint GetAppBarEdge(DockSide side)
    {
        return side switch
        {
            DockSide.Left => PInvoke.ABE_LEFT,
            DockSide.Top => PInvoke.ABE_TOP,
            DockSide.Right => PInvoke.ABE_RIGHT,
            DockSide.Bottom => PInvoke.ABE_BOTTOM,
            _ => throw new NotImplementedException(),
        };
    }
}
