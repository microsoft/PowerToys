// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.TextExpander.Storage;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Loads and watches the snippet file using the same grammar as the Settings editor.
/// </summary>
internal sealed class SnippetStore : IDisposable
{
    /// <summary>
    /// The name a new snippet file gets. Settings has the same name in
    /// <c>TextExpanderSnippetFileNames.Current</c>, and a test checks that they match.
    /// </summary>
    internal const string FileName = "PowerToysTextExpanderSnippets.txt";

    /// <summary>
    /// The name earlier versions used. It is still read, and never renamed: a folder sync service
    /// could propagate the rename while another PC still runs an earlier version.
    /// </summary>
    internal const string LegacyFileName = "snippets.txt";

    /// <summary>Immutable snapshot so the keyboard hook can read without locking.</summary>
    public sealed class Snapshot
    {
        public required Dictionary<string, string> Map { get; init; }

        /// <summary>
        /// Gets suffix index over the triggers. Replaces the old longest-first array, which the hook
        /// had to scan in full on every keystroke; see <see cref="TriggerIndex"/> for why that
        /// was a correctness problem and not only a slow one.
        /// </summary>
        public required TriggerIndex Triggers { get; init; }

        public required int MaxTriggerLength { get; init; }

        public static readonly Snapshot Empty = new()
        {
            Map = new Dictionary<string, string>(StringComparer.Ordinal),
            Triggers = TriggerIndex.Empty,
            MaxTriggerLength = 0,
        };
    }

    private sealed class StoreState
    {
        public StoreState(long generation, string filePath, bool resolveFolderNames, bool caseSensitive, Snapshot snapshot)
        {
            Generation = generation;
            FilePath = filePath;
            ResolveFolderNames = resolveFolderNames;
            CaseSensitive = caseSensitive;
            Snapshot = snapshot;
        }

        public long Generation { get; }

        public string FilePath { get; }

        public bool ResolveFolderNames { get; }

        public bool CaseSensitive { get; }

        public Snapshot Snapshot { get; }
    }

    private sealed class TransitionTarget
    {
        public TransitionTarget(long generation, string filePath, bool resolveFolderNames, bool caseSensitive)
        {
            Generation = generation;
            FilePath = filePath;
            ResolveFolderNames = resolveFolderNames;
            CaseSensitive = caseSensitive;
        }

        public long Generation { get; }

        public string FilePath { get; }

        public bool ResolveFolderNames { get; }

        public bool CaseSensitive { get; }
    }

    private sealed class PublishedState
    {
        public PublishedState(StoreState state, WatcherRegistration? watcher, bool hasLoadedState)
        {
            State = state;
            Watcher = watcher;
            HasLoadedState = hasLoadedState;
        }

        public StoreState State { get; }

        public WatcherRegistration? Watcher { get; }

        public bool HasLoadedState { get; }
    }

    private sealed class WatcherRegistration : IDisposable
    {
        private int _pendingFileEvent;
        private int _disposed;

        public WatcherRegistration(long identity, long transitionGeneration, FileSystemWatcher watcher)
        {
            Identity = identity;
            TransitionGeneration = transitionGeneration;
            Watcher = watcher;
        }

        public long Identity { get; }

        public long TransitionGeneration { get; }

        public FileSystemWatcher Watcher { get; }

        public bool RecordFileEvent()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return false;
            }

            Interlocked.Exchange(ref _pendingFileEvent, 1);
            return Volatile.Read(ref _disposed) == 0;
        }

        public bool ConsumePendingFileEvent()
            => Volatile.Read(ref _disposed) == 0
                && Interlocked.Exchange(ref _pendingFileEvent, 0) != 0;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Interlocked.Exchange(ref _pendingFileEvent, 0);
                Watcher.Dispose();
            }
        }
    }

    private enum ReloadRequestKind
    {
        DesiredTransition,
        PublishedWatcher,
    }

    private sealed class ReloadRequest
    {
        public ReloadRequest(
            long sequence,
            ReloadRequestKind kind,
            long desiredGeneration,
            WatcherRegistration? watcher,
            int failureCount,
            TimeSpan delay)
        {
            Sequence = sequence;
            Kind = kind;
            DesiredGeneration = desiredGeneration;
            Watcher = watcher;
            FailureCount = failureCount;
            Delay = delay;
        }

        public long Sequence { get; }

        public ReloadRequestKind Kind { get; }

        public long DesiredGeneration { get; }

        public WatcherRegistration? Watcher { get; }

        public int FailureCount { get; }

        public TimeSpan Delay { get; }
    }

    private readonly record struct ReloadRetry(
        ReloadRequestKind Kind,
        long DesiredGeneration,
        WatcherRegistration? Watcher,
        TransitionResult Failure);

    internal enum TransitionStatus
    {
        Succeeded,
        Unchanged,
        Failed,
        Superseded,
    }

    internal readonly record struct TransitionResult(
        TransitionStatus Status,
        int Count,
        string AttemptedPath,
        Exception? Error,
        bool HasCurrentState = true)
    {
        public bool Succeeded => Status is TransitionStatus.Succeeded or TransitionStatus.Unchanged;

        public bool Changed => Status == TransitionStatus.Succeeded;
    }

    internal sealed class TestHooks
    {
        public Action<long>? BeforeWatcherReload { get; init; }

        public Action<long>? AfterWatcherReload { get; init; }

        public Action<long>? BeforeWatcherActivation { get; init; }

        public Action<long>? AfterWatcherHandoffRead { get; init; }

        public Action<long, Action<Exception>>? WatcherErrorHandlerRegistered { get; init; }

        public Action<long, Action>? WatcherFileEventHandlerRegistered { get; init; }
    }

    private static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMilliseconds(250);
    private const int DefaultMaxReloadAttempts = 4;

    private readonly object _requestLock = new();
    private readonly object _transitionLock = new();
    private readonly object _debounceLock = new();
    private readonly Func<string, string[]> _readAllLines;
    private readonly TimeSpan _debounceDelay;
    private readonly TimeSpan _retryDelay;
    private readonly int _maxReloadAttempts;
    private readonly Action<string> _log;
    private readonly TestHooks? _testHooks;
    private volatile PublishedState _published;
    private volatile TransitionTarget _desired;
    private Timer? _debounce;
    private long _nextGeneration;
    private long _nextWatcherIdentity;
    private long _nextReloadSequence;
    private ReloadRequest? _scheduledReload;
    private bool _reloadWorkerActive;
    private int _watchingRequested;
    private int _disposed;

    public string FilePath => _published.State.FilePath;

    public Snapshot Current => _published.State.Snapshot;

    public bool HasLoadedState => _published.HasLoadedState;

    public bool IsWatching => _published.Watcher?.Watcher.EnableRaisingEvents == true;

    /// <summary>Raised after a successful reload. Fires on a thread pool thread.</summary>
    public event Action<int>? Reloaded;

    public SnippetStore(string filePath, bool resolveFolderNames = false, bool caseSensitive = true)
        : this(
            filePath,
            resolveFolderNames,
            caseSensitive,
            ReadAllLinesShared,
            DefaultDebounceDelay,
            DefaultRetryDelay,
            DefaultMaxReloadAttempts,
            message => System.Diagnostics.Trace.TraceWarning(message))
    {
    }

    internal SnippetStore(
        string filePath,
        bool resolveFolderNames,
        bool caseSensitive,
        Func<string, string[]> readAllLines,
        TimeSpan debounceDelay,
        TimeSpan retryDelay,
        int maxReloadAttempts,
        Action<string>? log = null,
        TestHooks? testHooks = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(readAllLines);
        ArgumentOutOfRangeException.ThrowIfLessThan(debounceDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(retryDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxReloadAttempts, 1);

        var initialState = new StoreState(0, filePath, resolveFolderNames, caseSensitive, Snapshot.Empty);
        _published = new PublishedState(initialState, watcher: null, hasLoadedState: false);
        _desired = new TransitionTarget(0, filePath, resolveFolderNames, caseSensitive);
        _readAllLines = readAllLines;
        _debounceDelay = debounceDelay;
        _retryDelay = retryDelay;
        _maxReloadAttempts = maxReloadAttempts;
        _log = log ?? (_ => { });
        _testHooks = testHooks;
    }

    // ── Path resolution ──────────────────────────────────────

    /// <summary>
    /// Resolves the snippet file for a configured folder, or falls back to
    /// <see cref="ResolveDefaultPath"/> when nothing was configured or the value is unusable,
    /// including paths that are not fully qualified after environment variable expansion.
    ///
    /// <para>
    /// A folder is accepted rather than a file path because that is what the setting asks for,
    /// and because pointing the engine at an arbitrary file makes the "reloads on save" promise
    /// harder to keep. A path that cannot be resolved falls back silently: the alternative is an
    /// engine that refuses to expand anything because a setting has a typo in it.
    /// </para>
    /// </summary>
    public static string ResolveConfiguredPath(string? configuredFolder)
        => ResolveConfiguredPath(configuredFolder, hostDataFolder: null);

    /// <summary>
    /// As above, but falls back to <paramref name="hostDataFolder"/> instead of
    /// <see cref="ResolveDefaultPath"/> when nothing is configured.
    ///
    /// <para>
    /// Under a host such as PowerToys the default must not be "beside the executable". That
    /// directory belongs to the host's installer: on a per-machine install it is not writable,
    /// and on a per-user one the file would be destroyed by the next upgrade. It is also not
    /// somewhere the host's own settings UI can predict, and a snippet editor that cannot find
    /// the file it is editing is no editor at all.
    /// </para>
    /// </summary>
    public static string ResolveConfiguredPath(string? configuredFolder, string? hostDataFolder)
        => ResolveConfiguredPathInfo(configuredFolder, hostDataFolder).FilePath;

    internal static SnippetPathResolution ResolveConfiguredPathInfo(string? configuredFolder, string? hostDataFolder)
    {
        if (string.IsNullOrWhiteSpace(configuredFolder))
        {
            string fallback = string.IsNullOrWhiteSpace(hostDataFolder)
                ? ResolveDefaultPath()
                : ResolveHostPath(hostDataFolder!);
            return new SnippetPathResolution(fallback, ResolveFolderNames: true);
        }

        try
        {
            string expanded = Environment.ExpandEnvironmentVariables(configuredFolder!.Trim().Trim('"'));

            // The engine and its host's editor run in different processes. Their working
            // directories cannot be a shared base for this setting, including drive-relative
            // (C:snips) and root-relative (\snips) paths on Windows.
            if (Path.IsPathFullyQualified(expanded))
            {
                string full = Path.GetFullPath(expanded);

                // Accept a file too: someone will inevitably paste the path to the snippet file itself.
                if (File.Exists(full))
                {
                    return new SnippetPathResolution(full, ResolveFolderNames: false);
                }

                Directory.CreateDirectory(full);
                return new SnippetPathResolution(ResolveInFolder(full), ResolveFolderNames: true);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            // An unusable setting must not prevent the host/default library from loading.
        }

        string fallbackPath = string.IsNullOrWhiteSpace(hostDataFolder)
            ? ResolveDefaultPath()
            : ResolveHostPath(hostDataFolder!);
        return new SnippetPathResolution(fallbackPath, ResolveFolderNames: true);
    }

    /// <summary>
    /// The snippet file inside the host data folder, falling back to the PowerToys per-user folder if that folder cannot be created.
    /// </summary>
    private static string ResolveHostPath(string hostDataFolder)
    {
        try
        {
            string full = Path.GetFullPath(hostDataFolder);
            Directory.CreateDirectory(full);
            return ResolveInFolder(full);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            return ResolveDefaultPath();
        }
    }

    /// <summary>
    /// Resolves the PowerToys per-user snippet file used when no settings path is available.
    /// </summary>
    public static string ResolveDefaultPath()
    {
        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "TextExpander");
        Directory.CreateDirectory(appData);
        return ResolveInFolder(appData);
    }

    /// <summary>
    /// Returns the snippet file in <paramref name="folder"/>: <see cref="FileName"/> if it exists,
    /// otherwise <see cref="LegacyFileName"/> if that exists, otherwise <see cref="FileName"/>.
    /// The legacy file wins when both exist only if the current file is the untouched starter,
    /// recovering a non-participating publication race. Settings uses the same rule.
    /// </summary>
    internal static string ResolveInFolder(string folder)
    {
        string current = Path.Combine(folder, FileName);
        string legacy = Path.Combine(folder, LegacyFileName);
        return TextExpanderStarter.FindExistingFile(current, legacy) ?? current;
    }

    // ── Loading ──────────────────────────────────────────────
    public TransitionResult Load()
    {
        TransitionTarget target;
        lock (_requestLock)
        {
            TransitionTarget desired = _desired;
            target = PublishTransition(desired.FilePath, desired.ResolveFolderNames, desired.CaseSensitive);
        }

        return ExecuteTransition(target);
    }

    private TransitionResult TryStageStateLocked(TransitionTarget target, out StoreState? candidate)
        => TryStageStateLocked(target, () => IsCurrent(target), out candidate);

    private TransitionResult TryStageStateLocked(
        TransitionTarget target,
        Func<bool> isStillCurrent,
        out StoreState? candidate)
    {
        candidate = null;
        string candidatePath = target.FilePath;
        try
        {
            if (target.ResolveFolderNames)
            {
                string? folder = Path.GetDirectoryName(target.FilePath);
                if (string.IsNullOrEmpty(folder))
                {
                    throw new IOException("The Text Expander snippet folder could not be resolved.");
                }

                candidatePath = TextExpanderStarter.ResolveOrCreateFileInFolder(folder, FileName, LegacyFileName).FilePath;
            }
            else
            {
                TextExpanderStarter.TryCreateFile(candidatePath);
            }

            string[] lines = _readAllLines(candidatePath);
            Dictionary<string, string> map = Parse(lines, target.CaseSensitive);
            var index = TriggerIndex.Build(map.Keys, target.CaseSensitive);
            var snapshot = new Snapshot
            {
                Map = map,
                Triggers = index,
                MaxTriggerLength = index.MaxTriggerLength,
            };

            if (Volatile.Read(ref _disposed) != 0)
            {
                return Failed(target, candidatePath, new ObjectDisposedException(nameof(SnippetStore)));
            }

            if (!isStillCurrent())
            {
                return Superseded(target, candidatePath);
            }

            candidate = new StoreState(
                target.Generation,
                candidatePath,
                target.ResolveFolderNames,
                target.CaseSensitive,
                snapshot);
            return new TransitionResult(TransitionStatus.Succeeded, map.Count, candidatePath, Error: null);
        }
        catch (Exception ex) when (IsLoadError(ex))
        {
            // Preserve the complete previous path, metadata, and snippet snapshot. The explicit
            // failed result keeps synchronous callers from mistaking that old count for success.
            return Failed(target, candidatePath, ex);
        }
    }

    private TransitionResult ExecutePublishedWatcherReloadLocked(WatcherRegistration watcher)
    {
        PublishedState current = _published;
        if (!ReferenceEquals(current.Watcher, watcher))
        {
            return Superseded(
                new TransitionTarget(
                    watcher.TransitionGeneration,
                    current.State.FilePath,
                    current.State.ResolveFolderNames,
                    current.State.CaseSensitive),
                current.State.FilePath);
        }

        var target = new TransitionTarget(
            current.State.Generation,
            current.State.FilePath,
            current.State.ResolveFolderNames,
            current.State.CaseSensitive);
        TransitionResult result = TryStageStateLocked(
            target,
            () => ReferenceEquals(_published.Watcher, watcher),
            out StoreState? candidate);
        if (!result.Succeeded || candidate is null)
        {
            return result;
        }

        if (!ReferenceEquals(_published.Watcher, watcher))
        {
            return Superseded(target, candidate.FilePath);
        }

        _published = new PublishedState(candidate, watcher, hasLoadedState: true);
        return result;
    }

    private TransitionResult ExecuteTransition(TransitionTarget target)
    {
        TransitionResult result;
        lock (_transitionLock)
        {
            result = ExecuteTransitionLocked(target);
        }

        if (!result.Succeeded && result.Status != TransitionStatus.Superseded)
        {
            ScheduleRetryAfterFailure(target, result);
        }

        return result;
    }

    private TransitionResult ExecuteTransitionLocked(TransitionTarget target)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return Failed(target, target.FilePath, new ObjectDisposedException(nameof(SnippetStore)));
        }

        if (!IsCurrent(target))
        {
            return Superseded(target, target.FilePath);
        }

        TransitionResult result = TryStageStateLocked(target, out StoreState? candidate);
        if (!result.Succeeded || candidate is null)
        {
            return result;
        }

        PublishedState previous = _published;
        WatcherRegistration? watcher = previous.Watcher;
        bool watcherAlreadyCoversTarget = watcher?.Watcher.EnableRaisingEvents == true
            && previous.State.Generation == target.Generation;
        bool needsCandidateWatcher = Volatile.Read(ref _watchingRequested) != 0
            && !watcherAlreadyCoversTarget;

        if (needsCandidateWatcher)
        {
            result = TryRegisterCandidateWatcherLocked(target, candidate, out watcher);
            if (!result.Succeeded || watcher is null)
            {
                watcher?.Dispose();
                return result;
            }

            // A supported file can arrive after folder resolution and before the watcher becomes
            // active. Re-resolve and read once more under the same generation before publishing
            // either the candidate state or its watcher.
            result = TryStageStateLocked(target, out StoreState? handoffCandidate);
            if (!result.Succeeded || handoffCandidate is null)
            {
                watcher.Dispose();
                return result;
            }

            candidate = handoffCandidate;
            try
            {
                _testHooks?.AfterWatcherHandoffRead?.Invoke(target.Generation);
            }
            catch (Exception ex) when (IsLoadError(ex))
            {
                watcher.Dispose();
                return Failed(target, candidate.FilePath, ex);
            }
        }

        if (Volatile.Read(ref _disposed) != 0)
        {
            if (needsCandidateWatcher)
            {
                watcher?.Dispose();
            }

            return Failed(target, candidate.FilePath, new ObjectDisposedException(nameof(SnippetStore)));
        }

        if (!IsCurrent(target))
        {
            if (needsCandidateWatcher)
            {
                watcher?.Dispose();
            }

            return Superseded(target, candidate.FilePath);
        }

        var next = new PublishedState(candidate, watcher, hasLoadedState: true);
        _published = next;
        bool candidateEventPending = needsCandidateWatcher
            && watcher is not null
            && watcher.ConsumePendingFileEvent();
        if (!ReferenceEquals(previous.Watcher, watcher))
        {
            previous.Watcher?.Dispose();
        }

        if (candidateEventPending)
        {
            ScheduleReload(watcher!);
        }

        return new TransitionResult(
            TransitionStatus.Succeeded,
            candidate.Snapshot.Map.Count,
            candidate.FilePath,
            Error: null);
    }

    private TransitionTarget PublishTransition(string filePath, bool resolveFolderNames, bool caseSensitive)
    {
        long generation = ++_nextGeneration;
        var target = new TransitionTarget(generation, filePath, resolveFolderNames, caseSensitive);
        _desired = target;
        CancelScheduledDesiredReload();
        return target;
    }

    private void CancelScheduledDesiredReload()
    {
        lock (_debounceLock)
        {
            if (_scheduledReload?.Kind == ReloadRequestKind.DesiredTransition)
            {
                _scheduledReload = null;
                if (!_reloadWorkerActive)
                {
                    _debounce?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                }
            }
        }
    }

    private bool IsCurrent(TransitionTarget target)
        => _desired.Generation == target.Generation;

    private static bool SameConfiguration(
        string firstPath,
        bool firstResolveFolderNames,
        bool firstCaseSensitive,
        string secondPath,
        bool secondResolveFolderNames,
        bool secondCaseSensitive)
    {
        if (firstResolveFolderNames != secondResolveFolderNames || firstCaseSensitive != secondCaseSensitive)
        {
            return false;
        }

        if (!firstResolveFolderNames)
        {
            return string.Equals(firstPath, secondPath, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(
            Path.GetDirectoryName(firstPath),
            Path.GetDirectoryName(secondPath),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameConfiguration(StoreState state, TransitionTarget target)
        => SameConfiguration(
            state.FilePath,
            state.ResolveFolderNames,
            state.CaseSensitive,
            target.FilePath,
            target.ResolveFolderNames,
            target.CaseSensitive);

    private static bool IsLoadError(Exception ex)
        => ex is IOException or UnauthorizedAccessException or System.Security.SecurityException
            or ArgumentException or NotSupportedException or PathTooLongException;

    private TransitionResult Failed(TransitionTarget target, string attemptedPath, Exception error)
    {
        PublishedState current = _published;
        return new TransitionResult(
            TransitionStatus.Failed,
            current.State.Snapshot.Map.Count,
            attemptedPath,
            error,
            current.HasLoadedState);
    }

    private TransitionResult Superseded(TransitionTarget target, string attemptedPath)
    {
        PublishedState current = _published;
        return new TransitionResult(
            TransitionStatus.Superseded,
            current.State.Snapshot.Map.Count,
            attemptedPath,
            new OperationCanceledException($"Text Expander snippet transition {target.Generation} was superseded."),
            current.HasLoadedState);
    }

    /// <summary>Reads with permissive sharing so an open editor does not block us.</summary>
    private static string[] ReadAllLinesShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true);

        var lines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lines.Add(line);
        }

        return [.. lines];
    }

    /// <summary>
    /// Parses the snippet file format: "//" comments, "trigger=replacement", and
    /// "trigger&lt;&lt;&lt; ... &gt;&gt;&gt;" multi-line blocks. Replacement text is taken from the
    /// raw line so trailing spaces survive.
    /// </summary>
    internal static Dictionary<string, string> Parse(string[] lines, bool caseSensitive = true)
    {
        var result = new Dictionary<string, string>(caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        int i = 0;

        while (i < lines.Length)
        {
            string line = lines[i];
            string stripped = line.Trim();
            i++;

            if (stripped.Length == 0 || stripped.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (stripped.EndsWith("<<<", StringComparison.Ordinal))
            {
                string trigger = stripped[..^3];
                var body = new List<string>();
                while (i < lines.Length)
                {
                    string bodyLine = lines[i];
                    i++;
                    if (bodyLine.Trim() == ">>>")
                    {
                        break;
                    }

                    body.Add(bodyLine);
                }

                if (trigger.Length > 0)
                {
                    result.Remove(trigger);
                    result[trigger] = string.Join("\n", body);
                }
            }
            else
            {
                int eq = stripped.IndexOf('=');
                if (eq > 0)
                {
                    int rawEq = line.IndexOf('=');
                    string trigger = stripped[..eq];
                    result.Remove(trigger);
                    result[trigger] = line[(rawEq + 1)..];
                }
            }
        }

        return result;
    }

    // ── Matching ─────────────────────────────────────────────

    /// <summary>
    /// Returns the longest trigger that is a suffix of <paramref name="tail"/>, or null.
    ///
    /// Runs inside the low-level keyboard hook on every keystroke, so it must not depend on how
    /// many snippets exist: the index makes this a walk of at most <c>MaxTriggerLength</c> steps
    /// whether the user has ten triggers or ten thousand.
    /// </summary>
    public string? Match(ReadOnlySpan<char> tail) => _published.State.Snapshot.Triggers.MatchLongestSuffix(tail);

    public bool TryGetReplacement(string trigger, out string replacement)
        => _published.State.Snapshot.Map.TryGetValue(trigger, out replacement!);

    /// <summary>
    /// Changes whether trigger matching distinguishes capitalization, then rebuilds the immutable
    /// lookup from the desired file. A failed transition keeps the previous complete state live
    /// while bounded retries continue toward the requested setting.
    /// </summary>
    public TransitionResult SetCaseSensitive(bool caseSensitive)
    {
        TransitionTarget target;
        lock (_requestLock)
        {
            TransitionTarget desired = _desired;
            PublishedState current = _published;
            if (desired.CaseSensitive == caseSensitive
                && current.HasLoadedState
                && current.State.Generation == desired.Generation
                && SameConfiguration(current.State, desired))
            {
                return new TransitionResult(
                    TransitionStatus.Unchanged,
                    current.State.Snapshot.Map.Count,
                    current.State.FilePath,
                    Error: null);
            }

            target = PublishTransition(desired.FilePath, desired.ResolveFolderNames, caseSensitive);
        }

        return ExecuteTransition(target);
    }

    // ── Watching ─────────────────────────────────────────────
    public TransitionResult StartWatching()
    {
        Interlocked.Exchange(ref _watchingRequested, 1);
        return ExecuteTransition(_desired);
    }

    /// <summary>
    /// Points this store at a different snippet file, reloading and re-watching.
    ///
    /// <para>
    /// Deliberately mutates this instance rather than handing back a new one: the keyboard hook
    /// captures the store at construction, so replacing the object would leave the hook reading
    /// the old file forever. The transition stages the parsed state, candidate watcher, and
    /// handoff re-read before one published-state swap, so consumers see either the complete old
    /// path/snapshot/watcher or the complete new set.
    /// </para>
    ///
    /// <para>
    /// The result distinguishes success, no change, failure, and a transition superseded by a
    /// newer request. Failure leaves the old state live while bounded retries retain this target.
    /// </para>
    /// </summary>
    public TransitionResult Repoint(string newPath, bool resolveFolderNames = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPath);

        TransitionTarget target;
        lock (_requestLock)
        {
            TransitionTarget desired = _desired;
            PublishedState current = _published;
            if (SameConfiguration(
                    desired.FilePath,
                    desired.ResolveFolderNames,
                    desired.CaseSensitive,
                    newPath,
                    resolveFolderNames,
                    desired.CaseSensitive)
                && current.HasLoadedState
                && current.State.Generation == desired.Generation
                && SameConfiguration(current.State, desired))
            {
                return new TransitionResult(
                    TransitionStatus.Unchanged,
                    current.State.Snapshot.Map.Count,
                    current.State.FilePath,
                    Error: null);
            }

            target = PublishTransition(newPath, resolveFolderNames, desired.CaseSensitive);
        }

        return ExecuteTransition(target);
    }

    private TransitionResult TryRegisterCandidateWatcherLocked(
        TransitionTarget target,
        StoreState state,
        out WatcherRegistration? watcher)
    {
        watcher = null;
        if (Volatile.Read(ref _watchingRequested) == 0 || Volatile.Read(ref _disposed) != 0)
        {
            return Failed(target, state.FilePath, new ObjectDisposedException(nameof(SnippetStore)));
        }

        FileSystemWatcher? fileWatcher = null;
        try
        {
            string? dir = Path.GetDirectoryName(state.FilePath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                throw new DirectoryNotFoundException($"The Text Expander snippet folder '{dir}' is not available.");
            }

            fileWatcher = new FileSystemWatcher(dir)
            {
                // Watch the directory rather than the file: many editors save by writing a
                // temporary file and renaming over the original, which destroys a file-level watch.
                // A folder setting watches both supported names, so a later preferred file can
                // replace a legacy one without Settings and the engine splitting onto two files.
                Filter = state.ResolveFolderNames ? "*" : Path.GetFileName(state.FilePath),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                IncludeSubdirectories = false,
            };

            watcher = new WatcherRegistration(++_nextWatcherIdentity, state.Generation, fileWatcher);
            WatcherRegistration registration = watcher;
            void ScheduleFileReload()
            {
                if (registration.RecordFileEvent())
                {
                    ScheduleReload(registration);
                }
            }

            FileSystemEventHandler onFileEvent = (_, e) =>
            {
                if (IsRelevantFileName(state, e.Name)
                    || (e is RenamedEventArgs renamed && IsRelevantFileName(state, renamed.OldName)))
                {
                    ScheduleFileReload();
                }
            };

            RenamedEventHandler onRenamed = (_, e) =>
            {
                if (IsRelevantFileName(state, e.Name) || IsRelevantFileName(state, e.OldName))
                {
                    ScheduleFileReload();
                }
            };

            fileWatcher.Changed += onFileEvent;
            fileWatcher.Created += onFileEvent;
            fileWatcher.Deleted += onFileEvent;
            fileWatcher.Renamed += onRenamed;
            Action<Exception> reportError = error => OnWatcherError(registration, error);
            fileWatcher.Error += (_, e) => reportError(e.GetException());
            _testHooks?.WatcherErrorHandlerRegistered?.Invoke(state.Generation, reportError);
            _testHooks?.WatcherFileEventHandlerRegistered?.Invoke(registration.Identity, ScheduleFileReload);

            _testHooks?.BeforeWatcherActivation?.Invoke(state.Generation);

            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

            fileWatcher.EnableRaisingEvents = true;
            return new TransitionResult(
                TransitionStatus.Succeeded,
                state.Snapshot.Map.Count,
                state.FilePath,
                Error: null);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException
                                      or System.Security.SecurityException or System.ComponentModel.Win32Exception
                                      or InvalidOperationException)
        {
            watcher?.Dispose();
            fileWatcher?.Dispose();
            watcher = null;
            _log($"Text Expander could not watch '{state.FilePath}': {ex.Message}");
            return Failed(target, state.FilePath, ex);
        }
    }

    private void RetireWatcher()
    {
        PublishedState current = _published;
        WatcherRegistration? watcher = current.Watcher;
        if (watcher is null)
        {
            return;
        }

        _published = new PublishedState(current.State, watcher: null, current.HasLoadedState);
        watcher?.Dispose();
    }

    private static bool IsRelevantFileName(StoreState state, string? name)
    {
        if (!state.ResolveFolderNames)
        {
            return string.Equals(name, Path.GetFileName(state.FilePath), StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(name, FileName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, LegacyFileName, StringComparison.OrdinalIgnoreCase);
    }

    internal TransitionResult ReloadPreferredPath() => Load();

    private void OnWatcherError(WatcherRegistration watcher, Exception error)
    {
        bool removed = false;
        string attemptedPath = _desired.FilePath;
        lock (_transitionLock)
        {
            PublishedState current = _published;
            if (ReferenceEquals(current.Watcher, watcher))
            {
                attemptedPath = current.State.FilePath;
                _published = new PublishedState(current.State, watcher: null, current.HasLoadedState);
                removed = true;
            }
        }

        if (!removed)
        {
            return;
        }

        watcher.Dispose();
        _log($"Text Expander stopped watcher {watcher.Identity} for transition {watcher.TransitionGeneration}: {error.Message}");

        TransitionTarget target = _desired;
        if (Volatile.Read(ref _disposed) == 0)
        {
            ScheduleRetryAfterFailure(target, Failed(target, attemptedPath, error));
        }
    }

    /// <summary>
    /// Coalesces save-event bursts while assigning every request its own monotonic sequence.
    /// An older completion can therefore clear only itself, never an event that arrived while it
    /// was reading.
    /// </summary>
    internal void ScheduleReload()
    {
        TransitionTarget target = _desired;
        ScheduleReload(
            ReloadRequestKind.DesiredTransition,
            target.Generation,
            watcher: null,
            failureCount: 0,
            delay: _debounceDelay);
    }

    private void ScheduleReload(WatcherRegistration watcher)
    {
        lock (_debounceLock)
        {
            if (Volatile.Read(ref _disposed) != 0
                || !ReferenceEquals(_published.Watcher, watcher))
            {
                return;
            }

            ScheduleReloadLocked(
                ReloadRequestKind.PublishedWatcher,
                desiredGeneration: _desired.Generation,
                watcher: watcher,
                failureCount: 0,
                delay: _debounceDelay);
        }
    }

    private void ScheduleReload(
        ReloadRequestKind kind,
        long desiredGeneration,
        WatcherRegistration? watcher,
        int failureCount,
        TimeSpan delay)
    {
        lock (_debounceLock)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            ScheduleReloadLocked(kind, desiredGeneration, watcher, failureCount, delay);
        }
    }

    private void ScheduleReloadLocked(
        ReloadRequestKind kind,
        long desiredGeneration,
        WatcherRegistration? watcher,
        int failureCount,
        TimeSpan delay)
    {
        _scheduledReload = CreateReloadRequestLocked(kind, desiredGeneration, watcher, failureCount, delay);
        ArmReloadTimerLocked(delay);
    }

    private ReloadRequest CreateReloadRequestLocked(
        ReloadRequestKind kind,
        long desiredGeneration,
        WatcherRegistration? watcher,
        int failureCount,
        TimeSpan delay)
        => new(++_nextReloadSequence, kind, desiredGeneration, watcher, failureCount, delay);

    private void ArmReloadTimerLocked(TimeSpan delay)
    {
        _debounce ??= new Timer(
            _ => ReloadScheduledTransition(),
            null,
            Timeout.Infinite,
            Timeout.Infinite);
        _debounce.Change(delay, Timeout.InfiniteTimeSpan);
    }

    private void ReloadScheduledTransition()
    {
        ReloadRequest? request;
        lock (_debounceLock)
        {
            if (Volatile.Read(ref _disposed) != 0 || _scheduledReload is null || _reloadWorkerActive)
            {
                return;
            }

            request = _scheduledReload;
            _reloadWorkerActive = true;
        }

        long hookGeneration = request.Watcher?.TransitionGeneration ?? request.DesiredGeneration;
        int? reloadedCount = null;
        ReloadRetry? retry = null;
        try
        {
            _testHooks?.BeforeWatcherReload?.Invoke(hookGeneration);
            ProcessReloadRequest(request, out reloadedCount, out retry);
        }
        catch (Exception ex)
        {
            _log($"Text Expander reload request {request.Sequence} failed unexpectedly: {ex.Message}");
        }
        finally
        {
            CompleteReloadRequest(request, retry);
            try
            {
                if (reloadedCount is int count)
                {
                    Reloaded?.Invoke(count);
                }
            }
            finally
            {
                _testHooks?.AfterWatcherReload?.Invoke(hookGeneration);
            }
        }
    }

    private void ProcessReloadRequest(
        ReloadRequest request,
        out int? reloadedCount,
        out ReloadRetry? retry)
    {
        reloadedCount = null;
        retry = null;
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        if (request.Kind == ReloadRequestKind.DesiredTransition)
        {
            TransitionTarget target = _desired;
            if (target.Generation != request.DesiredGeneration)
            {
                return;
            }

            TransitionResult result;
            lock (_transitionLock)
            {
                result = ExecuteTransitionLocked(target);
            }

            if (result.Succeeded)
            {
                reloadedCount = result.Count;
            }
            else if (result.Status == TransitionStatus.Failed)
            {
                retry = new ReloadRetry(
                    ReloadRequestKind.DesiredTransition,
                    target.Generation,
                    Watcher: null,
                    Failure: result);
            }

            return;
        }

        WatcherRegistration? watcher = request.Watcher;
        if (watcher is null)
        {
            return;
        }

        lock (_transitionLock)
        {
            TransitionResult publishedResult = ExecutePublishedWatcherReloadLocked(watcher);
            if (publishedResult.Status == TransitionStatus.Superseded)
            {
                return;
            }

            bool publishedReloadFailed = publishedResult.Status == TransitionStatus.Failed;
            if (publishedResult.Succeeded)
            {
                reloadedCount = publishedResult.Count;
            }

            TransitionTarget desired = _desired;
            PublishedState published = _published;
            bool desiredIsPublished = published.HasLoadedState
                && published.State.Generation == desired.Generation
                && SameConfiguration(published.State, desired);
            if (!desiredIsPublished && Volatile.Read(ref _disposed) == 0)
            {
                TransitionResult desiredResult = ExecuteTransitionLocked(desired);
                if (desiredResult.Succeeded)
                {
                    reloadedCount = desiredResult.Count;
                    return;
                }

                if (desiredResult.Status == TransitionStatus.Failed)
                {
                    retry = publishedReloadFailed
                        ? new ReloadRetry(ReloadRequestKind.PublishedWatcher, desired.Generation, watcher, desiredResult)
                        : new ReloadRetry(ReloadRequestKind.DesiredTransition, desired.Generation, Watcher: null, Failure: desiredResult);
                    return;
                }
            }

            if (publishedReloadFailed)
            {
                retry = new ReloadRetry(
                    ReloadRequestKind.PublishedWatcher,
                    desired.Generation,
                    watcher,
                    publishedResult);
            }
        }
    }

    private void CompleteReloadRequest(ReloadRequest request, ReloadRetry? retry)
    {
        bool disposed;
        bool retryScheduled = false;
        int failureAttempt = 0;
        TransitionResult? failure = retry?.Failure;

        lock (_debounceLock)
        {
            disposed = Volatile.Read(ref _disposed) != 0;
            if (_scheduledReload?.Sequence == request.Sequence)
            {
                if (!disposed && retry is ReloadRetry retryValue && ReloadRetryIsCurrent(retryValue))
                {
                    failureAttempt = request.FailureCount + 1;
                    if (failureAttempt < _maxReloadAttempts)
                    {
                        _scheduledReload = CreateReloadRequestLocked(
                            retryValue.Kind,
                            retryValue.DesiredGeneration,
                            retryValue.Watcher,
                            failureAttempt,
                            _retryDelay);
                        retryScheduled = true;
                    }
                    else
                    {
                        _scheduledReload = null;
                    }
                }
                else
                {
                    _scheduledReload = null;
                }
            }

            _reloadWorkerActive = false;
            if (!disposed && _scheduledReload is ReloadRequest pending)
            {
                ArmReloadTimerLocked(pending.Delay);
            }
            else
            {
                _debounce?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
        }

        if (disposed && failure is TransitionResult disposedFailure)
        {
            _log($"Text Expander stopped retrying '{disposedFailure.AttemptedPath}' because the snippet store was disposed.");
        }
        else if (failureAttempt > 0 && failure is TransitionResult failed)
        {
            LogReloadFailure(failed, failureAttempt, retryScheduled);
        }
    }

    private bool ReloadRetryIsCurrent(ReloadRetry retry)
        => retry.Kind == ReloadRequestKind.DesiredTransition
            ? _desired.Generation == retry.DesiredGeneration
            : retry.Watcher is not null && ReferenceEquals(_published.Watcher, retry.Watcher);

    private void ScheduleRetryAfterFailure(TransitionTarget target, TransitionResult result)
    {
        bool disposed;
        bool retryScheduled = false;
        bool targetIsCurrent;
        lock (_debounceLock)
        {
            disposed = Volatile.Read(ref _disposed) != 0;
            targetIsCurrent = _desired.Generation == target.Generation;
            if (!disposed && targetIsCurrent)
            {
                WatcherRegistration? pendingWatcher = _scheduledReload?.Watcher;
                bool preserveWatcherRequest = pendingWatcher is not null
                    && ReferenceEquals(_published.Watcher, pendingWatcher);
                if (preserveWatcherRequest || _maxReloadAttempts > 1)
                {
                    _scheduledReload = CreateReloadRequestLocked(
                        preserveWatcherRequest ? ReloadRequestKind.PublishedWatcher : ReloadRequestKind.DesiredTransition,
                        target.Generation,
                        preserveWatcherRequest ? pendingWatcher : null,
                        preserveWatcherRequest ? 0 : 1,
                        _retryDelay);
                    retryScheduled = true;
                    ArmReloadTimerLocked(_retryDelay);
                }
            }
        }

        if (disposed)
        {
            _log($"Text Expander stopped retrying '{result.AttemptedPath}' because the snippet store was disposed.");
        }
        else if (targetIsCurrent)
        {
            LogReloadFailure(result, attempt: 1, retryScheduled);
        }
    }

    private void LogReloadFailure(TransitionResult result, int attempt, bool retryScheduled)
    {
        string action = retryScheduled
            ? $"Retrying in {_retryDelay.TotalMilliseconds:0} ms."
            : _published.Watcher is not null
                ? "Stopped retrying until another file-system event or configuration change."
                : "Stopped retrying until the configuration changes.";
        _log($"Text Expander could not update '{result.AttemptedPath}' (attempt {attempt} of {_maxReloadAttempts}): {result.Error?.Message} {action}");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref _watchingRequested, 0);
        lock (_transitionLock)
        {
            RetireWatcher();
        }

        lock (_debounceLock)
        {
            _debounce?.Dispose();
            _debounce = null;
            _scheduledReload = null;
        }
    }
}
