// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Cycles keyboard selection across the selected list row and its hover icons (Run-style Tab).
/// The first Tab highlights the row; subsequent Tabs cycle icons while focus stays in search.
/// </summary>
public class NavigateHoverActionTabMessage(bool forward, bool searchBoxFocused = false)
{
    public bool Forward { get; } = forward;

    /// <summary>
    /// When true, the search box has focus and Tab cycles hover icons without moving focus
    /// (Run-style visual selection). When false, the message is ignored.
    /// </summary>
    public bool SearchBoxFocused { get; } = searchBoxFocused;

    public bool Handled { get; set; }
}
