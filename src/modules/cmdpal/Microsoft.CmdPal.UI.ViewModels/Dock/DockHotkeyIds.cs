// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

/// <summary>
/// Reserved identifiers for dock hotkeys. These ride the same registration and dispatch
/// path as command hotkeys, so they need IDs no extension would ever hand us.
/// </summary>
public static class DockHotkeyIds
{
    /// <summary>
    /// Reveals the dock (when auto-hide has it collapsed) and moves focus into it.
    /// </summary>
    public const string FocusDock = "com.microsoft.cmdpal.dock.focus";

    /// <summary>
    /// Returns true when the given command ID belongs to the dock rather than a command.
    /// </summary>
    public static bool IsDockHotkey(string commandId) =>
        string.Equals(commandId, FocusDock, StringComparison.Ordinal);
}
