// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Pages;

internal sealed partial class WorkspacesListPage : DynamicListPage
{
    private readonly CommandItem _emptyContent;

    public WorkspacesListPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\Workspaces.png");
        Title = "Workspaces";
        Name = "Workspaces";
        Id = "com.microsoft.powertoys.workspaces";

        _emptyContent = new CommandItem()
        {
            Title = "No workspaces found",
            Subtitle = "Create a workspace in PowerToys to get started.",
            Icon = IconHelpers.FromRelativePath("Assets\\Workspaces.png"),
        };

        EmptyContent = _emptyContent;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _emptyContent.Subtitle = string.IsNullOrWhiteSpace(newSearch)
            ? "Create a workspace in PowerToys to get started."
            : $"No workspaces matching '{newSearch}'";

        RaiseItemsChanged(0);
    }

    public override IListItem[] GetItems()
    {
        return WorkspaceItemsHelper.GetWorkspaceItems(SearchText);
    }
}
