// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Commands;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.AppList;

public sealed partial class AppListItem : ListItem, IPrecomputedListItem
{
    private static readonly CompositeFormat ShortcutAlsoListedAsFormat = CompositeFormat.Parse(Resources.shortcut_also_listed_as);

    private readonly AppItem _app;
    private readonly Lazy<Task<Details>> _detailsLoadTask;
    private readonly string[] _pathSearchTerms;
    private readonly AppShortcut? _shortcut;
    private AppListItem?[]? _shortcutResults;
    private IContextItem[]? _shortcutCommandsSource;
    private (string Title, string Subtitle)? _shortcutSubtitleSource;
    private InterlockedBoolean _shortcutPresentationRequested;
    private InterlockedBoolean _updatingShortcutPresentation;

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

    public AppItem App => _app;

    internal IReadOnlyList<string> SearchTerms { get; }

    internal IReadOnlyList<string> ExecutableNames { get; }

    /// <summary>Initializes a new instance of the <see cref="AppListItem"/> class with deferred icon requests.</summary>
    /// <param name="app">The application represented by this row and its command.</param>
    public AppListItem(AppItem app)
    {
        var appCommand = new AppCommand(app);
        Command = appCommand;
        _app = app;
        Title = app.Name;
        Subtitle = app.Subtitle;
        var terms = new[]
        {
            app.LaunchTarget,
            app.ResolvedTarget ?? string.Empty,
            app.DirectoryPath,
            app.AppUserModelId,
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
            return PathHelpers.IsExecutablePath(term)
                ? new[] { searchTerm, Path.GetFileName(term), Path.GetFileNameWithoutExtension(term) }
                : [searchTerm];
        })
        .Where(term => !string.IsNullOrWhiteSpace(term))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        // Only actual launch/target paths identify an executable; retained metadata may name other files.
        ExecutableNames = new[] { app.LaunchTarget, app.ResolvedTarget ?? string.Empty }
            .Concat(app.ExecutableSourcePaths)
            .Where(path => Path.IsPathFullyQualified(path) && PathHelpers.IsExecutablePath(path))
            .Select(path => Path.GetFileName(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _pathSearchTerms = terms.Where(Path.IsPathFullyQualified).Except(SearchTerms, StringComparer.OrdinalIgnoreCase).ToArray();
        Icon = appCommand.Icon = CreateIcon(app);

        MoreCommands = _app.Commands?.ToArray() ?? [];

        _detailsLoadTask = new Lazy<Task<Details>>(BuildDetails);
    }

    private AppListItem(AppListItem source, AppShortcut shortcut)
    {
        // Shortcut rows share immutable search inputs and deferred icon requests with the preferred row.
        _app = source._app;
        _pathSearchTerms = source._pathSearchTerms;
        SearchTerms = source.SearchTerms;
        ExecutableNames = source.ExecutableNames;
        _shortcut = shortcut;
        Title = shortcut.Name;
        Icon = source.Icon;
        Command = new AppCommand(_app, shortcut.Path) { Icon = CoalesceIcon(Icon as IconInfo) };
        _detailsLoadTask = new Lazy<Task<Details>>(BuildDetails);

        // Complete the first presentation before another caller can obtain the cached row.
        UpdateShortcutPresentation(source);
    }

    private async Task LoadDetailsAsync()
    {
        try
        {
            Details = await _detailsLoadTask.Value;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to load details for {_app.Name} ({_app.CatalogId})\n{ex}");
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
        metadata.Add(new DetailsElement() { Key = "Type", Data = new DetailsTags() { Tags = [new Tag(_app.AppTypeLabel)] } });
        if (_shortcut is not null || !_app.IsPackaged)
        {
            metadata.Add(new DetailsElement() { Key = "Path", Data = new DetailsLink() { Text = _shortcut?.Path ?? _app.LaunchTarget } });
        }

#if DEBUG
        metadata.Add(new DetailsElement() { Key = "[DEBUG] CatalogId", Data = new DetailsLink() { Text = _app.CatalogId } });
        metadata.Add(new DetailsElement() { Key = "[DEBUG] LaunchTarget", Data = new DetailsLink() { Text = _app.LaunchTarget } });
        metadata.Add(new DetailsElement() { Key = "[DEBUG] LaunchArguments", Data = new DetailsLink() { Text = _app.LaunchArguments } });
        metadata.Add(new DetailsElement() { Key = "[DEBUG] IconSource", Data = new DetailsLink() { Text = _app.IconSource } });
        metadata.Add(new DetailsElement() { Key = "[DEBUG] JumboIconSource", Data = new DetailsLink() { Text = _app.JumboIconSource ?? "(null)" } });
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

    private static IconInfo CreateIcon(AppItem app)
    {
        var fallbackPath = GetIconFallbackPath(app);
        var iconPath = !string.IsNullOrEmpty(app.IconSource) ? app.IconSource : fallbackPath;
        if (string.IsNullOrEmpty(iconPath))
        {
            return Icons.GenericAppIcon;
        }

        return new IconInfo(
            app.IsPackaged
                ? iconPath
                : AppIconProtocol.Create(iconPath, fallbackPath));
    }

    private static IconInfo? CreateHeroIcon(AppItem app)
    {
        var fallbackPath = GetIconFallbackPath(app);
        if (!string.IsNullOrEmpty(app.JumboIconSource))
        {
            return new IconInfo(
                app.IsPackaged
                    ? app.JumboIconSource
                    : AppIconProtocol.CreateJumbo(app.JumboIconSource, app.IconSource, fallbackPath));
        }

        if (!app.IsPackaged && app.LaunchTarget.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            // Let Shell preserve the shortcut's configured icon and native padding.
            // Direct jumbo resource extraction can enlarge small artwork to 256 pixels.
            return new IconInfo(AppIconProtocol.CreateJumbo(app.LaunchTarget, app.IconSource, fallbackPath));
        }

        if (!string.IsNullOrEmpty(app.IconSource))
        {
            return new IconInfo(
                app.IsPackaged
                    ? app.IconSource
                    : AppIconProtocol.CreateJumbo(app.IconSource, fallbackPath));
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
        return app.LaunchTarget.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(app.ResolvedTarget)
            ? app.ResolvedTarget
            : app.LaunchTarget;
    }

    public FuzzyTarget GetTitleTarget(IPrecomputedFuzzyMatcher matcher)
    {
        return _titleCache.GetOrUpdate(matcher, Title);
    }

    public FuzzyTarget GetSubtitleTarget(IPrecomputedFuzzyMatcher matcher)
    {
        return _subtitleCache.GetOrUpdate(matcher, Subtitle);
    }

    /// <summary>Returns a stable query-specific shortcut row without changing the canonical application or command identity.</summary>
    /// <param name="match">A match evaluated against this application's search targets.</param>
    public AppListItem GetSearchResult(AppSearch.Match match)
    {
        if (match.ShortcutIndex is not { } index
            || string.Equals(_app.Shortcuts[index].Name, Title, StringComparison.OrdinalIgnoreCase))
        {
            return this;
        }

        var results = Volatile.Read(ref _shortcutResults);
        if (results is null)
        {
            results = new AppListItem?[_app.Shortcuts.Count];
            results = Interlocked.CompareExchange(ref _shortcutResults, results, null) ?? results;
        }

        var result = Volatile.Read(ref results[index]);
        if (result is null)
        {
            var created = new AppListItem(this, _app.Shortcuts[index]);
            result = Interlocked.CompareExchange(ref results[index], created, null) ?? created;
        }

        result.UpdateShortcutPresentation(this);
        return result;
    }

    private void UpdateShortcutPresentation(AppListItem source)
    {
        _shortcutPresentationRequested.Set();
        if (!_updatingShortcutPresentation.Set())
        {
            return;
        }

        // Drain updates with one writer so property notifications never run under a lock.
        do
        {
            try
            {
                while (_shortcutPresentationRequested.Clear())
                {
                    var subtitleSource = (source.Title, source.Subtitle);
                    if (_shortcutSubtitleSource != subtitleSource)
                    {
                        _shortcutSubtitleSource = subtitleSource;
                        var alternateName = string.Format(CultureInfo.CurrentCulture, ShortcutAlsoListedAsFormat, subtitleSource.Title);
                        Subtitle = string.IsNullOrEmpty(subtitleSource.Subtitle)
                            ? alternateName
                            : $"{alternateName} • {subtitleSource.Subtitle}";
                    }

                    var commands = source.MoreCommands;
                    if (!ReferenceEquals(_shortcutCommandsSource, commands))
                    {
                        var shortcutCommands = GetShortcutCommands(commands);
                        _shortcutCommandsSource = commands;
                        MoreCommands = shortcutCommands;
                    }
                }
            }
            finally
            {
                _updatingShortcutPresentation.Clear();
            }
        }
        while (_shortcutPresentationRequested.Value && _updatingShortcutPresentation.Set());
    }

    /// <summary>Retargets shortcut location and launch actions while preserving application and host-owned actions.</summary>
    /// <param name="commands">The canonical application's or saved command's context actions.</param>
    public IContextItem[] GetShortcutCommands(IContextItem?[] commands)
    {
        if (_shortcut is null)
        {
            return commands.OfType<IContextItem>().ToArray();
        }

        var directory = Path.GetDirectoryName(_shortcut.Path)!;
        return commands.OfType<IContextItem>().Select(item =>
        {
            if (item is not ICommandContextItem context)
            {
                return item;
            }

            ICommand? replacement = context.Command switch
            {
                RunAsAdminCommand => new RunAsAdminCommand(_shortcut.Path, directory, packaged: false),
                RunAsUserCommand => new RunAsUserCommand(_shortcut.Path, directory),
                ShowFileInFolderCommand show => new ShowFileInFolderCommand(_shortcut.Path) { Name = show.Name, Result = show.Result },
                OpenFileCommand open when _app.IsPackaged => new ShowFileInFolderCommand(_shortcut.Path) { Name = open.Name, Result = open.Result },
                OpenInConsoleCommand => OpenInConsoleCommand.FromDirectory(directory),
                _ => null,
            };
            return replacement is null ? item : new CommandContextItem(replacement)
            {
                Title = context.Title,
                Subtitle = context.Subtitle,
                Icon = context.Icon,
                RequestedShortcut = context.RequestedShortcut,
                IsCritical = context.IsCritical,
            };
        }).ToArray();
    }

    /// <summary>Gets a complete matcher-specific bundle of title, description, and metadata search targets.</summary>
    /// <remarks>The bundle is replaced atomically when its matcher schema or presentation text changes.</remarks>
    internal AppSearch.Targets GetSearchTargets(IPrecomputedFuzzyMatcher matcher)
    {
        var title = Title;
        var description = _app.Subtitle;
        var targets = Volatile.Read(ref _searchTargets);
        if (targets is null
            || targets.SchemaId != matcher.SchemaId
            || targets.Title.Original != title
            || targets.Description.Original != description)
        {
            var metadata = SearchTerms.Select(matcher.PrecomputeTarget).ToArray();
            var pathMetadata = metadata.Concat(_pathSearchTerms.Select(matcher.PrecomputeTarget)).ToArray();
            AppSearch.ShortcutTarget[] shortcuts = [];
            if (_app.Shortcuts.Count > 0)
            {
                // Keep raw casing: a matcher's same-case bonus can distinguish otherwise equal names.
                var metadataCount = metadata.Length;
                var shortcutTargets = new List<AppSearch.ShortcutTarget>(_app.Shortcuts.Count);
                for (var i = 0; i < _app.Shortcuts.Count; i++)
                {
                    var name = _app.Shortcuts[i].Name;
                    var duplicate = false;
                    foreach (var existing in shortcutTargets)
                    {
                        if (string.Equals(name, existing.Target.Original, StringComparison.Ordinal))
                        {
                            duplicate = true;
                            break;
                        }
                    }

                    if (duplicate)
                    {
                        continue;
                    }

                    var index = 0;
                    while (index < metadataCount && !string.Equals(name, metadata[index].Original, StringComparison.Ordinal))
                    {
                        index++;
                    }

                    var isMetadata = index < metadataCount;
                    var target = isMetadata ? metadata[index] : matcher.PrecomputeTarget(name);
                    if (isMetadata)
                    {
                        metadata[index] = metadata[--metadataCount];
                    }

                    shortcutTargets.Add(new AppSearch.ShortcutTarget(target, i, isMetadata));
                }

                Array.Resize(ref metadata, metadataCount);
                shortcuts = [.. shortcutTargets];
            }

            targets = new AppSearch.Targets(
                matcher.SchemaId,
                matcher.PrecomputeTarget(title),
                matcher.PrecomputeTarget(description),
                metadata,
                pathMetadata,
                shortcuts);

            // Home and All Apps may search these rows concurrently. Publish a complete bundle;
            // each caller keeps its own bundle even if another matcher replaces the cache.
            Volatile.Write(ref _searchTargets, targets);
        }

        return targets;
    }
}
