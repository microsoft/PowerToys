// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FancyZonesCLI.Commands;

/// <summary>
/// Hotkey-related commands.
/// </summary>
internal static class HotkeyCommands
{
    public static (int ExitCode, string Output) GetHotkeys()
    {
        var hotkeys = FancyZonesData.ReadLayoutHotkeys();
        if (hotkeys?.Hotkeys == null || hotkeys.Hotkeys.Count == 0)
        {
            return (0, "No hotkeys configured.");
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Layout Hotkeys ===\n");
        sb.AppendLine("Press Win + Ctrl + Alt + <number> to switch layouts:\n");

        foreach (var hotkey in hotkeys.Hotkeys.OrderBy(h => h.Key))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  [{hotkey.Key}] => {hotkey.LayoutId}");
        }

        return (0, sb.ToString().TrimEnd());
    }

    public static (int ExitCode, string Output) SetHotkey(int key, string layoutUuid, Action<uint> notifyFancyZones, uint wmPrivLayoutHotkeysFileUpdate)
    {
        if (key < 0 || key > 9)
        {
            return (1, "Error: Key must be between 0 and 9");
        }

        // Check if this is a custom layout UUID
        var customLayouts = FancyZonesData.ReadCustomLayouts();
        var matchedLayout = customLayouts?.Layouts?.FirstOrDefault(l => l.Uuid.Equals(layoutUuid, StringComparison.OrdinalIgnoreCase));
        bool isCustomLayout = matchedLayout != null;
        string layoutName = matchedLayout?.Name ?? layoutUuid;

        var hotkeys = FancyZonesData.ReadLayoutHotkeys() ?? new LayoutHotkeys();

        hotkeys.Hotkeys ??= new List<LayoutHotkey>();

        // Remove existing hotkey for this key
        hotkeys.Hotkeys.RemoveAll(h => h.Key == key);

        // Add new hotkey
        hotkeys.Hotkeys.Add(new LayoutHotkey { Key = key, LayoutId = layoutUuid });

        // Save
        FancyZonesData.WriteLayoutHotkeys(hotkeys);

        // Notify FancyZones
        notifyFancyZones(wmPrivLayoutHotkeysFileUpdate);

        if (isCustomLayout)
        {
            return (0, $"✓ Hotkey {key} assigned to custom layout '{layoutName}'\n  Press Win + Ctrl + Alt + {key} to switch to this layout");
        }
        else
        {
            return (0, $"⚠ Warning: Hotkey {key} assigned to '{layoutUuid}'\n  Note: FancyZones hotkeys only work with CUSTOM layouts.\n  Template layouts (focus, columns, rows, etc.) cannot be used with hotkeys.\n  Create a custom layout in the FancyZones Editor to use this hotkey.");
        }
    }

    public static (int ExitCode, string Output) RemoveHotkey(int key, Action<uint> notifyFancyZones, uint wmPrivLayoutHotkeysFileUpdate)
    {
        var hotkeys = FancyZonesData.ReadLayoutHotkeys();
        if (hotkeys?.Hotkeys == null)
        {
            return (0, $"No hotkey assigned to key {key}");
        }

        var removed = hotkeys.Hotkeys.RemoveAll(h => h.Key == key);
        if (removed == 0)
        {
            return (0, $"No hotkey assigned to key {key}");
        }

        // Save
        FancyZonesData.WriteLayoutHotkeys(hotkeys);

        // Notify FancyZones
        notifyFancyZones(wmPrivLayoutHotkeysFileUpdate);

        return (0, $"Hotkey {key} removed");
    }
}
