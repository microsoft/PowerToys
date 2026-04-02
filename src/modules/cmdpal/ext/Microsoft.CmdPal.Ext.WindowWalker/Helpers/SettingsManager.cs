// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.CmdPal.Ext.WindowWalker.Helpers;

public class SettingsManager : JsonSettingsManager, ISettingsInterface
{
    private const string Namespace = "windowWalker";

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
    };

    private static string Namespaced(string propertyName) => $"{Namespace}.{propertyName}";

    private static SettingsManager? _instance;

    private readonly ToggleSetting _resultsFromVisibleDesktopOnly = new(
        Namespaced(nameof(ResultsFromVisibleDesktopOnly)),
        Resources.windowwalker_SettingResultsVisibleDesktop,
        Resources.windowwalker_SettingResultsVisibleDesktop,
        false);

    private readonly ToggleSetting _subtitleShowPid = new(
        Namespaced(nameof(SubtitleShowPid)),
        Resources.windowwalker_SettingTagPid,
        Resources.windowwalker_SettingTagPid,
        false);

    private readonly ToggleSetting _subtitleShowDesktopName = new(
        Namespaced(nameof(SubtitleShowDesktopName)),
        Resources.windowwalker_SettingTagDesktopName,
        Resources.windowwalker_SettingSubtitleDesktopName_Description,
        true);

    private readonly ToggleSetting _confirmKillProcess = new(
        Namespaced(nameof(ConfirmKillProcess)),
        Resources.windowwalker_SettingConfirmKillProcess,
        Resources.windowwalker_SettingConfirmKillProcess,
        true);

    private readonly ToggleSetting _killProcessTree = new(
        Namespaced(nameof(KillProcessTree)),
        Resources.windowwalker_SettingKillProcessTree,
        Resources.windowwalker_SettingKillProcessTree_Description,
        false);

    private readonly ToggleSetting _openAfterKillAndClose = new(
        Namespaced(nameof(KeepOpenAfterKillAndClose)),
        Resources.windowwalker_SettingOpenAfterKillAndClose,
        Resources.windowwalker_SettingOpenAfterKillAndClose_Description,
        false);

    private readonly ToggleSetting _hideKillProcessOnElevatedProcesses = new(
        Namespaced(nameof(HideKillProcessOnElevatedProcesses)),
        Resources.windowwalker_SettingHideKillProcess,
        Resources.windowwalker_SettingHideKillProcess,
        false);

    private readonly ToggleSetting _hideExplorerSettingInfo = new(
        Namespaced(nameof(HideExplorerSettingInfo)),
        Resources.windowwalker_SettingExplorerSettingInfo,
        Resources.windowwalker_SettingExplorerSettingInfo_Description,
        true);

    private readonly ToggleSetting _inMruOrder = new(
        Namespaced(nameof(InMruOrder)),
        Resources.windowwalker_SettingInMruOrder,
        Resources.windowwalker_SettingInMruOrder_Description,
        true);

    private readonly ToggleSetting _useWindowIcon = new(
        Namespaced(nameof(UseWindowIcon)),
        Resources.windowwalker_SettingUseWindowIcon,
        Resources.windowwalker_SettingUseWindowIcon_Description,
        true);

    public bool ResultsFromVisibleDesktopOnly => _resultsFromVisibleDesktopOnly.Value;

    public bool SubtitleShowPid => _subtitleShowPid.Value;

    public bool SubtitleShowDesktopName => _subtitleShowDesktopName.Value;

    public bool ConfirmKillProcess => _confirmKillProcess.Value;

    public bool KillProcessTree => _killProcessTree.Value;

    public bool KeepOpenAfterKillAndClose => _openAfterKillAndClose.Value;

    public bool HideKillProcessOnElevatedProcesses => _hideKillProcessOnElevatedProcesses.Value;

    public bool HideExplorerSettingInfo => _hideExplorerSettingInfo.Value;

    public bool InMruOrder => _inMruOrder.Value;

    public bool UseWindowIcon => _useWindowIcon.Value;

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        return Path.Combine(directory, $"{Namespace}.settings.json");
    }

    private static string LegacySettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        return Path.Combine(directory, "settings.json");
    }

    /// <summary>
    /// Migrates settings from a shared legacy file to this extension's own settings file.
    /// Call after registering all settings with <see cref="Settings"/> and before <see cref="LoadSettings"/>.
    /// Skips if <see cref="FilePath"/> already exists or <paramref name="legacyFilePath"/> is missing.
    /// </summary>
    private void MigrateFromLegacyFile(string legacyFilePath)
    {
        if (string.IsNullOrEmpty(FilePath) || string.IsNullOrEmpty(legacyFilePath))
        {
            return;
        }

        // Already migrated — per-extension file exists.
        if (File.Exists(FilePath))
        {
            return;
        }

        if (!File.Exists(legacyFilePath))
        {
            return;
        }

        try
        {
            var legacyContent = File.ReadAllText(legacyFilePath);
            if (JsonNode.Parse(legacyContent) is not JsonObject)
            {
                return;
            }

            // Extract only the keys this extension owns.
            Settings.Update(legacyContent);
            var settingsJson = Settings.ToJson();

            if (JsonNode.Parse(settingsJson) is JsonObject extracted && extracted.Count > 0)
            {
                var directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(FilePath, extracted.ToJsonString(_serializerOptions));
            }
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = $"Settings migration failed from '{legacyFilePath}' to '{FilePath}': {ex}" });
        }
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_resultsFromVisibleDesktopOnly);
        Settings.Add(_subtitleShowPid);
        Settings.Add(_subtitleShowDesktopName);
        Settings.Add(_confirmKillProcess);
        Settings.Add(_killProcessTree);
        Settings.Add(_openAfterKillAndClose);
        Settings.Add(_hideKillProcessOnElevatedProcesses);
        Settings.Add(_hideExplorerSettingInfo);
        Settings.Add(_inMruOrder);
        Settings.Add(_useWindowIcon);

        MigrateFromLegacyFile(LegacySettingsJsonPath());
        LoadSettings();

        Settings.SettingsChanged += (_, _) => SaveSettings();
    }

    internal static SettingsManager Instance
    {
        get
        {
            _instance ??= new SettingsManager();
            return _instance;
        }
    }
}
