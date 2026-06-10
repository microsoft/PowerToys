// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;

namespace Microsoft.ColorPicker.UITests.Next;

/// <summary>
/// Minimal reader for ColorPicker's persisted settings (<c>%LOCALAPPDATA%\Microsoft\PowerToys\ColorPicker\settings.json</c>).
/// Used as the authoritative source for the activation shortcut — the same JSON that the
/// runner reads to register its global hotkey, so it always matches what's actually active.
/// </summary>
internal static class ColorPickerSettingsFile
{
    public sealed record Shortcut(bool Win, bool Ctrl, bool Alt, bool Shift, int Code);

    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft", "PowerToys", "ColorPicker", "settings.json");

    public static Shortcut ReadShortcut()
    {
        if (!System.IO.File.Exists(Path))
        {
            // Defaults from ColorPickerProperties.cs: Win + Shift + C (0x43).
            return new Shortcut(true, false, false, true, 0x43);
        }

        var doc = JsonNode.Parse(System.IO.File.ReadAllText(Path))!;
        var s = doc["properties"]?["ActivationShortcut"];
        if (s is null)
        {
            return new Shortcut(true, false, false, true, 0x43);
        }

        return new Shortcut(
            Win: (bool?)s["win"] ?? false,
            Ctrl: (bool?)s["ctrl"] ?? false,
            Alt: (bool?)s["alt"] ?? false,
            Shift: (bool?)s["shift"] ?? false,
            Code: (int?)s["code"] ?? 0x43);
    }

    public static string Describe(Shortcut s)
    {
        var parts = new List<string>();
        if (s.Win) parts.Add("Win");
        if (s.Ctrl) parts.Add("Ctrl");
        if (s.Alt) parts.Add("Alt");
        if (s.Shift) parts.Add("Shift");
        parts.Add(((char)s.Code).ToString());
        return string.Join("+", parts);
    }

    /// <summary>
    /// Read the <c>showcolorname</c> flag. When true, the picker overlay uses the two-line layout
    /// from MainView.xaml (HEX TextBlock + ColorName TextBlock, both without an
    /// AutomationProperties.Name override → their UIA Name == their rendered Text).
    /// When false, the single-line layout is used and its single TextBlock has
    /// AutomationProperties.Name="{Binding ColorName}", which masks the HEX from UIA.
    /// </summary>
    public static bool ReadShowColorName()
    {
        if (!System.IO.File.Exists(Path))
        {
            return false;
        }

        var doc = JsonNode.Parse(System.IO.File.ReadAllText(Path))!;
        return (bool?)doc["properties"]?["showcolorname"]?["value"] ?? false;
    }

    /// <summary>Write the <c>showcolorname</c> flag. ColorPickerUI's FileSystemWatcher will pick it up.</summary>
    public static void WriteShowColorName(bool value)
    {
        var doc = JsonNode.Parse(System.IO.File.ReadAllText(Path))!;
        var props = doc["properties"]!;
        if (props["showcolorname"] is null)
        {
            props["showcolorname"] = new JsonObject();
        }

        props["showcolorname"]!["value"] = value;
        System.IO.File.WriteAllText(Path, doc.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = false }));
    }
}
