// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;
using PowerToysExtension.Modules;

namespace PowerToysExtension.Pages;

internal sealed partial class WorkspacesListPage : DynamicListPage
{
    private readonly CommandItem _emptyMessage;

    public WorkspacesListPage()
    {
        Icon = PowerToysResourcesHelper.IconFromSettingsIcon("Workspaces.png");
        Name = Title = "Workspaces";
        Id = "com.microsoft.cmdpal.powertoys.workspaces";
        SettingsChangeNotifier.SettingsChanged += OnSettingsChanged;
        _emptyMessage = new CommandItem()
        {
            Icon = PowerToysResourcesHelper.IconFromSettingsIcon("Workspaces.png"),
            Title = "No workspaces found",
            Subtitle = SearchText,
        };
        EmptyContent = _emptyMessage;
    }

    private void OnSettingsChanged()
    {
        RaiseItemsChanged(0);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _emptyMessage.Subtitle = newSearch;
        RaiseItemsChanged(0);
    }

    public override IListItem[] GetItems() => [.. new WorkspacesModuleCommandProvider().BuildCommands()];
}
