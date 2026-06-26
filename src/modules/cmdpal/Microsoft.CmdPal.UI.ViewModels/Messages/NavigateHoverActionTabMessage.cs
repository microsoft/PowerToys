// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Cycles keyboard selection across hover action icons on the selected list row (Run-style Tab).
/// </summary>
public class NavigateHoverActionTabMessage(bool forward, bool searchBoxFocused = false)
{
    public bool Forward { get; } = forward;

    /// <summary>
    /// When true, cycling only occurs if a hover icon is already highlighted.
    /// </summary>
    public bool SearchBoxFocused { get; } = searchBoxFocused;

    public bool Handled { get; set; }
}
