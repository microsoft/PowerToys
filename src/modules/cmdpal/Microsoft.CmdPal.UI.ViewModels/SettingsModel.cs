// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class SettingsModel : ObservableObject
{
    [JsonIgnore]
    public static readonly string FilePath;

    public event TypedEventHandler<SettingsModel, object?>? SettingsChanged;

    ///////////////////////////////////////////////////////////////////////////
    // SETTINGS HERE
    public HotkeySettings? Hotkey { get; set; } = new HotkeySettings(true, false, true, false, 0x20); // win+alt+space

    public bool ShowAppDetails { get; set; }

    public bool HotkeyGoesHome { get; set; }

    public bool BackspaceGoesBack { get; set; }

    public bool SingleClickActivates { get; set; }

    public bool HighlightSearchOnActivate { get; set; } = true;

    public Dictionary<string, ProviderSettings> ProviderSettings { get; set; } = [];

    public Dictionary<string, CommandAlias> Aliases { get; set; } = [];

    public List<TopLevelHotkey> CommandHotkeys { get; set; } = [];

    public MonitorBehavior SummonOn { get; set; } = MonitorBehavior.ToMouse;

    // END SETTINGS
    ///////////////////////////////////////////////////////////////////////////

    static SettingsModel()
    {
        FilePath = SettingsJsonPath();
    }

    public static SettingsModel LoadSettings()
    {
        if (string.IsNullOrEmpty(FilePath))
        {
            throw new InvalidOperationException($"You must set a valid {nameof(SettingsModel.FilePath)} before calling {nameof(LoadSettings)}");
        }

        if (!File.Exists(FilePath))
        {
            Debug.WriteLine("The provided settings file does not exist");
            return new();
        }

        try
        {
            // Read the JSON content from the file
            var jsonContent = File.ReadAllText(FilePath);

            var loaded = JsonSerializer.Deserialize<SettingsModel>(jsonContent, _deserializerOptions);

            Debug.WriteLine(loaded != null ? "Loaded settings file" : "Failed to parse");

            return loaded ?? new();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
        }

        return new();
    }

    public static void SaveSettings(SettingsModel model)
    {
        if (string.IsNullOrEmpty(FilePath))
        {
            throw new InvalidOperationException($"You must set a valid {nameof(FilePath)} before calling {nameof(SaveSettings)}");
        }

        try
        {
            // Serialize the main dictionary to JSON and save it to the file
            var settingsJson = JsonSerializer.Serialize(model, _serializerOptions);

            // Is it valid JSON?
            if (JsonNode.Parse(settingsJson) is JsonObject newSettings)
            {
                // Now, read the existing content from the file
                var oldContent = File.Exists(FilePath) ? File.ReadAllText(FilePath) : "{}";

                // Is it valid JSON?
                if (JsonNode.Parse(oldContent) is JsonObject savedSettings)
                {
                    foreach (var item in newSettings)
                    {
                        savedSettings[item.Key] = item.Value != null ? item.Value.DeepClone() : null;
                    }

                    var serialized = savedSettings.ToJsonString(_serializerOptions);
                    File.WriteAllText(FilePath, serialized);

                    // TODO: Instead of just raising the event here, we should
                    // have a file change watcher on the settings file, and
                    // reload the settings then
                    model.SettingsChanged?.Invoke(model, null);
                }
                else
                {
                    Debug.WriteLine("Failed to parse settings file as JsonObject.");
                }
            }
            else
            {
                Debug.WriteLine("Failed to parse settings file as JsonObject.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
        }
    }

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        // now, the settings is just next to the exe
        return Path.Combine(directory, "settings.json");
    }

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions _deserializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        IncludeFields = true,
        Converters = { new JsonStringEnumConverter() },
        AllowTrailingCommas = true,
    };
}

public enum MonitorBehavior
{
    ToMouse = 0,
    ToPrimary = 1,
    ToFocusedWindow = 2,
    InPlace = 3,
}
