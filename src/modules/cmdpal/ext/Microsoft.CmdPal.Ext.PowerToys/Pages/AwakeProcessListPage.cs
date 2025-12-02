// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Pages;

internal sealed partial class AwakeProcessListPage : DynamicListPage
{
    private readonly CommandItem _emptyContent;

    public AwakeProcessListPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\Awake.png");
        Title = "Bind Awake to process";
        Name = "AwakeProcessBinding";
        Id = "com.microsoft.powertoys.awake.processBinding";

        _emptyContent = new CommandItem()
        {
            Title = "No matching processes",
            Subtitle = "Try another search.",
            Icon = IconHelpers.FromRelativePath("Assets\\Awake.png"),
        };

        EmptyContent = _emptyContent;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _emptyContent.Subtitle = string.IsNullOrWhiteSpace(newSearch)
            ? "Try another search."
            : $"No processes matching '{newSearch}'";

        RaiseItemsChanged(0);
    }

    public override IListItem[] GetItems()
    {
        return AwakeCommandsFactory.GetProcessItems(SearchText);
    }
}
