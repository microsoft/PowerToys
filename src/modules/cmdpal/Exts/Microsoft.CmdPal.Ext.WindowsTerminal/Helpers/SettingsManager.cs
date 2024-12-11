// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.CmdPal.Ext.WindowsTerminal.Properties;
using Microsoft.CmdPal.Extensions.Helpers;

#nullable enable

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;

public class SettingsManager
{
    private readonly string _filePath;
    private readonly Settings _settings = new();

    private readonly ToggleSetting _showHiddenProfiles = new(nameof(ShowHiddenProfiles), Resources.show_hidden_profiles, Resources.show_hidden_profiles, false);
    private readonly ToggleSetting _openNewTab = new(nameof(OpenNewTab), Resources.open_new_tab, Resources.open_new_tab, false);
    private readonly ToggleSetting _openQuake = new(nameof(OpenQuake), Resources.open_quake, Resources.open_quake_description, false);

    public bool ShowHiddenProfiles => _showHiddenProfiles.Value;

    public bool OpenNewTab => _openNewTab.Value;

    public bool OpenQuake => _openQuake.Value;

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    internal static string SettingsJsonPath()
    {
        // Get the path to our exe
        var path = System.Reflection.Assembly.GetExecutingAssembly().Location;

        // Get the directory of the exe
        var directory = Path.GetDirectoryName(path) ?? string.Empty;

        // now, the state is just next to the exe
        return Path.Combine(directory, "wt_state.json");
    }

    public SettingsManager()
    {
        _filePath = SettingsJsonPath();

        _settings.Add(_showHiddenProfiles);
        _settings.Add(_openNewTab);
        _settings.Add(_openQuake);

        // Load settings from file upon initialization
        LoadSettings();
    }

    public Settings GetSettings()
    {
        return _settings;
    }

    public void SaveSettings()
    {
        try
        {
            // Serialize the main dictionary to JSON and save it to the file
            var settingsJson = _settings.ToJson();

            File.WriteAllText(_filePath, settingsJson);
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = ex.ToString() });
        }
    }

    public void LoadSettings()
    {
        if (!File.Exists(_filePath))
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = "The provided settings file does not exist" });
            return;
        }

        try
        {
            // Read the JSON content from the file
            var jsonContent = File.ReadAllText(_filePath);

            // Is it valid JSON?
            if (JsonNode.Parse(jsonContent) is JsonObject savedSettings)
            {
                _settings.Update(jsonContent);
            }
            else
            {
                ExtensionHost.LogMessage(new LogMessage() { Message = "Failed to parse settings file as JsonObject." });
            }
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = ex.ToString() });
        }
    }
}
