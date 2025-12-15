// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Pages;

internal sealed class FancyZonesLayoutListItem : ListItem
{
    private readonly Lazy<Task<IconInfo?>> _iconLoadTask;
    private int _isLoadingIcon;

    public override IIconInfo? Icon
    {
        get
        {
            if (Interlocked.Exchange(ref _isLoadingIcon, 1) == 0)
            {
                _ = LoadIconAsync();
            }

            return base.Icon;
        }
        set => base.Icon = value;
    }

    public FancyZonesLayoutListItem(ICommand command, FancyZonesLayoutDescriptor layout, IconInfo fallbackIcon)
        : base(command)
    {
        Title = layout.Title;
        Subtitle = layout.Subtitle;
        Icon = fallbackIcon;

        _iconLoadTask = new Lazy<Task<IconInfo?>>(async () => await FancyZonesThumbnailRenderer.RenderLayoutIconAsync(layout));
    }

    private async Task LoadIconAsync()
    {
        try
        {
            var icon = await _iconLoadTask.Value;
            if (icon is not null)
            {
                Icon = icon;
            }
        }
        catch
        {
            // Ignore icon failures.
        }
    }
}
