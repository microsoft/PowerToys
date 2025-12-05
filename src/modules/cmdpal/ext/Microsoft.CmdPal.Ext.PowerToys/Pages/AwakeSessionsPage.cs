// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Pages;

internal sealed partial class AwakeSessionsPage : DynamicListPage
{
    private readonly CommandItem _emptyContent;

    public AwakeSessionsPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\Awake.png");
        Title = "Awake actions";
        Name = "AwakeActions";
        Id = "com.microsoft.powertoys.awake.actions";

        _emptyContent = new CommandItem()
        {
            Title = "No Awake actions",
            Subtitle = "Try a different search phrase.",
            Icon = IconHelpers.FromRelativePath("Assets\\Awake.png"),
        };

        EmptyContent = _emptyContent;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _emptyContent.Subtitle = string.IsNullOrWhiteSpace(newSearch)
            ? "Try a different search phrase."
            : $"No Awake actions matching '{newSearch}'";

        RaiseItemsChanged(0);
    }

    public override IListItem[] GetItems()
    {
        return AwakeCommandsFactory.GetSessionItems(SearchText);
    }
}
