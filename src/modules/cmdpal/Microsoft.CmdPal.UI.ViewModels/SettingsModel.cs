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
    public static HotkeySettings DefaultActivationShortcut { get; } = new HotkeySettings(true, false, true, false, 0x20); // win+alt+space

    public HotkeySettings? Hotkey { get; set; } = DefaultActivationShortcut;

    public bool UseLowLevelGlobalHotkey { get; set; }

    public bool ShowAppDetails { get; set; }

    public bool HotkeyGoesHome { get; set; }

    public bool BackspaceGoesBack { get; set; }

    public bool SingleClickActivates { get; set; }

    public bool HighlightSearchOnActivate { get; set; } = true;

    public bool ShowSystemTrayIcon { get; set; } = true;

    public bool IgnoreShortcutWhenFullscreen { get; set; }

    public bool AllowExternalReload { get; set; }

    public Dictionary<string, ProviderSettings> ProviderSettings { get; set; } = [];

    public Dictionary<string, CommandAlias> Aliases { get; set; } = [];

    public List<TopLevelHotkey> CommandHotkeys { get; set; } = [];

    public MonitorBehavior SummonOn { get; set; } = MonitorBehavior.ToMouse;

    public bool DisableAnimations { get; set; } = true;

    // END SETTINGS
    ///////////////////////////////////////////////////////////////////////////

    static SettingsModel()
    {
        FilePath = SettingsJsonPath();
    }

    public ProviderSettings GetProviderSettings(CommandProviderWrapper provider)
    {
        ProviderSettings? settings;
        if (!ProviderSettings.TryGetValue(provider.ProviderId, out settings))
        {
            settings = new ProviderSettings(provider);
            settings.Connect(provider);
            ProviderSettings[provider.ProviderId] = settings;
        }
        else
        {
            settings.Connect(provider);
        }

        return settings;
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

            var loaded = JsonSerializer.Deserialize<SettingsModel>(jsonContent, JsonSerializationContext.Default.SettingsModel);

            Debug.WriteLine(loaded is not null ? "Loaded settings file" : "Failed to parse");

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
            var settingsJson = JsonSerializer.Serialize(model, JsonSerializationContext.Default.SettingsModel);

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
                        savedSettings[item.Key] = item.Value?.DeepClone();
                    }

                    var serialized = savedSettings.ToJsonString(JsonSerializationContext.Default.Options);
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

    // [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "<Pending>")]
    // private static readonly JsonSerializerOptions _serializerOptions = new()
    // {
    //    WriteIndented = true,
    //    Converters = { new JsonStringEnumConverter() },
    // };
    // private static readonly JsonSerializerOptions _deserializerOptions = new()
    // {
    //    PropertyNameCaseInsensitive = true,
    //    IncludeFields = true,
    //    Converters = { new JsonStringEnumConverter() },
    //    AllowTrailingCommas = true,
    // };
}

[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(HistoryItem))]
[JsonSerializable(typeof(SettingsModel))]
[JsonSerializable(typeof(AppStateModel))]
[JsonSerializable(typeof(RecentCommandsManager))]
[JsonSerializable(typeof(List<string>), TypeInfoPropertyName = "StringList")]
[JsonSerializable(typeof(List<HistoryItem>), TypeInfoPropertyName = "HistoryList")]
[JsonSerializable(typeof(Dictionary<string, object>), TypeInfoPropertyName = "Dictionary")]
[JsonSourceGenerationOptions(UseStringEnumConverter = true, WriteIndented = true, IncludeFields = true, PropertyNameCaseInsensitive = true, AllowTrailingCommas = true)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Just used here")]
internal sealed partial class JsonSerializationContext : JsonSerializerContext
{
}

public enum MonitorBehavior
{
    ToMouse = 0,
    ToPrimary = 1,
    ToFocusedWindow = 2,
    InPlace = 3,
}
