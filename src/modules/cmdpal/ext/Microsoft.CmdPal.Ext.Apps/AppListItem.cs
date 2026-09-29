// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps.Commands;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

public sealed partial class AppListItem : ListItem, IPrecomputedListItem
{
    private readonly AppItem _app;
    private readonly Lazy<Task<Details>> _detailsLoadTask;
    private readonly string[] _pathSearchTerms;

    private InterlockedBoolean _isLoadingDetails;

    private FuzzyTargetCache _titleCache;
    private FuzzyTargetCache _subtitleCache;
    private AppSearch.Targets? _searchTargets;

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

    internal IReadOnlyList<string> SearchTerms { get; }

    internal IReadOnlyList<string> ExecutableNames { get; }

    public AppListItem(AppItem app, bool useThumbnails)
    {
        var appCommand = new AppCommand(app);
        Command = appCommand;
        _app = app;
        Title = app.Name;
        Subtitle = app.Subtitle;
        var terms = new[]
        {
            app.ExePath,
            app.FullExecutablePath ?? string.Empty,
            app.DirPath,
            app.UserModelId,
            app.PackageFamilyName ?? string.Empty,
        }
        .Concat(app.MatchTerms)
        .Concat(app.ExecutableSourcePaths)
        .Where(term => !string.IsNullOrWhiteSpace(term))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
        SearchTerms = terms.SelectMany(term =>
        {
            var searchTerm = Path.IsPathFullyQualified(term) ? PathHelpers.GetAppSearchPath(term) : term;
            return Win32Program.IsExecutablePath(term)
                ? new[] { searchTerm, Path.GetFileName(term), Path.GetFileNameWithoutExtension(term) }
                : [searchTerm];
        })
        .Where(term => !string.IsNullOrWhiteSpace(term))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        // Only the actual launch/target paths identify an executable. Retained metadata
        // may name other files, and arguments identify a specialized launch rather than the executable alone.
        ExecutableNames = string.IsNullOrWhiteSpace(app.Arguments)
            ? new[] { app.ExePath, app.FullExecutablePath ?? string.Empty }
                .Concat(app.ExecutableSourcePaths)
                .Where(path => Path.IsPathFullyQualified(path) && Win32Program.IsExecutablePath(path))
                .Select(path => Path.GetFileName(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];
        _pathSearchTerms = terms.Where(Path.IsPathFullyQualified).Except(SearchTerms, StringComparer.OrdinalIgnoreCase).ToArray();
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
        var fallbackPath = GetIconFallbackPath(app);
        var iconPath = !string.IsNullOrEmpty(app.IcoPath) ? app.IcoPath : fallbackPath;
        if (string.IsNullOrEmpty(iconPath))
        {
            return Icons.GenericAppIcon;
        }

        return new IconInfo(
            !app.IsPackaged && useThumbnails
                ? AppIconProtocol.Create(iconPath, fallbackPath)
                : iconPath);
    }

    private static IconInfo? CreateHeroIcon(AppItem app)
    {
        var fallbackPath = GetIconFallbackPath(app);
        if (!string.IsNullOrEmpty(app.JumboIconPath))
        {
            return new IconInfo(
                app.IsPackaged
                    ? app.JumboIconPath
                    : AppIconProtocol.CreateJumbo(app.JumboIconPath, app.IcoPath, fallbackPath));
        }

        if (!app.IsPackaged && app.ExePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            // Let Shell preserve the shortcut's configured icon and native padding.
            // Direct jumbo resource extraction can enlarge small artwork to 256 pixels.
            return new IconInfo(AppIconProtocol.CreateJumbo(app.ExePath, app.IcoPath, fallbackPath));
        }

        if (!string.IsNullOrEmpty(app.IcoPath))
        {
            return new IconInfo(
                app.IsPackaged
                    ? app.IcoPath
                    : AppIconProtocol.CreateJumbo(app.IcoPath, fallbackPath));
        }

        if (!string.IsNullOrEmpty(fallbackPath))
        {
            return new IconInfo(
                app.IsPackaged
                    ? fallbackPath
                    : AppIconProtocol.CreateJumbo(fallbackPath));
        }

        return null;
    }

    private static string GetIconFallbackPath(AppItem app)
    {
        return app.ExePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(app.FullExecutablePath)
            ? app.FullExecutablePath
            : app.ExePath;
    }

    public FuzzyTarget GetTitleTarget(IPrecomputedFuzzyMatcher matcher)
        => _titleCache.GetOrUpdate(matcher, Title);

    public FuzzyTarget GetSubtitleTarget(IPrecomputedFuzzyMatcher matcher)
        => _subtitleCache.GetOrUpdate(matcher, Subtitle);

    internal AppSearch.Targets GetSearchTargets(IPrecomputedFuzzyMatcher matcher)
    {
        var title = Title;
        var description = _app.Subtitle;
        var targets = Volatile.Read(ref _searchTargets);
        if (targets is null || targets.SchemaId != matcher.SchemaId
            || targets.Title.Original != title || targets.Description.Original != description)
        {
            var metadata = SearchTerms.Select(matcher.PrecomputeTarget).ToArray();
            targets = new AppSearch.Targets(
                matcher.SchemaId,
                matcher.PrecomputeTarget(title),
                matcher.PrecomputeTarget(description),
                metadata,
                metadata.Concat(_pathSearchTerms.Select(matcher.PrecomputeTarget)).ToArray());

            // Home and All Apps may search these rows concurrently. Publish a complete bundle;
            // each caller keeps its own bundle even if another matcher replaces the cache.
            Volatile.Write(ref _searchTargets, targets);
        }

        return targets;
    }
}
