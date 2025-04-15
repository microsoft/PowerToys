// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class AppStateModel : ObservableObject
{
    [JsonIgnore]
    public static readonly string FilePath;

    public event TypedEventHandler<AppStateModel, object?>? StateChanged;

    ///////////////////////////////////////////////////////////////////////////
    // STATE HERE
    public RecentCommandsManager RecentCommands { get; private set; } = new();

    // END SETTINGS
    ///////////////////////////////////////////////////////////////////////////

    static AppStateModel()
    {
        FilePath = StateJsonPath();
    }

    public static AppStateModel LoadState()
    {
        if (string.IsNullOrEmpty(FilePath))
        {
            throw new InvalidOperationException($"You must set a valid {nameof(SettingsModel.FilePath)} before calling {nameof(LoadState)}");
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

            var loaded = JsonSerializer.Deserialize<AppStateModel>(jsonContent, _deserializerOptions);

            Debug.WriteLine(loaded != null ? "Loaded settings file" : "Failed to parse");

            return loaded ?? new();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
        }

        return new();
    }

    public static void SaveState(AppStateModel model)
    {
        if (string.IsNullOrEmpty(FilePath))
        {
            throw new InvalidOperationException($"You must set a valid {nameof(FilePath)} before calling {nameof(SaveState)}");
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
                    model.StateChanged?.Invoke(model, null);
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

    internal static string StateJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        // now, the settings is just next to the exe
        return Path.Combine(directory, "state.json");
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
        AllowTrailingCommas = true,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };
}
