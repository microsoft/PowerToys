// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

// This is an unfortunate interaction between CsWin32 and CsWinRT. 
// The warning is technically correct, but because you can't edit generated 
// files, you can't really fix it. Ideally CsWin32 would generate all types as
// partial, but it doesn't do that today
[assembly: SuppressMessage("Usage", "CsWinRT1028:Class is not marked partial", Justification = "Type is not passed across the WinRT ABI", Scope = "type", Target = "Windows.Win32.DeleteObjectSafeHandle")]
[assembly: SuppressMessage("Usage", "CsWinRT1028:Class is not marked partial", Justification = "Type is not passed across the WinRT ABI", Scope = "type", Target = "Windows.Win32.DestroyIconSafeHandle")]
// Relatedly, we added a NativeMethods.json to be able to fix some
// error CA1420: Setting SetLastError to 'true' requires runtime marshalling to be enabled
// errors. 

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Converts icon paths to IconSource and IconElement objects for WinUI controls.
/// This is a C# port of the C++ IconPathConverter from Microsoft.Terminal.UI.
/// </summary>
public static class IconPathConverter
{
    /// <summary>
    /// Creates an IconSource for the given path with default settings.
    /// </summary>
    /// <param name="iconPath">The path to the icon.</param>
    /// <returns>An IconSource with its source set, if possible.</returns>
    public static IconSource? IconSource(string iconPath, string? fontFamily = null)
    {
        return IconSource(iconPath, false, fontFamily, 24);
    }

    /// <summary>
    /// Creates an IconSource for the given path.
    /// </summary>
    /// <param name="iconPath">The path to the icon.</param>
    /// <param name="monochrome">Whether to show the icon as monochrome.</param>
    /// <param name="targetSize">The target size for the icon.</param>
    /// <returns>An IconSource with its source set, if possible.</returns>
    public static IconSource? IconSource(string iconPath, bool monochrome, string? fontFamily = null, int targetSize = 24)
    {
        if (TryGetIconIndex(iconPath, out var iconPathWithoutIndex, out var index))
        {
            var bitmapSource = GetImageIconSourceForBinary(iconPathWithoutIndex, index, targetSize);
            if (bitmapSource != null)
            {
                var imageIconSource = new ImageIconSource
                {
                    ImageSource = bitmapSource
                };
                return imageIconSource;
            }
        }

        return GetIconSource(iconPath, monochrome, fontFamily, targetSize);
    }

    /// <summary>
    /// Creates an IconElement for the given path with default size.
    /// </summary>
    /// <param name="iconPath">The path to the icon.</param>
    /// <returns>An IconElement with its IconSource set, if possible.</returns>
    public static IconElement Icon(string iconPath)
    {
        return Icon(iconPath, 24);
    }

    /// <summary>
    /// Creates an IconElement for the given path.
    /// </summary>
    /// <param name="iconPath">The path to the icon.</param>
    /// <param name="targetSize">The target size for the icon.</param>
    /// <returns>An IconElement with its IconSource set, if possible.</returns>
    public static IconElement Icon(string iconPath, int targetSize)
    {
        if (TryGetIconIndex(iconPath, out var iconPathWithoutIndex, out var index))
        {
            var bitmapSource = GetImageIconSourceForBinary(iconPathWithoutIndex, index, targetSize);
            if (bitmapSource != null)
            {
                var icon = new ImageIcon
                {
                    Source = bitmapSource,
                    Width = targetSize,
                    Height = targetSize
                };
                return icon;
            }
        }

        var source = IconSource(iconPath, false, targetSize);
        var iconSourceElement = new IconSourceElement
        {
            IconSource = source
        };
        return iconSourceElement;
    }

    /// <summary>
    /// Creates an IconSource for the given path.
    /// </summary>
    /// <param name="iconPath">The path to the icon.</param>
    /// <param name="monochrome">Whether to show the icon as monochrome.</param>
    /// <param name="targetSize">The target size for the icon.</param>
    /// <returns>An IconSource with its source set, if possible.</returns>
    private static IconSource? GetIconSource(string iconPath, bool monochrome, string? fontFamily, int targetSize)
    {
        IconSource? iconSource = null;

        if (!string.IsNullOrEmpty(iconPath))
        {
            var expandedIconPath = ExpandIconPath(iconPath);
            iconSource = GetColoredBitmapIcon(expandedIconPath, monochrome);

            // If we fail to set the icon source using the "icon" as a path,
            // let's try it as a symbol/emoji.
            //
            // Anything longer than 2 characters isn't an emoji or symbol, so
            // don't do this if it's just an invalid path.
            if (iconSource == null && iconPath.Length <= 2)
            {
                try
                {
                    var icon = new FontIconSource();
                    var ch = iconPath[0];

                    // The range of MDL2 Icons isn't explicitly defined, but
                    // we're using this based off the table on:
                    // https://docs.microsoft.com/en-us/windows/uwp/design/style/segoe-ui-symbol-font
                    var isMDL2Icon = ch >= '\uE700' && ch <= '\uF8FF';
                    if (isMDL2Icon)
                    {
                        icon.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
                    }
                    else if (fontFamily != null)
                    {
                        icon.FontFamily = new FontFamily(fontFamily);
                    }
                    else
                    {
                        // Note: you do need to manually set the font here.
                        icon.FontFamily = new FontFamily("Segoe UI");
                    }
                    icon.FontSize = targetSize;
                    icon.Glyph = iconPath;
                    iconSource = icon;
                }
                catch
                {
                    // Ignore exceptions when creating font icons
                }
            }
        }

        if (iconSource == null)
        {
            // Set the default IconSource to a BitmapIconSource with a null source
            // (instead of just null) because there's a really weird crash when swapping
            // data bound IconSourceElements in a ListViewTemplate (i.e. CommandPalette).
            // Swapping between null IconSources and non-null IconSources causes a crash
            // to occur, but swapping between IconSources with a null source and non-null IconSources
            // work perfectly fine.
            var icon = new BitmapIconSource
            {
                UriSource = null
            };
            iconSource = icon;
        }

        return iconSource;
    }

    /// <summary>
    /// Creates a colored bitmap icon source for the given path.
    /// </summary>
    /// <param name="path">The full, expanded path to the icon.</param>
    /// <param name="monochrome">Whether to show the icon as monochrome.</param>
    /// <returns>An IconSource with its source set, if possible.</returns>
    private static IconSource? GetColoredBitmapIcon(string path, bool monochrome)
    {
        // FontIcon uses glyphs in the private use area, whereas valid URIs only contain ASCII characters.
        // To skip throwing on Uri construction, we can quickly check if the first character is ASCII.
        if (!string.IsNullOrEmpty(path) && path[0] < 128)
        {
            try
            {
                var iconUri = new Uri(path);

                if (Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    var iconSource = new ImageIconSource();
                    var source = new SvgImageSource(iconUri);
                    iconSource.ImageSource = source;
                    return iconSource;
                }
                else
                {
                    var iconSource = new BitmapIconSource
                    {
                        // Make sure to set this to false, so we keep the RGB data of the
                        // image. Otherwise, the icon will be white for all the
                        // non-transparent pixels in the image.
                        ShowAsMonochrome = monochrome,
                        UriSource = iconUri
                    };
                    return iconSource;
                }
            }
            catch (UriFormatException)
            {
                // Ignore exceptions when creating URI-based icons
            }
        }

        return null;
    }

    /// <summary>
    /// Expands environment variables in the icon path.
    /// </summary>
    /// <param name="iconPath">The path that may contain environment variables.</param>
    /// <returns>The expanded path.</returns>
    private static string ExpandIconPath(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
        {
            return iconPath;
        }

        // Use Environment.ExpandEnvironmentVariables as the C# equivalent of wil::ExpandEnvironmentStringsW
        return Environment.ExpandEnvironmentVariables(iconPath);
    }

    /// <summary>
    /// Attempts to get the icon index from the icon path provided.
    /// </summary>
    /// <param name="iconPath">The full icon path, including the index if present.</param>
    /// <param name="iconPathWithoutIndex">The icon path without the index.</param>
    /// <param name="iconIndex">The icon index if present.</param>
    /// <returns>True if the iconPath is an exe/dll/lnk file, false otherwise.</returns>
    private static bool TryGetIconIndex(string iconPath, out string iconPathWithoutIndex, out int iconIndex)
    {
        iconPathWithoutIndex = iconPath;
        iconIndex = 0;

        if (string.IsNullOrEmpty(iconPath))
        {
            return false;
        }

        // Does iconPath have a comma in it? If so, split the string on the
        // comma and look for the index and extension.
        var commaIndex = iconPath.IndexOf(',');

        // Split the path on the comma
        iconPathWithoutIndex = commaIndex >= 0 ? iconPath.Substring(0, commaIndex) : iconPath;

        // It's an exe, dll, or lnk, so we need to extract the icon from the file.
        var extension = Path.GetExtension(iconPathWithoutIndex).ToLowerInvariant();
        if (extension != ".exe" && extension != ".dll" && extension != ".lnk")
        {
            return false;
        }

        if (commaIndex >= 0)
        {
            // Convert the string iconIndex to a signed int to support negative numbers which represent an Icon's ID.
            var indexString = iconPath.Substring(commaIndex + 1);
            if (int.TryParse(indexString, out iconIndex))
            {
                return true;
            }

            // Failed to parse, return false
            return false;
        }

        // We had a binary path, but no index. Default to 0.
        iconIndex = 0;
        return true;
    }

    /// <summary>
    /// Gets an image icon source for a binary file (exe, dll, lnk).
    /// </summary>
    /// <param name="iconPathWithoutIndex">The path to the binary file.</param>
    /// <param name="index">The icon index within the file.</param>
    /// <param name="targetSize">The target size for the icon.</param>
    /// <returns>A SoftwareBitmapSource if successful, null otherwise.</returns>
    private static SoftwareBitmapSource? GetImageIconSourceForBinary(string iconPathWithoutIndex, int index, int targetSize)
    {
        try
        {
            using var swBitmap = GetBitmapFromIconFile(iconPathWithoutIndex, index, (uint)targetSize);
            if (swBitmap == null)
            {
                return null;
            }

            var bitmapSource = new SoftwareBitmapSource();
            _ = bitmapSource.SetBitmapAsync(swBitmap);
            return bitmapSource;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts a bitmap from an icon file using Win32 APIs.
    /// This implementation uses SHDefExtractIcon to extract icons from executables, DLLs, and shortcut files.
    /// </summary>
    /// <param name="iconPath">The path to the icon file.</param>
    /// <param name="iconIndex">The index of the icon within the file.</param>
    /// <param name="iconSize">The desired icon size.</param>
    /// <returns>A SoftwareBitmap if successful, null otherwise.</returns>
    private static SoftwareBitmap? GetBitmapFromIconFile(string iconPath, int iconIndex, uint iconSize)
    {
        try
        {
            DestroyIconSafeHandle? hIconLarge;
            DestroyIconSafeHandle? hIconSmall;
            // Extract the icon using SHDefExtractIcon
            var result = PInvoke.SHDefExtractIcon(
                iconPath,
                iconIndex,
                0,
                out hIconLarge,
                out hIconSmall,
                iconSize);
            using (hIconLarge)
            using (hIconSmall)
            {
                if (result != 0 || hIconLarge.IsInvalid)
                {
                    return null;
                }

                // For simplicity, convert HICON to bitmap using a more straightforward approach
                // This could be enhanced to use WIC directly for better performance
                return ConvertHIconToSoftwareBitmap(hIconLarge);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts an HICON to a SoftwareBitmap using Win32 APIs.
    /// </summary>
    /// <param name="hIcon">The icon handle to convert.</param>
    /// <returns>A SoftwareBitmap if successful, null otherwise.</returns>
    private static unsafe SoftwareBitmap? ConvertHIconToSoftwareBitmap(DestroyIconSafeHandle hIcon)
    {
        // Get icon information
        if (!PInvoke.GetIconInfo(hIcon, out var iconInfo))
        {
            return null;
        }
        // Create device contexts
        var hdcScreen = PInvoke.GetDC(HWND.Null);
        var hdcMem = PInvoke.CreateCompatibleDC(hdcScreen);

        // Get the icon size (assuming 32x32 for now, could be enhanced to get actual size)
        const int iconSize = 32;

        // Create a bitmap to draw the icon onto
        using var hBitmap = PInvoke.CreateCompatibleBitmap_SafeHandle(hdcScreen, iconSize, iconSize);
        using var hOldBitmap = PInvoke.SelectObject(hdcMem, hBitmap);

        // Draw the icon onto the bitmap
        PInvoke.DrawIconEx(hdcMem, 0, 0, hIcon, iconSize, iconSize, 0, null, (DI_FLAGS)0x0003); // DI_NORMAL

        // Get bitmap info
        var bitmapInfo = new BITMAPINFO();
        bitmapInfo.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
        bitmapInfo.bmiHeader.biWidth = iconSize;
        bitmapInfo.bmiHeader.biHeight = -iconSize; // Top-down DIB
        bitmapInfo.bmiHeader.biPlanes = 1;
        bitmapInfo.bmiHeader.biBitCount = 32;
        bitmapInfo.bmiHeader.biCompression = 0; // BI_RGB

        // Allocate buffer for pixel data
        var pixelDataSize = iconSize * iconSize * 4; // 4 bytes per pixel (BGRA)
        var pixelData = new byte[pixelDataSize];

        // Get the pixel data - need to pin the byte array
        fixed (byte* pixelPtr = pixelData)
        {
            var result = PInvoke.GetDIBits(
                hdcMem,
                hBitmap,
                0,
                iconSize,
                pixelPtr,
                &bitmapInfo,
                0); // DIB_RGB_COLORS

            if (result == 0)
            {
                return null;
            }
        }

        // Create SoftwareBitmap from pixel data
        var softwareBitmap = SoftwareBitmap.CreateCopyFromBuffer(
            pixelData.AsBuffer(),
            BitmapPixelFormat.Bgra8,
            iconSize,
            iconSize,
            BitmapAlphaMode.Premultiplied);

        return softwareBitmap;
    }
}
