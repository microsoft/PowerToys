// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CmdPal.Ext.Apps.Commands;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

public sealed partial class AppListItem : ListItem
{
    private static readonly Tag _appTag = new("App");

    private readonly AppCommand _appCommand;
    private readonly AppItem _app;
    private readonly Lazy<Details> _details;
    private readonly Lazy<Task<IconInfo?>> _iconLoadTask;

    private InterlockedBoolean _isLoadingIcon;

    public override IDetails? Details { get => _details.Value; set => base.Details = value; }

    public override IIconInfo? Icon
    {
        get
        {
            if (_isLoadingIcon.Set())
            {
                _ = LoadIconAsync();
            }

            return base.Icon;
        }
        set => base.Icon = value;
    }

    public string AppIdentifier => _app.AppIdentifier;

    public AppListItem(AppItem app, bool useThumbnails, bool isPinned)
    {
        Command = _appCommand = new AppCommand(app);
        _app = app;
        Title = app.Name;
        Subtitle = app.Subtitle;
        Tags = [_appTag];
        Icon = Icons.GenericAppIcon;

        MoreCommands = AddPinCommands(_app.Commands!, isPinned);

        _details = new Lazy<Details>(() =>
        {
            var t = BuildDetails();
            t.Wait();
            return t.Result;
        });

        _iconLoadTask = new Lazy<Task<IconInfo?>>(async () => await FetchIcon(useThumbnails));
    }

    private async Task LoadIconAsync()
    {
        try
        {
            Icon = _appCommand.Icon = await _iconLoadTask.Value ?? Icons.GenericAppIcon;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to load icon for {AppIdentifier}\n{ex}");
        }
    }

    private async Task<Details> BuildDetails()
    {
        // Build metadata, with app type, path, etc.
        var metadata = new List<DetailsElement>();
        metadata.Add(new DetailsElement() { Key = "Type", Data = new DetailsTags() { Tags = [new Tag(_app.Type)] } });
        if (!_app.IsPackaged)
        {
            metadata.Add(new DetailsElement() { Key = "Path", Data = new DetailsLink() { Text = _app.ExePath } });
        }

        // Icon
        IconInfo? heroImage = null;
        if (_app.IsPackaged)
        {
            heroImage = new IconInfo(_app.IcoPath);
        }
        else
        {
            try
            {
                var stream = await ThumbnailHelper.GetThumbnail(_app.ExePath, true);
                if (stream is not null)
                {
                    heroImage = IconInfo.FromStream(stream);
                }
            }
            catch (Exception)
            {
                // do nothing if we fail to load an icon.
                // Logging it would be too NOISY, there's really no need.
            }
        }

        return new Details()
        {
            Title = this.Title,
            HeroImage = heroImage ?? this.Icon ?? Icons.GenericAppIcon,
            Metadata = metadata.ToArray(),
        };
    }

    private async Task<IconInfo> FetchIcon(bool useThumbnails)
    {
        IconInfo? icon = null;
        if (_app.IsPackaged)
        {
            icon = new IconInfo(_app.IcoPath);
            return icon;
        }

        if (useThumbnails)
        {
            try
            {
                var stream = await ThumbnailHelper.GetThumbnail(_app.ExePath);
                if (stream is not null)
                {
                    icon = IconInfo.FromStream(stream);
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Failed to load icon for {AppIdentifier}:\n{ex}");
            }

            icon = icon ?? new IconInfo(_app.IcoPath);
        }
        else
        {
            icon = new IconInfo(_app.IcoPath);
        }

        return icon;
    }

    private IContextItem[] AddPinCommands(List<IContextItem> commands, bool isPinned)
    {
        var newCommands = new List<IContextItem>();
        newCommands.AddRange(commands);

        newCommands.Add(new Separator());

        if (isPinned)
        {
            newCommands.Add(
                new CommandContextItem(
                    new UnpinAppCommand(this.AppIdentifier))
                {
                    RequestedShortcut = KeyChords.TogglePin,
                });
        }
        else
        {
            newCommands.Add(
                new CommandContextItem(
                    new PinAppCommand(this.AppIdentifier))
                {
                    RequestedShortcut = KeyChords.TogglePin,
                });
        }

        return newCommands.ToArray();
    }
}
