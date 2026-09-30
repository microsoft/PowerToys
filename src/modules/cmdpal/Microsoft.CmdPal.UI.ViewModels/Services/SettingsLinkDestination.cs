// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>Current UI destination for a stable settings link.</summary>
/// <param name="LinkId">Canonical public link ID.</param>
/// <param name="PageTag">Current NavigationView page tag.</param>
/// <param name="ElementId">Current XAML target ID.</param>
/// <param name="RequiresExtensionProvider">Whether the link requires an extension provider ID.</param>
/// <param name="Action">Action invoked after navigation.</param>
public sealed record SettingsLinkDestination(
    string LinkId,
    string PageTag,
    string? ElementId = null,
    bool RequiresExtensionProvider = false,
    SettingsLinkAction Action = SettingsLinkAction.None);
