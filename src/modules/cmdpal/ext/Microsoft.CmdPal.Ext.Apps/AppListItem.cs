// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CmdPal.Ext.Apps.Commands;
using Microsoft.CmdPal.Ext.Apps.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

public sealed partial class AppListItem : ListItem
{
    private readonly AppCommand _appCommand;
    private readonly AppItem _app;
    private readonly Lazy<Task<Details>> _detailsLoadTask;
    private readonly Lazy<Task<IconInfo?>> _iconLoadTask;

    private InterlockedBoolean _isLoadingIcon;
    private InterlockedBoolean _isLoadingDetails;

    public override IDetails? Details
    {
        get
        {
            if (_isLoadingDetails.Set())
            {
                _ = LoadDetailsAsync();
            }

            return base.Details;
        }
        set => base.Details = value;
    }

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

    public AppItem App => _app;

    public AppListItem(AppItem app, bool useThumbnails, bool isPinned)
    {
        Command = _appCommand = new AppCommand(app);
        _app = app;
        Title = app.Name;
        Subtitle = app.Subtitle;
        Icon = Icons.GenericAppIcon;

        MoreCommands = AddPinCommands(_app.Commands!, isPinned);

        _detailsLoadTask = new Lazy<Task<Details>>(BuildDetails);
        _iconLoadTask = new Lazy<Task<IconInfo?>>(async () => await FetchIcon(useThumbnails).ConfigureAwait(false));
    }

    private async Task LoadDetailsAsync()
    {
        try
        {
            Details = await _detailsLoadTask.Value;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to load details for {AppIdentifier}\n{ex}");
        }
    }

    private async Task LoadIconAsync()
    {
        try
        {
            Icon = _appCommand.Icon = CoalesceIcon(await _iconLoadTask.Value);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to load icon for {AppIdentifier}\n{ex}");
        }
    }

    private static IconInfo CoalesceIcon(IconInfo? value)
    {
        return CoalesceIcon(value, Icons.GenericAppIcon)!;
    }

    private static IconInfo? CoalesceIcon(IconInfo? value, IconInfo? replacement)
    {
        return IconIsNullOrEmpty(value) ? replacement : value;
    }

    private static bool IconIsNullOrEmpty(IconInfo? value)
    {
        return value == null || (string.IsNullOrEmpty(value.Light?.Icon) && value.Light?.Data is null) || (string.IsNullOrEmpty(value.Dark?.Icon) && value.Dark?.Data is null);
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

#if DEBUG
        metadata.Add(new DetailsElement() { Key = "[DEBUG] AppIdentifier", Data = new DetailsLink() { Text = _app.AppIdentifier } });
        metadata.Add(new DetailsElement() { Key = "[DEBUG] ExePath", Data = new DetailsLink() { Text = _app.ExePath } });
        metadata.Add(new DetailsElement() { Key = "[DEBUG] IcoPath", Data = new DetailsLink() { Text = _app.IcoPath } });
        metadata.Add(new DetailsElement() { Key = "[DEBUG] JumboIconPath", Data = new DetailsLink() { Text = _app.JumboIconPath ?? "(null)" } });
#endif

        // Icon
        IconInfo? heroImage = null;
        if (_app.IsPackaged)
        {
            heroImage = new IconInfo(_app.JumboIconPath ?? _app.IcoPath);
        }
        else
        {
            // Get the icon from the system
            if (!string.IsNullOrEmpty(_app.JumboIconPath))
            {
                var randomAccessStream = await IconExtractor.GetIconStreamAsync(_app.JumboIconPath, 64);
                if (randomAccessStream != null)
                {
                    heroImage = IconInfo.FromStream(randomAccessStream);
                }
            }

            if (IconIsNullOrEmpty(heroImage) && !string.IsNullOrEmpty(_app.IcoPath))
            {
                var randomAccessStream = await IconExtractor.GetIconStreamAsync(_app.IcoPath, 64);
                if (randomAccessStream != null)
                {
                    heroImage = IconInfo.FromStream(randomAccessStream);
                }
            }

            // do nothing if we fail to load an icon.
            // Logging it would be too NOISY, there's really no need.
            if (IconIsNullOrEmpty(heroImage) && !string.IsNullOrEmpty(_app.JumboIconPath))
            {
                heroImage = await TryLoadThumbnail(_app.JumboIconPath, jumbo: true, logOnFailure: false);
            }

            if (IconIsNullOrEmpty(heroImage) && !string.IsNullOrEmpty(_app.IcoPath))
            {
                heroImage = await TryLoadThumbnail(_app.IcoPath, jumbo: true, logOnFailure: false);
            }

            if (IconIsNullOrEmpty(heroImage) && !string.IsNullOrEmpty(_app.ExePath))
            {
                heroImage = await TryLoadThumbnail(_app.ExePath, jumbo: true, logOnFailure: false);
            }
        }

        return new Details()
        {
            Title = this.Title,
            HeroImage = CoalesceIcon(CoalesceIcon(heroImage, this.Icon as IconInfo)),
            Metadata = [..metadata],
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
            if (!string.IsNullOrEmpty(_app.IcoPath))
            {
                icon = await TryLoadThumbnail(_app.IcoPath, jumbo: false, logOnFailure: true);
            }

            if (IconIsNullOrEmpty(icon) && !string.IsNullOrEmpty(_app.ExePath))
            {
                icon = await TryLoadThumbnail(_app.ExePath, jumbo: false, logOnFailure: true);
            }
        }

        icon ??= new IconInfo(_app.IcoPath);

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

    private async Task<IconInfo?> TryLoadThumbnail(string path, bool jumbo, bool logOnFailure)
    {
        return await Task.Run(async () =>
        {
            try
            {
                var stream = await ThumbnailHelper.GetThumbnail(path, jumbo).ConfigureAwait(false);
                if (stream is not null)
                {
                    return IconInfo.FromStream(stream);
                }
            }
            catch (Exception ex)
            {
                if (logOnFailure)
                {
                    Logger.LogDebug($"Failed to load icon {path} for {AppIdentifier}:\n{ex}");
                }
            }

            return null;
        }).ConfigureAwait(false);
    }
}
