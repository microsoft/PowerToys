// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps;

/// <summary>
/// Projects current catalog snapshots into stable application list items shared by UI consumers.
/// </summary>
public sealed partial class AppListItemSource : IAppListItemSource
{
    private const CompareOptions TitleCompareOptions = CompareOptions.IgnoreCase | CompareOptions.NumericOrdering;
    private static readonly CompareInfo TitleCompareInfo = CultureInfo.CurrentCulture.CompareInfo;

    private readonly Lock _stateLock = new();
    private readonly IAppCatalog _appCatalog;
    private readonly AllAppsSettings _settings;
    private readonly MEL.ILogger<AppListItemSource> _logger;

    private PublishedState _publishedState = new(new AppListItemSnapshot([], []), IsLoading: true, HideDescriptions: false, ResultLimit: 0);
    private CommandContextItem? _editExclusionPatterns;
    private bool _initializationCompleted;
    private int _userRefreshCount;
    private InterlockedBoolean _synchronizeRequested;
    private InterlockedBoolean _synchronizing;
    private InterlockedBoolean _disposed;

    /// <summary>
    /// Initializes a new application list-item source backed by the supplied catalog and presentation settings.
    /// </summary>
    /// <param name="appCatalog">The canonical application catalog to project.</param>
    /// <param name="settings">Settings that control presentation-only projection choices.</param>
    public AppListItemSource(IAppCatalog appCatalog, AllAppsSettings settings)
        : this(appCatalog, settings, NullLogger<AppListItemSource>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new application list-item source with injected presentation settings and logging.
    /// </summary>
    /// <param name="appCatalog">The canonical application catalog to project.</param>
    /// <param name="settings">Settings that control presentation-only projection choices.</param>
    /// <param name="logger">The diagnostic logger for projection failures.</param>
    public AppListItemSource(
        IAppCatalog appCatalog,
        AllAppsSettings settings,
        MEL.ILogger<AppListItemSource> logger)
    {
        _appCatalog = appCatalog ?? throw new ArgumentNullException(nameof(appCatalog));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _publishedState = _publishedState with { HideDescriptions = _settings.HideAppDescriptions, ResultLimit = _settings.EffectiveSearchResultLimit };
        _appCatalog.Changed += OnCatalogChanged;
        _appCatalog.VisibilityChanged += OnCatalogVisibilityChanged;
        _settings.Settings.SettingsChanged += OnSettingsChanged;
        _ = InitializeAsync();
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public AppListItemSnapshot GetSnapshot() => Volatile.Read(ref _publishedState).Snapshot;

    /// <inheritdoc />
    public bool IsLoading => Volatile.Read(ref _publishedState).IsLoading;

    /// <inheritdoc />
    public int TopLevelResultLimit => _settings.EffectiveSearchResultLimit;

    /// <inheritdoc />
    public async Task RefreshAsync()
    {
        UpdateLoadingState(1);
        try
        {
            await _appCatalog.RefreshAsync().ConfigureAwait(false);
        }
        finally
        {
            UpdateLoadingState(-1);
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            await _appCatalog.InitializeAsync().ConfigureAwait(false);
            SynchronizeWithCatalog();
        }
        catch (Exception ex)
        {
            LogInitializationFailed(_logger, ex);
        }

        lock (_stateLock)
        {
            if (_disposed.Value)
            {
                return;
            }

            _initializationCompleted = true;
            PublishLoadingStateUnderLock(_userRefreshCount > 0);
        }

        RaiseChanged();
    }

    private void OnCatalogChanged(object? sender, AppCatalogChangedEventArgs args) => SynchronizeWithCatalog();

    private void OnCatalogVisibilityChanged(object? sender, AppVisibilityChangedEventArgs args) => SynchronizeWithCatalog();

    private void OnSettingsChanged(object sender, Settings args)
    {
        var state = Volatile.Read(ref _publishedState);
        if (state.HideDescriptions != _settings.HideAppDescriptions
            || state.ResultLimit != _settings.EffectiveSearchResultLimit
            || state.Snapshot.ExecutableNameMatchMode != _settings.ExecutableNameMatchMode)
        {
            SynchronizeWithCatalog();
        }
    }

    private void UpdateLoadingState(int refreshCountDelta)
    {
        var changed = false;
        lock (_stateLock)
        {
            if (_disposed.Value)
            {
                return;
            }

            _userRefreshCount += refreshCountDelta;
            var isLoading = !_initializationCompleted || _userRefreshCount > 0;
            if (_publishedState.IsLoading != isLoading)
            {
                PublishLoadingStateUnderLock(isLoading);
                changed = true;
            }
        }

        if (changed)
        {
            RaiseChanged();
        }
    }

    private void SynchronizeWithCatalog()
    {
        _synchronizeRequested.Set();
        if (!_synchronizing.Set())
        {
            return;
        }

        do
        {
            try
            {
                while (_synchronizeRequested.Clear())
                {
                    if (_disposed.Value)
                    {
                        return;
                    }

                    SynchronizeCore();
                }
            }
            finally
            {
                _synchronizing.Clear();
            }
        }
        while (_synchronizeRequested.Value && _synchronizing.Set());
    }

    private void SynchronizeCore()
    {
        var catalogSnapshot = _appCatalog.GetSnapshot();
        var state = Volatile.Read(ref _publishedState);
        var snapshot = state.Snapshot;
        var hideDescriptions = _settings.HideAppDescriptions;
        var resultLimit = _settings.EffectiveSearchResultLimit;
        var executableNameMatchMode = _settings.ExecutableNameMatchMode;
        var presentationChanged = state.HideDescriptions != hideDescriptions;
        var existingItems = new Dictionary<string, (AppListItem Item, AppVisibility Visibility)>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in snapshot.VisibleItems)
        {
            existingItems[item.App.CatalogId] = (item, AppVisibility.Visible);
        }

        foreach (var item in snapshot.HiddenItems)
        {
            existingItems[item.App.CatalogId] = (item, AppVisibility.Hidden);
        }

        foreach (var item in snapshot.PatternHiddenItems)
        {
            existingItems[item.App.CatalogId] = (item, AppVisibility.HiddenByPattern);
        }

        var commandAliases = _settings.RetainAppCommandAliases(catalogSnapshot.Items.Concat(catalogSnapshot.HiddenItems).Concat(catalogSnapshot.PatternHiddenItems));
        var updated = new AppListItemSnapshot(
            BuildList(catalogSnapshot.Items, AppVisibility.Visible, hideDescriptions, existingItems, presentationChanged ? null : snapshot.VisibleItems),
            BuildList(catalogSnapshot.HiddenItems, AppVisibility.Hidden, hideDescriptions, existingItems, presentationChanged ? null : snapshot.HiddenItems),
            BuildList(catalogSnapshot.PatternHiddenItems, AppVisibility.HiddenByPattern, hideDescriptions, existingItems, presentationChanged ? null : snapshot.PatternHiddenItems),
            commandAliases,
            executableNameMatchMode);

        var resolutionChanged = !updated.HasSameCommandResolution(snapshot);

        if (ReferenceEquals(updated.VisibleItems, snapshot.VisibleItems)
            && ReferenceEquals(updated.HiddenItems, snapshot.HiddenItems)
            && ReferenceEquals(updated.PatternHiddenItems, snapshot.PatternHiddenItems))
        {
            if (!resolutionChanged && !presentationChanged && state.ResultLimit == resultLimit && snapshot.ExecutableNameMatchMode == executableNameMatchMode)
            {
                return;
            }

            if (!resolutionChanged && snapshot.ExecutableNameMatchMode == executableNameMatchMode)
            {
                updated = snapshot;
            }
        }

        lock (_stateLock)
        {
            if (_disposed.Value)
            {
                return;
            }

            Volatile.Write(ref _publishedState, _publishedState with { Snapshot = updated, HideDescriptions = hideDescriptions, ResultLimit = resultLimit });
        }

        // Property notifications can change the catalog again. Publish first and drain those
        // changes in the next pass without holding a lock across consumer callbacks.
        UpdatePresentation(updated.VisibleItems, AppVisibility.Visible, hideDescriptions, existingItems);
        UpdatePresentation(updated.HiddenItems, AppVisibility.Hidden, hideDescriptions, existingItems);
        UpdatePresentation(updated.PatternHiddenItems, AppVisibility.HiddenByPattern, hideDescriptions, existingItems);
        RaiseChanged();
    }

    private IReadOnlyList<AppListItem> BuildList(
        IReadOnlyList<AppItem> apps,
        AppVisibility visibility,
        bool hideDescriptions,
        IReadOnlyDictionary<string, (AppListItem Item, AppVisibility Visibility)> existingItems,
        IReadOnlyList<AppListItem>? previousItems)
    {
        var items = new List<AppListItem>(apps.Count);
        foreach (var app in apps)
        {
            if (existingItems.TryGetValue(app.CatalogId, out var existing)
                && ReferenceEquals(existing.Item.App, app))
            {
                items.Add(existing.Item);
            }
            else
            {
                var item = new AppListItem(app, useThumbnails: true)
                {
                    Subtitle = hideDescriptions ? string.Empty : app.Subtitle,
                };
                SetVisibilityCommand(item, visibility);
                items.Add(item);
            }
        }

        // ponytail: O(n log n) per snapshot; use incremental ordering if large catalogs make projection slow.
        items.Sort(static (left, right) => TitleCompareInfo.Compare(left.Title, right.Title, TitleCompareOptions));
        return previousItems is not null && items.SequenceEqual(previousItems) ? previousItems : [.. items];
    }

    private void UpdatePresentation(
        IReadOnlyList<AppListItem> items,
        AppVisibility visibility,
        bool hideDescriptions,
        IReadOnlyDictionary<string, (AppListItem Item, AppVisibility Visibility)> existingItems)
    {
        foreach (var item in items)
        {
            item.Subtitle = hideDescriptions ? string.Empty : item.App.Subtitle;
            if (existingItems.TryGetValue(item.App.CatalogId, out var previous)
                && ReferenceEquals(previous.Item, item)
                && previous.Visibility != visibility)
            {
                SetVisibilityCommand(item, visibility);
            }
        }
    }

    private void SetVisibilityCommand(AppListItem item, AppVisibility visibility)
    {
        var hidden = visibility == AppVisibility.Hidden;
        var command = visibility == AppVisibility.HiddenByPattern
            ? _editExclusionPatterns ??= new CommandContextItem(_settings.Settings.SettingsPage) { Title = Resources.edit_exclusion_patterns }
            : new CommandContextItem(
                new AnonymousCommand(() => _ = SetAppHiddenAsync(item.App.CatalogId, !hidden))
                {
                    Name = hidden ? Resources.unhide_app : Resources.hide_app,
                    Icon = hidden ? Icons.Unhide : Icons.Hide,
                    Result = CommandResult.KeepOpen(),
                });
        item.MoreCommands =
        [
            .. item.App.Commands ?? [],
            command,
        ];
    }

    private async Task SetAppHiddenAsync(string catalogId, bool hidden)
    {
        try
        {
            await _appCatalog.SetAppHiddenAsync(catalogId, hidden).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogVisibilityChangeFailed(_logger, catalogId, ex);
        }
    }

    private void PublishLoadingStateUnderLock(bool isLoading)
    {
        Volatile.Write(ref _publishedState, _publishedState with { IsLoading = isLoading });
    }

    private void RaiseChanged()
    {
        if (!_disposed.Value)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed.Set())
        {
            return;
        }

        _appCatalog.Changed -= OnCatalogChanged;
        _appCatalog.VisibilityChanged -= OnCatalogVisibilityChanged;
        _settings.Settings.SettingsChanged -= OnSettingsChanged;
        Changed = null;
        _settings.WaitForAliasSavesAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    private sealed record PublishedState(AppListItemSnapshot Snapshot, bool IsLoading, bool HideDescriptions, int ResultLimit);

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to initialize the application index.")]
    private static partial void LogInitializationFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to change visibility for application '{CatalogId}'.")]
    private static partial void LogVisibilityChangeFailed(
        MEL.ILogger logger,
        string catalogId,
        Exception exception);
}
