// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// The notice's colors, which follow Windows: light or dark, as apps are set to be, or the high contrast theme's own.
/// The icon and the bold heading say what the notice is, so color is never the only cue.
/// </summary>
internal static class NoticeTheme
{
    /// <summary>A flyout's colors in the light theme, with the caution color for the icon, as in the design.</summary>
    internal static readonly Palette LightPalette = new(Fill: 0xFFFFFF, Text: 0x1B1B1B, Border: 0xE0E0E0, Icon: 0x9D5D00, HighContrast: false, Dark: false);

    /// <summary>A flyout's colors in the dark theme, with a faintly lighter outline so the card stands out on dark apps.</summary>
    internal static readonly Palette DarkPalette = new(Fill: 0x2C2C2C, Text: 0xFFFFFF, Border: 0x3D3D3D, Icon: 0xFCE100, HighContrast: false, Dark: true);

    /// <summary>Colors as 0xRRGGBB.</summary>
    internal readonly record struct Palette(uint Fill, uint Text, uint Border, uint Icon, bool HighContrast, bool Dark);

    /// <summary>
    /// Chooses the colors. High contrast takes the theme's window colors, with its text color for the outline and the
    /// icon, as Windows draws a flyout; otherwise the notice is light or dark, as apps are set to be.
    /// </summary>
    internal static Palette Choose(bool highContrast, bool appsUseLightTheme, uint windowColor, uint windowTextColor)
        => highContrast
            ? new Palette(windowColor, windowTextColor, windowTextColor, windowTextColor, HighContrast: true, Dark: false)
            : appsUseLightTheme ? LightPalette : DarkPalette;

    /// <summary>Reads Windows' settings now, so a change applies from the next notice.</summary>
    public static Palette Current()
        => Choose(
            IsHighContrast(),
            AppsUseLightTheme(),
            SwapRedAndBlue(Native.GetSysColor(Native.COLOR_WINDOW)),
            SwapRedAndBlue(Native.GetSysColor(Native.COLOR_WINDOWTEXT)));

    /// <summary>Turns a COLORREF, with red in the low byte, into 0xRRGGBB, and back.</summary>
    internal static uint SwapRedAndBlue(uint color) => ((color & 0xFF) << 16) | (color & 0xFF00) | ((color >> 16) & 0xFF);

    private static bool IsHighContrast()
    {
        var contrast = new Native.HIGHCONTRASTW { CbSize = (uint)Marshal.SizeOf<Native.HIGHCONTRASTW>() };
        return Native.SystemParametersInfoW(Native.SPI_GETHIGHCONTRAST, contrast.CbSize, ref contrast, 0)
            && (contrast.DwFlags & Native.HCF_HIGHCONTRASTON) != 0;
    }

    private static bool AppsUseLightTheme()
    {
        try
        {
            using RegistryKey? personalize = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return personalize?.GetValue("AppsUseLightTheme") is not int light || light != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return true;
        }
    }
}
