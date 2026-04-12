// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ManagedCommon;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps.Commands;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

public sealed partial class AppListItem : ListItem, IPrecomputedListItem
{
    private readonly AppCommand _appCommand;
    private readonly AppItem _app;
    private readonly Lazy<Details> _detailsLoadTask;

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
                LoadDetails();
            }

            return base.Details;
        }
        set => base.Details = value;
    }

    public string AppIdentifier => _app.AppIdentifier;

    public AppItem App => _app;

    public AppListItem(AppItem app, bool useThumbnails)
    {
        Command = _appCommand = new AppCommand(app);
        _app = app;
        Title = app.Name;
        Subtitle = app.Subtitle;

        MoreCommands = _app.Commands?.ToArray() ?? [];

        _detailsLoadTask = new Lazy<Details>(BuildDetails);

        var icon = CoalesceIcon(FetchIcon(useThumbnails));
        Icon = icon;
        _appCommand.Icon = icon;
    }

    private void LoadDetails()
    {
        try
        {
            Details = _detailsLoadTask.Value;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to load details for {AppIdentifier}\n{ex}");
        }
    }

    private static IconInfo CoalesceIcon(IconInfo? value)
    {
        return CoalesceIcon(value, Icons.AppIconPlaceholder)!;
    }

    private static IconInfo? CoalesceIcon(IconInfo? value, IconInfo? replacement)
    {
        return IconIsNullOrEmpty(value) ? replacement : value;
    }

    private static bool IconIsNullOrEmpty(IconInfo? value)
    {
        return value == null || (string.IsNullOrEmpty(value.Light?.Icon) && value.Light?.Data is null) || (string.IsNullOrEmpty(value.Dark?.Icon) && value.Dark?.Data is null);
    }

    private Details BuildDetails()
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
            heroImage = new IconInfo(BuildPackagedHeroIconUri());
        }
        else
        {
            heroImage = new IconInfo(BuildHeroIconUri());
        }

        return new Details()
        {
            Title = this.Title,
            HeroImage = CoalesceIcon(CoalesceIcon(heroImage, this.Icon as IconInfo)),
            Metadata = [.. metadata],
        };
    }

    private IconInfo FetchIcon(bool useThumbnails)
    {
        return _app.IsPackaged
            ? new IconInfo(BuildPackagedListIconUri())
            : new IconInfo(BuildListIconUri(useThumbnails));
    }

    private string BuildPackagedHeroIconUri()
    {
        return CmdPalUri.IconBuilder()
            .AddIcon(_app.JumboIconPath)
            .AddIcon(_app.IcoPath)
            .AddIcon(Icons.AppIconPlaceholderPath)
            .Build();
    }

    private string BuildPackagedListIconUri()
    {
        return CmdPalUri.IconBuilder()
            .AddIcon(_app.IcoPath)
            .AddIcon(Icons.AppIconPlaceholderPath)
            .Build();
    }

    private string BuildHeroIconUri()
    {
        return CmdPalUri.IconBuilder()
            .AddIcon(_app.JumboIconPath)
            .AddIcon(_app.IcoPath)
            .AddThumbnail(_app.JumboIconPath)
            .AddThumbnail(_app.IcoPath)
            .AddThumbnail(_app.ExePath)
            .Build();
    }

    private string BuildListIconUri(bool useThumbnails)
    {
        var builder = CmdPalUri.IconBuilder();

        if (useThumbnails)
        {
            builder = builder
                .AddThumbnail(_app.IcoPath)
                .AddThumbnail(_app.ExePath);
        }

        return builder
            .AddIcon(_app.IcoPath)
            .AddIcon(_app.ExePath)
            .AddIcon(Icons.AppIconPlaceholderPath)
            .Build();
    }

    public FuzzyTarget GetTitleTarget(IPrecomputedFuzzyMatcher matcher)
        => _titleCache.GetOrUpdate(matcher, Title);

    public FuzzyTarget GetSubtitleTarget(IPrecomputedFuzzyMatcher matcher)
        => _subtitleCache.GetOrUpdate(matcher, Subtitle);
}
