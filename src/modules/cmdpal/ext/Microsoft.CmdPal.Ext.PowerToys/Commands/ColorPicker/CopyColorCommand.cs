// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToysExtension.Commands;

/// <summary>
/// Copies the most recently picked color from the PowerToys Color Picker history if available.
/// </summary>
internal sealed partial class CopyColorCommand : InvokableCommand
{
    public CopyColorCommand()
    {
        Name = "Copy last picked color";
    }

    public override CommandResult Invoke()
    {
        try
        {
            var color = TryGetLastColor();
            if (string.IsNullOrEmpty(color))
            {
                return CommandResult.ShowToast("No color found in Color Picker history.");
            }

            System.Windows.Forms.Clipboard.SetText(color);
            return CommandResult.ShowToast($"Copied {color}");
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to copy color: {ex.Message}");
        }
    }

    private static string? TryGetLastColor()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var historyPath = Path.Combine(localAppData, "Microsoft", "PowerToys", "ColorPicker", "colorHistory.json");
        if (!File.Exists(historyPath))
        {
            return null;
        }

        var lines = File.ReadAllLines(historyPath);

        // crude parse: look for last occurrence of "#RRGGBB" in the file
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i];
            var idx = line.IndexOf('#');
            if (idx >= 0 && line.Length >= idx + 7)
            {
                var candidate = line.Substring(idx, 7);
                if (candidate.Length == 7 && candidate[0] == '#')
                {
                    return candidate.ToUpper(CultureInfo.InvariantCulture);
                }
            }
        }

        return null;
    }
}
