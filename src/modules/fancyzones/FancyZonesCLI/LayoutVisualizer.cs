// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace FancyZonesCLI;

public static class LayoutVisualizer
{
    public static void DrawTemplateLayout(TemplateLayout template)
    {
        Console.WriteLine("    Visual Preview:");

        switch (template.Type.ToLowerInvariant())
        {
            case "focus":
                DrawFocusLayout(template.ZoneCount > 0 ? template.ZoneCount : 3);
                break;
            case "columns":
                DrawGridLayout(1, template.ZoneCount > 0 ? template.ZoneCount : 3);
                break;
            case "rows":
                DrawGridLayout(template.ZoneCount > 0 ? template.ZoneCount : 3, 1);
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

                DrawGridLayoutWithZoneCount(rows, cols, zoneCount);
                break;
            case "priority-grid":
                DrawPriorityGridLayout(template.ZoneCount > 0 ? template.ZoneCount : 3);
                break;
            case "blank":
                Console.WriteLine("    (No zones)");
                break;
            default:
                Console.WriteLine($"    ({template.Type} layout)");
                break;
        }
    }

    public static void DrawCustomLayout(CustomLayout layout)
    {
        if (layout.Info.ValueKind == JsonValueKind.Undefined || layout.Info.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        Console.WriteLine("    Visual Preview:");

        if (layout.Type == "grid" &&
            layout.Info.TryGetProperty("rows", out var rows) &&
            layout.Info.TryGetProperty("columns", out var cols))
        {
            int r = rows.GetInt32();
            int c = cols.GetInt32();

            // Check if there's a cell-child-map (merged cells)
            if (layout.Info.TryGetProperty("cell-child-map", out var cellMap))
            {
                DrawGridLayoutWithMergedCells(r, c, cellMap);
            }
            else
            {
                int height = r >= 4 ? 12 : 8;
                DrawGridLayout(r, c, 30, height);
            }
        }
        else if (layout.Type == "canvas" &&
                 layout.Info.TryGetProperty("zones", out var zones) &&
                 layout.Info.TryGetProperty("ref-width", out var refWidth) &&
                 layout.Info.TryGetProperty("ref-height", out var refHeight))
        {
            DrawCanvasLayout(zones, refWidth.GetInt32(), refHeight.GetInt32());
        }
    }

    private static void DrawFocusLayout(int zoneCount = 3)
    {
        // Focus layout: overlapping zones with cascading offset
        // Show first 2, ellipsis, and last 1 if more than 4 zones
        if (zoneCount == 1)
        {
            Console.WriteLine("    +-------+");
            Console.WriteLine("    |       |");
            Console.WriteLine("    |       |");
            Console.WriteLine("    +-------+");
        }
        else if (zoneCount == 2)
        {
            Console.WriteLine("    +-------+");
            Console.WriteLine("    |       |");
            Console.WriteLine("    | +-------+");
            Console.WriteLine("    +-|       |");
            Console.WriteLine("      |       |");
            Console.WriteLine("      +-------+");
        }
        else
        {
            Console.WriteLine("    +-------+");
            Console.WriteLine("    |       |");
            Console.WriteLine("    | +-------+");
            Console.WriteLine("    +-|       |");
            Console.WriteLine("      | +-------+");
            Console.WriteLine("      +-|       |");

            // Middle ellipsis
            Console.WriteLine("        ...");
            Console.WriteLine($"        (total: {zoneCount} zones)");
            Console.WriteLine("        ...");

            // Show indication of last zone (without full indent)
            Console.WriteLine("        | +-------+");
            Console.WriteLine("        +-|       |");
            Console.WriteLine("          |       |");
            Console.WriteLine("          +-------+");
        }
    }

    private static void DrawPriorityGridLayout(int zoneCount = 3)
    {
        // Priority Grid has predefined layouts for zone counts 1-11
        // Data format from GridLayoutModel._priorityData
        if (zoneCount >= 1 && zoneCount <= 11)
        {
            int[,] cellMap = GetPriorityGridCellMap(zoneCount);
            DrawGridLayoutWithCellMap(cellMap);
        }
        else
        {
            // > 11 zones: fallback to grid layout
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

            DrawGridLayoutWithZoneCount(rows, cols, zoneCount);
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

    private static void DrawGridLayoutWithCellMap(int[,] cellMap, int width = 30, int height = 8)
    {
        int rows = cellMap.GetLength(0);
        int cols = cellMap.GetLength(1);

        int cellWidth = width / cols;
        int cellHeight = height / rows;

        for (int r = 0; r < rows; r++)
        {
            // Top border
            Console.Write("    +");
            for (int c = 0; c < cols; c++)
            {
                // Check if this cell should merge with the cell above
                bool mergeTop = r > 0 && cellMap[r, c] == cellMap[r - 1, c];

                // Check if this cell should merge with the cell to the left
                bool mergeLeft = c > 0 && cellMap[r, c] == cellMap[r, c - 1];

                if (mergeTop)
                {
                    if (mergeLeft)
                    {
                        Console.Write(new string(' ', cellWidth));
                    }
                    else
                    {
                        Console.Write(new string(' ', cellWidth - 1));
                        Console.Write("+");
                    }
                }
                else
                {
                    if (mergeLeft)
                    {
                        Console.Write(new string('-', cellWidth));
                    }
                    else
                    {
                        Console.Write(new string('-', cellWidth - 1));
                        Console.Write("+");
                    }
                }
            }

            Console.WriteLine();

            // Cell content
            for (int h = 0; h < cellHeight - 1; h++)
            {
                Console.Write("    ");
                for (int c = 0; c < cols; c++)
                {
                    bool mergeLeft = c > 0 && cellMap[r, c] == cellMap[r, c - 1];
                    if (mergeLeft)
                    {
                        Console.Write(" ");
                    }
                    else
                    {
                        Console.Write("|");
                    }

                    Console.Write(new string(' ', cellWidth - 1));
                }

                Console.WriteLine("|");
            }
        }

        // Bottom border
        Console.Write("    +");
        for (int c = 0; c < cols; c++)
        {
            Console.Write(new string('-', cellWidth - 1));
            Console.Write("+");
        }

        Console.WriteLine();
    }

    private static void DrawGridLayoutWithMergedCells(int rows, int cols, JsonElement cellMap)
    {
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

        // Find unique zones and their count
        var zones = new HashSet<int>();
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                zones.Add(zoneMap[r, c]);
            }
        }

        int cellHeight = displayHeight / rows;
        int cellWidth = displayWidth / cols;

        // Draw top border
        Console.Write("    +");
        Console.Write(new string('-', displayWidth));
        Console.WriteLine("+");

        // Draw rows
        for (int r = 0; r < rows; r++)
        {
            // For each row, find the column range of each zone
            var zoneRanges = new Dictionary<int, (int Start, int End)>();
            for (int c = 0; c < cols; c++)
            {
                int zone = zoneMap[r, c];
                if (zoneRanges.TryGetValue(zone, out var range))
                {
                    zoneRanges[zone] = (range.Start, c);
                }
                else
                {
                    zoneRanges[zone] = (c, c);
                }
            }

            for (int h = 0; h < cellHeight; h++)
            {
                Console.Write("    |");

                for (int c = 0; c < cols; c++)
                {
                    int currentZone = zoneMap[r, c];
                    int leftZone = c > 0 ? zoneMap[r, c - 1] : -1;
                    bool needLeftBorder = c > 0 && currentZone != leftZone;

                    // Check if this zone has a top border
                    bool zoneHasTopBorder = false;
                    if (r > 0 && h == 0)
                    {
                        int topZone = zoneMap[r - 1, c];
                        zoneHasTopBorder = currentZone != topZone;
                    }

                    // Draw left border if needed
                    if (needLeftBorder)
                    {
                        Console.Write("|");

                        // Fill rest of cell
                        for (int w = 1; w < cellWidth; w++)
                        {
                            Console.Write(zoneHasTopBorder ? "-" : " ");
                        }
                    }
                    else
                    {
                        // No left border, fill entire cell
                        for (int w = 0; w < cellWidth; w++)
                        {
                            Console.Write(zoneHasTopBorder ? "-" : " ");
                        }
                    }
                }

                Console.WriteLine("|");
            }
        }

        // Draw bottom border
        Console.Write("    +");
        Console.Write(new string('-', displayWidth));
        Console.WriteLine("+");
    }

    public static void DrawGridLayoutWithZoneCount(int rows, int cols, int zoneCount, int width = 30, int height = 8)
    {
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
            Console.Write("    +");
            for (int c = 0; c < cols; c++)
            {
                // Check if this cell should merge with the previous one (same zone)
                bool mergeLeft = c > 0 && zoneMap[r, c] == zoneMap[r, c - 1];
                if (mergeLeft)
                {
                    Console.Write(new string('-', cellWidth));
                }
                else
                {
                    Console.Write(new string('-', cellWidth - 1));
                    Console.Write("+");
                }
            }

            Console.WriteLine();

            // Cell content
            for (int h = 0; h < cellHeight - 1; h++)
            {
                Console.Write("    ");
                for (int c = 0; c < cols; c++)
                {
                    // Check if this cell should merge with the previous one
                    bool mergeLeft = c > 0 && zoneMap[r, c] == zoneMap[r, c - 1];
                    if (mergeLeft)
                    {
                        Console.Write(" ");
                    }
                    else
                    {
                        Console.Write("|");
                    }

                    Console.Write(new string(' ', cellWidth - 1));
                }

                Console.WriteLine("|");
            }
        }

        // Bottom border
        Console.Write("    +");
        for (int c = 0; c < cols; c++)
        {
            Console.Write(new string('-', cellWidth - 1));
            Console.Write("+");
        }

        Console.WriteLine();
    }

    public static void DrawGridLayout(int rows, int cols, int width = 30, int height = 8)
    {
        int cellWidth = width / cols;
        int cellHeight = height / rows;

        for (int r = 0; r < rows; r++)
        {
            // Top border
            Console.Write("    +");
            for (int c = 0; c < cols; c++)
            {
                Console.Write(new string('-', cellWidth - 1));
                Console.Write("+");
            }

            Console.WriteLine();

            // Cell content
            for (int h = 0; h < cellHeight - 1; h++)
            {
                Console.Write("    ");
                for (int c = 0; c < cols; c++)
                {
                    Console.Write("|");
                    Console.Write(new string(' ', cellWidth - 1));
                }

                Console.WriteLine("|");
            }
        }

        // Bottom border
        Console.Write("    +");
        for (int c = 0; c < cols; c++)
        {
            Console.Write(new string('-', cellWidth - 1));
            Console.Write("+");
        }

        Console.WriteLine();
    }

    private static void DrawCanvasLayout(JsonElement zones, int refWidth, int refHeight)
    {
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

            // Clamp to display bounds
            if (dx + dw > displayWidth)
            {
                dw = displayWidth - dx;
            }

            if (dy + dh > displayHeight)
            {
                dh = displayHeight - dy;
            }

            zoneList.Add((dx, dy, dw, dh, zoneId));

            // Fill the grid for this zone
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
        Console.Write("    +");
        Console.Write(new string('-', displayWidth));
        Console.WriteLine("+");

        // Draw each row
        for (int r = 0; r < displayHeight; r++)
        {
            Console.Write("    |");
            for (int c = 0; c < displayWidth; c++)
            {
                var zonesHere = zoneGrid[r, c];

                if (zonesHere.Count == 0)
                {
                    Console.Write(" ");
                }
                else
                {
                    // Get the topmost zone at this position
                    int topZone = zonesHere[zonesHere.Count - 1];
                    var rect = zoneList[topZone];

                    int x = rect.X;
                    int y = rect.Y;
                    int w = rect.Width;
                    int h = rect.Height;

                    bool isTopEdge = r == y;
                    bool isBottomEdge = r == y + h - 1;
                    bool isLeftEdge = c == x;
                    bool isRightEdge = c == x + w - 1;

                    // Draw borders
                    if ((isTopEdge || isBottomEdge) && (isLeftEdge || isRightEdge))
                    {
                        Console.Write("+");
                    }
                    else if (isTopEdge || isBottomEdge)
                    {
                        Console.Write("-");
                    }
                    else if (isLeftEdge || isRightEdge)
                    {
                        Console.Write("|");
                    }
                    else
                    {
                        // Use shading to show different zones
                        char[] shades = { '.', ':', '░', '▒', '▓', '█', '◆', '●', '■', '▪' };
                        Console.Write(shades[topZone % shades.Length]);
                    }
                }
            }

            Console.WriteLine("|");
        }

        // Draw bottom border
        Console.Write("    +");
        Console.Write(new string('-', displayWidth));
        Console.WriteLine("+");

        // Draw legend
        Console.WriteLine();
        Console.Write("    Legend: ");
        char[] legendShades = { '.', ':', '░', '▒', '▓', '█', '◆', '●', '■', '▪' };
        for (int i = 0; i < Math.Min(zoneId, legendShades.Length); i++)
        {
            if (i > 0)
            {
                Console.Write(", ");
            }

            Console.Write($"Zone {i} = {legendShades[i]}");
        }

        Console.WriteLine();
    }
}
