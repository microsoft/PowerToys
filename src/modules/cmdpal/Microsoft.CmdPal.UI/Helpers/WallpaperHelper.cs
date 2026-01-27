// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using ManagedCsWin32;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Lightweight helper to access wallpaper information.
/// </summary>
internal sealed partial class WallpaperHelper
{
    private readonly ILogger _logger;
    private readonly IDesktopWallpaper? _desktopWallpaper;

    public WallpaperHelper(ILogger logger)
    {
        _logger = logger;

        try
        {
            var desktopWallpaper = ComHelper.CreateComInstance<IDesktopWallpaper>(
                ref Unsafe.AsRef(in CLSID.DesktopWallpaper),
                CLSCTX.ALL);

            _desktopWallpaper = desktopWallpaper;
        }
        catch (Exception ex)
        {
            // If COM initialization fails, keep helper usable with safe fallbacks
            Log_FailedToInitializeDesktopWallpaper(ex);
            _desktopWallpaper = null;
        }
    }

    private string? GetWallpaperPathForFirstMonitor()
    {
        try
        {
            if (_desktopWallpaper is null)
            {
                return null;
            }

            _desktopWallpaper.GetMonitorDevicePathCount(out var monitorCount);

            for (uint i = 0; monitorCount != 0 && i < monitorCount; i++)
            {
                _desktopWallpaper.GetMonitorDevicePathAt(i, out var monitorId);
                if (string.IsNullOrEmpty(monitorId))
                {
                    continue;
                }

                _desktopWallpaper.GetWallpaper(monitorId, out var wallpaperPath);

                if (!string.IsNullOrWhiteSpace(wallpaperPath) && File.Exists(wallpaperPath))
                {
                    return wallpaperPath;
                }
            }
        }
        catch (Exception ex)
        {
            Log_FailedToQueryWallpaperPath(ex);
        }

        return null;
    }

    /// <summary>
    /// Gets the wallpaper background color.
    /// </summary>
    /// <returns>The wallpaper background color, or black if it cannot be determined.</returns>
    public Color GetWallpaperColor()
    {
        try
        {
            if (_desktopWallpaper is null)
            {
                return Colors.Black;
            }

            _desktopWallpaper.GetBackgroundColor(out var colorref);
            var r = (byte)(colorref.Value & 0x000000FF);
            var g = (byte)((colorref.Value & 0x0000FF00) >> 8);
            var b = (byte)((colorref.Value & 0x00FF0000) >> 16);
            return Color.FromArgb(255, r, g, b);
        }
        catch (Exception ex)
        {
            Log_FailedToLoadWallpaperColor(ex);
            return Colors.Black;
        }
    }

    /// <summary>
    /// Gets the wallpaper image for the primary monitor.
    /// </summary>
    /// <returns>The wallpaper image, or null if it cannot be determined.</returns>
    public BitmapImage? GetWallpaperImage()
    {
        try
        {
            var path = GetWallpaperPathForFirstMonitor();
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var image = new BitmapImage();
            using var stream = File.OpenRead(path);
            var randomAccessStream = stream.AsRandomAccessStream();
            if (randomAccessStream == null)
            {
                Log_FailedToConvertStream();
                return null;
            }

            image.SetSource(randomAccessStream);
            return image;
        }
        catch (Exception ex)
        {
            Log_FailedToLoadWallpaperImage(ex);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to initialize DesktopWallpaper COM interface")]
    partial void Log_FailedToInitializeDesktopWallpaper(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to query wallpaper path")]
    partial void Log_FailedToQueryWallpaperPath(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load wallpaper color")]
    partial void Log_FailedToLoadWallpaperColor(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to convert file stream to RandomAccessStream for wallpaper image")]
    partial void Log_FailedToConvertStream();

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load wallpaper image")]
    partial void Log_FailedToLoadWallpaperImage(Exception ex);

    // blittable type for COM interop
    [StructLayout(LayoutKind.Sequential)]
    internal readonly partial struct COLORREF
    {
        internal readonly uint Value;
    }

    // blittable type for COM interop
    [StructLayout(LayoutKind.Sequential)]
    internal readonly partial struct RECT
    {
        internal readonly int Left;
        internal readonly int Top;
        internal readonly int Right;
        internal readonly int Bottom;
    }

    // COM interface for IDesktopWallpaper, GeneratedComInterface to be AOT compatible
    [GeneratedComInterface]
    [Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal partial interface IDesktopWallpaper
    {
        void SetWallpaper(
            [MarshalAs(UnmanagedType.LPWStr)] string? monitorId,
            [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

        void GetWallpaper(
            [MarshalAs(UnmanagedType.LPWStr)] string? monitorId,
            [MarshalAs(UnmanagedType.LPWStr)] out string wallpaper);

        void GetMonitorDevicePathAt(uint monitorIndex, [MarshalAs(UnmanagedType.LPWStr)] out string monitorId);

        void GetMonitorDevicePathCount(out uint count);

        void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string? monitorId, out RECT rect);

        void SetBackgroundColor(COLORREF color);

        void GetBackgroundColor(out COLORREF color);

        // Other methods omitted for brevity
    }
}
