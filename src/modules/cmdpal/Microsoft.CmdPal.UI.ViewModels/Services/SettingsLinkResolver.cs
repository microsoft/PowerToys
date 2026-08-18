// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CmdPal.UI.Messages;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>Default settings link compatibility catalog.</summary>
public sealed class SettingsLinkResolver : ISettingsLinkResolver
{
    private static readonly SettingsLinkDestination[] KnownDestinations =
    [
        new(SettingsLinkIds.General.Page, SettingsPageTags.General),
        CreateTarget(SettingsLinkIds.General.Activation, SettingsPageTags.General, "activation-section"),
        CreateTarget(SettingsLinkIds.General.ActivationKey, SettingsPageTags.General, "activation-key"),
        CreateTarget(SettingsLinkIds.General.AutoGoHome, SettingsPageTags.General, "auto-go-home"),
        CreateTarget(SettingsLinkIds.General.KeepPreviousQuery, SettingsPageTags.General, "keep-previous-query"),
        CreateTarget(SettingsLinkIds.General.HighlightSearch, SettingsPageTags.General, "highlight-search"),
        CreateTarget(SettingsLinkIds.General.AppBehavior, SettingsPageTags.General, "behavior-section"),
        CreateTarget(SettingsLinkIds.General.SystemTrayIcon, SettingsPageTags.General, "system-tray-icon"),
        CreateTarget(SettingsLinkIds.General.AltF4, SettingsPageTags.General, "alt-f4"),
        CreateTarget(SettingsLinkIds.General.ExternalLinks, SettingsPageTags.General, "external-links-section"),
        CreateTarget(SettingsLinkIds.General.ExternalCommandLinks, SettingsPageTags.General, "external-command-links"),
        CreateTarget(SettingsLinkIds.General.AboutSection, SettingsPageTags.General, "about-section"),
        CreateTarget(SettingsLinkIds.General.About, SettingsPageTags.General, "about"),
        CreateTarget(SettingsLinkIds.General.SendFeedback, SettingsPageTags.General, "send-feedback"),

        new(SettingsLinkIds.Appearance.Page, SettingsPageTags.Appearance),
        CreateTarget(SettingsLinkIds.Appearance.Visuals, SettingsPageTags.Appearance, "visuals-section"),
        CreateTarget(SettingsLinkIds.Appearance.Theme, SettingsPageTags.Appearance, "theme"),
        CreateTarget(SettingsLinkIds.Appearance.Backdrop, SettingsPageTags.Appearance, "backdrop"),
        CreateTarget(SettingsLinkIds.Appearance.Background, SettingsPageTags.Appearance, "background"),
        CreateTarget(SettingsLinkIds.Appearance.DisableAnimations, SettingsPageTags.Appearance, "disable-animations"),
        CreateTarget(SettingsLinkIds.Appearance.Layout, SettingsPageTags.Appearance, "layout-section"),
        CreateTarget(SettingsLinkIds.Appearance.CompactMode, SettingsPageTags.Appearance, "compact-mode"),
        CreateTarget(SettingsLinkIds.Appearance.LaunchPosition, SettingsPageTags.Appearance, "launch-position"),
        CreateTarget(SettingsLinkIds.Appearance.ToastPosition, SettingsPageTags.Appearance, "toast-position"),
        CreateTarget(SettingsLinkIds.Appearance.Interaction, SettingsPageTags.Appearance, "interaction-section"),
        CreateTarget(SettingsLinkIds.Appearance.SingleClickActivation, SettingsPageTags.Appearance, "single-click-activation"),
        CreateTarget(SettingsLinkIds.Appearance.ShowAppDetails, SettingsPageTags.Appearance, "show-app-details"),
        CreateTarget(SettingsLinkIds.Appearance.BackspaceGoesBack, SettingsPageTags.Appearance, "backspace-goes-back"),
        CreateTarget(SettingsLinkIds.Appearance.EscapeKeyBehavior, SettingsPageTags.Appearance, "escape-key-behavior"),

        new(SettingsLinkIds.Extensions.Page, SettingsPageTags.Extensions),
        CreateTarget(SettingsLinkIds.Extensions.Search, SettingsPageTags.Extensions, "search"),
        CreateTarget(
            SettingsLinkIds.Extensions.FallbackOrder,
            SettingsPageTags.Extensions,
            "more",
            action: SettingsLinkAction.OpenFallbackOrder),
        CreateTarget(SettingsLinkIds.Extensions.More, SettingsPageTags.Extensions, "more"),
        CreateTarget(SettingsLinkIds.Extensions.Providers, SettingsPageTags.Extensions, "providers"),

        new(
            SettingsLinkIds.Extensions.Extension.Page,
            SettingsPageTags.Extensions,
            RequiresExtensionProvider: true),
        CreateTarget(
            SettingsLinkIds.Extensions.Extension.Enabled,
            SettingsPageTags.Extensions,
            "enabled",
            requiresExtensionProvider: true),
        CreateTarget(
            SettingsLinkIds.Extensions.Extension.SearchWeight,
            SettingsPageTags.Extensions,
            "search-weight",
            requiresExtensionProvider: true),
        CreateTarget(
            SettingsLinkIds.Extensions.Extension.Commands,
            SettingsPageTags.Extensions,
            "commands",
            requiresExtensionProvider: true),
        CreateTarget(
            SettingsLinkIds.Extensions.Extension.Fallbacks,
            SettingsPageTags.Extensions,
            "fallbacks",
            requiresExtensionProvider: true),
        CreateTarget(
            SettingsLinkIds.Extensions.Extension.Settings,
            SettingsPageTags.Extensions,
            "settings",
            requiresExtensionProvider: true),

        new(SettingsLinkIds.Dock.Page, SettingsPageTags.Dock),
        CreateTarget(SettingsLinkIds.Dock.Enabled, SettingsPageTags.Dock, "enabled"),
        CreateTarget(SettingsLinkIds.Dock.AppearanceSection, SettingsPageTags.Dock, "appearance-section"),
        CreateTarget(SettingsLinkIds.Dock.Position, SettingsPageTags.Dock, "position"),
        CreateTarget(SettingsLinkIds.Dock.Size, SettingsPageTags.Dock, "size"),
        CreateTarget(SettingsLinkIds.Dock.Theme, SettingsPageTags.Dock, "theme"),
        CreateTarget(SettingsLinkIds.Dock.Backdrop, SettingsPageTags.Dock, "backdrop"),
        CreateTarget(SettingsLinkIds.Dock.Background, SettingsPageTags.Dock, "background"),
        CreateTarget(SettingsLinkIds.Dock.BehaviorSection, SettingsPageTags.Dock, "behavior-section"),
        CreateTarget(SettingsLinkIds.Dock.AlwaysOnTop, SettingsPageTags.Dock, "always-on-top"),
        CreateTarget(SettingsLinkIds.Dock.AutoHide, SettingsPageTags.Dock, "auto-hide"),
        CreateTarget(SettingsLinkIds.Dock.Monitors, SettingsPageTags.Dock, "monitors"),
    ];

    private readonly IReadOnlyDictionary<string, SettingsLinkDestination> _destinations;

    internal static ReadOnlySpan<SettingsLinkDestination> Destinations => KnownDestinations;

    public SettingsLinkResolver()
    {
        var destinations = new Dictionary<string, SettingsLinkDestination>(StringComparer.OrdinalIgnoreCase);
        foreach (var destination in KnownDestinations)
        {
            destinations.Add(destination.LinkId, destination);
        }

        _destinations = destinations;
    }

    /// <inheritdoc/>
    public SettingsLinkResolution Resolve(string? linkId, string? extensionProviderId)
    {
        if (!TryResolve(linkId, out var destination))
        {
            if (linkId?.StartsWith($"{SettingsLinkIds.Extensions.Extension.Page}/", StringComparison.OrdinalIgnoreCase) == true)
            {
                return string.IsNullOrWhiteSpace(extensionProviderId)
                    ? new(_destinations[SettingsLinkIds.Extensions.Page], SettingsLinkFallback.UnknownProvider)
                    : new(_destinations[SettingsLinkIds.Extensions.Extension.Page], SettingsLinkFallback.UnknownLink);
            }

            return new(_destinations[SettingsLinkIds.General.Page], SettingsLinkFallback.UnknownLink);
        }

        if (destination.RequiresExtensionProvider && string.IsNullOrWhiteSpace(extensionProviderId))
        {
            return new(_destinations[SettingsLinkIds.Extensions.Page], SettingsLinkFallback.UnknownProvider);
        }

        return new(destination, SettingsLinkFallback.None);
    }

    /// <inheritdoc/>
    public SettingsLinkFallback ClassifyUnavailableTarget(bool isHidden, bool extensionDisabled)
        => isHidden
            ? SettingsLinkFallback.HiddenTarget
            : extensionDisabled
                ? SettingsLinkFallback.DisabledProvider
                : SettingsLinkFallback.MissingTarget;

    /// <inheritdoc/>
    public bool TryResolve(
        string? linkId,
        [NotNullWhen(true)] out SettingsLinkDestination? destination)
    {
        destination = null;
        return linkId is not null && _destinations.TryGetValue(linkId, out destination);
    }

    /// <inheritdoc/>
    public bool TryResolveDestination(
        string? pageTag,
        string? elementId,
        bool requiresExtensionProvider,
        [NotNullWhen(true)] out SettingsLinkDestination? destination)
    {
        foreach (var candidate in KnownDestinations)
        {
            if (candidate.RequiresExtensionProvider == requiresExtensionProvider &&
                string.Equals(candidate.PageTag, pageTag, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.ElementId, elementId, StringComparison.OrdinalIgnoreCase))
            {
                destination = candidate;
                return true;
            }
        }

        destination = null;
        return false;
    }

    private static SettingsLinkDestination CreateTarget(
        string linkId,
        string pageTag,
        string elementId,
        bool requiresExtensionProvider = false,
        SettingsLinkAction action = SettingsLinkAction.None)
        => new(linkId, pageTag, elementId, requiresExtensionProvider, action);
}
