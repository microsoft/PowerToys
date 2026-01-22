// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Pages;

internal sealed partial class FancyZonesLayoutListItem : ListItem
{
    private readonly Lazy<Task<IconInfo?>> _iconLoadTask;
    private readonly string _layoutId;
    private readonly string _layoutTitle;

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
        _layoutId = layout.Id;
        _layoutTitle = layout.Title;

        _iconLoadTask = new Lazy<Task<IconInfo?>>(async () => await FancyZonesThumbnailRenderer.RenderLayoutIconAsync(layout));
    }

    private async Task LoadIconAsync()
    {
        try
        {
            Logger.LogDebug($"FancyZones layout icon load starting. LayoutId={_layoutId} Title=\"{_layoutTitle}\"");
            var icon = await _iconLoadTask.Value;
            if (icon is not null)
            {
                Icon = icon;
                Logger.LogDebug($"FancyZones layout icon load succeeded. LayoutId={_layoutId} Title=\"{_layoutTitle}\"");
            }
            else
            {
                Logger.LogDebug($"FancyZones layout icon load returned null. LayoutId={_layoutId} Title=\"{_layoutTitle}\"");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"FancyZones layout icon load failed. LayoutId={_layoutId} Title=\"{_layoutTitle}\" Exception={ex}");
        }
    }
}
