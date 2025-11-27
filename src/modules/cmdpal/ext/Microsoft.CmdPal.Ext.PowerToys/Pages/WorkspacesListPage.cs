// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Pages;

internal sealed partial class WorkspacesListPage : DynamicListPage
{
    private readonly CommandItem _emptyMessage;

    public WorkspacesListPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\Workspaces.png");
        Name = Title = "Workspaces";
        Id = "com.microsoft.cmdpal.powertoys.workspaces";
        _emptyMessage = new CommandItem()
        {
            Icon = IconHelpers.FromRelativePath("Assets\\Workspaces.png"),
            Title = "No workspaces found",
            Subtitle = SearchText,
        };
        EmptyContent = _emptyMessage;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _emptyMessage.Subtitle = newSearch;
        RaiseItemsChanged(0);
    }

    public override IListItem[] GetItems() => WorkspaceItemsHelper.FilteredItems(SearchText);
}
