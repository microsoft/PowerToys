// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.UI.Xaml.Media;
using Windows.Win32;
using WinUIEx;

namespace Microsoft.CmdPal.UI.Dock;

internal static class DockSettingsToViews
{
    public static double WidthForSize(DockSize size)
    {
        return size switch
        {
            DockSize.Small => 128,
            DockSize.Medium => 192,
            DockSize.Large => 256,
            _ => throw new NotImplementedException(),
        };
    }

    public static double TitleTextFontSizeForSize(DockSize size)
    {
        return size switch
        {
            DockSize.Small => 12,
            DockSize.Medium => 16,
            DockSize.Large => 20,
            _ => throw new NotImplementedException(),
        };
    }

    public static double TitleTextMaxWidthForSize(DockSize size)
    {
        return WidthForSize(size) - TitleTextFontSizeForSize(size);
    }

    public static double HeightForSize(DockSize size)
    {
        return size switch
        {
            DockSize.Small => 32,
            DockSize.Medium => 54,
            DockSize.Large => 76,
            _ => throw new NotImplementedException(),
        };
    }

    public static double IconSizeForSize(DockSize size)
    {
        return size switch
        {
            DockSize.Small => 32 / 2,
            DockSize.Medium => 54 / 2,
            DockSize.Large => 76 / 2,
            _ => throw new NotImplementedException(),
        };
    }

    public static Microsoft.UI.Xaml.Media.SystemBackdrop? GetSystemBackdrop(DockBackdrop backdrop)
    {
        return backdrop switch
        {
            DockBackdrop.Mica => new MicaBackdrop(),
            DockBackdrop.Transparent => new TransparentTintBackdrop(),
            DockBackdrop.Acrylic => null, // new DesktopAcrylicBackdrop(),
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
#pragma warning restore SA1402 // File may only contain a single type
