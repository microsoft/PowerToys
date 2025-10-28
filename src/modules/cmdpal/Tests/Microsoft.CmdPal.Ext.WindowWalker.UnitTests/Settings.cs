// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WindowWalker.Helpers;

namespace Microsoft.CmdPal.Ext.WindowWalker.UnitTests;

public class Settings : ISettingsInterface
{
    private readonly bool resultsFromVisibleDesktopOnly;
    private readonly bool subtitleShowPid;
    private readonly bool subtitleShowDesktopName;
    private readonly bool confirmKillProcess;
    private readonly bool killProcessTree;
    private readonly bool openAfterKillAndClose;
    private readonly bool hideKillProcessOnElevatedProcesses;
    private readonly bool hideExplorerSettingInfo;
    private readonly bool inMruOrder;
    private readonly bool useWindowIcon;

    public Settings(
        bool resultsFromVisibleDesktopOnly = false,
        bool subtitleShowPid = false,
        bool subtitleShowDesktopName = true,
        bool confirmKillProcess = true,
        bool killProcessTree = false,
        bool openAfterKillAndClose = false,
        bool hideKillProcessOnElevatedProcesses = false,
        bool hideExplorerSettingInfo = true,
        bool inMruOrder = true,
        bool useWindowIcon = true)
    {
        this.resultsFromVisibleDesktopOnly = resultsFromVisibleDesktopOnly;
        this.subtitleShowPid = subtitleShowPid;
        this.subtitleShowDesktopName = subtitleShowDesktopName;
        this.confirmKillProcess = confirmKillProcess;
        this.killProcessTree = killProcessTree;
        this.openAfterKillAndClose = openAfterKillAndClose;
        this.hideKillProcessOnElevatedProcesses = hideKillProcessOnElevatedProcesses;
        this.hideExplorerSettingInfo = hideExplorerSettingInfo;
        this.inMruOrder = inMruOrder;
        this.useWindowIcon = useWindowIcon;
    }

    public bool ResultsFromVisibleDesktopOnly => resultsFromVisibleDesktopOnly;

    public bool SubtitleShowPid => subtitleShowPid;

    public bool SubtitleShowDesktopName => subtitleShowDesktopName;

    public bool ConfirmKillProcess => confirmKillProcess;

    public bool KillProcessTree => killProcessTree;

    public bool OpenAfterKillAndClose => openAfterKillAndClose;

    public bool HideKillProcessOnElevatedProcesses => hideKillProcessOnElevatedProcesses;

    public bool HideExplorerSettingInfo => hideExplorerSettingInfo;

    public bool InMruOrder => inMruOrder;

    public bool UseWindowIcon => useWindowIcon;
}
