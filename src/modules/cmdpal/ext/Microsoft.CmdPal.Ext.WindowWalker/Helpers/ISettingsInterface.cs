// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.WindowWalker.Helpers;

public interface ISettingsInterface
{
    public bool ResultsFromVisibleDesktopOnly { get; }

    public bool SubtitleShowPid { get; }

    public bool SubtitleShowDesktopName { get; }

    public bool ConfirmKillProcess { get; }

    public bool KillProcessTree { get; }

    public bool OpenAfterKillAndClose { get; }

    public bool HideKillProcessOnElevatedProcesses { get; }

    public bool HideExplorerSettingInfo { get; }

    public bool InMruOrder { get; }
}
