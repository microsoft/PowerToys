// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RaycastStore.Pages;

internal sealed partial class InstalledExtensionsPage : DynamicListPage
{
    private readonly InstalledExtensionTracker _tracker;

    public InstalledExtensionsPage(InstalledExtensionTracker tracker)
    {
        _tracker = tracker;
        Icon = Icons.InstalledIcon;
        Name = "Installed Raycast Extensions";
        Title = "Installed Raycast Extensions";
        PlaceholderText = "Filter installed extensions...";
        ShowDetails = true;
        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Icon = Icons.ExtensionIcon,
            Title = "No Raycast extensions installed",
            Subtitle = "Browse and install extensions from the Raycast store",
        };
    }

    public override IListItem[] GetItems()
    {
        _tracker.Refresh();
        IReadOnlyList<InstalledRaycastExtension> installed = _tracker.GetInstalledExtensions();
        if (installed.Count == 0)
        {
            return Array.Empty<IListItem>();
        }

        List<IListItem> list = new();
        var filter = SearchText?.Trim() ?? string.Empty;

        foreach (InstalledRaycastExtension ext in installed)
        {
            if (string.IsNullOrEmpty(filter) ||
                ext.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                ext.RaycastName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new InstalledExtensionListItem(ext, _tracker, OnUninstallComplete));
            }
        }

        return list.ToArray();
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (oldSearch != newSearch)
        {
            RaiseItemsChanged();
        }
    }

    private void OnUninstallComplete()
    {
        RaiseItemsChanged();
    }
}
