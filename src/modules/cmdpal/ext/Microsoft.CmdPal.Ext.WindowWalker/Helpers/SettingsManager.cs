// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.CmdPal.Ext.WindowWalker.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowWalker.Helpers;

public class SettingsManager : JsonSettingsManager
{
    private static readonly string _namespace = "windowWalker";

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private static SettingsManager? instance;

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
        Namespaced(nameof(OpenAfterKillAndClose)),
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

    public bool ResultsFromVisibleDesktopOnly => _resultsFromVisibleDesktopOnly.Value;

    public bool SubtitleShowPid => _subtitleShowPid.Value;

    public bool SubtitleShowDesktopName => _subtitleShowDesktopName.Value;

    public bool ConfirmKillProcess => _confirmKillProcess.Value;

    public bool KillProcessTree => _killProcessTree.Value;

    public bool OpenAfterKillAndClose => _openAfterKillAndClose.Value;

    public bool HideKillProcessOnElevatedProcesses => _hideKillProcessOnElevatedProcesses.Value;

    public bool HideExplorerSettingInfo => _hideExplorerSettingInfo.Value;

    public bool InMruOrder => _inMruOrder.Value;

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        // now, the state is just next to the exe
        return Path.Combine(directory, "settings.json");
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

        // Load settings from file upon initialization
        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }

    internal static SettingsManager Instance
    {
        get
        {
            instance ??= new SettingsManager();
            return instance;
        }
    }
}
