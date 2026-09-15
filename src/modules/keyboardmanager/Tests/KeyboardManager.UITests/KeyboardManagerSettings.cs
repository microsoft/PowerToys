// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;

namespace Microsoft.PowerToys.KeyboardManager.UITests;

internal static class KeyboardManagerSettings
{
    private const uint EventModifyState = 0x0002;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string SettingsDirectory = Path.Combine(
        SettingsConfigHelper.PowerToysSettingsRoot,
        KeyboardManagerTestConstants.ModuleName);

    public static readonly string ModuleSettingsPath = Path.Combine(SettingsDirectory, "settings.json");
    public static readonly string ProfilePath = Path.Combine(SettingsDirectory, "default.json");
    public static readonly string EditorSettingsPath = Path.Combine(SettingsDirectory, "editorSettings.json");
    public static readonly IReadOnlyList<string> ManagedPaths =
        new[] { ModuleSettingsPath, ProfilePath, EditorSettingsPath };

    public static void ConfigureUnifiedEditorBaseline()
    {
        Directory.CreateDirectory(SettingsDirectory);
        var root = ReadObject(ModuleSettingsPath);
        var properties = root["properties"] as JsonObject ?? new JsonObject();
        root["properties"] = properties;
        root["name"] = KeyboardManagerTestConstants.ModuleName;
        root["version"] = "1";
        properties["activeConfiguration"] = new JsonObject { ["value"] = "default" };
        properties["keyboardConfigurations"] = new JsonObject
        {
            ["value"] = new JsonArray("default"),
        };
        properties["useNewEditor"] = true;
        WriteJson(ModuleSettingsPath, root);
        ResetToEmptyProfile(signal: false);
    }

    public static JsonObject BuildProfile(
        IEnumerable<KeyValuePair<int, int[]>>? singleKeyRemaps = null,
        IEnumerable<ShortcutRemap>? shortcutRemaps = null,
        bool includeLoadProbe = true)
    {
        var keyMappings = new JsonArray();
        if (includeLoadProbe)
        {
            keyMappings.Add(CreateKeyMapping(
                KeyboardManagerTestConstants.LoadProbeSourceKey,
                new[] { KeyboardManagerTestConstants.LoadProbeTargetKey }));
        }

        foreach (var mapping in singleKeyRemaps ?? Enumerable.Empty<KeyValuePair<int, int[]>>())
        {
            keyMappings.Add(CreateKeyMapping(mapping.Key, mapping.Value));
        }

        var globalShortcuts = new JsonArray();
        var appSpecificShortcuts = new JsonArray();
        foreach (var mapping in shortcutRemaps ?? Enumerable.Empty<ShortcutRemap>())
        {
            var node = new JsonObject
            {
                ["originalKeys"] = ToKeyString(mapping.OriginalKeys),
                ["newRemapKeys"] = ToKeyString(mapping.NewKeys),
                ["operationType"] = 0,
                ["exactMatch"] = false,
            };

            if (string.IsNullOrWhiteSpace(mapping.TargetApp))
            {
                globalShortcuts.Add(node);
            }
            else
            {
                node["targetApp"] = mapping.TargetApp;
                appSpecificShortcuts.Add(node);
            }
        }

        return new JsonObject
        {
            ["remapKeys"] = new JsonObject { ["inProcess"] = keyMappings },
            ["remapKeysToText"] = new JsonObject { ["inProcess"] = new JsonArray() },
            ["remapShortcuts"] = new JsonObject
            {
                ["global"] = globalShortcuts,
                ["appSpecific"] = appSpecificShortcuts,
            },
            ["remapShortcutsToText"] = new JsonObject
            {
                ["global"] = new JsonArray(),
                ["appSpecific"] = new JsonArray(),
            },
        };
    }

    public static KeyValuePair<int, int[]> SingleKeyRemap(int source, params int[] target) =>
        new(source, target);

    public static void ApplyProfile(JsonObject profile)
    {
        WriteJson(ProfilePath, profile);
        SignalSettingsChanged();
    }

    public static void ResetToEmptyProfile(bool signal = true)
    {
        WriteJson(ProfilePath, BuildProfile(includeLoadProbe: false));
        File.Delete(EditorSettingsPath);
        if (signal)
        {
            SignalSettingsChanged();
        }
    }

    public static JsonObject ReadProfile() => ReadObject(ProfilePath);

    public static JsonObject ReadEditorSettings() => ReadObject(EditorSettingsPath);

    public static void SignalSettingsChanged()
    {
        var eventHandle = OpenEvent(EventModifyState, false, KeyboardManagerTestConstants.SettingsChangedEventName);
        if (eventHandle == IntPtr.Zero)
        {
            eventHandle = CreateEvent(IntPtr.Zero, false, false, KeyboardManagerTestConstants.SettingsChangedEventName);
        }

        if (eventHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"Could not open the Keyboard Manager settings event. Win32 error: {Marshal.GetLastWin32Error()}.");
        }

        try
        {
            if (!SetEvent(eventHandle))
            {
                throw new InvalidOperationException(
                    $"Could not signal the Keyboard Manager settings event. Win32 error: {Marshal.GetLastWin32Error()}.");
            }
        }
        finally
        {
            CloseHandle(eventHandle);
        }
    }

    private static JsonObject CreateKeyMapping(int source, int[] target) =>
        new()
        {
            ["originalKeys"] = source.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["newRemapKeys"] = ToKeyString(target),
        };

    private static string ToKeyString(IEnumerable<int> keys) =>
        string.Join(";", keys.Select(key => key.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    private static JsonObject ReadObject(string path)
    {
        if (!File.Exists(path))
        {
            return new JsonObject();
        }

        return JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject();
    }

    private static void WriteJson(string path, JsonObject value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, value.ToJsonString(JsonOptions));
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenEvent(uint desiredAccess, bool inheritHandle, string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateEvent(IntPtr eventAttributes, bool manualReset, bool initialState, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetEvent(IntPtr eventHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
