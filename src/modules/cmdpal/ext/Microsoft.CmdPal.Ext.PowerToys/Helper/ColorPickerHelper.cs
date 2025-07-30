// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using Microsoft.CmdPal.Ext.PowerToys.Commands;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

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
                Icon = IconHelpers.FromRelativePath("Assets\\ColorPicker.png"),
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
