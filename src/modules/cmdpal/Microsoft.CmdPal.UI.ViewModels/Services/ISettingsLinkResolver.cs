// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>Maps stable settings link IDs to the current UI layout.</summary>
/// <remarks>UI reorganization changes destinations, not existing link IDs.</remarks>
public interface ISettingsLinkResolver
{
    /// <summary>Resolves a settings link to an available destination and reports any fallback.</summary>
    /// <param name="linkId">Public settings link ID.</param>
    /// <param name="extensionProviderId">Provider ID for extension settings links.</param>
    /// <returns>The destination to open and the reason for a redirect, if any.</returns>
    SettingsLinkResolution Resolve(string? linkId, string? extensionProviderId);

    /// <summary>Classifies a target that could not be reached on its settings page.</summary>
    SettingsLinkFallback ClassifyUnavailableTarget(bool isHidden, bool extensionDisabled);

    /// <summary>Resolves a case-insensitive link ID and returns its canonical destination.</summary>
    /// <param name="linkId">Public settings link ID.</param>
    /// <param name="destination">Canonical link ID and current UI destination.</param>
    /// <returns><see langword="true"/> when the link ID is registered.</returns>
    bool TryResolve(
        string? linkId,
        [NotNullWhen(true)] out SettingsLinkDestination? destination);

    /// <summary>Resolves a current UI location to its first registered public link.</summary>
    /// <param name="pageTag">Current NavigationView page tag.</param>
    /// <param name="elementId">Current XAML target ID, or <see langword="null"/> for the page.</param>
    /// <param name="requiresExtensionProvider">Whether the location belongs to an extension provider.</param>
    /// <param name="destination">Preferred public link and current UI destination.</param>
    /// <returns><see langword="true"/> when the UI location is registered.</returns>
    bool TryResolveDestination(
        string? pageTag,
        string? elementId,
        bool requiresExtensionProvider,
        [NotNullWhen(true)] out SettingsLinkDestination? destination);
}
