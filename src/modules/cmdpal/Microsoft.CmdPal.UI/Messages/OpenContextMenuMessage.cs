// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;

namespace Microsoft.CmdPal.UI.Messages;

/// <summary>
/// Requests opening a context menu in the palette.
/// </summary>
/// <param name="Request">The context and optional presentation overrides to open.</param>
/// <remarks>Send on the UI thread.</remarks>
public record OpenContextMenuMessage(ContextMenuRequest Request);

/// <summary>
/// Specifies which edge of the menu contains the filter box, independently of popup placement.
/// </summary>
public enum ContextMenuFilterLocation
{
    /// <summary>
    /// Places the filter above the commands.
    /// </summary>
    Top,

    /// <summary>
    /// Places the filter below the commands.
    /// </summary>
    Bottom,
}
