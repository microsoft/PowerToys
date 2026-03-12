// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using KeyboardManager.ModuleServices;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Pages;

internal sealed partial class KeyboardManagerMappingsPage : DynamicListPage
{
    private readonly CommandItem _emptyMessage;
    private readonly IconInfo _icon;

    public KeyboardManagerMappingsPage()
    {
        _icon = PowerToysResourcesHelper.IconFromSettingsIcon("KeyboardManager.png");
        Icon = _icon;
        Name = Title = "Keyboard Manager mappings";
        Id = "com.microsoft.cmdpal.powertoys.keyboardManager.mappings";
        ShowDetails = true;
        _emptyMessage = new CommandItem
        {
            Title = "No Keyboard Manager mappings found",
            Subtitle = "Create mappings in Keyboard Manager to inspect them here.",
            Icon = _icon,
        };
        EmptyContent = _emptyMessage;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        RaiseItemsChanged(0);
    }

    public override IListItem[] GetItems()
    {
        var result = KeyboardManagerMappingService.Instance.GetMappingsAsync().GetAwaiter().GetResult();
        if (!result.Success || result.Value is null)
        {
            _emptyMessage.Subtitle = result.Error ?? "Failed to read Keyboard Manager mappings.";
            return Array.Empty<IListItem>();
        }

        IEnumerable<KeyboardManagerMappingRecord> mappings = result.Value;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            mappings = mappings.Where(mapping =>
                mapping.TriggerDisplay.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
                mapping.TargetDisplay.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
                mapping.Subtitle.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
                mapping.TargetApp.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase));
        }

        return mappings
            .OrderBy(mapping => mapping.TriggerDisplay, StringComparer.CurrentCultureIgnoreCase)
            .Select(mapping => (IListItem)new KeyboardManagerMappingListItem(mapping, _icon))
            .ToArray();
    }
}
