// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Apps.Programs;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

internal sealed class SettingsAppVisibilityStore : IAppVisibilityStore
{
    private readonly AllAppsSettings _settings;

    public SettingsAppVisibilityStore(AllAppsSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public bool IsHidden(AppCatalogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        foreach (var hiddenApp in _settings.DisabledProgramSources)
        {
            if (string.Equals(hiddenApp.UniqueIdentifier, item.Identity, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public bool SetHidden(AppCatalogItem item, bool hidden)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (hidden)
        {
            if (IsHidden(item))
            {
                return false;
            }

            _settings.DisabledProgramSources.Add(new DisabledProgramSource { UniqueIdentifier = item.Identity });
            return true;
        }

        var changed = false;
        for (var index = _settings.DisabledProgramSources.Count - 1; index >= 0; index--)
        {
            if (string.Equals(
                _settings.DisabledProgramSources[index].UniqueIdentifier,
                item.Identity,
                StringComparison.OrdinalIgnoreCase))
            {
                _settings.DisabledProgramSources.RemoveAt(index);
                changed = true;
            }
        }

        return changed;
    }

    public void Persist() => _settings.SaveSettings();
}
