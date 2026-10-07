// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CmdPal.Ext.Apps.Win32;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps.AppList;

/// <summary>
/// Projects current catalog snapshots into stable application list items shared by UI consumers.
/// </summary>
public sealed partial class AppListItemSource : IAppListItemSource
{
    /// <inheritdoc />
    public event EventHandler? Changed;

    private const CompareOptions TitleCompareOptions = CompareOptions.IgnoreCase | CompareOptions.NumericOrdering;
    private static readonly CompareInfo TitleCompareInfo = CultureInfo.CurrentCulture.CompareInfo;

    private readonly Lock _stateLock = new();
    private readonly IAppCatalog _appCatalog;
    private readonly AllAppsSettings _settings;
    private readonly MEL.ILogger<AppListItemSource> _logger;
    private readonly AppExecutionAliasCache? _executionAliasCache;
    private readonly long? _diagnosticStartedAt;

    private PublishedState _publishedState = new(new AppListItemSnapshot([], []), IsLoading: true, HideDescriptions: false, ResultLimit: 0);
    private CommandContextItem? _editExclusionPatterns;
    private bool _initializationCompleted;
    private int _userRefreshCount;
    private (long Timestamp, string Kind)? _loadingStarted;
    private bool _firstItemsReported;
    private InterlockedBoolean _synchronizeRequested;
    private InterlockedBoolean _synchronizing;
    private InterlockedBoolean _disposed;

    /// <inheritdoc />
    public bool IsLoading => Volatile.Read(ref _publishedState).IsLoading;

    /// <inheritdoc />
    public int TopLevelResultLimit => _settings.EffectiveSearchResultLimit;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppListItemSource"/> class. Initializes a new application list-item source backed by the supplied catalog and presentation settings.
    /// </summary>
    /// <param name="appCatalog">The canonical application catalog to project.</param>
    /// <param name="settings">Settings that control presentation-only projection choices.</param>
    public AppListItemSource(IAppCatalog appCatalog, AllAppsSettings settings)
        : this(appCatalog, settings, NullLogger<AppListItemSource>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppListItemSource"/> class. Initializes a new application list-item source with injected presentation settings and logging.
    /// </summary>
    /// <param name="appCatalog">The canonical application catalog to project.</param>
    /// <param name="settings">Settings that control presentation-only projection choices.</param>
    /// <param name="logger">The diagnostic logger for projection failures.</param>
    /// <param name="executionAliasCache">The optional, independently owned background alias cache.</param>
    public AppListItemSource(
        IAppCatalog appCatalog,
        AllAppsSettings settings,
        MEL.ILogger<AppListItemSource> logger,
        AppExecutionAliasCache? executionAliasCache = null)
    {
        ArgumentNullException.ThrowIfNull(appCatalog);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);

        _appCatalog = appCatalog;
        _settings = settings;
        _logger = logger;
        _executionAliasCache = executionAliasCache;
        if (ShouldLogDiagnostics())
        {
            _diagnosticStartedAt = Stopwatch.GetTimestamp();
            _loadingStarted = (_diagnosticStartedAt.Value, "startup");
        }

        lock (_stateLock)
        {
            if (_executionAliasCache is not null)
            {
                _executionAliasCache.Changed += OnExecutionAliasOwnersChanged;
            }

            _publishedState = _publishedState with
            {
                Snapshot = _executionAliasCache is null
                    ? _publishedState.Snapshot
                    : _publishedState.Snapshot.WithExecutionAliasOwners(_executionAliasCache.GetSnapshot()),
                HideDescriptions = _settings.HideAppDescriptions,
                ResultLimit = _settings.EffectiveSearchResultLimit,
            };
        }

        _appCatalog.Changed += OnCatalogChanged;
        _appCatalog.VisibilityChanged += OnCatalogVisibilityChanged;
        _settings.Settings.SettingsChanged += OnSettingsChanged;
        _executionAliasCache?.RequestRefresh();
        _ = InitializeAsync();
    }

    /// <inheritdoc />
    public AppListItemSnapshot GetSnapshot()
    {
        return Volatile.Read(ref _publishedState).Snapshot;
    }

    /// <inheritdoc />
    public void RequestExecutionAliasRefresh()
    {
        if (!_disposed.Value)
        {
            _executionAliasCache?.RequestRefresh();
        }
    }

    private void OnExecutionAliasOwnersChanged(object? sender, EventArgs args)
    {
        lock (_stateLock)
        {
            if (_disposed.Value)
            {
                return;
            }

            var state = _publishedState;
            var owners = _executionAliasCache!.GetSnapshot();
            var snapshot = state.Snapshot.WithExecutionAliasOwners(owners);
            if (ReferenceEquals(snapshot, state.Snapshot))
            {
                return;
            }

            Volatile.Write(ref _publishedState, state with { Snapshot = snapshot });
        }

        RaiseChanged();
    }

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

        (string Kind, double DurationMs)? completedLoading;
        lock (_stateLock)
        {
            if (_disposed.Value)
            {
                return;
            }

            _initializationCompleted = true;
            completedLoading = PublishLoadingStateUnderLock(_userRefreshCount > 0);
        }

        if (completedLoading is { } completed)
        {
            LogDiagnosticLoadingCompleted(_logger, completed.Kind, completed.DurationMs);
        }

        RaiseChanged();
    }

    private void OnCatalogChanged(object? sender, AppCatalogChangedEventArgs args)
    {
        SynchronizeWithCatalog();
    }

    private void OnCatalogVisibilityChanged(object? sender, AppVisibilityChangedEventArgs args)
    {
        SynchronizeWithCatalog();
    }

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
        (string Kind, double DurationMs)? completedLoading = null;
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
                completedLoading = PublishLoadingStateUnderLock(isLoading);
                changed = true;
            }
        }

        if (completedLoading is { } completed)
        {
            LogDiagnosticLoadingCompleted(_logger, completed.Kind, completed.DurationMs);
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
        var diagnostics = ShouldLogDiagnostics();
        var started = diagnostics ? Stopwatch.GetTimestamp() : 0;
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

        var updated = new AppListItemSnapshot(
            BuildList(catalogSnapshot.Items, AppVisibility.Visible, hideDescriptions, existingItems, presentationChanged ? null : snapshot.VisibleItems),
            BuildList(catalogSnapshot.HiddenItems, AppVisibility.Hidden, hideDescriptions, existingItems, presentationChanged ? null : snapshot.HiddenItems),
            BuildList(catalogSnapshot.PatternHiddenItems, AppVisibility.HiddenByPattern, hideDescriptions, existingItems, presentationChanged ? null : snapshot.PatternHiddenItems),
            catalogSnapshot.CommandAliases,
            executableNameMatchMode,
            snapshot.ExecutionAliasOwners);

        var resolutionChanged = !updated.HasSameCommandResolution(snapshot);
        if (diagnostics)
        {
            var durationMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var rowCount = updated.VisibleItems.Count + updated.HiddenItems.Count + updated.PatternHiddenItems.Count;
            var reusedRows = updated.VisibleItems.Concat(updated.HiddenItems).Concat(updated.PatternHiddenItems).Count(
                item => existingItems.TryGetValue(item.App.CatalogId, out var existing) && ReferenceEquals(existing.Item, item));
            LogDiagnosticProjection(_logger, durationMs, rowCount, reusedRows, rowCount - reusedRows, resolutionChanged);
        }

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

        double? firstItemsMs = null;
        lock (_stateLock)
        {
            if (_disposed.Value)
            {
                return;
            }

            if (_executionAliasCache is not null)
            {
                updated = updated.WithExecutionAliasOwners(_executionAliasCache.GetSnapshot());
            }

            Volatile.Write(ref _publishedState, _publishedState with { Snapshot = updated, HideDescriptions = hideDescriptions, ResultLimit = resultLimit });
            if (diagnostics && !_firstItemsReported && _diagnosticStartedAt is { } diagnosticStartedAt && updated.VisibleItems.Count > 0)
            {
                _firstItemsReported = true;
                firstItemsMs = Stopwatch.GetElapsedTime(diagnosticStartedAt).TotalMilliseconds;
            }
        }

        if (firstItemsMs is { } duration)
        {
            LogDiagnosticFirstItems(_logger, duration, updated.VisibleItems.Count);
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
                var item = new AppListItem(app)
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

    private bool ShouldLogDiagnostics()
    {
        return _settings.EnableCatalogDiagnostics && _logger.IsEnabled(LogLevel.Information);
    }

    private (string Kind, double DurationMs)? PublishLoadingStateUnderLock(bool isLoading)
    {
        (string Kind, double DurationMs)? completed = null;
        if (_publishedState.IsLoading != isLoading)
        {
            if (isLoading)
            {
                _loadingStarted = ShouldLogDiagnostics() ? (Stopwatch.GetTimestamp(), "refresh") : null;
            }
            else
            {
                if (ShouldLogDiagnostics() && _loadingStarted is { } started)
                {
                    completed = (started.Kind, Stopwatch.GetElapsedTime(started.Timestamp).TotalMilliseconds);
                }

                _loadingStarted = null;
            }
        }

        Volatile.Write(ref _publishedState, _publishedState with { IsLoading = isLoading });
        return completed;
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
        if (_executionAliasCache is not null)
        {
            _executionAliasCache.Changed -= OnExecutionAliasOwnersChanged;
        }

        Changed = null;
        GC.SuppressFinalize(this);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to initialize the application index.")]
    private static partial void LogInitializationFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to change visibility for application '{CatalogId}'.")]
    private static partial void LogVisibilityChangeFailed(
        MEL.ILogger logger,
        string catalogId,
        Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "[AppCatalog diagnostics] Row projection took {DurationMs} ms: rows={RowCount}, reused={ReusedCount}, created={CreatedCount}, resolutionChanged={ResolutionChanged}.")]
    private static partial void LogDiagnosticProjection(MEL.ILogger logger, double durationMs, int rowCount, int reusedCount, int createdCount, bool resolutionChanged);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "[AppCatalog diagnostics] First non-empty row snapshot after {DurationMs} ms: visible={VisibleCount}.")]
    private static partial void LogDiagnosticFirstItems(MEL.ILogger logger, double durationMs, int visibleCount);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "[AppCatalog diagnostics] {Kind} loading completed after {DurationMs} ms.")]
    private static partial void LogDiagnosticLoadingCompleted(MEL.ILogger logger, string kind, double durationMs);

    private sealed record PublishedState(AppListItemSnapshot Snapshot, bool IsLoading, bool HideDescriptions, int ResultLimit);
}
