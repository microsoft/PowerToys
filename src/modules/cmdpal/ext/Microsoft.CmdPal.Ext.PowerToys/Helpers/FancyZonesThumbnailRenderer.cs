// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FancyZonesEditorCommon.Data;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

using InteropConstants = PowerToys.Interop.Constants;

namespace PowerToysExtension.Helpers;

internal static class FancyZonesThumbnailRenderer
{
    internal readonly record struct NormalizedRect(float X, float Y, float Width, float Height);

    private readonly record struct BgraColor(byte B, byte G, byte R, byte A);

    public static async Task<IconInfo?> RenderLayoutIconAsync(FancyZonesLayoutDescriptor layout, int sizePx = 72)
    {
        try
        {
            Logger.LogDebug($"FancyZones thumbnail render starting. LayoutId={layout.Id} Type={layout.ApplyLayout.Type} ZoneCount={layout.ApplyLayout.ZoneCount} Source={layout.Source}");
            if (sizePx < 16)
            {
                sizePx = 16;
            }

            var cachedIcon = TryGetCachedIcon(layout);
            if (cachedIcon is not null)
            {
                Logger.LogDebug($"FancyZones thumbnail cache hit. LayoutId={layout.Id}");
                return cachedIcon;
            }

            var rects = GetNormalizedRectsForLayout(layout);
            Logger.LogDebug($"FancyZones thumbnail rects computed. LayoutId={layout.Id} RectCount={rects.Count}");
            var pixelBytes = RenderBgra(rects, sizePx, layout.ApplyLayout.ShowSpacing && layout.ApplyLayout.Spacing > 0 ? layout.ApplyLayout.Spacing : 0);
            var stream = new InMemoryRandomAccessStream();

            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                (uint)sizePx,
                (uint)sizePx,
                96,
                96,
                pixelBytes);

            await encoder.FlushAsync();
            stream.Seek(0);

            var cachePath = GetCachePath(layout);
            if (!string.IsNullOrEmpty(cachePath))
            {
                try
                {
                    var tempPath = FormattableString.Invariant($"{cachePath}.{Guid.NewGuid():N}.tmp");
                    Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                    await WriteStreamToFileAsync(stream, tempPath);
                    File.Move(tempPath, cachePath, overwrite: true);

                    var fileIcon = new IconInfo(cachePath);
                    Logger.LogDebug($"FancyZones thumbnail render succeeded (file cache). LayoutId={layout.Id} Path=\"{cachePath}\"");
                    return fileIcon;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"FancyZones thumbnail write cache failed. LayoutId={layout.Id} Path=\"{cachePath}\" Exception={ex}");
                }
            }

            // Fallback: return an in-memory stream icon. This may not marshal reliably cross-proc,
            // so prefer the file-cached path above.
            stream.Seek(0);
            var inMemoryIcon = IconInfo.FromStream(stream);
            Logger.LogDebug($"FancyZones thumbnail render succeeded (in-memory). LayoutId={layout.Id}");
            return inMemoryIcon;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"FancyZones thumbnail render failed. LayoutId={layout.Id} Type={layout.ApplyLayout.Type} ZoneCount={layout.ApplyLayout.ZoneCount} Source={layout.Source} Exception={ex}");
            return null;
        }
    }

    private static IconInfo? TryGetCachedIcon(FancyZonesLayoutDescriptor layout)
    {
        var cachePath = GetCachePath(layout);
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
            Logger.LogWarning($"FancyZones thumbnail cache check failed. LayoutId={layout.Id} Path=\"{cachePath}\" Exception={ex}");
        }

        return null;
    }

    /// <summary>
    /// Removes cached thumbnail files that no longer correspond to any current layout.
    /// Call this on startup or periodically to prevent unbounded cache growth.
    /// </summary>
    public static void PurgeOrphanedCache()
    {
        try
        {
            var cacheFolder = GetCacheFolder();
            if (string.IsNullOrEmpty(cacheFolder) || !Directory.Exists(cacheFolder))
            {
                return;
            }

            // Get all current layouts and compute their expected cache file names
            var layouts = FancyZonesDataService.GetLayouts();
            var validHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var layout in layouts)
            {
                validHashes.Add(ComputeLayoutHash(layout) + ".png");
            }

            // Delete any .png files not in the valid set
            var deletedCount = 0;
            foreach (var filePath in Directory.EnumerateFiles(cacheFolder, "*.png"))
            {
                var fileName = Path.GetFileName(filePath);
                if (!validHashes.Contains(fileName))
                {
                    try
                    {
                        File.Delete(filePath);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"FancyZones thumbnail cache purge: failed to delete \"{filePath}\". Exception={ex.Message}");
                    }
                }
            }

            if (deletedCount > 0)
            {
                Logger.LogInfo($"FancyZones thumbnail cache purge: deleted {deletedCount} orphaned file(s).");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"FancyZones thumbnail cache purge failed. Exception={ex}");
        }
    }

    private static string? GetCacheFolder()
    {
        var basePath = InteropConstants.AppDataPath();
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return null;
        }

        return Path.Combine(basePath, "CmdPal", "PowerToysExtension", "Cache", "FancyZones", "LayoutThumbnails");
    }

    private static string? GetCachePath(FancyZonesLayoutDescriptor layout)
    {
        try
        {
            var cacheFolder = GetCacheFolder();
            if (string.IsNullOrEmpty(cacheFolder))
            {
                return null;
            }

            var fileName = ComputeLayoutHash(layout) + ".png";
            return Path.Combine(cacheFolder, fileName);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"FancyZones thumbnail cache path failed. LayoutId={layout.Id} Exception={ex}");
            return null;
        }
    }

    private static string ComputeLayoutHash(FancyZonesLayoutDescriptor layout)
    {
        var customType = layout.Custom?.Type?.Trim() ?? string.Empty;
        var customInfo = layout.Custom is not null && layout.Custom.Value.Info.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null
            ? layout.Custom.Value.Info.GetRawText()
            : string.Empty;

        var fingerprint = FormattableString.Invariant(
            $"{layout.Id}|{layout.Source}|{layout.ApplyLayout.Type}|{layout.ApplyLayout.ZoneCount}|{layout.ApplyLayout.ShowSpacing}|{layout.ApplyLayout.Spacing}|{customType}|{customInfo}");

        var bytes = Encoding.UTF8.GetBytes(fingerprint);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

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

    internal static List<NormalizedRect> GetNormalizedRectsForLayout(FancyZonesLayoutDescriptor layout)
    {
        var type = layout.ApplyLayout.Type.ToLowerInvariant();
        if (layout.Source == FancyZonesLayoutSource.Custom && layout.Custom is not null)
        {
            return GetCustomRects(layout.Custom.Value);
        }

        return type switch
        {
            "columns" => GetColumnsRects(layout.ApplyLayout.ZoneCount),
            "rows" => GetRowsRects(layout.ApplyLayout.ZoneCount),
            "grid" => GetGridRects(layout.ApplyLayout.ZoneCount),
            "priority-grid" => GetPriorityGridRects(layout.ApplyLayout.ZoneCount),
            "focus" => GetFocusRects(layout.ApplyLayout.ZoneCount),
            "blank" => new List<NormalizedRect>(),
            _ => GetGridRects(layout.ApplyLayout.ZoneCount),
        };
    }

    private static List<NormalizedRect> GetCustomRects(CustomLayouts.CustomLayoutWrapper custom)
    {
        var type = custom.Type?.Trim().ToLowerInvariant() ?? string.Empty;
        if (custom.Info.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return new List<NormalizedRect>();
        }

        return type switch
        {
            "grid" => GetCustomGridRects(custom.Info),
            "canvas" => GetCustomCanvasRects(custom.Info),
            _ => new List<NormalizedRect>(),
        };
    }

    private static List<NormalizedRect> GetCustomCanvasRects(JsonElement info)
    {
        if (!info.TryGetProperty("ref-width", out var refWidthProp) ||
            !info.TryGetProperty("ref-height", out var refHeightProp) ||
            !info.TryGetProperty("zones", out var zonesProp))
        {
            return new List<NormalizedRect>();
        }

        if (refWidthProp.ValueKind != JsonValueKind.Number || refHeightProp.ValueKind != JsonValueKind.Number || zonesProp.ValueKind != JsonValueKind.Array)
        {
            return new List<NormalizedRect>();
        }

        var refWidth = Math.Max(1, refWidthProp.GetInt32());
        var refHeight = Math.Max(1, refHeightProp.GetInt32());
        var rects = new List<NormalizedRect>(zonesProp.GetArrayLength());

        foreach (var zone in zonesProp.EnumerateArray())
        {
            if (zone.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!zone.TryGetProperty("X", out var xProp) ||
                !zone.TryGetProperty("Y", out var yProp) ||
                !zone.TryGetProperty("width", out var wProp) ||
                !zone.TryGetProperty("height", out var hProp))
            {
                continue;
            }

            if (xProp.ValueKind != JsonValueKind.Number ||
                yProp.ValueKind != JsonValueKind.Number ||
                wProp.ValueKind != JsonValueKind.Number ||
                hProp.ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            var x = xProp.GetSingle() / refWidth;
            var y = yProp.GetSingle() / refHeight;
            var w = wProp.GetSingle() / refWidth;
            var h = hProp.GetSingle() / refHeight;
            rects.Add(NormalizeRect(x, y, w, h));
        }

        return rects;
    }

    private static List<NormalizedRect> GetCustomGridRects(JsonElement info)
    {
        if (!TryGetGridDefinition(info, out var rows, out var cols, out var rowsPercents, out var colsPercents, out var cellMap))
        {
            return new List<NormalizedRect>();
        }

        return BuildRectsFromGridDefinition(rows, cols, rowsPercents, colsPercents, cellMap);
    }

    private static bool TryGetGridDefinition(
        JsonElement info,
        out int rows,
        out int cols,
        out int[] rowPercents,
        out int[] colPercents,
        out int[][] cellChildMap)
    {
        rows = 0;
        cols = 0;
        rowPercents = Array.Empty<int>();
        colPercents = Array.Empty<int>();
        cellChildMap = Array.Empty<int[]>();

        if (!info.TryGetProperty("rows", out var rowsProp) ||
            !info.TryGetProperty("columns", out var colsProp) ||
            !info.TryGetProperty("rows-percentage", out var rowsPercentsProp) ||
            !info.TryGetProperty("columns-percentage", out var colsPercentsProp) ||
            !info.TryGetProperty("cell-child-map", out var cellMapProp))
        {
            return false;
        }

        if (rowsProp.ValueKind != JsonValueKind.Number ||
            colsProp.ValueKind != JsonValueKind.Number ||
            rowsPercentsProp.ValueKind != JsonValueKind.Array ||
            colsPercentsProp.ValueKind != JsonValueKind.Array ||
            cellMapProp.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        rows = rowsProp.GetInt32();
        cols = colsProp.GetInt32();
        if (rows <= 0 || cols <= 0)
        {
            return false;
        }

        rowPercents = rowsPercentsProp.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.Number).Select(v => v.GetInt32()).ToArray();
        colPercents = colsPercentsProp.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.Number).Select(v => v.GetInt32()).ToArray();

        if (rowPercents.Length != rows || colPercents.Length != cols)
        {
            return false;
        }

        var mapRows = new List<int[]>(rows);
        foreach (var row in cellMapProp.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var cells = row.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.Number).Select(v => v.GetInt32()).ToArray();
            if (cells.Length != cols)
            {
                return false;
            }

            mapRows.Add(cells);
        }

        if (mapRows.Count != rows)
        {
            return false;
        }

        cellChildMap = mapRows.ToArray();
        return true;
    }

    private static List<NormalizedRect> GetColumnsRects(int zoneCount)
    {
        zoneCount = Math.Clamp(zoneCount, 1, 16);
        var rects = new List<NormalizedRect>(zoneCount);
        for (var i = 0; i < zoneCount; i++)
        {
            rects.Add(new NormalizedRect(i / (float)zoneCount, 0, 1f / zoneCount, 1f));
        }

        return rects;
    }

    private static List<NormalizedRect> GetRowsRects(int zoneCount)
    {
        zoneCount = Math.Clamp(zoneCount, 1, 16);
        var rects = new List<NormalizedRect>(zoneCount);
        for (var i = 0; i < zoneCount; i++)
        {
            rects.Add(new NormalizedRect(0, i / (float)zoneCount, 1f, 1f / zoneCount));
        }

        return rects;
    }

    private static List<NormalizedRect> GetGridRects(int zoneCount)
    {
        zoneCount = Math.Clamp(zoneCount, 1, 25);
        var rows = 1;
        while (zoneCount / rows >= rows)
        {
            rows++;
        }

        rows--;
        var cols = zoneCount / rows;
        if (zoneCount % rows != 0)
        {
            cols++;
        }

        var rowPercents = Enumerable.Repeat(10000 / rows, rows).ToArray();
        var colPercents = Enumerable.Repeat(10000 / cols, cols).ToArray();
        var cellMap = new int[rows][];

        var index = 0;
        for (var r = 0; r < rows; r++)
        {
            cellMap[r] = new int[cols];
            for (var c = 0; c < cols; c++)
            {
                cellMap[r][c] = index;
                index++;
                if (index == zoneCount)
                {
                    index--;
                }
            }
        }

        return BuildRectsFromGridDefinition(rows, cols, rowPercents, colPercents, cellMap);
    }

    private static List<NormalizedRect> GetPriorityGridRects(int zoneCount)
    {
        zoneCount = Math.Clamp(zoneCount, 1, 25);

        if (zoneCount is >= 1 and <= 11 && PriorityGrid.TryGetValue(zoneCount, out var def))
        {
            return BuildRectsFromGridDefinition(def.Rows, def.Cols, def.RowPercents, def.ColPercents, def.CellMap);
        }

        return GetGridRects(zoneCount);
    }

    private static List<NormalizedRect> GetFocusRects(int zoneCount)
    {
        zoneCount = Math.Clamp(zoneCount, 1, 8);
        var rects = new List<NormalizedRect>(zoneCount);
        for (var i = 0; i < zoneCount; i++)
        {
            var offset = i * 0.06f;
            rects.Add(new NormalizedRect(0.1f + offset, 0.1f + offset, 0.8f, 0.8f));
        }

        return rects;
    }

    private static List<NormalizedRect> BuildRectsFromGridDefinition(int rows, int cols, int[] rowPercents, int[] colPercents, int[][] cellChildMap)
    {
        const float multiplier = 10000f;

        var rowPrefix = new float[rows + 1];
        var colPrefix = new float[cols + 1];

        for (var r = 0; r < rows; r++)
        {
            rowPrefix[r + 1] = rowPrefix[r] + (rowPercents[r] / multiplier);
        }

        for (var c = 0; c < cols; c++)
        {
            colPrefix[c + 1] = colPrefix[c] + (colPercents[c] / multiplier);
        }

        var maxZone = -1;
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                maxZone = Math.Max(maxZone, cellChildMap[r][c]);
            }
        }

        var rects = new List<NormalizedRect>(maxZone + 1);
        for (var i = 0; i <= maxZone; i++)
        {
            rects.Add(new NormalizedRect(1, 1, 0, 0));
        }

        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var zoneId = cellChildMap[r][c];
                if (zoneId < 0 || zoneId >= rects.Count)
                {
                    continue;
                }

                var x1 = colPrefix[c];
                var y1 = rowPrefix[r];
                var x2 = colPrefix[c + 1];
                var y2 = rowPrefix[r + 1];

                var existing = rects[zoneId];
                if (existing.Width <= 0 || existing.Height <= 0)
                {
                    rects[zoneId] = new NormalizedRect(x1, y1, x2 - x1, y2 - y1);
                }
                else
                {
                    var ex2 = existing.X + existing.Width;
                    var ey2 = existing.Y + existing.Height;
                    var nx1 = Math.Min(existing.X, x1);
                    var ny1 = Math.Min(existing.Y, y1);
                    var nx2 = Math.Max(ex2, x2);
                    var ny2 = Math.Max(ey2, y2);
                    rects[zoneId] = new NormalizedRect(nx1, ny1, nx2 - nx1, ny2 - ny1);
                }
            }
        }

        return rects
            .Where(r => r.Width > 0 && r.Height > 0)
            .Select(r => NormalizeRect(r.X, r.Y, r.Width, r.Height))
            .ToList();
    }

    private static NormalizedRect NormalizeRect(float x, float y, float w, float h)
    {
        x = Math.Clamp(x, 0, 1);
        y = Math.Clamp(y, 0, 1);
        w = Math.Clamp(w, 0, 1 - x);
        h = Math.Clamp(h, 0, 1 - y);
        return new NormalizedRect(x, y, w, h);
    }

    private static byte[] RenderBgra(IReadOnlyList<NormalizedRect> rects, int sizePx, int spacing)
    {
        var pixels = new byte[sizePx * sizePx * 4];

        var border = Premultiply(new BgraColor(0x30, 0x30, 0x30, 0xFF));
        var frame = Premultiply(new BgraColor(0x40, 0x40, 0x40, 0xA0));
        var fill = Premultiply(new BgraColor(0xFF, 0xD8, 0x8C, 0xC0)); // light-ish blue with alpha
        var background = Premultiply(new BgraColor(0x00, 0x00, 0x00, 0x00));

        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i + 0] = background.B;
            pixels[i + 1] = background.G;
            pixels[i + 2] = background.R;
            pixels[i + 3] = background.A;
        }

        DrawRectBorder(pixels, sizePx, 1, 1, sizePx - 1, sizePx - 1, frame);

        var gapPx = spacing > 0 ? Math.Clamp(spacing / 8, 1, 3) : 0;
        foreach (var rect in rects)
        {
            var (x1, y1, x2, y2) = ToPixelBounds(rect, sizePx, gapPx);
            if (x2 <= x1 || y2 <= y1)
            {
                continue;
            }

            FillRect(pixels, sizePx, x1, y1, x2, y2, fill);
            DrawRectBorder(pixels, sizePx, x1, y1, x2, y2, border);
        }

        return pixels;
    }

    private static (int X1, int Y1, int X2, int Y2) ToPixelBounds(NormalizedRect rect, int sizePx, int gapPx)
    {
        var x1 = (int)MathF.Round(rect.X * sizePx);
        var y1 = (int)MathF.Round(rect.Y * sizePx);
        var x2 = (int)MathF.Round((rect.X + rect.Width) * sizePx);
        var y2 = (int)MathF.Round((rect.Y + rect.Height) * sizePx);

        x1 = Math.Clamp(x1 + gapPx, 0, sizePx - 1);
        y1 = Math.Clamp(y1 + gapPx, 0, sizePx - 1);
        x2 = Math.Clamp(x2 - gapPx, 1, sizePx);
        y2 = Math.Clamp(y2 - gapPx, 1, sizePx);

        if (x2 <= x1 + 1)
        {
            x2 = Math.Min(sizePx, x1 + 2);
        }

        if (y2 <= y1 + 1)
        {
            y2 = Math.Min(sizePx, y1 + 2);
        }

        return (x1, y1, x2, y2);
    }

    private static void FillRect(byte[] pixels, int sizePx, int x1, int y1, int x2, int y2, BgraColor color)
    {
        for (var y = y1; y < y2; y++)
        {
            var rowStart = y * sizePx * 4;
            for (var x = x1; x < x2; x++)
            {
                var i = rowStart + (x * 4);
                pixels[i + 0] = color.B;
                pixels[i + 1] = color.G;
                pixels[i + 2] = color.R;
                pixels[i + 3] = color.A;
            }
        }
    }

    private static void DrawRectBorder(byte[] pixels, int sizePx, int x1, int y1, int x2, int y2, BgraColor color)
    {
        var left = x1;
        var right = x2 - 1;
        var top = y1;
        var bottom = y2 - 1;

        for (var x = left; x <= right; x++)
        {
            SetPixel(pixels, sizePx, x, top, color);
            SetPixel(pixels, sizePx, x, bottom, color);
        }

        for (var y = top; y <= bottom; y++)
        {
            SetPixel(pixels, sizePx, left, y, color);
            SetPixel(pixels, sizePx, right, y, color);
        }
    }

    private static void SetPixel(byte[] pixels, int sizePx, int x, int y, BgraColor color)
    {
        if ((uint)x >= (uint)sizePx || (uint)y >= (uint)sizePx)
        {
            return;
        }

        var i = ((y * sizePx) + x) * 4;
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

    private sealed record PriorityGridDefinition(int Rows, int Cols, int[] RowPercents, int[] ColPercents, int[][] CellMap);

    private static readonly IReadOnlyDictionary<int, PriorityGridDefinition> PriorityGrid = new Dictionary<int, PriorityGridDefinition>
    {
        [1] = new PriorityGridDefinition(1, 1, [10000], [10000], [[0]]),
        [2] = new PriorityGridDefinition(1, 2, [10000], [6667, 3333], [[0, 1]]),
        [3] = new PriorityGridDefinition(1, 3, [10000], [2500, 5000, 2500], [[0, 1, 2]]),
        [4] = new PriorityGridDefinition(2, 3, [5000, 5000], [2500, 5000, 2500], [[0, 1, 2], [0, 1, 3]]),
        [5] = new PriorityGridDefinition(2, 3, [5000, 5000], [2500, 5000, 2500], [[0, 1, 2], [3, 1, 4]]),
        [6] = new PriorityGridDefinition(3, 3, [3333, 3334, 3333], [2500, 5000, 2500], [[0, 1, 2], [0, 1, 3], [4, 1, 5]]),
        [7] = new PriorityGridDefinition(3, 3, [3333, 3334, 3333], [2500, 5000, 2500], [[0, 1, 2], [3, 1, 4], [5, 1, 6]]),
        [8] = new PriorityGridDefinition(3, 4, [3333, 3334, 3333], [2500, 2500, 2500, 2500], [[0, 1, 2, 3], [4, 1, 2, 5], [6, 1, 2, 7]]),
        [9] = new PriorityGridDefinition(3, 4, [3333, 3334, 3333], [2500, 2500, 2500, 2500], [[0, 1, 2, 3], [4, 1, 2, 5], [6, 1, 7, 8]]),
        [10] = new PriorityGridDefinition(3, 4, [3333, 3334, 3333], [2500, 2500, 2500, 2500], [[0, 1, 2, 3], [4, 1, 5, 6], [7, 1, 8, 9]]),
        [11] = new PriorityGridDefinition(3, 4, [3333, 3334, 3333], [2500, 2500, 2500, 2500], [[0, 1, 2, 3], [4, 1, 5, 6], [7, 8, 9, 10]]),
    };
}
