// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls;

namespace Microsoft.CmdPal.UI.Dock;

internal enum DockPageBackAction
{
    None,
    GoBack,
    Close,
}

internal static class DockPageBackNavigation
{
    /// <summary>
    /// Maps a back request to the dock flyout's behavior. Like the palette, Backspace only
    /// goes back (when the user enables it) and never closes the flyout.
    /// </summary>
    public static DockPageBackAction GetAction(
        SearchBarBackRequestKind kind,
        bool fromBackspace,
        bool canGoBack,
        bool backspaceGoesBack) =>
        kind switch
        {
            SearchBarBackRequestKind.Dismiss or SearchBarBackRequestKind.Hide => DockPageBackAction.Close,
            _ when fromBackspace => canGoBack && backspaceGoesBack ? DockPageBackAction.GoBack : DockPageBackAction.None,
            _ => canGoBack ? DockPageBackAction.GoBack : DockPageBackAction.Close,
        };
}
