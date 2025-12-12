// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using static Common.UI.SettingsDeepLink;

namespace PowerToysExtension.Helpers;

/// <summary>
/// Reads PowerToys module enablement flags from the global settings.json.
/// </summary>
internal static class ModuleEnablementService
{
    internal static string SettingsFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft",
        "PowerToys",
        "settings.json");

    internal static bool IsModuleEnabled(SettingsWindow module)
    {
        var key = GetEnabledKey(module);
        return string.IsNullOrEmpty(key) || IsKeyEnabled(key);
    }

    internal static bool IsKeyEnabled(string enabledKey)
    {
        if (string.IsNullOrWhiteSpace(enabledKey))
        {
            return true;
        }

        try
        {
            var enabled = ReadEnabledFlags();
            return enabled is null || !enabled.TryGetValue(enabledKey, out var value) || value;
        }
        catch
        {
            return true;
        }
    }

    private static Dictionary<string, bool>? ReadEnabledFlags()
    {
        if (!File.Exists(SettingsFilePath))
        {
            return null;
        }

        var json = File.ReadAllText(SettingsFilePath).Trim('\0');
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("enabled", out var enabledRoot) ||
            enabledRoot.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in enabledRoot.EnumerateObject())
        {
            if (prop.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                result[prop.Name] = prop.Value.GetBoolean();
            }
        }

        return result;
    }

    private static string? GetEnabledKey(SettingsWindow module) => module switch
    {
        SettingsWindow.Awake => "Awake",
        SettingsWindow.AdvancedPaste => "AdvancedPaste",
        SettingsWindow.AlwaysOnTop => "AlwaysOnTop",
        SettingsWindow.ColorPicker => "ColorPicker",
        SettingsWindow.CropAndLock => "CropAndLock",
        SettingsWindow.EnvironmentVariables => "EnvironmentVariables",
        SettingsWindow.FancyZones => "FancyZones",
        SettingsWindow.FileExplorer => "File Explorer Preview",
        SettingsWindow.FileLocksmith => "FileLocksmith",
        SettingsWindow.Hosts => "Hosts",
        SettingsWindow.ImageResizer => "Image Resizer",
        SettingsWindow.KBM => "Keyboard Manager",
        SettingsWindow.LightSwitch => "LightSwitch",
        SettingsWindow.MeasureTool => "Measure Tool",
        SettingsWindow.MouseWithoutBorders => "MouseWithoutBorders",
        SettingsWindow.NewPlus => "NewPlus",
        SettingsWindow.Peek => "Peek",
        SettingsWindow.PowerAccent => "QuickAccent",
        SettingsWindow.PowerLauncher => "PowerToys Run",
        SettingsWindow.Run => "PowerToys Run",
        SettingsWindow.PowerRename => "PowerRename",
        SettingsWindow.PowerOCR => "TextExtractor",
        SettingsWindow.RegistryPreview => "RegistryPreview",
        SettingsWindow.ShortcutGuide => "Shortcut Guide",
        SettingsWindow.Workspaces => "Workspaces",
        SettingsWindow.ZoomIt => "ZoomIt",
        SettingsWindow.CmdNotFound => "CmdNotFound",
        SettingsWindow.CmdPal => "CmdPal",
        _ => null,
    };
}
