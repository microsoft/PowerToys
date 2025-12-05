// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FancyZonesCLI;

public static class LayoutVisualizer
{
    public static string DrawTemplateLayout(TemplateLayout template)
    {
        var sb = new StringBuilder();
        sb.AppendLine("    Visual Preview:");

        switch (template.Type.ToLowerInvariant())
        {
            case "focus":
                sb.Append(RenderFocusLayout(template.ZoneCount > 0 ? template.ZoneCount : 3));
                break;
            case "columns":
                sb.Append(RenderGridLayout(1, template.ZoneCount > 0 ? template.ZoneCount : 3));
                break;
            case "rows":
                sb.Append(RenderGridLayout(template.ZoneCount > 0 ? template.ZoneCount : 3, 1));
                break;
            case "grid":
                // Grid layout: calculate rows and columns from zone count
                // Algorithm from GridLayoutModel.InitGrid() - tries to make it close to square
                // with cols >= rows preference
                int zoneCount = template.ZoneCount > 0 ? template.ZoneCount : 3;
                int rows = 1;
                while (zoneCount / rows >= rows)
                {
                    rows++;
                }

                rows--;
                int cols = zoneCount / rows;
                if (zoneCount % rows != 0)
                {
                    cols++;
                }

                sb.Append(RenderGridLayoutWithZoneCount(rows, cols, zoneCount));
                break;
            case "priority-grid":
                sb.Append(RenderPriorityGridLayout(template.ZoneCount > 0 ? template.ZoneCount : 3));
                break;
            case "blank":
                sb.AppendLine("    (No zones)");
                break;
            default:
                sb.AppendLine(CultureInfo.InvariantCulture, $"    ({template.Type} layout)");
                break;
        }

        return sb.ToString();
    }

    public static string DrawCustomLayout(CustomLayout layout)
    {
        if (layout.Info.ValueKind == JsonValueKind.Undefined || layout.Info.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("    Visual Preview:");

        if (layout.Type == "grid" &&
            layout.Info.TryGetProperty("rows", out var rows) &&
            layout.Info.TryGetProperty("columns", out var cols))
        {
            int r = rows.GetInt32();
            int c = cols.GetInt32();

            // Check if there's a cell-child-map (merged cells)
            if (layout.Info.TryGetProperty("cell-child-map", out var cellMap))
            {
                sb.Append(RenderGridLayoutWithMergedCells(r, c, cellMap));
            }
            else
            {
                int height = r >= 4 ? 12 : 8;
                sb.Append(RenderGridLayout(r, c, 30, height));
            }
        }
        else if (layout.Type == "canvas" &&
                 layout.Info.TryGetProperty("zones", out var zones) &&
                 layout.Info.TryGetProperty("ref-width", out var refWidth) &&
                 layout.Info.TryGetProperty("ref-height", out var refHeight))
        {
            sb.Append(RenderCanvasLayout(zones, refWidth.GetInt32(), refHeight.GetInt32()));
        }

        return sb.ToString();
    }

    private static string RenderFocusLayout(int zoneCount = 3)
    {
        var sb = new StringBuilder();

        // Focus layout: overlapping zones with cascading offset
        if (zoneCount == 1)
        {
            sb.AppendLine("    +-------+");
            sb.AppendLine("    |       |");
            sb.AppendLine("    |       |");
            sb.AppendLine("    +-------+");
        }
        else if (zoneCount == 2)
        {
            sb.AppendLine("    +-------+");
            sb.AppendLine("    |       |");
            sb.AppendLine("    | +-------+");
            sb.AppendLine("    +-|       |");
            sb.AppendLine("      |       |");
            sb.AppendLine("      +-------+");
        }
        else
        {
            sb.AppendLine("    +-------+");
            sb.AppendLine("    |       |");
            sb.AppendLine("    | +-------+");
            sb.AppendLine("    +-|       |");
            sb.AppendLine("      | +-------+");
            sb.AppendLine("      +-|       |");
            sb.AppendLine("        ...");
            sb.AppendLine(CultureInfo.InvariantCulture, $"        (total: {zoneCount} zones)");
            sb.AppendLine("        ...");
            sb.AppendLine("        | +-------+");
            sb.AppendLine("        +-|       |");
            sb.AppendLine("          |       |");
            sb.AppendLine("          +-------+");
        }

        return sb.ToString();
    }

    private static string RenderPriorityGridLayout(int zoneCount = 3)
    {
        // Priority Grid has predefined layouts for zone counts 1-11
        // Data format from GridLayoutModel._priorityData
        if (zoneCount >= 1 && zoneCount <= 11)
        {
            int[,] cellMap = GetPriorityGridCellMap(zoneCount);
            return RenderGridLayoutWithCellMap(cellMap);
        }
        else
        {
            // > 11 zones: use grid layout
            int rows = 1;
            while (zoneCount / rows >= rows)
            {
                rows++;
            }

            rows--;
            int cols = zoneCount / rows;
            if (zoneCount % rows != 0)
            {
                cols++;
            }

            return RenderGridLayoutWithZoneCount(rows, cols, zoneCount);
        }
    }

    private static int[,] GetPriorityGridCellMap(int zoneCount)
    {
        // Parsed from Editor's _priorityData byte arrays
        return zoneCount switch
        {
            1 => new int[,] { { 0 } },
            2 => new int[,] { { 0, 1 } },
            3 => new int[,] { { 0, 1, 2 } },
            4 => new int[,] { { 0, 1, 2 }, { 0, 1, 3 } },
            5 => new int[,] { { 0, 1, 2 }, { 3, 1, 4 } },
            6 => new int[,] { { 0, 1, 2 }, { 0, 1, 3 }, { 4, 1, 5 } },
            7 => new int[,] { { 0, 1, 2 }, { 3, 1, 4 }, { 5, 1, 6 } },
            8 => new int[,] { { 0, 1, 2, 3 }, { 4, 1, 2, 5 }, { 6, 1, 2, 7 } },
            9 => new int[,] { { 0, 1, 2, 3 }, { 4, 1, 2, 5 }, { 6, 1, 7, 8 } },
            10 => new int[,] { { 0, 1, 2, 3 }, { 4, 1, 5, 6 }, { 7, 1, 8, 9 } },
            11 => new int[,] { { 0, 1, 2, 3 }, { 4, 1, 5, 6 }, { 7, 8, 9, 10 } },
            _ => new int[,] { { 0 } },
        };
    }

    private static string RenderGridLayoutWithCellMap(int[,] cellMap, int width = 30, int height = 8)
    {
        var sb = new StringBuilder();
        int rows = cellMap.GetLength(0);
        int cols = cellMap.GetLength(1);

        int cellWidth = width / cols;
        int cellHeight = height / rows;

        for (int r = 0; r < rows; r++)
        {
            // Top border
            sb.Append("    +");
            for (int c = 0; c < cols; c++)
            {
                bool mergeTop = r > 0 && cellMap[r, c] == cellMap[r - 1, c];
                bool mergeLeft = c > 0 && cellMap[r, c] == cellMap[r, c - 1];

                if (mergeTop)
                {
                    sb.Append(mergeLeft ? new string(' ', cellWidth) : new string(' ', cellWidth - 1) + "+");
                }
                else
                {
                    sb.Append(mergeLeft ? new string('-', cellWidth) : new string('-', cellWidth - 1) + "+");
                }
            }

            sb.AppendLine();

            // Cell content
            for (int h = 0; h < cellHeight - 1; h++)
            {
                sb.Append("    ");
                for (int c = 0; c < cols; c++)
                {
                    bool mergeLeft = c > 0 && cellMap[r, c] == cellMap[r, c - 1];
                    sb.Append(mergeLeft ? ' ' : '|');
                    sb.Append(' ', cellWidth - 1);
                }

                sb.AppendLine("|");
            }
        }

        // Bottom border
        sb.Append("    +");
        for (int c = 0; c < cols; c++)
        {
            sb.Append('-', cellWidth - 1);
            sb.Append('+');
        }

        sb.AppendLine();
        return sb.ToString();
    }

    private static string RenderGridLayoutWithMergedCells(int rows, int cols, JsonElement cellMap)
    {
        var sb = new StringBuilder();
        const int displayWidth = 39;
        const int displayHeight = 12;

        // Build zone map from cell-child-map
        int[,] zoneMap = new int[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            var rowArray = cellMap[r];
            for (int c = 0; c < cols; c++)
            {
                zoneMap[r, c] = rowArray[c].GetInt32();
            }
        }

        int cellHeight = displayHeight / rows;
        int cellWidth = displayWidth / cols;

        // Draw top border
        sb.Append("    +");
        sb.Append('-', displayWidth);
        sb.AppendLine("+");

        // Draw rows
        for (int r = 0; r < rows; r++)
        {
            for (int h = 0; h < cellHeight; h++)
            {
                sb.Append("    |");

                for (int c = 0; c < cols; c++)
                {
                    int currentZone = zoneMap[r, c];
                    int leftZone = c > 0 ? zoneMap[r, c - 1] : -1;
                    bool needLeftBorder = c > 0 && currentZone != leftZone;

                    bool zoneHasTopBorder = r > 0 && h == 0 && currentZone != zoneMap[r - 1, c];

                    if (needLeftBorder)
                    {
                        sb.Append('|');
                        sb.Append(zoneHasTopBorder ? '-' : ' ', cellWidth - 1);
                    }
                    else
                    {
                        sb.Append(zoneHasTopBorder ? '-' : ' ', cellWidth);
                    }
                }

                sb.AppendLine("|");
            }
        }

        // Draw bottom border
        sb.Append("    +");
        sb.Append('-', displayWidth);
        sb.AppendLine("+");

        return sb.ToString();
    }

    public static string RenderGridLayoutWithZoneCount(int rows, int cols, int zoneCount, int width = 30, int height = 8)
    {
        var sb = new StringBuilder();

        // Build zone map like Editor's InitGrid
        int[,] zoneMap = new int[rows, cols];
        int index = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                zoneMap[r, c] = index++;
                if (index == zoneCount)
                {
                    index--; // Remaining cells use the last zone index
                }
            }
        }

        int cellWidth = width / cols;
        int cellHeight = height / rows;

        for (int r = 0; r < rows; r++)
        {
            // Top border
            sb.Append("    +");
            for (int c = 0; c < cols; c++)
            {
                bool mergeLeft = c > 0 && zoneMap[r, c] == zoneMap[r, c - 1];
                sb.Append('-', mergeLeft ? cellWidth : cellWidth - 1);
                if (!mergeLeft)
                {
                    sb.Append('+');
                }
            }

            sb.AppendLine();

            // Cell content
            for (int h = 0; h < cellHeight - 1; h++)
            {
                sb.Append("    ");
                for (int c = 0; c < cols; c++)
                {
                    bool mergeLeft = c > 0 && zoneMap[r, c] == zoneMap[r, c - 1];
                    sb.Append(mergeLeft ? ' ' : '|');
                    sb.Append(' ', cellWidth - 1);
                }

                sb.AppendLine("|");
            }
        }

        // Bottom border
        sb.Append("    +");
        for (int c = 0; c < cols; c++)
        {
            sb.Append('-', cellWidth - 1);
            sb.Append('+');
        }

        sb.AppendLine();
        return sb.ToString();
    }

    public static string RenderGridLayout(int rows, int cols, int width = 30, int height = 8)
    {
        var sb = new StringBuilder();
        int cellWidth = width / cols;
        int cellHeight = height / rows;

        for (int r = 0; r < rows; r++)
        {
            // Top border
            sb.Append("    +");
            for (int c = 0; c < cols; c++)
            {
                sb.Append('-', cellWidth - 1);
                sb.Append('+');
            }

            sb.AppendLine();

            // Cell content
            for (int h = 0; h < cellHeight - 1; h++)
            {
                sb.Append("    ");
                for (int c = 0; c < cols; c++)
                {
                    sb.Append('|');
                    sb.Append(' ', cellWidth - 1);
                }

                sb.AppendLine("|");
            }
        }

        // Bottom border
        sb.Append("    +");
        for (int c = 0; c < cols; c++)
        {
            sb.Append('-', cellWidth - 1);
            sb.Append('+');
        }

        sb.AppendLine();
        return sb.ToString();
    }

    private static string RenderCanvasLayout(JsonElement zones, int refWidth, int refHeight)
    {
        var sb = new StringBuilder();
        const int displayWidth = 49;
        const int displayHeight = 15;

        // Create a 2D array to track which zones occupy each position
        var zoneGrid = new List<int>[displayHeight, displayWidth];
        for (int i = 0; i < displayHeight; i++)
        {
            for (int j = 0; j < displayWidth; j++)
            {
                zoneGrid[i, j] = new List<int>();
            }
        }

        // Map each zone to the grid
        int zoneId = 0;
        var zoneList = new List<(int X, int Y, int Width, int Height, int Id)>();

        foreach (var zone in zones.EnumerateArray())
        {
            int x = zone.GetProperty("X").GetInt32();
            int y = zone.GetProperty("Y").GetInt32();
            int w = zone.GetProperty("width").GetInt32();
            int h = zone.GetProperty("height").GetInt32();

            int dx = Math.Max(0, Math.Min(displayWidth - 1, x * displayWidth / refWidth));
            int dy = Math.Max(0, Math.Min(displayHeight - 1, y * displayHeight / refHeight));
            int dw = Math.Max(3, w * displayWidth / refWidth);
            int dh = Math.Max(2, h * displayHeight / refHeight);

            if (dx + dw > displayWidth)
            {
                dw = displayWidth - dx;
            }

            if (dy + dh > displayHeight)
            {
                dh = displayHeight - dy;
            }

            zoneList.Add((dx, dy, dw, dh, zoneId));

            for (int r = dy; r < dy + dh && r < displayHeight; r++)
            {
                for (int c = dx; c < dx + dw && c < displayWidth; c++)
                {
                    zoneGrid[r, c].Add(zoneId);
                }
            }

            zoneId++;
        }

        // Draw top border
        sb.Append("    +");
        sb.Append('-', displayWidth);
        sb.AppendLine("+");

        // Draw each row
        char[] shades = { '.', ':', '░', '▒', '▓', '█', '◆', '●', '■', '▪' };

        for (int r = 0; r < displayHeight; r++)
        {
            sb.Append("    |");
            for (int c = 0; c < displayWidth; c++)
            {
                var zonesHere = zoneGrid[r, c];

                if (zonesHere.Count == 0)
                {
                    sb.Append(' ');
                }
                else
                {
                    int topZone = zonesHere[zonesHere.Count - 1];
                    var rect = zoneList[topZone];

                    bool isTopEdge = r == rect.Y;
                    bool isBottomEdge = r == rect.Y + rect.Height - 1;
                    bool isLeftEdge = c == rect.X;
                    bool isRightEdge = c == rect.X + rect.Width - 1;

                    if ((isTopEdge || isBottomEdge) && (isLeftEdge || isRightEdge))
                    {
                        sb.Append('+');
                    }
                    else if (isTopEdge || isBottomEdge)
                    {
                        sb.Append('-');
                    }
                    else if (isLeftEdge || isRightEdge)
                    {
                        sb.Append('|');
                    }
                    else
                    {
                        sb.Append(shades[topZone % shades.Length]);
                    }
                }
            }

            sb.AppendLine("|");
        }

        // Draw bottom border
        sb.Append("    +");
        sb.Append('-', displayWidth);
        sb.AppendLine("+");

        // Draw legend
        sb.AppendLine();
        sb.Append("    Legend: ");
        for (int i = 0; i < Math.Min(zoneId, shades.Length); i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(CultureInfo.InvariantCulture, $"Zone {i} = {shades[i]}");
        }

        sb.AppendLine();
        return sb.ToString();
    }
}
