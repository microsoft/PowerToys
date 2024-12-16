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

namespace Microsoft.CmdPal.Ext.WindowWalker.Helpers;

public class SettingsManager
{
    private readonly string _filePath;
    private readonly Settings _settings = new();

    private static SettingsManager? instance;

    private readonly ToggleSetting _resultsFromVisibleDesktopOnly = new(
        nameof(ResultsFromVisibleDesktopOnly),
        Resources.windowwalker_SettingResultsVisibleDesktop,
        Resources.windowwalker_SettingResultsVisibleDesktop,
        false);

    private readonly ToggleSetting _subtitleShowPid = new(
        nameof(SubtitleShowPid),
        Resources.windowwalker_SettingTagPid,
        Resources.windowwalker_SettingTagPid,
        false);

    private readonly ToggleSetting _subtitleShowDesktopName = new(
        nameof(SubtitleShowDesktopName),
        Resources.windowwalker_SettingTagDesktopName,
        Resources.windowwalker_SettingSubtitleDesktopName_Description,
        true);

    private readonly ToggleSetting _confirmKillProcess = new(
        nameof(ConfirmKillProcess),
        Resources.windowwalker_SettingConfirmKillProcess,
        Resources.windowwalker_SettingConfirmKillProcess,
        true);

    private readonly ToggleSetting _killProcessTree = new(
        nameof(KillProcessTree),
        Resources.windowwalker_SettingKillProcessTree,
        Resources.windowwalker_SettingKillProcessTree_Description,
        false);

    private readonly ToggleSetting _openAfterKillAndClose = new(
        nameof(OpenAfterKillAndClose),
        Resources.windowwalker_SettingOpenAfterKillAndClose,
        Resources.windowwalker_SettingOpenAfterKillAndClose_Description,
        false);

    private readonly ToggleSetting _hideKillProcessOnElevatedProcesses = new(
        nameof(HideKillProcessOnElevatedProcesses),
        Resources.windowwalker_SettingHideKillProcess,
        Resources.windowwalker_SettingHideKillProcess,
        false);

    private readonly ToggleSetting _hideExplorerSettingInfo = new(
        nameof(HideExplorerSettingInfo),
        Resources.windowwalker_SettingExplorerSettingInfo,
        Resources.windowwalker_SettingExplorerSettingInfo_Description,
        false);

    private readonly ToggleSetting _inMruOrder = new(
        nameof(InMruOrder),
        Resources.windowwalker_SettingInMruOrder,
        Resources.windowwalker_SettingInMruOrder_Description,
        true);

    public bool ResultsFromVisibleDesktopOnly => _resultsFromVisibleDesktopOnly.Value;

    public bool SubtitleShowPid => _subtitleShowPid.Value;

    public bool SubtitleShowDesktopName => _subtitleShowDesktopName.Value;

    public bool ConfirmKillProcess => _confirmKillProcess.Value;

    public bool KillProcessTree => _killProcessTree.Value;

    public bool OpenAfterKillAndClose => _openAfterKillAndClose.Value;

    public bool HideKillProcessOnElevatedProcesses => _hideKillProcessOnElevatedProcesses.Value;

    public bool HideExplorerSettingInfo => _hideExplorerSettingInfo.Value;

    public bool InMruOrder => _inMruOrder.Value;

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
        _settings.Add(_hideKillProcessOnElevatedProcesses);
        _settings.Add(_hideExplorerSettingInfo);
        _settings.Add(_inMruOrder);

        // Load settings from file upon initialization
        LoadSettings();
    }

    internal static SettingsManager Instance
    {
        get
        {
            instance ??= new SettingsManager();
            return instance;
        }
    }

    public Settings GetSettings() => _settings;

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
