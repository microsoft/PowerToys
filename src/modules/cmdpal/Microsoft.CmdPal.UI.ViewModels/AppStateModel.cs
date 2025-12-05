// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;
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
    // Make sure that you make the setters public (JsonSerializer.Deserialize will fail silently otherwise)!
    // Make sure that any new types you add are added to JsonSerializationContext!
    public RecentCommandsManager RecentCommands { get; set; } = new();

    public List<string> RunHistory { get; set; } = [];

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
            throw new InvalidOperationException($"You must set a valid {nameof(FilePath)} before calling {nameof(LoadState)}");
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

            var loaded = JsonSerializer.Deserialize<AppStateModel>(jsonContent, JsonSerializationContext.Default.AppStateModel);

            Debug.WriteLine(loaded is not null ? "Loaded settings file" : "Failed to parse");

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
            var settingsJson = JsonSerializer.Serialize(model, JsonSerializationContext.Default.AppStateModel!);

            // validate JSON
            if (JsonNode.Parse(settingsJson) is not JsonObject newSettings)
            {
                Logger.LogError("Failed to parse app state as a JsonObject.");
                return;
            }

            // read previous settings
            if (!TryReadSavedState(out var savedSettings))
            {
                savedSettings = new JsonObject();
            }

            // merge new settings into old ones
            foreach (var item in newSettings)
            {
                savedSettings[item.Key] = item.Value?.DeepClone();
            }

            var serialized = savedSettings.ToJsonString(JsonSerializationContext.Default.AppStateModel!.Options);
            File.WriteAllText(FilePath, serialized);

            // TODO: Instead of just raising the event here, we should
            // have a file change watcher on the settings file, and
            // reload the settings then
            model.StateChanged?.Invoke(model, null);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to save application state to {FilePath}:", ex);
        }
    }

    private static bool TryReadSavedState([NotNullWhen(true)] out JsonObject? savedSettings)
    {
        savedSettings = null;

        // read existing content from the file
        string oldContent;
        try
        {
            if (File.Exists(FilePath))
            {
                oldContent = File.ReadAllText(FilePath);
            }
            else
            {
                // file doesn't exist (might not have been created yet), so consider this a success
                // and return empty settings
                savedSettings = new JsonObject();
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to read app state file {FilePath}:\n{ex}");
            return false;
        }

        // detect empty file, just for sake of logging
        if (string.IsNullOrWhiteSpace(oldContent))
        {
            Logger.LogInfo($"App state file is empty: {FilePath}");
            return false;
        }

        // is it valid JSON?
        try
        {
            savedSettings = JsonNode.Parse(oldContent) as JsonObject;
            return savedSettings != null;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to parse app state from {FilePath}:\n{ex}");
            return false;
        }
    }

    internal static string StateJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        // now, the settings is just next to the exe
        return Path.Combine(directory, "state.json");
    }
}
