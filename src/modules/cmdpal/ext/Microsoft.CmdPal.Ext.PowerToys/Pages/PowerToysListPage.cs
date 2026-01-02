// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Pages;

internal sealed partial class PowerToysListPage : ListPage
{
    private readonly CommandItem _empty;

    public PowerToysListPage()
    {
        Icon = PowerToysResourcesHelper.IconFromSettingsIcon("PowerToys.png");
        Name = Title = "PowerToys";
        Id = "com.microsoft.cmdpal.powertoys";
        SettingsChangeNotifier.SettingsChanged += OnSettingsChanged;
        _empty = new CommandItem()
        {
            Icon = PowerToysResourcesHelper.IconFromSettingsIcon("PowerToys.png"),
            Title = "No matching module found",
            Subtitle = SearchText,
        };
        EmptyContent = _empty;
    }

    private void OnSettingsChanged()
    {
        RaiseItemsChanged(0);
    }

    public override IListItem[] GetItems() => ModuleCommandCatalog.GetAllItems();
}
