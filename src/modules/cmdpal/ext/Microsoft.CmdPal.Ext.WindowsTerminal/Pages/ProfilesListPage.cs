// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ManagedCommon;
using Microsoft.CmdPal.Ext.WindowsTerminal.Commands;
using Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;
using Microsoft.CmdPal.Ext.WindowsTerminal.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Pages;

internal sealed partial class ProfilesListPage : ListPage, INotifyItemsChanged
{
    event TypedEventHandler<object, IItemsChangedEventArgs> INotifyItemsChanged.ItemsChanged
    {
        add
        {
            ItemsChanged += value;
            EnsureInitialized();
            SelectTerminalFilter();
        }

        remove
        {
            ItemsChanged -= value;
        }
    }

    private readonly TerminalQuery _terminalQuery = new();
    private readonly SettingsManager _terminalSettings;
    private readonly AppSettingsManager _appSettingsManager;

    private bool showHiddenProfiles;
    private bool openNewTab;
    private bool openQuake;

    private bool initialized;
    private TerminalChannelFilters? terminalFilters;

    public ProfilesListPage(SettingsManager terminalSettings, AppSettingsManager appSettingsManager)
    {
        Icon = Icons.TerminalIcon;
        Name = Resources.profiles_list_page_name;
        _terminalSettings = terminalSettings;
        _terminalSettings.Settings.SettingsChanged += Settings_SettingsChanged;
        _appSettingsManager = appSettingsManager;
    }

    private void Settings_SettingsChanged(object sender, Settings args)
    {
        EnsureInitialized();
        RaiseItemsChanged();
    }

    private List<ListItem> Query()
    {
        EnsureInitialized();

        showHiddenProfiles = _terminalSettings.ShowHiddenProfiles;
        openNewTab = _terminalSettings.OpenNewTab;
        openQuake = _terminalSettings.OpenQuake;

        var profiles = _terminalQuery.GetProfiles()!;

        switch (_terminalSettings.ProfileSortOrder)
        {
            case ProfileSortOrder.MostRecentlyUsed:
                var mru = _appSettingsManager.Current.RecentlyUsedProfiles ?? [];
                profiles = profiles.OrderBy(p =>
                    {
                        var key = new TerminalProfileKey(p.Terminal?.AppUserModelId ?? string.Empty, p.Name ?? string.Empty);
                        var index = mru.IndexOf(key);
                        return index == -1 ? int.MaxValue : index;
                    })
                    .ThenBy(static p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
                break;
            case ProfileSortOrder.Default:
            case ProfileSortOrder.Alphabetical:
            default:
                profiles = profiles.OrderBy(static p => p.Name, StringComparer.CurrentCultureIgnoreCase);
                break;
        }

        if (terminalFilters?.IsAllSelected == false)
        {
            profiles = profiles.Where(profile => profile.Terminal.AppUserModelId == terminalFilters.CurrentFilterId);
        }

        var result = new List<ListItem>();

        foreach (var profile in profiles)
        {
            if (profile.Hidden && !showHiddenProfiles)
            {
                continue;
            }

            result.Add(new ListItem(new LaunchProfileCommand(profile.Terminal.AppUserModelId, profile.Name, profile.Terminal.LogoPath, openNewTab, openQuake, _appSettingsManager))
            {
                Title = profile.Name,
                Subtitle = profile.Terminal.DisplayName,
                MoreCommands = [
                    new CommandContextItem(new LaunchProfileAsAdminCommand(profile.Terminal.AppUserModelId, profile.Name, openNewTab, openQuake, _appSettingsManager)),
                ],
            });
        }

        return result;
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        var terminals = _terminalQuery.GetTerminals();
        terminalFilters = new TerminalChannelFilters(terminals);
        terminalFilters.PropChanged += TerminalFiltersOnPropChanged;
        SelectTerminalFilter();
        Filters = terminalFilters;
        initialized = true;
    }

    private void SelectTerminalFilter()
    {
        Trace.Assert(terminalFilters != null);

        // Select the preferred channel if it exists; we always select the preferred channel,
        // but user have an option to save the preferred channel when he changes the filter
        if (_terminalSettings.SaveLastSelectedChannel)
        {
            if (!string.IsNullOrWhiteSpace(_appSettingsManager.Current.LastSelectedChannel) &&
                terminalFilters.ContainsFilter(_appSettingsManager.Current.LastSelectedChannel))
            {
                terminalFilters.CurrentFilterId = _appSettingsManager.Current.LastSelectedChannel;
            }
        }
        else
        {
            terminalFilters.CurrentFilterId = TerminalChannelFilters.AllTerminalsFilterId;
        }
    }

    private void TerminalFiltersOnPropChanged(object sender, IPropChangedEventArgs args)
    {
        Trace.Assert(terminalFilters != null);

        RaiseItemsChanged();
        _appSettingsManager.Current.LastSelectedChannel = terminalFilters.CurrentFilterId;
        _appSettingsManager.Save();
    }

    public override IListItem[] GetItems()
    {
        try
        {
            return [.. Query()];
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to list Windows Terminal profiles", ex);
            throw;
        }
    }
}
