// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

public partial class AllAppsCommandProvider : CommandProvider
{
    public static readonly AllAppsPage Page = new();

    private readonly CommandItem _listItem;

    public AllAppsCommandProvider()
    {
        Id = "AllApps";
        DisplayName = "Installed apps";
        Icon = new IconInfo("\ue71d");

        _listItem = new(Page) { Subtitle = "Search installed apps" };
    }

    public override ICommandItem[] TopLevelCommands() => [_listItem];

    public ICommandItem? LookupApp(string displayName)
    {
        var items = Page.GetItems();

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
}
