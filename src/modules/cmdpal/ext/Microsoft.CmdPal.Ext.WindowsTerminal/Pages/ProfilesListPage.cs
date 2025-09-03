// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
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
            // Invoked every time a page is loaded
            ItemsChanged += value;
            if (!initialized)
            {
                Initialize();
            }

            // Ensure the correct filter is selected
            SelectTerminalFilter();
        }
        remove => ItemsChanged -= value;
    }

    private readonly TerminalQuery _terminalQuery = new();
    private readonly SettingsManager _terminalSettings;

    private bool showHiddenProfiles;
    private bool openNewTab;
    private bool openQuake;

    private bool initialized;
    private TerminalChannelFilters _terminalFilters;

    public ProfilesListPage(SettingsManager terminalSettings)
    {
        Icon = Icons.TerminalIcon;
        Name = Resources.profiles_list_page_name;
        _terminalSettings = terminalSettings;
    }

    private List<ListItem> Query()
    {
        if (!initialized)
        {
            Initialize();
        }

        showHiddenProfiles = _terminalSettings.ShowHiddenProfiles;
        openNewTab = _terminalSettings.OpenNewTab;
        openQuake = _terminalSettings.OpenQuake;

        var profiles = _terminalQuery.GetProfiles();

        if (!_terminalFilters.IsAllSelected)
        {
            profiles = profiles.Where(profile => profile.Terminal.AppUserModelId == Filters.CurrentFilterId);
        }

        var result = new List<ListItem>();

        foreach (var profile in profiles)
        {
            if (profile.Hidden && !showHiddenProfiles)
            {
                continue;
            }

            result.Add(new ListItem(new LaunchProfileCommand(profile.Terminal.AppUserModelId, profile.Name, profile.Terminal.LogoPath, openNewTab, openQuake))
            {
                Title = profile.Name,
                Subtitle = profile.Terminal.DisplayName,
                MoreCommands = [
                    new CommandContextItem(new LaunchProfileAsAdminCommand(profile.Terminal.AppUserModelId, profile.Name, openNewTab, openQuake)),
                ],
            });
        }

        return result;
    }

    private void Initialize()
    {
        var terminals = _terminalQuery.GetTerminals().ToList();

        _terminalFilters = new TerminalChannelFilters(terminals);
        _terminalFilters.PropChanged += TerminalFiltersOnPropChanged;
        SelectTerminalFilter();
        Filters = _terminalFilters;
        initialized = true;
    }

    private void SelectTerminalFilter()
    {
        // Select the preferred channel if it exists; we always select the preferred channel,
        // but user have an option to save the preferred channel when he changes the filter
        if (!string.IsNullOrWhiteSpace(_terminalSettings.PreferredChannelAppId))
        {
            if (_terminalFilters.ContainsFilter(_terminalSettings.PreferredChannelAppId))
            {
                _terminalFilters.CurrentFilterId = _terminalSettings.PreferredChannelAppId;
            }
        }
        else
        {
            _terminalFilters.CurrentFilterId = TerminalChannelFilters.AllTerminalsFilterId;
        }
    }

    private void TerminalFiltersOnPropChanged(object sender, IPropChangedEventArgs args)
    {
        RaiseItemsChanged();
        if (_terminalSettings.SaveLastSelectedChannel)
        {
            _terminalSettings.PreferredChannelAppId = _terminalFilters.CurrentFilterId;
            _terminalSettings.SaveSettings();
        }
    }

    public override IListItem[] GetItems() => [.. Query()];
}
