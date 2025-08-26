// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CmdPal.Ext.Apps.State;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps;

public partial class AllAppsCommandProvider : CommandProvider
{
    public const string WellKnownId = "AllApps";

    public static readonly AllAppsPage Page = new();

    private readonly AllAppsPage _page;
    private readonly CommandItem _listItem;

    public AllAppsCommandProvider()
        : this(Page)
    {
    }

    public AllAppsCommandProvider(AllAppsPage page)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        Id = WellKnownId;
        DisplayName = Resources.installed_apps;
        Icon = Icons.AllAppsIcon;
        Settings = AllAppsSettings.Instance.Settings;

        _listItem = new(_page)
        {
            Subtitle = Resources.search_installed_apps,
            MoreCommands = [new CommandContextItem(AllAppsSettings.Instance.Settings.SettingsPage)],
        };

        // Subscribe to pin state changes to refresh the command provider
        PinnedAppsManager.Instance.PinStateChanged += OnPinStateChanged;
    }

    public override ICommandItem[] TopLevelCommands() => [_listItem, .._page.GetPinnedApps()];

    public ICommandItem? LookupApp(string displayName)
    {
        var items = _page.GetItems();

        // We're going to do this search in two directions:
        // First, is this name a substring of any app...
        var nameMatches = items.Where(i => i.Title.Contains(displayName));

        // ... Then, does any app have this name as a substring ...
        // Only get one of these - "Terminal Preview" contains both "Terminal" and "Terminal Preview", so just take the best one
        var appMatches = items.Where(i => displayName.Contains(i.Title)).OrderByDescending(i => i.Title.Length).Take(1);

        // ... Now, combine those two
        var both = nameMatches.Concat(appMatches);

        if (both.Count() == 1)
        {
            return both.First();
        }
        else if (nameMatches.Count() == 1 && appMatches.Count() == 1)
        {
            if (nameMatches.First() == appMatches.First())
            {
                return nameMatches.First();
            }
        }

        return null;
    }

    private void OnPinStateChanged(object? sender, System.EventArgs e)
    {
        RaiseItemsChanged(0);
    }
}
