// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// Identifies the semantic UI surface that requested an icon.
/// </summary>
public enum IconRequestSite
{
    Unknown,
    ListItem,
    ContextMenu,
    PageHeader,
    Details,
    Filter,
    Dock,
    Toast,
    Settings,
    Parameter,
    Image,
    Tag,
    Fallback,
    EmptyState,
}
