// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

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

        return item.IdentityAliases.Any(_settings.IsAppHidden);
    }

    public bool SetHidden(AppCatalogItem item, bool hidden)
    {
        ArgumentNullException.ThrowIfNull(item);

        var changed = _settings.SetAppHidden(item.Identity, hidden);
        if (!hidden)
        {
            foreach (var identity in item.IdentityAliases)
            {
                changed |= _settings.SetAppHidden(identity, hidden: false);
            }
        }

        return changed;
    }

    public void Persist()
    {
        _settings.SaveSettings();
    }
}
