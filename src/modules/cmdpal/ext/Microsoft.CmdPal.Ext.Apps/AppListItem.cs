// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps.Commands;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

public sealed partial class AppListItem : ListItem, IPrecomputedListItem
{
    private readonly AppItem _app;
    private readonly Lazy<Task<Details>> _detailsLoadTask;

    private InterlockedBoolean _isLoadingDetails;

    private FuzzyTargetCache _titleCache;
    private FuzzyTargetCache _subtitleCache;

    public override string Title
    {
        get => base.Title;
        set
        {
            if (!string.Equals(base.Title, value, StringComparison.Ordinal))
            {
                base.Title = value;
                _titleCache.Invalidate();
            }
        }
    }

    public override string Subtitle
    {
        get => base.Subtitle;
        set
        {
            if (!string.Equals(value, base.Subtitle, StringComparison.Ordinal))
            {
                base.Subtitle = value;
                _subtitleCache.Invalidate();
            }
        }
    }

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

    public string AppIdentifier => _app.AppIdentifier;

    public AppItem App => _app;

    public AppListItem(AppItem app, bool useThumbnails)
    {
        var appCommand = new AppCommand(app);
        Command = appCommand;
        _app = app;
        Title = app.Name;
        Subtitle = app.Subtitle;
        Icon = appCommand.Icon = CreateIcon(app, useThumbnails);

        MoreCommands = _app.Commands?.ToArray() ?? [];

        _detailsLoadTask = new Lazy<Task<Details>>(BuildDetails);
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

    private Task<Details> BuildDetails()
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
        var heroImage = CreateHeroIcon(_app);

        return Task.FromResult(new Details()
        {
            Title = this.Title,
            HeroImage = CoalesceIcon(CoalesceIcon(heroImage, this.Icon as IconInfo)),
            Metadata = [.. metadata],
        });
    }

    private static IconInfo CreateIcon(AppItem app, bool useThumbnails)
    {
        var iconPath = !string.IsNullOrEmpty(app.IcoPath) ? app.IcoPath : app.ExePath;
        if (string.IsNullOrEmpty(iconPath))
        {
            return Icons.GenericAppIcon;
        }

        return new IconInfo(
            !app.IsPackaged && useThumbnails
                ? AppIconProtocol.Create(iconPath, app.ExePath)
                : iconPath);
    }

    private static IconInfo? CreateHeroIcon(AppItem app)
    {
        if (!string.IsNullOrEmpty(app.JumboIconPath))
        {
            return new IconInfo(
                app.IsPackaged
                    ? app.JumboIconPath
                    : AppIconProtocol.CreateJumbo(app.JumboIconPath, app.IcoPath, app.ExePath));
        }

        if (!string.IsNullOrEmpty(app.IcoPath))
        {
            return new IconInfo(
                app.IsPackaged
                    ? app.IcoPath
                    : AppIconProtocol.CreateJumbo(app.IcoPath, app.ExePath));
        }

        if (!string.IsNullOrEmpty(app.ExePath))
        {
            return new IconInfo(
                app.IsPackaged
                    ? app.ExePath
                    : AppIconProtocol.CreateJumbo(app.ExePath));
        }

        return null;
    }

    public FuzzyTarget GetTitleTarget(IPrecomputedFuzzyMatcher matcher)
        => _titleCache.GetOrUpdate(matcher, Title);

    public FuzzyTarget GetSubtitleTarget(IPrecomputedFuzzyMatcher matcher)
        => _subtitleCache.GetOrUpdate(matcher, Subtitle);
}
