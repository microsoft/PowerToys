// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Opt-in, process-local measurements for the CmdPal icon pipeline.
/// No icon strings, paths, glyphs, application identifiers, or item data are recorded.
/// Diagnostic scopes are static developer-authored labels.
/// </summary>
internal static class IconLoadDiagnostics
{
    private static readonly object ReportsLock = new();
    private static readonly List<IconLoadDiagnosticsReport> Reports = [];
    private static long _nextSessionId;
    private static IconLoadDiagnosticsSession? _activeSession;
    private static IconLoadDiagnosticsSession? _etwSession;

    public static bool IsRecording => Volatile.Read(ref _activeSession) is not null;

    public static long? ActiveSessionId => Volatile.Read(ref _activeSession)?.Id;

    public static IReadOnlyList<IconLoadDiagnosticsReport> GetReports()
    {
        lock (ReportsLock)
        {
            return Reports.ToArray();
        }
    }

    public static long Start(DispatcherQueue? dispatcherQueue = null)
    {
        var session = new IconLoadDiagnosticsSession(
            Interlocked.Increment(ref _nextSessionId),
            dispatcherQueue);
        Interlocked.Exchange(ref _activeSession, session)?.Stop();
        Interlocked.Exchange(ref _etwSession, null)?.Stop();
        return session.Id;
    }

    public static IconLoadDiagnosticsReport? StopAndCreateReport()
    {
        var session = Interlocked.Exchange(ref _activeSession, null);
        if (session is null)
        {
            return null;
        }

        session.Stop();
        var report = session.CreateReport();
        lock (ReportsLock)
        {
            Reports.Add(report);
        }

        Logger.LogInfo(report.Text);

        return report;
    }

    public static void Reset()
    {
        Interlocked.Exchange(ref _activeSession, null)?.Stop();
        Interlocked.Exchange(ref _etwSession, null)?.Stop();
        lock (ReportsLock)
        {
            Reports.Clear();
        }
    }

    public static IconRequestMeasurement BeginRequest(IconRequestReason reason, double scale)
    {
        return BeginRequest(reason, scale, default);
    }

    public static IconRequestMeasurement BeginRequest(IconRequestReason reason, double scale, IconRequestOrigin origin)
    {
        var session = GetCurrentSession();
        return session is null
            ? default
            : session.BeginRequest(reason, scale, origin);
    }

    public static IconLoadMeasurement? CreateLoad(
        IconRequestMeasurement request,
        string? iconString,
        bool hasStream,
        double width,
        double height,
        double scale)
    {
        var session = request.Session ?? GetCurrentSession();
        if (session is null || !IsCurrentSession(session))
        {
            return null;
        }

        return session.CreateLoad(ClassifyInput(iconString, hasStream), width, height, scale);
    }

    internal static void RecordCacheLookup(
        Size iconSize,
        IconCachePartition partition,
        int capacity,
        bool hit)
    {
        GetCurrentSession()?.RecordCacheLookup(iconSize, partition, capacity, hit);
    }

    internal static void RecordCacheEntryAdded(
        Size iconSize,
        IconCachePartition partition,
        int capacity,
        int entryCount)
    {
        GetCurrentSession()?.RecordCacheEntryAdded(iconSize, partition, capacity, entryCount);
    }

    internal static void RecordCacheEntryRemoved(
        Size iconSize,
        IconCachePartition partition,
        int capacity,
        int entryCount,
        AdaptiveCacheRemovalReason reason)
    {
        GetCurrentSession()?.RecordCacheEntryRemoved(
            iconSize,
            partition,
            capacity,
            entryCount,
            reason);
    }

    internal static ShellIconMeasurement BeginShellIconRequest(ShellItemIconRequest request)
    {
        var session = GetCurrentSession();
        if (session is null)
        {
            return default;
        }

        var requestKind = Microsoft.CommandPalette.Extensions.Toolkit.ShellItemIconProtocol.IsProtocol(request.CacheIdentity)
            ? ShellIconRequestKind.Protocol
            : request.CacheIdentity.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                ? ShellIconRequestKind.FileUri
                : ShellIconRequestKind.LegacyPath;
        return new ShellIconMeasurement(session, requestKind);
    }

    internal static void RecordShellAssociationChangedNotification() =>
        GetCurrentSession()?.RecordShellIconStep(
            ShellIconDiagnosticStep.AssociationChangedNotification,
            0,
            0);

    internal static void RecordShellIconCacheInvalidation(ShellIconCacheInvalidationReason reason) =>
        GetCurrentSession()?.RecordShellIconStep(
            ShellIconDiagnosticStep.LocationCacheInvalidated,
            (int)reason,
            0);

    public static long BeginElementUpdate()
    {
        return GetCurrentSession() is null ? 0 : Stopwatch.GetTimestamp();
    }

    public static void RecordElementUpdate(bool reused, IconSource? source, long startedAt)
    {
        var session = GetCurrentSession();
        if (session is null)
        {
            return;
        }

        var elapsedTicks = startedAt == 0 ? -1 : Stopwatch.GetTimestamp() - startedAt;
        session.RecordElementUpdate(reused, ClassifyResult(source), elapsedTicks);
    }

    internal static void OnEtwDisabled()
    {
        Interlocked.Exchange(ref _etwSession, null)?.Stop();
    }

    private static IconLoadDiagnosticsSession? GetCurrentSession()
    {
        var activeSession = Volatile.Read(ref _activeSession);
        if (activeSession is not null)
        {
            return activeSession;
        }

        if (!IconLoadEventSource.Log.IsEnabled())
        {
            // OnEventCommand normally retires the hidden session as soon as the last listener
            // detaches. Avoid an unconditional interlocked write on every disabled hot-path
            // probe while still covering a disable racing this read.
            if (Volatile.Read(ref _etwSession) is not null)
            {
                OnEtwDisabled();
            }

            return null;
        }

        var etwSession = Volatile.Read(ref _etwSession);
        if (etwSession is null)
        {
            var candidate = new IconLoadDiagnosticsSession(Interlocked.Increment(ref _nextSessionId));
            etwSession = Interlocked.CompareExchange(ref _etwSession, candidate, null);
            if (etwSession is null)
            {
                etwSession = candidate;
            }
            else
            {
                candidate.Stop();
            }
        }

        // An explicit text session may have started while the hidden ETW session was created.
        activeSession = Volatile.Read(ref _activeSession);
        if (activeSession is not null)
        {
            if (ReferenceEquals(Interlocked.CompareExchange(ref _etwSession, null, etwSession), etwSession))
            {
                etwSession.Stop();
            }

            return activeSession;
        }

        return etwSession;
    }

    private static bool IsCurrentSession(IconLoadDiagnosticsSession session)
    {
        var activeSession = Volatile.Read(ref _activeSession);
        if (activeSession is not null)
        {
            return ReferenceEquals(session, activeSession);
        }

        if (!IconLoadEventSource.Log.IsEnabled())
        {
            OnEtwDisabled();
            return false;
        }

        return ReferenceEquals(session, Volatile.Read(ref _etwSession));
    }

    internal static SchedulerCommandMeasurement? BeginSchedulerCommand(IconLoadQueue.QueueCommandKind kind)
    {
        var session = GetCurrentSession();
        if (session is null)
        {
            return null;
        }

        var publishedAt = Stopwatch.GetTimestamp();
        session.RecordSchedulerCommandPublished(kind);
        return new SchedulerCommandMeasurement(session, kind, publishedAt);
    }

    internal static DemandedIdleCapacityMeasurement? BeginDemandedIdleCapacity(
        int demandedQueueDepth,
        int availableWorkerSlots)
    {
        var session = GetCurrentSession();
        if (session is null)
        {
            return null;
        }

        var startedAt = Stopwatch.GetTimestamp();
        session.RecordDemandedIdleCapacityStarted(demandedQueueDepth, availableWorkerSlots);
        return new DemandedIdleCapacityMeasurement(session, startedAt);
    }

    internal static SpeculativeDispatchDeferralMeasurement? BeginSpeculativeDispatchDeferral(
        int speculativeQueueDepth,
        int workerCount,
        int reservedWorkerSlots)
    {
        var session = Volatile.Read(ref _activeSession);
        if (session is null)
        {
            return null;
        }

        var startedAt = Stopwatch.GetTimestamp();
        session.RecordSpeculativeDispatchDeferralStarted(speculativeQueueDepth, workerCount, reservedWorkerSlots);
        return new SpeculativeDispatchDeferralMeasurement(session, startedAt, workerCount);
    }

    private static IconLoadInputKind ClassifyInput(string? iconString, bool hasStream)
    {
        if (!string.IsNullOrEmpty(iconString))
        {
            if (IconProtocolRegistry.Find(iconString) is { } protocolProcessor)
            {
                return protocolProcessor.ClassifyInput(iconString);
            }

            if (ShellItemIconRequestClassifier.TryClassify(iconString, out _))
            {
                return IconLoadInputKind.ShellItemIcon;
            }

            var path = iconString.AsSpan();
            var comma = path.IndexOf(',');
            if (comma >= 0)
            {
                path = path[..comma];
            }

            if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                return IconLoadInputKind.ShellBinary;
            }

            return IconLoadInputKind.String;
        }

        return hasStream ? IconLoadInputKind.Stream : IconLoadInputKind.Empty;
    }

    internal static IconLoadResultKind ClassifyResult(IconSource? result)
    {
        try
        {
            return result switch
            {
                null => IconLoadResultKind.Empty,
                FontIconSource font when font.FontFamily?.Source.StartsWith("Segoe UI Emoji", StringComparison.OrdinalIgnoreCase) == true => IconLoadResultKind.EmojiGlyph,
                FontIconSource font when font.FontFamily?.Source.StartsWith("Segoe Fluent Icons", StringComparison.OrdinalIgnoreCase) == true => IconLoadResultKind.FluentGlyph,
                FontIconSource => IconLoadResultKind.OtherGlyph,
                ImageIconSource { ImageSource: SvgImageSource } => IconLoadResultKind.Svg,
                ImageIconSource { ImageSource: SoftwareBitmapSource } => IconLoadResultKind.SoftwareBitmap,
                ImageIconSource { ImageSource: BitmapImage } => IconLoadResultKind.Bitmap,
                BitmapIconSource => IconLoadResultKind.Fallback,
                _ => IconLoadResultKind.Other,
            };
        }
        catch
        {
            return IconLoadResultKind.Other;
        }
    }

    internal sealed class SchedulerCommandMeasurement
    {
        private readonly IconLoadDiagnosticsSession _session;
        private readonly IconLoadQueue.QueueCommandKind _kind;
        private readonly long _publishedAt;
        private int _processed;
        private int _dispatched;

        public SchedulerCommandMeasurement(
            IconLoadDiagnosticsSession session,
            IconLoadQueue.QueueCommandKind kind,
            long publishedAt)
        {
            _session = session;
            _kind = kind;
            _publishedAt = publishedAt;
        }

        public void Processed()
        {
            if (Interlocked.Exchange(ref _processed, 1) == 0)
            {
                _session.RecordSchedulerCommandProcessed(
                    _kind,
                    Stopwatch.GetTimestamp() - _publishedAt);
            }
        }

        public SchedulerWakeMeasurement CreateWakeMeasurement()
        {
            return new SchedulerWakeMeasurement(_session, _kind, _publishedAt);
        }

        public void WorkerDispatched(bool demanded)
        {
            if (Interlocked.Exchange(ref _dispatched, 1) == 0)
            {
                _session.RecordWorkerDispatched(
                    demanded,
                    Stopwatch.GetTimestamp() - _publishedAt);
            }
        }
    }

    internal sealed class SchedulerWakeMeasurement
    {
        private readonly IconLoadDiagnosticsSession _session;
        private readonly IconLoadQueue.QueueCommandKind _triggerKind;
        private readonly long _signaledAt;
        private long _wakeTicks = -1;
        private int _woke;
        private int _completed;

        public SchedulerWakeMeasurement(
            IconLoadDiagnosticsSession session,
            IconLoadQueue.QueueCommandKind triggerKind,
            long signaledAt)
        {
            _session = session;
            _triggerKind = triggerKind;
            _signaledAt = signaledAt;
        }

        public void Woke(long wokeAt)
        {
            if (Interlocked.Exchange(ref _woke, 1) == 0)
            {
                var wakeTicks = Math.Max(0, wokeAt - _signaledAt);
                Volatile.Write(ref _wakeTicks, wakeTicks);
                _session.RecordSchedulerCoordinatorWoke(_triggerKind, wakeTicks);
            }
        }

        public void BatchCompleted(
            int commandCount,
            int dispatchedWorkItemCount,
            long drainTicks,
            long passTicks)
        {
            if (Interlocked.Exchange(ref _completed, 1) == 0)
            {
                var wakeTicks = Volatile.Read(ref _wakeTicks);
                Debug.Assert(wakeTicks >= 0, "A scheduler batch must wake before it completes.");
                _session.RecordSchedulerBatchCompleted(
                    _triggerKind,
                    wakeTicks,
                    commandCount,
                    dispatchedWorkItemCount,
                    drainTicks,
                    passTicks);
            }
        }
    }

    internal sealed class DemandedIdleCapacityMeasurement
    {
        private readonly IconLoadDiagnosticsSession _session;
        private readonly long _startedAt;
        private int _completed;

        public DemandedIdleCapacityMeasurement(IconLoadDiagnosticsSession session, long startedAt)
        {
            _session = session;
            _startedAt = startedAt;
        }

        public bool IsForActiveSession => IsCurrentSession(_session);

        public void Observe(int demandedQueueDepth, int availableWorkerSlots)
        {
            _session.RecordDemandedIdleCapacityObserved(demandedQueueDepth, availableWorkerSlots);
        }

        public void Complete()
        {
            if (Interlocked.Exchange(ref _completed, 1) == 0)
            {
                _session.RecordDemandedIdleCapacityCompleted(Stopwatch.GetTimestamp() - _startedAt);
            }
        }
    }

    internal sealed class SpeculativeDispatchDeferralMeasurement
    {
        private readonly IconLoadDiagnosticsSession _session;
        private readonly long _startedAt;
        private readonly int _workerCount;
        private int _completed;

        public SpeculativeDispatchDeferralMeasurement(
            IconLoadDiagnosticsSession session,
            long startedAt,
            int workerCount)
        {
            _session = session;
            _startedAt = startedAt;
            _workerCount = workerCount;
        }

        public bool IsForActiveSession => ReferenceEquals(_session, Volatile.Read(ref _activeSession));

        public void Observe(int speculativeQueueDepth, int reservedWorkerSlots)
        {
            _session.RecordSpeculativeDispatchDeferralObserved(
                speculativeQueueDepth,
                _workerCount,
                reservedWorkerSlots);
        }

        public void Complete()
        {
            if (Interlocked.Exchange(ref _completed, 1) == 0)
            {
                _session.RecordSpeculativeDispatchDeferralCompleted(Stopwatch.GetTimestamp() - _startedAt);
            }
        }
    }
}
