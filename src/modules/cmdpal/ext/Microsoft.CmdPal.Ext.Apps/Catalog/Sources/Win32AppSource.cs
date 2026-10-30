// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

/// <summary>
/// Adapts one Win32 discovery origin into an independently cacheable and refreshable catalog source.
/// </summary>
internal sealed partial class Win32AppSource : IAppSource
{
    public event EventHandler<AppSourceInvalidatedEventArgs>? Invalidated;

    private const string CatalogSourcePrefix = "win32:";
    private const int IndexingMaxDegreeOfParallelism = 2;
    private const int IncrementalRefreshPathLimit = 256;
    private const int RawExecutablePriorityOffset = 1000;
    private const int PortableAppMaximumDepth = 1;

    private static readonly IReadOnlyList<string> PortableAppSuffixes = ["exe"];
    private static readonly TimeSpan InitialWatcherRecoveryDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaximumWatcherRecoveryDelay = TimeSpan.FromMinutes(5);

    private readonly IWin32ProgramSource _source;
    private readonly string _configurationKey;
    private readonly Func<string, bool, Win32Program> _programLoader;
    private readonly Func<bool> _diagnosticsEnabled;
    private readonly Func<TimeSpan, CancellationToken, Task> _recoveryDelayAsync;
    private readonly MEL.ILogger<Win32AppSource> _logger;
    private readonly bool _createWatchers;
    private readonly Lock _watchersLock = new();
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, WatcherRecoveryState> _watcherRecoveries = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IndexedCandidate> _indexedCandidates = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _disposeCancellation = new();
    private Task? _initializationTask;
    private long _nextWatcherRecoveryGeneration;
    private InterlockedBoolean _disposed;

    public string Id => CatalogSourcePrefix + _source.Id;

    public string CacheKey => _source.CacheKey;

    /// <summary>Gets settings-derived state used by the source provider to retain unchanged instances.</summary>
    internal string ConfigurationKey => _configurationKey;

    /// <summary>Gets a snapshot of paths with an active watcher, for diagnostics and tests.</summary>
    internal IReadOnlyList<string> WatchedPaths
    {
        get
        {
            lock (_watchersLock)
            {
                return new List<string>(_watchers.Keys);
            }
        }
    }

    internal Win32AppSource(
        IWin32ProgramSource source,
        Func<bool>? diagnosticsEnabled = null,
        MEL.ILogger<Win32AppSource>? logger = null)
        : this(source, Win32Program.LoadFromPath, createWatchers: true, diagnosticsEnabled, logger: logger)
    {
    }

    internal Win32AppSource(
        IWin32ProgramSource source,
        Func<string, bool, Win32Program> programLoader,
        bool createWatchers,
        Func<bool>? diagnosticsEnabled = null,
        Func<TimeSpan, CancellationToken, Task>? recoveryDelayAsync = null,
        MEL.ILogger<Win32AppSource>? logger = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _configurationKey = source.ConfigurationKey;
        _programLoader = programLoader ?? throw new ArgumentNullException(nameof(programLoader));
        _createWatchers = createWatchers;
        _diagnosticsEnabled = diagnosticsEnabled ?? (() => false);
        _recoveryDelayAsync = recoveryDelayAsync ?? Task.Delay;
        _logger = logger ?? NullLogger<Win32AppSource>.Instance;
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        lock (_watchersLock)
        {
            ObjectDisposedException.ThrowIf(_disposed.Value, this);
            return _initializationTask ??= _createWatchers
                ? Task.Run(InitializeWatchers, cancellationToken)
                : Task.CompletedTask;
        }
    }

    public Task<IReadOnlyList<AppCatalogItem>> LoadAsync(CancellationToken cancellationToken, bool background = false, IReadOnlyList<AppSourcePathChange>? dirtyPaths = null)
    {
        return AppSourceWork.Run<IReadOnlyList<AppCatalogItem>>(
            () => Load(cancellationToken, background, dirtyPaths),
            background,
            cancellationToken);
    }

    private IReadOnlyList<AppCatalogItem> Load(CancellationToken cancellationToken, bool background = false, IReadOnlyList<AppSourcePathChange>? dirtyPaths = null)
    {
        if (!_source.IsEnabled)
        {
            return new AppSourceScanResult([], isFullScan: true);
        }

        var candidates = new Dictionary<string, Win32ProgramCandidate>(StringComparer.OrdinalIgnoreCase);
        var failedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var retryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in _source.GetCandidates(
            (path, error) => RecordReadFailure(path, error, failedPaths, retryPaths),
            cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = candidate.Path;
            if (string.IsNullOrWhiteSpace(path)
                || (!HasProfileOption(_source.Profile, Win32ProgramSourceProfile.IncludeRawExecutables)
                    && Win32Program.IsExecutablePath(path)))
            {
                continue;
            }

            if (candidates.TryGetValue(path, out var existing))
            {
                if (candidate.MatchTerms.Count > 0)
                {
                    candidates[path] = existing with { MatchTerms = [.. existing.MatchTerms, .. candidate.MatchTerms] };
                }
            }
            else
            {
                candidates.Add(path, candidate);
            }
        }

        HashSet<string>? forceReadPaths = null;
        if (dirtyPaths is { Count: > 0 })
        {
            forceReadPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var change in dirtyPaths)
            {
                forceReadPaths.Add(change.Path);
                if (!string.IsNullOrWhiteSpace(change.OldPath))
                {
                    forceReadPaths.Add(change.OldPath);
                }
            }
        }

        var items = IndexCandidates([.. candidates.Values], background, cancellationToken, forceReadPaths: forceReadPaths);
        foreach (var path in _indexedCandidates.Keys)
        {
            if (!candidates.ContainsKey(path)
                && !failedPaths.Any(failed => string.Equals(path, failed, StringComparison.OrdinalIgnoreCase) || PathHelpers.IsPathInsideDirectory(path, failed)))
            {
                _indexedCandidates.TryRemove(path, out _);
            }
        }

        LogDiagnostic($"Full refresh indexed {candidates.Count} candidate path(s) into {items.Count} item(s).");
        return CompleteScan(items, failedPaths, retryPaths, isFullScan: true);
    }

    public Task<IReadOnlyList<AppCatalogItem>> ApplyChangesAsync(
        IReadOnlyList<AppCatalogItem> currentItems,
        IReadOnlyList<AppSourcePathChange> changes,
        CancellationToken cancellationToken,
        bool background = false)
    {
        ArgumentNullException.ThrowIfNull(currentItems);
        ArgumentNullException.ThrowIfNull(changes);

        return AppSourceWork.Run<IReadOnlyList<AppCatalogItem>>(
            () => ApplyChanges(currentItems, changes, background, cancellationToken),
            background,
            cancellationToken);
    }

    private IReadOnlyList<AppCatalogItem> ApplyChanges(
        IReadOnlyList<AppCatalogItem> currentItems,
        IReadOnlyList<AppSourcePathChange> changes,
        bool background,
        CancellationToken cancellationToken)
    {
        if (changes.Count == 0)
        {
            return currentItems;
        }

        if (!_source.SupportsIncrementalChanges)
        {
            return PromoteDirtyPathsToFullRefresh("the origin requires full enumeration", changes, background, cancellationToken);
        }

        if (changes.Count > IncrementalRefreshPathLimit)
        {
            return PromoteDirtyPathsToFullRefresh(
                $"received {changes.Count} dirty paths",
                changes,
                background,
                cancellationToken);
        }

        var affectedPaths = new List<AffectedPath>();
        var affectedPathKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidatePaths = new List<string>();
        var candidatePathKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var failedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var retryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missingPaths = new List<AffectedPath>();
        var checkedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var change in changes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(change.OldPath)
                && !TryCollectDirtyPath(
                    change.OldPath,
                    affectedPaths,
                    affectedPathKeys,
                    candidatePaths,
                    candidatePathKeys,
                    failedPaths,
                    retryPaths,
                    missingPaths,
                    cancellationToken))
            {
                return PromoteDirtyPathsToFullRefresh("candidate collection exceeded its limit", changes, background, cancellationToken);
            }

            if (!TryCollectDirtyPath(
                change.Path,
                affectedPaths,
                affectedPathKeys,
                candidatePaths,
                candidatePathKeys,
                failedPaths,
                retryPaths,
                missingPaths,
                cancellationToken))
            {
                return PromoteDirtyPathsToFullRefresh("candidate collection exceeded its limit", changes, background, cancellationToken);
            }
        }

        var affectedIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in currentItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsAffected(item.Provenance.References, affectedPaths))
            {
                continue;
            }

            affectedIdentities.Add(item.Identity);
            foreach (var reference in item.Provenance.References)
            {
                if (string.Equals(reference.SourceId, _source.Id, StringComparison.Ordinal)
                    && MatchesAnyAffectedPath(reference.ItemId, missingPaths))
                {
                    checkedPaths.Add(reference.ItemId);
                }

                if (!string.Equals(reference.SourceId, _source.Id, StringComparison.Ordinal)
                    || MatchesAnyAffectedPath(reference.ItemId, affectedPaths))
                {
                    continue;
                }

                if (!TryAddCandidatePath(candidatePaths, candidatePathKeys, reference.ItemId))
                {
                    return PromoteDirtyPathsToFullRefresh("candidate collection exceeded its limit", changes, background, cancellationToken);
                }
            }
        }

        if (affectedIdentities.Count == 0 && candidatePaths.Count == 0 && failedPaths.Count == 0)
        {
            return currentItems;
        }

        var updatedItems = new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in currentItems)
        {
            if (!affectedIdentities.Contains(item.Identity))
            {
                updatedItems[item.Identity] = item;
            }
        }

        // Dirty paths are explicit retry or watcher requests, so they must be read even when stamps match.
        var scan = IndexPaths(candidatePaths, background, cancellationToken);
        checkedPaths.UnionWith(scan.CheckedPaths);
        foreach (var item in scan)
        {
            updatedItems[item.Identity] = updatedItems.TryGetValue(item.Identity, out var existing)
                ? existing.MergeProvenance(item)
                : item;
        }

        LogDiagnostic(
            $"Incremental refresh reconciled {affectedPaths.Count} dirty path(s), affected "
            + $"{affectedIdentities.Count} existing identity/identities, and indexed {candidatePaths.Count} path(s).");
        return CompleteScan(
            new AppSourceScanResult(new List<AppCatalogItem>(updatedItems.Values), scan.IsComplete, scan.RetryPaths, scan.FailedPaths, checkedPaths),
            failedPaths,
            retryPaths);
    }

    private static void RecordReadFailure(string path, Exception error, HashSet<string> failedPaths, HashSet<string> retryPaths)
    {
        failedPaths.Add(path);
        if (Win32Program.IsRetryableReadFailure(error))
        {
            retryPaths.Add(path);
        }
    }

    private static AppSourceScanResult CompleteScan(AppSourceScanResult scan, HashSet<string> failedPaths, HashSet<string> retryPaths, bool isFullScan = false)
    {
        failedPaths.UnionWith(scan.FailedPaths ?? []);
        retryPaths.UnionWith(scan.RetryPaths);
        return new AppSourceScanResult(
            scan,
            scan.IsComplete && failedPaths.Count == 0,
            [.. retryPaths],
            [.. failedPaths],
            scan.CheckedPaths,
            isFullScan: isFullScan,
            reusedRejectedPaths: scan.ReusedRejectedPaths);
    }

    private bool TryCollectDirtyPath(
        string dirtyPath,
        List<AffectedPath> affectedPaths,
        HashSet<string> affectedPathKeys,
        List<string> candidatePaths,
        HashSet<string> candidatePathKeys,
        HashSet<string> failedPaths,
        HashSet<string> retryPaths,
        List<AffectedPath> missingPaths,
        CancellationToken cancellationToken)
    {
        var normalizedPath = PathHelpers.NormalizePath(dirtyPath);
        if (affectedPathKeys.Add(normalizedPath))
        {
            var isFile = File.Exists(normalizedPath);
            var isDirectory = Directory.Exists(normalizedPath);
            affectedPaths.Add(new AffectedPath(normalizedPath, isDirectory || (!isFile && !isDirectory)));
        }

        foreach (var currentPath in _source.GetPathsForChange(
            normalizedPath,
            (path, error) => RecordReadFailure(path, error, failedPaths, retryPaths),
            path => missingPaths.Add(new AffectedPath(path, IncludeDescendants: true)),
            cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryAddCandidatePath(candidatePaths, candidatePathKeys, currentPath))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryAddCandidatePath(
        List<string> candidatePaths,
        HashSet<string> candidatePathKeys,
        string candidatePath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath)
            || (!HasProfileOption(_source.Profile, Win32ProgramSourceProfile.IncludeRawExecutables)
                && Win32Program.IsExecutablePath(candidatePath))
            || !candidatePathKeys.Add(candidatePath))
        {
            return true;
        }

        if (candidatePaths.Count >= IncrementalRefreshPathLimit)
        {
            return false;
        }

        candidatePaths.Add(candidatePath);
        return true;
    }

    private IReadOnlyList<AppCatalogItem> PromoteDirtyPathsToFullRefresh(
        string reason,
        IReadOnlyList<AppSourcePathChange> changes,
        bool background,
        CancellationToken cancellationToken)
    {
        LogDiagnostic($"Promoting incremental reconciliation to a full source refresh because it {reason}.");
        return Load(cancellationToken, background, changes);
    }

    private static bool IsAffected(
        IReadOnlyList<AppCatalogSourceReference> sourceReferences,
        IReadOnlyList<AffectedPath> affectedPaths)
    {
        foreach (var reference in sourceReferences)
        {
            if (MatchesAnyAffectedPath(reference.ItemId, affectedPaths))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesAnyAffectedPath(string candidatePath, IReadOnlyList<AffectedPath> affectedPaths)
    {
        foreach (var affectedPath in affectedPaths)
        {
            if (string.Equals(candidatePath, affectedPath.Path, StringComparison.OrdinalIgnoreCase)
                || (affectedPath.IncludeDescendants
                    && PathHelpers.IsPathInsideDirectory(candidatePath, affectedPath.Path)))
            {
                return true;
            }
        }

        return false;
    }

    private AppSourceScanResult IndexPaths(
        IReadOnlyList<string> paths,
        bool background,
        CancellationToken cancellationToken)
    {
        var candidates = new Win32ProgramCandidate[paths.Count];
        for (var index = 0; index < paths.Count; index++)
        {
            candidates[index] = new Win32ProgramCandidate(paths[index], []);
        }

        return IndexCandidates(candidates, background, cancellationToken, reuseUnchanged: false);
    }

    private AppSourceScanResult IndexCandidates(
        IReadOnlyList<Win32ProgramCandidate> candidates,
        bool background,
        CancellationToken cancellationToken,
        bool reuseUnchanged = true,
        IReadOnlySet<string>? forceReadPaths = null)
    {
        var diagnostics = _diagnosticsEnabled() && _logger.IsEnabled(LogLevel.Information);
        var started = diagnostics ? Stopwatch.GetTimestamp() : 0;
        var loadedCandidates = 0;
        var reusedCandidates = 0;
        var indexedItems = new ConcurrentBag<AppCatalogItem>();
        var retryPaths = new ConcurrentBag<string>();
        var reusedRejectedPaths = new ConcurrentBag<string>();
        var failedPaths = new ConcurrentBag<string>();
        var checkedPaths = new ConcurrentBag<string>();
        void IndexCandidate(Win32ProgramCandidate candidate)
        {
            var path = candidate.Path;
            var stamp = FileStamp.TryRead(path);
            if (background && reuseUnchanged
                && forceReadPaths?.Contains(path) != true
                && stamp is { Exists: true }
                && (stamp.Value.Attributes & FileAttributes.ReparsePoint) == 0
                && _indexedCandidates.TryGetValue(path, out var cached)
                && (cached.Item is null || string.Equals(path, cached.Item.Provenance.References[0].ItemId, StringComparison.Ordinal))
                && cached.SourceStamp == stamp
                && candidate.MatchTerms.SequenceEqual(cached.MatchTerms, StringComparer.OrdinalIgnoreCase)
                && cached.TargetStamp is not null
                && (cached.TargetStamp.Value.Attributes & FileAttributes.ReparsePoint) == 0
                && cached.TargetStamp == (string.Equals(path, cached.TargetPath, StringComparison.OrdinalIgnoreCase)
                    ? stamp
                    : FileStamp.TryRead(cached.TargetPath)))
            {
                if (cached.Item is not null)
                {
                    indexedItems.Add(cached.Item);
                }
                else
                {
                    reusedRejectedPaths.Add(path);
                }

                checkedPaths.Add(path);
                if (diagnostics)
                {
                    Interlocked.Increment(ref reusedCandidates);
                }

                return;
            }

            _indexedCandidates.TryRemove(path, out var previousCandidate);
            if (diagnostics)
            {
                Interlocked.Increment(ref loadedCandidates);
            }

            var candidateStarted = diagnostics ? Stopwatch.GetTimestamp() : 0;
            var program = _programLoader(
                path,
                HasProfileOption(_source.Profile, Win32ProgramSourceProfile.LoadAsRunCommand));
            if (diagnostics)
            {
                var durationMs = Stopwatch.GetElapsedTime(candidateStarted).TotalMilliseconds;
                if (durationMs >= 250)
                {
                    LogDiagnosticSlowCandidate(_logger, Id, path, durationMs, program.Valid, program.RetryableReadFailure);
                }
            }

            if (!program.Valid)
            {
                if (program.Unreadable || program.RetryableReadFailure)
                {
                    failedPaths.Add(path);
                    if (previousCandidate?.Item is not null)
                    {
                        indexedItems.Add(previousCandidate.Item);
                        _indexedCandidates[path] = previousCandidate;
                    }

                    if (program.RetryableReadFailure)
                    {
                        retryPaths.Add(path);
                    }
                }
                else
                {
                    checkedPaths.Add(path);
                    CacheCandidate(candidate, stamp, program, item: null);
                    if (File.Exists(path) && string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase))
                    {
                        retryPaths.Add(path);
                    }
                }

                return;
            }

            checkedPaths.Add(path);
            var isNonApp = program.AppType is Win32Program.ApplicationType.GenericFile or Win32Program.ApplicationType.Folder;
            if (isNonApp && !HasProfileOption(_source.Profile, Win32ProgramSourceProfile.IncludeNonApplications))
            {
                CacheCandidate(candidate, stamp, program, item: null);
                return;
            }

            var workingDirectory = Win32Program.GetDistinctWorkingDirectory(program.AppExecutionAlias?.TargetPath ?? program.FullPath, program.WorkingDirectory, program.ExplicitAppUserModelId);
            var target = string.IsNullOrWhiteSpace(program.FullPath) ? LaunchTarget.FilePath(path) : program.Target;

            var identity = CreateCatalogIdentity(target.IdentityToken, program.Arguments, workingDirectory);
            var launchIdentity = CreateCatalogIdentity(LaunchTarget.FilePath(path).IdentityToken, program.Arguments, workingDirectory);
            var item = new AppCatalogItem(
                identity,
                GetRepresentationPriority(program, path) + _source.Priority,
                new AppCatalogSourceReference(_source.Id, path),
                CreateMatchTerms(program, path, candidate.MatchTerms),
                Win32AppPayload.From(program),
                [launchIdentity]);
            indexedItems.Add(item);
            CacheCandidate(candidate, stamp, program, item);
        }

        if (background)
        {
            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IndexCandidate(candidate);
            }
        }
        else
        {
            Parallel.ForEach(
                candidates,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = IndexingMaxDegreeOfParallelism,
                },
                IndexCandidate);
        }

        var itemsByIdentity = new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in indexedItems)
        {
            if (itemsByIdentity.TryGetValue(item.Identity, out var existing))
            {
                itemsByIdentity[item.Identity] = existing.MergeProvenance(item);
            }
            else
            {
                itemsByIdentity.Add(item.Identity, item);
            }
        }

        if (diagnostics)
        {
            var durationMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            LogDiagnosticIndexing(_logger, Id, background, candidates.Count, loadedCandidates, reusedCandidates, itemsByIdentity.Count, durationMs);
        }

        return new AppSourceScanResult(
            new List<AppCatalogItem>(itemsByIdentity.Values),
            failedPaths.IsEmpty,
            [.. retryPaths],
            [.. failedPaths],
            new HashSet<string>(checkedPaths, StringComparer.OrdinalIgnoreCase),
            reusedRejectedPaths: [.. reusedRejectedPaths]);
    }

    private void CacheCandidate(Win32ProgramCandidate candidate, FileStamp? stamp, Win32Program program, AppCatalogItem? item)
    {
        if (program.AppExecutionAlias is not null)
        {
            // Execution-alias ownership can change without a useful file-stamp change.
            return;
        }

        var targetPath = string.IsNullOrWhiteSpace(program.FullPath) ? candidate.Path : program.FullPath;
        _indexedCandidates[candidate.Path] = new IndexedCandidate(
            stamp,
            targetPath,
            string.Equals(candidate.Path, targetPath, StringComparison.OrdinalIgnoreCase) ? stamp : FileStamp.TryRead(targetPath),
            candidate.MatchTerms.ToArray(),
            item);
    }

    private static bool HasProfileOption(
        Win32ProgramSourceProfile profile,
        Win32ProgramSourceProfile option)
    {
        return (profile & option) != 0;
    }

    private static int GetRepresentationPriority(Win32Program program, string sourcePath)
    {
        return Win32Program.IsExecutablePath(sourcePath) && program.AppExecutionAlias is null
            ? RawExecutablePriorityOffset
            : 0;
    }

    private static string CreateCatalogIdentity(string target, string? arguments, string workingDirectory)
    {
        var encodedArguments = Convert.ToHexString(Encoding.UTF8.GetBytes(arguments ?? string.Empty));
        var identity = $"win32:{target}|args:{encodedArguments}";
        return string.IsNullOrEmpty(workingDirectory)
            ? identity
            : $"{identity}|cwd:{workingDirectory}";
    }

    private static List<string> CreateMatchTerms(Win32Program program, string sourcePath, IReadOnlyList<string> sourceTerms)
    {
        List<string> terms = [];
        var uniqueTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddMatchTerm(terms, uniqueTerms, program.Name);
        AddMatchTerm(terms, uniqueTerms, program.NameLocalized);
        AddMatchTerm(terms, uniqueTerms, program.Description);
        AddMatchTerm(terms, uniqueTerms, program.FullPath);
        AddMatchTerm(terms, uniqueTerms, program.FullPathLocalized);
        AddMatchTerm(terms, uniqueTerms, program.ParentDirectory);
        AddMatchTerm(terms, uniqueTerms, program.ExecutableName);
        AddMatchTerm(terms, uniqueTerms, program.ExecutableNameLocalized);
        AddMatchTerm(terms, uniqueTerms, program.LnkResolvedExecutableName);
        AddMatchTerm(terms, uniqueTerms, program.LnkResolvedExecutableNameLocalized);
        AddMatchTerm(terms, uniqueTerms, program.ExplicitAppUserModelId);
        AddMatchTerm(terms, uniqueTerms, sourcePath);
        foreach (var term in sourceTerms)
        {
            AddMatchTerm(terms, uniqueTerms, term);
        }

        return terms;
    }

    private static void AddMatchTerm(List<string> terms, HashSet<string> uniqueTerms, string? term)
    {
        if (!string.IsNullOrWhiteSpace(term) && uniqueTerms.Add(term))
        {
            terms.Add(term);
        }
    }

    /// <summary>Creates the configured Win32 discovery origins without probing their paths.</summary>
    internal static IReadOnlyList<IWin32ProgramSource> CreateProgramSources(AllAppsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        List<IWin32ProgramSource> sources =
        [
            new StartMenuAppSource(settings),
            new DesktopAppSource(settings),
            new RegistryAppSource(settings),
            new PathEnvironmentAppSource(settings),
        ];

        AddCustomSources(
            sources,
            settings.CustomShortcutFolders,
            "custom-shortcut",
            settings.ProgramSuffixes,
            Win32ProgramSourceProfile.IncludeNonApplications | Win32ProgramSourceProfile.RecurseSubdirectories,
            int.MaxValue);
        AddCustomSources(
            sources,
            settings.PortableAppFolders,
            "portable",
            PortableAppSuffixes,
            Win32ProgramSourceProfile.IncludeRawExecutables | Win32ProgramSourceProfile.RecurseSubdirectories,
            PortableAppMaximumDepth);

        return sources;
    }

    private static void AddCustomSources(
        List<IWin32ProgramSource> sources,
        IReadOnlyList<string> configuredPaths,
        string idPrefix,
        IReadOnlyList<string> suffixes,
        Win32ProgramSourceProfile profile,
        int maximumDepth)
    {
        var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var configuredPath in configuredPaths)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                continue;
            }

            var expandedPath = Environment.ExpandEnvironmentVariables(configuredPath.Trim());
            var normalizedPath = PathHelpers.NormalizePath(expandedPath);
            if (string.IsNullOrWhiteSpace(normalizedPath) || !uniquePaths.Add(normalizedPath))
            {
                continue;
            }

            sources.Add(new CustomDirectoryAppSource(
                idPrefix,
                normalizedPath,
                suffixes,
                profile,
                maximumDepth));
        }
    }

    private void InitializeWatchers()
    {
        if (_disposed.Value || !_source.IsEnabled)
        {
            return;
        }

        var watchedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sourcePath in _source.WatchPaths)
        {
            var path = PathHelpers.NormalizePath(sourcePath);
            if (string.IsNullOrWhiteSpace(path) || !watchedPaths.Add(path))
            {
                continue;
            }

            TryCreateWatcher(
                path,
                _source.MaximumDepth > 0,
                previousAttempt: 0);
        }
    }

    private bool TryCreateWatcher(string path, bool recurse, int previousAttempt)
    {
        FileSystemWatcher? watcher = null;
        try
        {
            watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = recurse,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
            };
            watcher.Created += OnFileSystemChanged;
            watcher.Deleted += OnFileSystemChanged;
            watcher.Changed += OnFileSystemChanged;
            watcher.Renamed += OnFileSystemRenamed;
            watcher.Error += OnFileSystemWatcherError;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            watcher?.Dispose();
            if (previousAttempt == 0 && Directory.Exists(path))
            {
                LogWatcherSetupFailed(_logger, path, ex);
            }

            LogDiagnostic($"Watcher creation failed for '{path}' (attempt {previousAttempt + 1}): {ex.Message}");
            lock (_watchersLock)
            {
                ScheduleWatcherRecoveryUnderLock(path, recurse, previousAttempt + 1);
            }

            return false;
        }

        var registered = false;
        lock (_watchersLock)
        {
            if (!_disposed.Value
                && !_watchers.ContainsKey(path)
                && !_watcherRecoveries.ContainsKey(path))
            {
                _watchers.Add(path, watcher);
                registered = true;
            }
        }

        if (!registered)
        {
            DisposeWatcher(watcher);
            return false;
        }

        try
        {
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or ObjectDisposedException)
        {
            var removed = false;
            lock (_watchersLock)
            {
                if (_watchers.TryGetValue(path, out var registeredWatcher)
                    && ReferenceEquals(registeredWatcher, watcher))
                {
                    _watchers.Remove(path);
                    removed = true;
                    ScheduleWatcherRecoveryUnderLock(path, recurse, previousAttempt + 1);
                }
            }

            DisposeWatcher(watcher);
            if (removed)
            {
                if (previousAttempt == 0 && Directory.Exists(path))
                {
                    LogWatcherSetupFailed(_logger, path, ex);
                }

                LogDiagnostic($"Watcher creation failed for '{path}' (attempt {previousAttempt + 1}): {ex.Message}");
            }

            return false;
        }

        lock (_watchersLock)
        {
            registered = _watchers.TryGetValue(path, out var registeredWatcher)
                && ReferenceEquals(registeredWatcher, watcher);
        }

        if (registered)
        {
            LogDiagnostic($"Watcher armed for '{path}'.");
        }

        return registered;
    }

    private void ScheduleWatcherRecoveryUnderLock(string path, bool recurse, int attempt)
    {
        if (_disposed.Value || _watchers.ContainsKey(path) || _watcherRecoveries.ContainsKey(path))
        {
            return;
        }

        var state = new WatcherRecoveryState(
            attempt,
            ++_nextWatcherRecoveryGeneration,
            recurse);
        _watcherRecoveries[path] = state;
        var delay = GetWatcherRecoveryDelay(attempt);
        LogDiagnostic($"Watcher for '{path}' will be re-armed in {delay.TotalSeconds:0} seconds (attempt {attempt}).");
        _ = RecoverWatcherAsync(path, state, delay);
    }

    private async Task RecoverWatcherAsync(string path, WatcherRecoveryState scheduledState, TimeSpan delay)
    {
        // Recovery can be injected with an already-completed delay in tests. Always yield so
        // watcher construction cannot run reentrantly under the lock that scheduled recovery.
        await Task.Yield();

        try
        {
            await _recoveryDelayAsync(delay, _disposeCancellation.Token).ConfigureAwait(false);
            var recovered = false;
            var shouldRecover = false;
            lock (_watchersLock)
            {
                if (_disposed.Value
                    || !_watcherRecoveries.TryGetValue(path, out var currentState)
                    || currentState.Generation != scheduledState.Generation)
                {
                    return;
                }

                _watcherRecoveries.Remove(path);
                shouldRecover = true;
            }

            if (shouldRecover && IsDesiredWatcherPath(path, out var recurse))
            {
                recovered = TryCreateWatcher(path, recurse, scheduledState.Attempt);
            }

            if (recovered)
            {
                LogDiagnostic($"Watcher for '{path}' was re-armed; reconciling its blind interval.");
                RaiseInvalidated(AppSourceInvalidatedEventArgs.FullRefresh);
            }
        }
        catch (OperationCanceledException) when (_disposeCancellation.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (_disposed.Value)
        {
        }
        catch (Exception ex)
        {
            LogWatcherRecoveryFailed(_logger, path, ex);
            if (IsDesiredWatcherPath(path, out var recurse))
            {
                lock (_watchersLock)
                {
                    ScheduleWatcherRecoveryUnderLock(
                        path,
                        recurse,
                        scheduledState.Attempt + 1);
                }
            }
        }
    }

    private bool IsDesiredWatcherPath(string path, out bool recurse)
    {
        recurse = _source.MaximumDepth > 0;
        if (!_source.IsEnabled)
        {
            return false;
        }

        foreach (var sourcePath in _source.WatchPaths)
        {
            if (string.Equals(PathHelpers.NormalizePath(sourcePath), path, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the capped backoff used for a one-based watcher recovery attempt.</summary>
    internal static TimeSpan GetWatcherRecoveryDelay(int attempt)
    {
        var exponent = Math.Clamp(Math.Max(attempt, 1) - 1, 0, 10);
        var delayTicks = InitialWatcherRecoveryDelay.Ticks * (1L << exponent);
        return TimeSpan.FromTicks(Math.Min(delayTicks, MaximumWatcherRecoveryDelay.Ticks));
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs args)
    {
        if (args.ChangeType == WatcherChangeTypes.Changed && Directory.Exists(args.FullPath))
        {
            return;
        }

        if (_source.IsRelevantPath(args.FullPath))
        {
            LogDiagnostic($"Watcher reported {args.ChangeType} for '{args.FullPath}'.");
            RaiseInvalidated(
                AppSourceInvalidatedEventArgs.ForPath(
                    new AppSourcePathChange(args.ChangeType, args.FullPath)));
        }
    }

    private void OnFileSystemRenamed(object sender, RenamedEventArgs args)
    {
        if (_source.IsRelevantPath(args.FullPath) || _source.IsRelevantPath(args.OldFullPath))
        {
            LogDiagnostic($"Watcher reported Renamed from '{args.OldFullPath}' to '{args.FullPath}'.");
            RaiseInvalidated(
                AppSourceInvalidatedEventArgs.ForPath(
                    new AppSourcePathChange(WatcherChangeTypes.Renamed, args.FullPath, args.OldFullPath)));
        }
    }

    private void OnFileSystemWatcherError(object sender, ErrorEventArgs args)
    {
        string? failedPath = null;
        FileSystemWatcher? failedWatcher = null;
        if (sender is FileSystemWatcher watcher)
        {
            lock (_watchersLock)
            {
                foreach (var registeredWatcher in _watchers)
                {
                    if (ReferenceEquals(watcher, registeredWatcher.Value))
                    {
                        failedPath = registeredWatcher.Key;
                        break;
                    }
                }

                if (failedPath is not null)
                {
                    var recurse = watcher.IncludeSubdirectories;
                    _watchers.Remove(failedPath);
                    failedWatcher = watcher;
                    ScheduleWatcherRecoveryUnderLock(failedPath, recurse, attempt: 1);
                }
            }
        }

        if (failedWatcher is not null)
        {
            DisposeWatcher(failedWatcher);
        }

        if (failedPath is not null)
        {
            LogWatcherFailed(_logger, failedPath, args.GetException());
            LogDiagnostic($"Watcher for '{failedPath}' failed; requesting a source reconciliation.");
            RaiseInvalidated(AppSourceInvalidatedEventArgs.FullRefresh);
        }
    }

    private void DisposeWatcher(FileSystemWatcher watcher)
    {
        try
        {
            watcher.EnableRaisingEvents = false;
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
        {
        }

        watcher.Created -= OnFileSystemChanged;
        watcher.Deleted -= OnFileSystemChanged;
        watcher.Changed -= OnFileSystemChanged;
        watcher.Renamed -= OnFileSystemRenamed;
        watcher.Error -= OnFileSystemWatcherError;
        watcher.Dispose();
    }

    private void RaiseInvalidated(AppSourceInvalidatedEventArgs args)
    {
        if (!_disposed.Value)
        {
            Invalidated?.Invoke(this, args);
        }
    }

    private void LogDiagnostic(string message)
    {
        if (_diagnosticsEnabled())
        {
            LogDiagnosticMessage(_logger, Id, message);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Failed to watch application source directory '{Path}'.")]
    private static partial void LogWatcherSetupFailed(MEL.ILogger logger, string path, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to recover the application source watcher for '{Path}'.")]
    private static partial void LogWatcherRecoveryFailed(MEL.ILogger logger, string path, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "The application source watcher for '{Path}' reported an error.")]
    private static partial void LogWatcherFailed(MEL.ILogger logger, string path, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "[AppCatalog diagnostics] [{SourceId}] {Message}")]
    private static partial void LogDiagnosticMessage(MEL.ILogger logger, string sourceId, string message);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "[AppCatalog diagnostics] Source {SourceId} indexed {CandidateCount} candidates in {DurationMs} ms: background={Background}, loaded={LoadedCount}, reused={ReusedCount}, items={ItemCount}.")]
    private static partial void LogDiagnosticIndexing(MEL.ILogger logger, string sourceId, bool background, int candidateCount, int loadedCount, int reusedCount, int itemCount, double durationMs);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "[AppCatalog diagnostics] Source {SourceId} read '{Path}' in {DurationMs} ms: valid={Valid}, retryableReadFailure={RetryableReadFailure}.")]
    private static partial void LogDiagnosticSlowCandidate(MEL.ILogger logger, string sourceId, string path, double durationMs, bool valid, bool retryableReadFailure);

    public void Dispose()
    {
        if (!_disposed.Set())
        {
            return;
        }

        _disposeCancellation.Cancel();
        List<FileSystemWatcher> watchers;
        lock (_watchersLock)
        {
            watchers = new List<FileSystemWatcher>(_watchers.Values);
            _watchers.Clear();
            _watcherRecoveries.Clear();
            Invalidated = null;
        }

        foreach (var watcher in watchers)
        {
            DisposeWatcher(watcher);
        }

        _disposeCancellation.Dispose();
    }

    private sealed record IndexedCandidate(
        FileStamp? SourceStamp,
        string TargetPath,
        FileStamp? TargetStamp,
        IReadOnlyList<string> MatchTerms,
        AppCatalogItem? Item);

    private sealed record AffectedPath(string Path, bool IncludeDescendants);

    private sealed record WatcherRecoveryState(int Attempt, long Generation, bool IncludeSubdirectories);
}
