// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Dashboard;

/// <summary>
/// Reserved identifiers for dashboard hotkeys. These ride the same registration and dispatch
/// path as command hotkeys, so they need IDs no extension would ever hand us.
/// </summary>
public static class DashboardHotkeyIds
{
    /// <summary>
    /// Shows the dashboard, or hides it when it's already showing.
    /// </summary>
    public const string Toggle = "com.microsoft.cmdpal.dashboard.toggle";

    public static bool IsDashboardHotkey(string commandId) =>
        string.Equals(commandId, Toggle, StringComparison.Ordinal);
}
