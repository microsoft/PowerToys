// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;
using Microsoft.CmdPal.Ext.PowerToys.Commands;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;
using Color = Microsoft.CommandPalette.Extensions.Color;

namespace Microsoft.CmdPal.Ext.PowerToys.Helper;

internal sealed class ColorPickerHelper
{
    private const string ColorPickerHistoryFilename = "colorHistory.json";
    private const string ColorPickerModuleName = "ColorPicker";

    internal static List<string> LoadSavedColors()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var historyFile = Path.Combine(localAppData, "Microsoft", "PowerToys", ColorPickerModuleName, ColorPickerHistoryFilename);
        if (!File.Exists(historyFile))
        {
            return [];
        }

        var jsonSettingsString = System.IO.File.ReadAllText(historyFile).Trim('\0');
        return JsonSerializer.Deserialize<List<string>>(jsonSettingsString, PowerToysJsonContext.Default.ListString) ?? [];
    }

    public static async Task<IconInfo> CreateColorCircleIcon(System.Drawing.Color color)
    {
        const int iconSize = 32;

        using var bitmap = new Bitmap(iconSize, iconSize, PixelFormat.Format32bppArgb);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            graphics.Clear(System.Drawing.Color.Transparent);

            using var brush = new SolidBrush(color);
            graphics.FillEllipse(brush, 2, 2, iconSize - 4, iconSize - 4);

            using var pen = new Pen(System.Drawing.Color.FromArgb(128, 0, 0, 0), 1);
            graphics.DrawEllipse(pen, 2, 2, iconSize - 4, iconSize - 4);
        }

        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, ImageFormat.Png);
        memoryStream.Position = 0;

        var randomAccessStream = new InMemoryRandomAccessStream();
        using var outputStream = randomAccessStream.GetOutputStreamAt(0);
        using var dataWriter = new DataWriter(outputStream);

        dataWriter.WriteBytes(memoryStream.ToArray());
        await dataWriter.StoreAsync();
        await dataWriter.FlushAsync();

        return IconInfo.FromStream(randomAccessStream);
    }

    internal static IListItem[] GetColorItems(string searchText)
    {
        var colorItems = new List<ListItem>();
        foreach (var colorItem in LoadSavedColors())
        {
            var parts = colorItem.Split('|');
            var color = new Color()
            {
                A = byte.Parse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture),
                R = byte.Parse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture),
                G = byte.Parse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture),
                B = byte.Parse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture),
            };

            var item = new ListItem(new CopyColorCommand(color))
            {
                Icon = CreateColorCircleIcon(System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B)).GetAwaiter().GetResult(),
                Title = $"#{color.R:X2}{color.G:X2}{color.B:X2}",
                Subtitle = "Copy color",
            };

            if (item.Title.Contains(searchText))
            {
                colorItems.Add(item);
            }
        }

        return [.. colorItems];
    }
}
