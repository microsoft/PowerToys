// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.WinUI.Controls;
using ManagedCommon;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using ClipboardHelper = Microsoft.CommandPalette.Extensions.Toolkit.ClipboardHelper;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI.Settings;

/// <summary>Creates Settings deep-link menus for registered UI destinations.</summary>
public sealed class SettingsLinkContextMenuService
{
    private const string CopyGlyph = "\uE71B";
    private const string CopiedAnnouncementId = "SettingsLinkCopied";

    private readonly ICmdPalProtocolActivation _protocolActivation;
    private readonly ISettingsLinkResolver _settingsLinkResolver;
    private MenuFlyout? _activeMenu;

    /// <summary>Initializes the menu service.</summary>
    /// <param name="protocolActivation">Canonical URI generator.</param>
    /// <param name="settingsLinkResolver">Settings destination catalog.</param>
    public SettingsLinkContextMenuService(
        ICmdPalProtocolActivation protocolActivation,
        ISettingsLinkResolver settingsLinkResolver)
    {
        ArgumentNullException.ThrowIfNull(protocolActivation);
        ArgumentNullException.ThrowIfNull(settingsLinkResolver);

        _protocolActivation = protocolActivation;
        _settingsLinkResolver = settingsLinkResolver;
    }

    /// <summary>Shows a copy-link menu for the nearest registered destination.</summary>
    /// <param name="args">Context request.</param>
    /// <param name="activePage">Current Settings frame page.</param>
    /// <returns><see langword="true"/> when a menu was shown.</returns>
    public bool TryShow(ContextRequestedEventArgs args, Page? activePage)
    {
        ArgumentNullException.ThrowIfNull(args);

        try
        {
            if (args.OriginalSource is not DependencyObject source ||
                !TryCreateLink(source, activePage, out var target, out var route, out var itemText))
            {
                return false;
            }

            var uri = _protocolActivation.CreateUri(route);
            var menuItem = new MenuFlyoutItem
            {
                Text = itemText,
                Icon = new FontIcon { Glyph = CopyGlyph },
            };
            menuItem.Click += (_, _) => CopyLink(uri, target);

            Hide();
            var menu = new MenuFlyout();
            menu.Items.Add(menuItem);
            menu.Closed += Menu_Closed;
            _activeMenu = menu;

            if (!args.TryGetPosition(target, out var position))
            {
                position = new(0, target.ActualHeight);
            }

            menu.ShowAt(target, new FlyoutShowOptions
            {
                ShowMode = FlyoutShowMode.Standard,
                Position = position,
            });
            return true;
        }
        catch (Exception ex)
        {
            Hide();
            Logger.LogError("Failed to show the settings link context menu.", ex);
            return false;
        }
    }

    internal void Hide()
    {
        var menu = _activeMenu;
        _activeMenu = null;
        if (menu is null)
        {
            return;
        }

        menu.Closed -= Menu_Closed;
        try
        {
            menu.Hide();
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to hide the settings link context menu.", ex);
        }
    }

    private void Menu_Closed(object? sender, object args)
    {
        if (_activeMenu is { } menu && ReferenceEquals(sender, menu))
        {
            menu.Closed -= Menu_Closed;
            _activeMenu = null;
        }
    }

    private bool TryCreateLink(
        DependencyObject source,
        Page? activePage,
        [NotNullWhen(true)] out FrameworkElement? target,
        [NotNullWhen(true)] out CmdPalProtocolRoute? route,
        out string itemText)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (activePage is ExtensionsPage &&
                current is SettingsCard { DataContext: ProviderSettingsViewModel provider } providerCard &&
                TryCreateSettingsRoute(
                    SettingsPageTags.Extensions,
                    elementId: null,
                    providerId: provider.ProviderId,
                    out route))
            {
                target = providerCard;
                itemText = RS_.GetString("SettingsLink_CopyExtensionSettings_Name");
                return true;
            }

            if (current is FrameworkElement element)
            {
                var targetId = SettingsPageTarget.GetId(element);
                if (targetId.Length > 0 && TryCreateTargetRoute(activePage, targetId, out route))
                {
                    target = element;
                    itemText = GetTargetItemText(route);
                    return true;
                }

                if (element is NavigationViewItem navigationItem &&
                    TryCreateNavigationRoute(navigationItem, out route))
                {
                    target = navigationItem;
                    itemText = RS_.GetString("SettingsLink_CopyPage_Name");
                    return true;
                }
            }
        }

        target = null;
        route = null;
        itemText = string.Empty;
        return false;
    }

    private bool TryCreateTargetRoute(
        Page? activePage,
        string targetId,
        [NotNullWhen(true)] out CmdPalProtocolRoute? route)
    {
        if (activePage is ExtensionPage { ViewModel: { } provider })
        {
            return TryCreateSettingsRoute(
                SettingsPageTags.Extensions,
                targetId,
                provider.ProviderId,
                out route);
        }

        var pageTag = activePage switch
        {
            GeneralPage => SettingsPageTags.General,
            AppearancePage => SettingsPageTags.Appearance,
            ExtensionsPage => SettingsPageTags.Extensions,
            DockSettingsPage => SettingsPageTags.Dock,
            _ => null,
        };

        return TryCreateSettingsRoute(pageTag, targetId, providerId: null, out route);
    }

    private bool TryCreateNavigationRoute(
        NavigationViewItem navigationItem,
        [NotNullWhen(true)] out CmdPalProtocolRoute? route)
    {
        if (navigationItem.Tag is not string pageTag)
        {
            route = null;
            return false;
        }

        if (string.Equals(pageTag, SettingsPageTags.Gallery, StringComparison.Ordinal))
        {
            route = new CmdPalProtocolRoute.OpenSettings(new OpenSettingsMessage(SettingsPageTags.Gallery));
            return true;
        }

        return TryCreateSettingsRoute(pageTag, elementId: null, providerId: null, out route);
    }

    private bool TryCreateSettingsRoute(
        string? pageTag,
        string? elementId,
        string? providerId,
        [NotNullWhen(true)] out CmdPalProtocolRoute? route)
    {
        route = null;
        if (!_settingsLinkResolver.TryResolveDestination(
                pageTag,
                elementId,
                providerId is not null,
                out var destination))
        {
            return false;
        }

        route = new CmdPalProtocolRoute.OpenSettings(new OpenSettingsMessage(
            SettingsLinkId: destination.LinkId,
            ExtensionProviderId: providerId));
        return true;
    }

    private static void CopyLink(Uri uri, FrameworkElement target)
    {
        try
        {
            ClipboardHelper.SetText(uri.AbsoluteUri);
            UIHelper.AnnounceActionForAccessibility(
                target,
                RS_.GetString("SettingsLink_Copied_Announcement"),
                CopiedAnnouncementId);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to copy the settings link.", ex);
        }
    }

    private static string GetTargetItemText(CmdPalProtocolRoute route)
    {
        if (route is CmdPalProtocolRoute.OpenSettings settings &&
            string.Equals(
                settings.Message.SettingsLinkId,
                SettingsLinkIds.Extensions.FallbackOrder,
                StringComparison.Ordinal))
        {
            return RS_.GetString("SettingsLink_CopyFallbackOrder_Name");
        }

        return RS_.GetString("SettingsLink_CopySetting_Name");
    }
}
