// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.CmdPal.Ext.WindowWalker.Properties;
using Microsoft.CmdPal.Extensions.Helpers;

#nullable enable

namespace Microsoft.CmdPal.Ext.WindowWalker.Helpers;

public class SettingsManager
{
    private readonly string _filePath;
    private readonly Settings _settings = new();

    private readonly ToggleSetting _resultsFromVisibleDesktopOnly = new(nameof(ResultsFromVisibleDesktopOnly), Resources.wox_plugin_windowwalker_SettingResultsVisibleDesktop, Resources.wox_plugin_windowwalker_SettingResultsVisibleDesktop, false);
    private readonly ToggleSetting _subtitleShowPid = new(nameof(SubtitleShowPid), Resources.wox_plugin_windowwalker_SettingSubtitlePid, Resources.wox_plugin_windowwalker_SettingSubtitlePid, false);
    private readonly ToggleSetting _subtitleShowDesktopName = new(nameof(SubtitleShowDesktopName), Resources.wox_plugin_windowwalker_SettingSubtitleDesktopName, Resources.wox_plugin_windowwalker_SettingSubtitleDesktopName_Description, true);
    private readonly ToggleSetting _confirmKillProcess = new(nameof(ConfirmKillProcess), Resources.wox_plugin_windowwalker_SettingConfirmKillProcess, Resources.wox_plugin_windowwalker_SettingConfirmKillProcess, true);
    private readonly ToggleSetting _killProcessTree = new(nameof(KillProcessTree), Resources.wox_plugin_windowwalker_SettingKillProcessTree, Resources.wox_plugin_windowwalker_SettingKillProcessTree_Description, false);
    private readonly ToggleSetting _openAfterKillAndClose = new(nameof(OpenAfterKillAndClose), Resources.wox_plugin_windowwalker_SettingOpenAfterKillAndClose, Resources.wox_plugin_windowwalker_SettingOpenAfterKillAndClose_Description, false);
    private readonly ToggleSetting _hideKillProcessOnElevatedProcess = new(nameof(HideKillProcessOnElevatedProcess), Resources.wox_plugin_windowwalker_SettingHideKillProcess, Resources.wox_plugin_windowwalker_SettingHideKillProcess, false);
    private readonly ToggleSetting _hideExplorerSettingInfo = new(nameof(HideExplorerSettingInfo), Resources.wox_plugin_windowwalker_SettingExplorerSettingInfo, Resources.wox_plugin_windowwalker_SettingExplorerSettingInfo_Description, false);

    public bool ResultsFromVisibleDesktopOnly => _resultsFromVisibleDesktopOnly.Value;

    public bool SubtitleShowPid => _subtitleShowPid.Value;

    public bool SubtitleShowDesktopName => _subtitleShowDesktopName.Value;

    public bool ConfirmKillProcess => _confirmKillProcess.Value;
    public bool KillProcessTree => _killProcessTree.Value;

    public bool OpenAfterKillAndClose => _openAfterKillAndClose.Value;

    public bool HideKillProcessOnElevatedProcess => _hideKillProcessOnElevatedProcess.Value;

    public bool HideExplorerSettingInfo => _hideExplorerSettingInfo.Value;

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
        return Path.Combine(directory, "window_walker_state.json");
    }

    public SettingsManager()
    {
        _filePath = SettingsJsonPath();

        _settings.Add(_resultsFromVisibleDesktopOnly);
        _settings.Add(_subtitleShowPid);
        _settings.Add(_subtitleShowDesktopName);
        _settings.Add(_confirmKillProcess);
        _settings.Add(_killProcessTree);
        _settings.Add(_openAfterKillAndClose);
        _settings.Add(_hideKillProcessOnElevatedProcess);
        _settings.Add(_hideExplorerSettingInfo);

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