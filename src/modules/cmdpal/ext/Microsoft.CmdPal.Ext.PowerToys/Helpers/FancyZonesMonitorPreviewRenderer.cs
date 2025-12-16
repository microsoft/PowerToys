// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.Interop;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PowerToysExtension.Helpers;

internal static class FancyZonesMonitorPreviewRenderer
{
    public static IconInfo? TryRenderMonitorHeroImage(FancyZonesMonitorDescriptor monitor)
    {
        try
        {
            var cached = TryGetCachedIcon(monitor);
            if (cached is not null)
            {
                return cached;
            }

            var icon = RenderMonitorHeroImageAsync(monitor).GetAwaiter().GetResult();
            return icon;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"FancyZones monitor hero render failed. Monitor={monitor.Data.Monitor} Index={monitor.Index} Exception={ex}");
            return null;
        }
    }

    private static IconInfo? TryGetCachedIcon(FancyZonesMonitorDescriptor monitor)
    {
        var cachePath = GetCachePath(monitor);
        if (string.IsNullOrEmpty(cachePath))
        {
            return null;
        }

        try
        {
            if (File.Exists(cachePath))
            {
                return new IconInfo(cachePath);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"FancyZones monitor hero cache check failed. Path=\"{cachePath}\" Monitor={monitor.Data.Monitor} Index={monitor.Index} Exception={ex}");
        }

        return null;
    }

    private static async Task<IconInfo?> RenderMonitorHeroImageAsync(FancyZonesMonitorDescriptor monitor)
    {
        var cachePath = GetCachePath(monitor);
        if (string.IsNullOrEmpty(cachePath))
        {
            return null;
        }

        var (widthPx, heightPx) = ComputeCanvasSize(monitor.Data);
        Logger.LogDebug($"FancyZones monitor hero render starting. Monitor={monitor.Data.Monitor} Index={monitor.Index} Size={widthPx}x{heightPx}");

        var (layoutRectangles, spacing) = GetLayoutRectangles(monitor.Data);
        var pixelBytes = RenderMonitorPreviewBgra(widthPx, heightPx, layoutRectangles, spacing);

        var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            (uint)widthPx,
            (uint)heightPx,
            96,
            96,
            pixelBytes);
        await encoder.FlushAsync();
        stream.Seek(0);

        try
        {
            var tempPath = FormattableString.Invariant($"{cachePath}.{Guid.NewGuid():N}.tmp");
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await WriteStreamToFileAsync(stream, tempPath);
            File.Move(tempPath, cachePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"FancyZones monitor hero cache write failed. Path=\"{cachePath}\" Monitor={monitor.Data.Monitor} Index={monitor.Index} Exception={ex}");
            return null;
        }

        Logger.LogDebug($"FancyZones monitor hero render succeeded. Monitor={monitor.Data.Monitor} Index={monitor.Index} Path=\"{cachePath}\"");
        return new IconInfo(cachePath);
    }

    private static (int WidthPx, int HeightPx) ComputeCanvasSize(FancyZonesEditorMonitor monitor)
    {
        const int maxDim = 320;
        var w = monitor.WorkAreaWidth > 0 ? monitor.WorkAreaWidth : monitor.MonitorWidth;
        var h = monitor.WorkAreaHeight > 0 ? monitor.WorkAreaHeight : monitor.MonitorHeight;

        if (w <= 0 || h <= 0)
        {
            return (maxDim, 180);
        }

        var aspect = (float)w / h;
        if (aspect >= 1)
        {
            var height = (int)Math.Clamp(Math.Round(maxDim / aspect), 90, maxDim);
            return (maxDim, height);
        }
        else
        {
            var width = (int)Math.Clamp(Math.Round(maxDim * aspect), 90, maxDim);
            return (width, maxDim);
        }
    }

    private static (List<FancyZonesThumbnailRenderer.NormalizedRect> Rects, int Spacing) GetLayoutRectangles(FancyZonesEditorMonitor monitor)
    {
        if (!FancyZonesDataService.TryGetAppliedLayoutForMonitor(monitor, out var applied) || applied is null)
        {
            return ([], 0);
        }

        var layout = FindLayoutDescriptor(applied);
        if (layout is null)
        {
            return ([], 0);
        }

        var rects = FancyZonesThumbnailRenderer.GetNormalizedRectsForLayout(layout);
        var spacing = layout.ApplyLayout.ShowSpacing && layout.ApplyLayout.Spacing > 0 ? layout.ApplyLayout.Spacing : 0;
        return (rects, spacing);
    }

    private static FancyZonesLayoutDescriptor? FindLayoutDescriptor(FancyZonesAppliedLayout applied)
    {
        try
        {
            var layouts = FancyZonesDataService.GetLayouts();

            if (!string.IsNullOrWhiteSpace(applied.Uuid) &&
                !applied.Uuid.Equals("{00000000-0000-0000-0000-000000000000}", StringComparison.OrdinalIgnoreCase))
            {
                return layouts.FirstOrDefault(l => l.Source == FancyZonesLayoutSource.Custom &&
                                                  l.Custom is not null &&
                                                  string.Equals(l.Custom.Uuid?.Trim(), applied.Uuid.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            var type = applied.Type?.Trim().ToLowerInvariant() ?? string.Empty;
            var zoneCount = applied.ZoneCount;
            return layouts.FirstOrDefault(l =>
                l.Source == FancyZonesLayoutSource.Template &&
                string.Equals(l.ApplyLayout.Type, type, StringComparison.OrdinalIgnoreCase) &&
                l.ApplyLayout.ZoneCount == zoneCount &&
                l.ApplyLayout.ShowSpacing == applied.ShowSpacing &&
                l.ApplyLayout.Spacing == applied.Spacing);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetCachePath(FancyZonesMonitorDescriptor monitor)
    {
        try
        {
            var basePath = Constants.AppDataPath();
            if (string.IsNullOrWhiteSpace(basePath))
            {
                return null;
            }

            var cacheFolder = Path.Combine(basePath, "CmdPal", "PowerToysExtension", "Cache", "FancyZones", "MonitorPreviews");
            var fileName = ComputeMonitorHash(monitor) + ".png";
            return Path.Combine(cacheFolder, fileName);
        }
        catch
        {
            return null;
        }
    }

    private static string ComputeMonitorHash(FancyZonesMonitorDescriptor monitor)
    {
        var appliedFingerprint = string.Empty;
        if (FancyZonesDataService.TryGetAppliedLayoutForMonitor(monitor.Data, out var applied) && applied is not null)
        {
            appliedFingerprint = FormattableString.Invariant($"{applied.Type}|{applied.Uuid}|{applied.ZoneCount}|{applied.ShowSpacing}|{applied.Spacing}");
        }

        var identity = FormattableString.Invariant(
            $"{monitor.Data.Monitor}|{monitor.Data.MonitorInstanceId}|{monitor.Data.MonitorSerialNumber}|{monitor.Data.MonitorNumber}|{monitor.Data.VirtualDesktop}|{monitor.Data.WorkAreaWidth}x{monitor.Data.WorkAreaHeight}|{monitor.Data.MonitorWidth}x{monitor.Data.MonitorHeight}|{appliedFingerprint}");

        var bytes = Encoding.UTF8.GetBytes(identity);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static byte[] RenderMonitorPreviewBgra(
        int widthPx,
        int heightPx,
        IReadOnlyList<FancyZonesThumbnailRenderer.NormalizedRect> rects,
        int spacing)
    {
        var pixels = new byte[widthPx * heightPx * 4];

        var frame = Premultiply(new BgraColor(0x80, 0x80, 0x80, 0xFF));
        var bezelFill = Premultiply(new BgraColor(0x20, 0x20, 0x20, 0x18));
        var screenFill = Premultiply(new BgraColor(0x00, 0x00, 0x00, 0x00));
        var border = Premultiply(new BgraColor(0xFF, 0xD8, 0x8C, 0xFF));
        var fill = Premultiply(new BgraColor(0xFF, 0xD8, 0x8C, 0xC0));
        var background = Premultiply(new BgraColor(0x00, 0x00, 0x00, 0x00));

        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i + 0] = background.B;
            pixels[i + 1] = background.G;
            pixels[i + 2] = background.R;
            pixels[i + 3] = background.A;
        }

        DrawRectBorder(pixels, widthPx, heightPx, 0, 0, widthPx, heightPx, frame);

        const int bezel = 3;
        FillRect(pixels, widthPx, heightPx, 1, 1, widthPx - 1, heightPx - 1, bezelFill);
        FillRect(pixels, widthPx, heightPx, 1 + bezel, 1 + bezel, widthPx - 1 - bezel, heightPx - 1 - bezel, screenFill);

        var innerLeft = 1 + bezel;
        var innerTop = 1 + bezel;
        var innerRight = widthPx - 1 - bezel;
        var innerBottom = heightPx - 1 - bezel;
        var innerWidth = Math.Max(1, innerRight - innerLeft);
        var innerHeight = Math.Max(1, innerBottom - innerTop);

        var gapPx = spacing > 0 ? Math.Clamp(spacing / 8, 1, 3) : 0;
        foreach (var rect in rects)
        {
            var (x1, y1, x2, y2) = ToPixelBounds(rect, innerLeft, innerTop, innerWidth, innerHeight, gapPx);
            if (x2 <= x1 || y2 <= y1)
            {
                continue;
            }

            FillRect(pixels, widthPx, heightPx, x1, y1, x2, y2, fill);
            DrawRectBorder(pixels, widthPx, heightPx, x1, y1, x2, y2, border);
        }

        return pixels;
    }

    private static (int X1, int Y1, int X2, int Y2) ToPixelBounds(
        FancyZonesThumbnailRenderer.NormalizedRect rect,
        int originX,
        int originY,
        int widthPx,
        int heightPx,
        int gapPx)
    {
        var x1 = originX + (int)MathF.Round(rect.X * widthPx);
        var y1 = originY + (int)MathF.Round(rect.Y * heightPx);
        var x2 = originX + (int)MathF.Round((rect.X + rect.Width) * widthPx);
        var y2 = originY + (int)MathF.Round((rect.Y + rect.Height) * heightPx);

        x1 = Math.Clamp(x1 + gapPx, originX, originX + widthPx - 1);
        y1 = Math.Clamp(y1 + gapPx, originY, originY + heightPx - 1);
        x2 = Math.Clamp(x2 - gapPx, originX + 1, originX + widthPx);
        y2 = Math.Clamp(y2 - gapPx, originY + 1, originY + heightPx);

        if (x2 <= x1 + 1)
        {
            x2 = Math.Min(originX + widthPx, x1 + 2);
        }

        if (y2 <= y1 + 1)
        {
            y2 = Math.Min(originY + heightPx, y1 + 2);
        }

        return (x1, y1, x2, y2);
    }

    private static void FillRect(byte[] pixels, int widthPx, int heightPx, int x1, int y1, int x2, int y2, BgraColor color)
    {
        for (var y = y1; y < y2; y++)
        {
            if ((uint)y >= (uint)heightPx)
            {
                continue;
            }

            var rowStart = y * widthPx * 4;
            for (var x = x1; x < x2; x++)
            {
                if ((uint)x >= (uint)widthPx)
                {
                    continue;
                }

                var i = rowStart + (x * 4);
                pixels[i + 0] = color.B;
                pixels[i + 1] = color.G;
                pixels[i + 2] = color.R;
                pixels[i + 3] = color.A;
            }
        }
    }

    private static void DrawRectBorder(byte[] pixels, int widthPx, int heightPx, int x1, int y1, int x2, int y2, BgraColor color)
    {
        var left = x1;
        var right = x2 - 1;
        var top = y1;
        var bottom = y2 - 1;

        for (var x = left; x <= right; x++)
        {
            SetPixel(pixels, widthPx, heightPx, x, top, color);
            SetPixel(pixels, widthPx, heightPx, x, bottom, color);
        }

        for (var y = top; y <= bottom; y++)
        {
            SetPixel(pixels, widthPx, heightPx, left, y, color);
            SetPixel(pixels, widthPx, heightPx, right, y, color);
        }
    }

    private static void SetPixel(byte[] pixels, int widthPx, int heightPx, int x, int y, BgraColor color)
    {
        if ((uint)x >= (uint)widthPx || (uint)y >= (uint)heightPx)
        {
            return;
        }

        var i = ((y * widthPx) + x) * 4;
        pixels[i + 0] = color.B;
        pixels[i + 1] = color.G;
        pixels[i + 2] = color.R;
        pixels[i + 3] = color.A;
    }

    private static BgraColor Premultiply(BgraColor color)
    {
        if (color.A == 0 || color.A == 255)
        {
            return color;
        }

        byte Premul(byte c) => (byte)(((c * color.A) + 127) / 255);
        return new BgraColor(Premul(color.B), Premul(color.G), Premul(color.R), color.A);
    }

    private readonly record struct BgraColor(byte B, byte G, byte R, byte A);

    private static async Task WriteStreamToFileAsync(IRandomAccessStream stream, string filePath)
    {
        stream.Seek(0);
        var size = stream.Size;
        if (size == 0)
        {
            File.WriteAllBytes(filePath, Array.Empty<byte>());
            return;
        }

        if (size > int.MaxValue)
        {
            throw new InvalidOperationException("Icon stream too large.");
        }

        using var input = stream.GetInputStreamAt(0);
        using var reader = new DataReader(input);
        await reader.LoadAsync((uint)size);
        var bytes = new byte[(int)size];
        reader.ReadBytes(bytes);
        File.WriteAllBytes(filePath, bytes);
    }
}
