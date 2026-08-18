// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Messages;

/// <summary>Settings-window navigation request.</summary>
/// <param name="SettingsPageTag">NavigationView page tag.</param>
/// <param name="ExtensionGalleryId">Optional gallery extension ID.</param>
/// <param name="SettingsLinkId">Optional stable settings link ID.</param>
/// <param name="ExtensionProviderId">Optional extension-settings provider ID.</param>
/// <remarks>Protocol routes set <paramref name="SettingsLinkId"/>; trusted UI navigation may set <paramref name="SettingsPageTag"/>.</remarks>
public record OpenSettingsMessage(
    string SettingsPageTag = "",
    string? ExtensionGalleryId = null,
    string? SettingsLinkId = null,
    string? ExtensionProviderId = null);
