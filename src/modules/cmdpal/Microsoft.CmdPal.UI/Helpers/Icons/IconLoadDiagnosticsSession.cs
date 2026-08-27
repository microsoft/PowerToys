// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CmdPal.UI.Controls;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Helpers;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "The diagnostics facade owns the session and always calls Stop. Avoid implementing a WinRT interface on this internal NativeAOT type.")]
internal sealed class IconLoadDiagnosticsSession
{
    private readonly object _stopLock = new();
    private readonly object _queueDemandLock = new();
    private readonly DateTimeOffset _startedUtc = DateTimeOffset.UtcNow;
    private readonly long _startedAt = Stopwatch.GetTimestamp();
    private readonly long[] _requestStatuses = new long[Enum.GetValues<IconRequestStatus>().Length];
    private readonly long[] _providerResolutions = new long[Enum.GetValues<IconProviderResolution>().Length];
    private readonly long[] _inputKinds = new long[Enum.GetValues<IconLoadInputKind>().Length];
    private readonly long[] _resultKinds = new long[Enum.GetValues<IconLoadResultKind>().Length];
    private readonly DiagnosticHistogram[][] _requestLatencyByResolutionAndResult = CreateRequestMeasurements();
    private readonly DiagnosticHistogram[] _appliedRequestLatencyByResolution = CreateResolutionMeasurements();
    private readonly DiagnosticHistogram _requestLatency = new();
    private readonly DiagnosticHistogram _loadLatency = new();
    private readonly DiagnosticHistogram _directGlyphLatency = new();
    private readonly DiagnosticHistogram[] _directGlyphLatencyByResultKind = CreateResultMeasurements();
    private readonly DiagnosticHistogram _queueLatency = new();
    private readonly DiagnosticHistogram _demandedQueueLatency = new();
    private readonly DiagnosticHistogram _speculativeQueueLatency = new();
    private readonly DiagnosticHistogram _demandArrivalToWorkerStartWithActiveSpeculative = new();
    private readonly DiagnosticHistogram _directlyBlockedDemandArrivalToWorkerStart = new();
    private readonly DiagnosticHistogram _backgroundPreparationLatency = new();
    private readonly DiagnosticHistogram _dispatcherWaitLatency = new();
    private readonly DiagnosticHistogram _dispatcherWorkLatency = new();
    private readonly DiagnosticHistogram _dispatcherUiExecutionLatency = new();
    private readonly DiagnosticHistogram _dispatcherAsyncSuspensionLatency = new();
    private readonly DiagnosticHistogram[] _dispatcherWaitLatencyByDemand = CreateDemandMeasurements();
    private readonly DiagnosticHistogram[] _dispatcherWorkLatencyByDemand = CreateDemandMeasurements();
    private readonly DiagnosticHistogram[] _dispatcherUiExecutionLatencyByDemand = CreateDemandMeasurements();
    private readonly DiagnosticHistogram[] _dispatcherAsyncSuspensionLatencyByDemand = CreateDemandMeasurements();
    private readonly DiagnosticHistogram[] _dispatcherUiExecutionLatencyBySliceKind = CreateDispatcherUiSliceMeasurements();
    private readonly DispatcherMaterializationMeasurements[] _dispatcherMaterializationMeasurements = CreateDispatcherMaterializationMeasurements();
    private readonly ConcurrentQueue<DispatcherOutlierSample> _dispatcherOutliers = new();
    private readonly DiagnosticHistogram _uiProbeWaitLatency = new();
    private readonly DiagnosticHistogram _elementUpdateLatency = new();
    private readonly long[] _schedulerCommandsPublished = new long[Enum.GetValues<IconLoadQueue.QueueCommandKind>().Length];
    private readonly long[] _schedulerCommandsProcessed = new long[Enum.GetValues<IconLoadQueue.QueueCommandKind>().Length];
    private readonly DiagnosticHistogram[] _schedulerCommandLatency = CreateSchedulerCommandMeasurements();
    private readonly DiagnosticHistogram _schedulerSignalToWakeLatency = new();
    private readonly DiagnosticHistogram _schedulerEmptyBatchSignalToWakeLatency = new();
    private readonly DiagnosticHistogram[] _schedulerSignalToWakeLatencyByCommandKind = CreateSchedulerCommandMeasurements();
    private readonly DiagnosticHistogram _schedulerBatchDrainLatency = new();
    private readonly DiagnosticHistogram _schedulerPassLatency = new();
    private readonly DiagnosticHistogram _workerReadyToDispatchLatency = new();
    private readonly DiagnosticHistogram _workerReadyToDemandedDispatchLatency = new();
    private readonly DiagnosticHistogram _workerReadyToSpeculativeDispatchLatency = new();
    private readonly DiagnosticHistogram _demandedIdleCapacityDuration = new();
    private readonly DiagnosticHistogram _speculativeDispatchDeferralDuration = new();
    private readonly InputKindMeasurements[] _inputKindMeasurements = CreateInputKindMeasurements();
    private readonly ElementKindMeasurements[] _elementKindMeasurements = CreateElementKindMeasurements();
    private readonly ConditionalWeakTable<Task<IconSource?>, IconLoadMeasurement> _loadsByTask = new();
    private readonly ConcurrentDictionary<CacheDescriptor, CacheMeasurements> _cacheMeasurements = new();
    private readonly long[] _shellIconRequestKinds = new long[Enum.GetValues<ShellIconRequestKind>().Length];
    private readonly long[] _shellIconIdentityKinds = new long[Enum.GetValues<ShellIconIdentityKind>().Length];
    private readonly long[] _shellIconExtractionKinds = new long[Enum.GetValues<ShellIconIdentityKind>().Length];
    private readonly long[] _shellIconCacheInvalidationReasons = new long[Enum.GetValues<ShellIconCacheInvalidationReason>().Length];
    private readonly long[] _shellImageListSizes = new long[Enum.GetValues<ShellImageListSize>().Length];
    private readonly DiagnosticHistogram _shellIconIdentityResolutionLatency = new();
    private readonly DiagnosticHistogram _shellIconExtractionLatency = new();
    private readonly DiagnosticHistogram _shellHIconConversionLatency = new();
    private readonly ConcurrentDictionary<long, RequestDemandState> _requestDemandStates = new();

    // These lightweight states intentionally survive load completion so later cache hits can be
    // attributed and the final per-load demand aggregates can be built without retaining icon tasks.
    private readonly ConcurrentDictionary<long, LoadDemandState> _loadDemandStates = new();
    private readonly ConcurrentDictionary<RequestOriginKey, RequestOriginMeasurements> _requestOriginMeasurements = new();
    private readonly long[] _invalidatedRequestLoadStages = new long[Enum.GetValues<IconLoadDemandStage>().Length];
    private readonly long[] _capacityInterferingSpeculativeStartsByInputKind = new long[Enum.GetValues<IconLoadInputKind>().Length];
    private readonly long[] _currentActiveSpeculativeWorkersByInputKind = new long[Enum.GetValues<IconLoadInputKind>().Length];
    private readonly long[] _speculativeWorkerOccupancyAtDemandArrivalsByInputKind = new long[Enum.GetValues<IconLoadInputKind>().Length];
    private readonly long[] _directlyBlockedDemandArrivalsByInputKind = new long[Enum.GetValues<IconLoadInputKind>().Length];
    private readonly long _processCpuStartedTicks;
    private readonly long _managedAllocatedBytesStarted;
    private readonly long _gcPauseStartedTicks;
    private readonly int _gen0CollectionsStarted;
    private readonly int _gen1CollectionsStarted;
    private readonly int _gen2CollectionsStarted;
    private readonly long _workingSetStartedBytes;
    private readonly IconUiResponsivenessProbe? _uiResponsivenessProbe;

    private DateTimeOffset _stoppedUtc;
    private long _stoppedAt;
    private long _processCpuStoppedTicks;
    private long _managedAllocatedBytesStopped;
    private long _gcPauseStoppedTicks;
    private int _gen0CollectionsStopped;
    private int _gen1CollectionsStopped;
    private int _gen2CollectionsStopped;
    private long _workingSetStoppedBytes;
    private long _nextRequestId;
    private long _nextLoadId;
    private long _requestsStarted;
    private long _loadsCreated;
    private long _loadsRejected;
    private long _loadsAbandonedBeforeStart;
    private long _directGlyphLoads;
    private long _currentHighQueueDepth;
    private long _currentLowQueueDepth;
    private long _maximumHighQueueDepth;
    private long _maximumLowQueueDepth;
    private long _currentDemandedQueueDepth;
    private long _currentSpeculativeQueueDepth;
    private long _maximumDemandedQueueDepth;
    private long _maximumSpeculativeQueueDepth;
    private long _queuedDemandDemotions;
    private long _queuedDemandPromotions;
    private long _demandedWorkerStarts;
    private long _speculativeWorkerStarts;
    private long _speculativeStartsWithDemandedLoadsQueued;
    private long _capacityInterferingSpeculativeStarts;
    private long _demandedLoadsBeyondCapacityAtSpeculativeStarts;
    private long _maximumDemandedLoadsBeyondCapacityAtSpeculativeStart;
    private long _currentActiveDemandedWorkers;
    private long _currentActiveSpeculativeWorkers;
    private long _maximumActiveSpeculativeWorkers;
    private long _demandedQueueArrivals;
    private long _demandedArrivalsWithActiveSpeculativeWorkers;
    private long _speculativeWorkerOccupancyAtDemandArrivals;
    private long _maximumSpeculativeWorkersAtDemandArrival;
    private long _demandedArrivalsDirectlyBlockedBySpeculativeCapacity;
    private long _currentSchedulerCommandBacklog;
    private long _maximumSchedulerCommandBacklog;
    private long _schedulerBatchesCompleted;
    private long _schedulerEmptyBatches;
    private long _schedulerCommandsDrained;
    private long _maximumSchedulerBatchSize;
    private long _schedulerWorkItemsDispatched;
    private long _maximumSchedulerDispatchCount;
    private long _demandedIdleCapacityIntervalsStarted;
    private long _currentDemandedIdleCapacityIntervals;
    private long _maximumDemandedQueueDepthWithIdleCapacity;
    private long _maximumAvailableWorkerSlotsWithDemandedWork;
    private long _speculativeDispatchDeferralIntervalsStarted;
    private long _currentSpeculativeDispatchDeferralIntervals;
    private long _maximumSpeculativeQueueDepthDuringDeferral;
    private long _maximumWorkerCountDuringSpeculativeDispatchDeferral;
    private long _maximumReservedWorkerSlotsDuringDeferral;
    private long _activeWorkers;
    private long _maximumActiveWorkers;
    private long _dispatcherEnqueuedDemanded;
    private long _dispatcherEnqueuedSpeculative;
    private long _dispatcherStartedDemanded;
    private long _dispatcherStartedSpeculative;
    private long _dispatcherCompletedDemanded;
    private long _dispatcherCompletedSpeculative;
    private long _dispatcherWaitFailures;
    private long _currentDispatcherWaits;
    private long _maximumDispatcherWaits;
    private long _currentDispatcherCallbacks;
    private long _maximumDispatcherCallbacks;
    private long _elementsCreated;
    private long _elementsReused;
    private long _uiProbeEnqueued;
    private long _uiProbeCompleted;
    private long _uiProbeSkipped;
    private long _uiProbeRejected;
    private long _shellIconLocationCacheHits;
    private long _shellIconLocationCacheMisses;
    private long _shellIconRawInFlightJoins;
    private long _shellIconCanonicalCacheHits;
    private long _shellIconCanonicalInFlightJoins;
    private long _shellIconCanonicalNewLoads;
    private long _shellIconExtractionsSucceeded;
    private long _shellIconExtractionsEmpty;
    private long _shellIconExtractionsFailed;
    private long _shellIconAssociationChangedNotifications;
    private long _shellImageListRequestedPixelTotal;
    private long _shellImageListSourceWidthTotal;
    private long _shellImageListSourceHeightTotal;
    private long _shellImageListSourceSizeSamples;
    private long _shellImageListSourceSmallerThanRequest;
    private long _shellImageListSourceEqualToRequest;
    private long _shellImageListSourceLargerThanRequest;
    private long _shellImageListMaximumRequestedPixels;
    private long _shellImageListMaximumSourcePixels;

    public long Id { get; }

    internal IconLoadDiagnosticsSession(long id, DispatcherQueue? dispatcherQueue = null)
    {
        Id = id;
        _processCpuStartedTicks = GetProcessCpuTicks();
        _managedAllocatedBytesStarted = GC.GetTotalAllocatedBytes(precise: false);
        _gcPauseStartedTicks = GC.GetTotalPauseDuration().Ticks;
        _gen0CollectionsStarted = GC.CollectionCount(0);
        _gen1CollectionsStarted = GC.CollectionCount(1);
        _gen2CollectionsStarted = GC.CollectionCount(2);
        _workingSetStartedBytes = GetWorkingSetBytes();
        if (dispatcherQueue is not null)
        {
            // The probe retains this session and starts RunAsync during construction. Keep its
            // creation after all state used by RecordUiProbe* is initialized because a timer tick
            // may call back as soon as RunAsync starts.
            _uiResponsivenessProbe = new IconUiResponsivenessProbe(dispatcherQueue, this);
        }
    }

    internal void RecordUiProbeEnqueued() => Interlocked.Increment(ref _uiProbeEnqueued);

    internal void RecordUiProbeCompleted(long elapsedTicks)
    {
        Interlocked.Increment(ref _uiProbeCompleted);
        _uiProbeWaitLatency.Record(elapsedTicks);
        IconLoadEventSource.Log.UiResponsivenessProbeCompleted(Id, ToMicroseconds(elapsedTicks));
    }

    internal void RecordUiProbeSkipped() => Interlocked.Increment(ref _uiProbeSkipped);

    internal void RecordUiProbeRejected() => Interlocked.Increment(ref _uiProbeRejected);

    internal void RecordCacheLookup(
        Size iconSize,
        IconCachePartition partition,
        int capacity,
        bool hit)
    {
        GetCacheMeasurements(iconSize, partition, capacity).RecordLookup(hit);
    }

    internal void RecordCacheEntryAdded(
        Size iconSize,
        IconCachePartition partition,
        int capacity,
        int entryCount)
    {
        GetCacheMeasurements(iconSize, partition, capacity).RecordAdded(entryCount);
    }

    internal void RecordCacheEntryRemoved(
        Size iconSize,
        IconCachePartition partition,
        int capacity,
        int entryCount,
        AdaptiveCacheRemovalReason reason)
    {
        GetCacheMeasurements(iconSize, partition, capacity).RecordRemoved(entryCount, reason);
    }

    internal void RecordShellIconStep(ShellIconDiagnosticStep step, int detail, long elapsedTicks)
    {
        switch (step)
        {
            case ShellIconDiagnosticStep.Request:
                Interlocked.Increment(ref _shellIconRequestKinds[detail]);
                break;
            case ShellIconDiagnosticStep.LocationCacheHit:
                Interlocked.Increment(ref _shellIconLocationCacheHits);
                break;
            case ShellIconDiagnosticStep.LocationCacheMiss:
                Interlocked.Increment(ref _shellIconLocationCacheMisses);
                break;
            case ShellIconDiagnosticStep.RawInFlightJoin:
                Interlocked.Increment(ref _shellIconRawInFlightJoins);
                break;
            case ShellIconDiagnosticStep.IdentityResolved:
                Interlocked.Increment(ref _shellIconIdentityKinds[detail]);
                _shellIconIdentityResolutionLatency.Record(elapsedTicks);
                break;
            case ShellIconDiagnosticStep.CanonicalCacheHit:
                Interlocked.Increment(ref _shellIconCanonicalCacheHits);
                break;
            case ShellIconDiagnosticStep.CanonicalInFlightJoin:
                Interlocked.Increment(ref _shellIconCanonicalInFlightJoins);
                break;
            case ShellIconDiagnosticStep.CanonicalNewLoad:
                Interlocked.Increment(ref _shellIconCanonicalNewLoads);
                break;
            case ShellIconDiagnosticStep.ExtractionSucceeded:
                Interlocked.Increment(ref _shellIconExtractionsSucceeded);
                Interlocked.Increment(ref _shellIconExtractionKinds[detail]);
                _shellIconExtractionLatency.Record(elapsedTicks);
                break;
            case ShellIconDiagnosticStep.ExtractionEmpty:
                Interlocked.Increment(ref _shellIconExtractionsEmpty);
                Interlocked.Increment(ref _shellIconExtractionKinds[detail]);
                _shellIconExtractionLatency.Record(elapsedTicks);
                break;
            case ShellIconDiagnosticStep.ExtractionFailed:
                Interlocked.Increment(ref _shellIconExtractionsFailed);
                Interlocked.Increment(ref _shellIconExtractionKinds[detail]);
                _shellIconExtractionLatency.Record(elapsedTicks);
                break;
            case ShellIconDiagnosticStep.AssociationChangedNotification:
                Interlocked.Increment(ref _shellIconAssociationChangedNotifications);
                break;
            case ShellIconDiagnosticStep.LocationCacheInvalidated:
                Interlocked.Increment(ref _shellIconCacheInvalidationReasons[detail]);
                break;
        }

        IconLoadEventSource.Log.ShellIconStepCompleted(
            Id,
            (int)step,
            detail,
            ToMicroseconds(elapsedTicks));
    }

    internal void RecordShellImageListExtraction(
        ShellImageListSize imageListSize,
        int requestedPixelSize,
        int sourceWidth,
        int sourceHeight,
        long hIconConversionTicks)
    {
        var normalizedRequestedSize = Math.Max(0, requestedPixelSize);
        var normalizedSourceWidth = Math.Max(0, sourceWidth);
        var normalizedSourceHeight = Math.Max(0, sourceHeight);
        var sourceEdge = Math.Max(normalizedSourceWidth, normalizedSourceHeight);

        Interlocked.Increment(ref _shellImageListSizes[(int)imageListSize]);
        Interlocked.Add(ref _shellImageListRequestedPixelTotal, normalizedRequestedSize);
        UpdateMaximum(ref _shellImageListMaximumRequestedPixels, normalizedRequestedSize);

        if (sourceEdge > 0)
        {
            Interlocked.Increment(ref _shellImageListSourceSizeSamples);
            Interlocked.Add(ref _shellImageListSourceWidthTotal, normalizedSourceWidth);
            Interlocked.Add(ref _shellImageListSourceHeightTotal, normalizedSourceHeight);
            UpdateMaximum(ref _shellImageListMaximumSourcePixels, sourceEdge);

            if (sourceEdge < normalizedRequestedSize)
            {
                Interlocked.Increment(ref _shellImageListSourceSmallerThanRequest);
            }
            else if (sourceEdge == normalizedRequestedSize)
            {
                Interlocked.Increment(ref _shellImageListSourceEqualToRequest);
            }
            else
            {
                Interlocked.Increment(ref _shellImageListSourceLargerThanRequest);
            }
        }

        if (hIconConversionTicks > 0)
        {
            _shellHIconConversionLatency.Record(hIconConversionTicks);
        }

        IconLoadEventSource.Log.ShellImageListExtractionCompleted(
            Id,
            (int)imageListSize,
            normalizedRequestedSize,
            normalizedSourceWidth,
            normalizedSourceHeight,
            ToMicroseconds(hIconConversionTicks));
    }

    internal bool IsLoadDemanded(long loadId)
    {
        return _loadDemandStates.TryGetValue(loadId, out var demandState) && demandState.IsDemanded;
    }

    public void RecordSchedulerCommandPublished(IconLoadQueue.QueueCommandKind kind)
    {
        Interlocked.Increment(ref _schedulerCommandsPublished[(int)kind]);
        var backlog = Interlocked.Increment(ref _currentSchedulerCommandBacklog);
        UpdateMaximum(ref _maximumSchedulerCommandBacklog, backlog);
    }

    public void RecordSchedulerCommandProcessed(IconLoadQueue.QueueCommandKind kind, long elapsedTicks)
    {
        Interlocked.Increment(ref _schedulerCommandsProcessed[(int)kind]);
        _schedulerCommandLatency[(int)kind].Record(elapsedTicks);
        var backlog = Interlocked.Decrement(ref _currentSchedulerCommandBacklog);
        Debug.Assert(backlog >= 0, "A processed scheduler command must have been published in the same diagnostic session.");
        IconLoadEventSource.Log.SchedulerCommandProcessed(
            Id,
            (int)kind,
            ToMicroseconds(elapsedTicks),
            Math.Max(0, backlog));
    }

    public void RecordSchedulerCoordinatorWoke(IconLoadQueue.QueueCommandKind triggerKind, long elapsedTicks)
    {
        IconLoadEventSource.Log.SchedulerCoordinatorWoke(
            Id,
            (int)triggerKind,
            ToMicroseconds(elapsedTicks));
    }

    public void RecordSchedulerBatchCompleted(
        IconLoadQueue.QueueCommandKind triggerKind,
        long wakeTicks,
        int commandCount,
        int dispatchedWorkItemCount,
        long drainTicks,
        long passTicks)
    {
        Interlocked.Increment(ref _schedulerBatchesCompleted);
        if (commandCount == 0)
        {
            Interlocked.Increment(ref _schedulerEmptyBatches);
            _schedulerEmptyBatchSignalToWakeLatency.Record(wakeTicks);
        }
        else
        {
            _schedulerSignalToWakeLatency.Record(wakeTicks);
            _schedulerSignalToWakeLatencyByCommandKind[(int)triggerKind].Record(wakeTicks);
            _schedulerBatchDrainLatency.Record(drainTicks);
            _schedulerPassLatency.Record(passTicks);
        }

        Interlocked.Add(ref _schedulerCommandsDrained, commandCount);
        Interlocked.Add(ref _schedulerWorkItemsDispatched, dispatchedWorkItemCount);
        UpdateMaximum(ref _maximumSchedulerBatchSize, commandCount);
        UpdateMaximum(ref _maximumSchedulerDispatchCount, dispatchedWorkItemCount);
        IconLoadEventSource.Log.SchedulerBatchCompleted(
            Id,
            commandCount,
            dispatchedWorkItemCount,
            ToMicroseconds(drainTicks),
            ToMicroseconds(passTicks));
    }

    public void RecordWorkerDispatched(bool demanded, long elapsedTicks)
    {
        _workerReadyToDispatchLatency.Record(elapsedTicks);
        (demanded
            ? _workerReadyToDemandedDispatchLatency
            : _workerReadyToSpeculativeDispatchLatency).Record(elapsedTicks);
        IconLoadEventSource.Log.WorkerReadyToDispatchCompleted(
            Id,
            demanded ? 1 : 0,
            ToMicroseconds(elapsedTicks));
    }

    public void RecordDemandedIdleCapacityStarted(int demandedQueueDepth, int availableWorkerSlots)
    {
        Interlocked.Increment(ref _demandedIdleCapacityIntervalsStarted);
        Interlocked.Increment(ref _currentDemandedIdleCapacityIntervals);
        RecordDemandedIdleCapacityObserved(demandedQueueDepth, availableWorkerSlots);
    }

    public void RecordDemandedIdleCapacityObserved(int demandedQueueDepth, int availableWorkerSlots)
    {
        UpdateMaximum(ref _maximumDemandedQueueDepthWithIdleCapacity, demandedQueueDepth);
        UpdateMaximum(ref _maximumAvailableWorkerSlotsWithDemandedWork, availableWorkerSlots);
    }

    public void RecordDemandedIdleCapacityCompleted(long elapsedTicks)
    {
        var activeIntervals = Interlocked.Decrement(ref _currentDemandedIdleCapacityIntervals);
        Debug.Assert(activeIntervals >= 0, "A demanded-idle-capacity interval must start before it completes.");
        _demandedIdleCapacityDuration.Record(elapsedTicks);
        IconLoadEventSource.Log.DemandedIdleCapacityCompleted(Id, ToMicroseconds(elapsedTicks));
    }

    public void RecordSpeculativeDispatchDeferralStarted(
        int speculativeQueueDepth,
        int workerCount,
        int reservedWorkerSlots)
    {
        Interlocked.Increment(ref _speculativeDispatchDeferralIntervalsStarted);
        Interlocked.Increment(ref _currentSpeculativeDispatchDeferralIntervals);
        RecordSpeculativeDispatchDeferralObserved(speculativeQueueDepth, workerCount, reservedWorkerSlots);
    }

    public void RecordSpeculativeDispatchDeferralObserved(
        int speculativeQueueDepth,
        int workerCount,
        int reservedWorkerSlots)
    {
        UpdateMaximum(ref _maximumSpeculativeQueueDepthDuringDeferral, speculativeQueueDepth);
        UpdateMaximum(ref _maximumWorkerCountDuringSpeculativeDispatchDeferral, workerCount);
        UpdateMaximum(ref _maximumReservedWorkerSlotsDuringDeferral, reservedWorkerSlots);
    }

    public void RecordSpeculativeDispatchDeferralCompleted(long elapsedTicks)
    {
        var activeIntervals = Interlocked.Decrement(ref _currentSpeculativeDispatchDeferralIntervals);
        Debug.Assert(activeIntervals >= 0, "A speculative-dispatch-deferral interval must start before it completes.");
        _speculativeDispatchDeferralDuration.Record(elapsedTicks);
        IconLoadEventSource.Log.SpeculativeDispatchDeferralCompleted(Id, ToMicroseconds(elapsedTicks));
    }

    public IconRequestMeasurement BeginRequest(IconRequestReason reason, double scale, IconRequestOrigin origin)
    {
        origin = origin.Normalize();
        var requestId = Interlocked.Increment(ref _nextRequestId);
        Interlocked.Increment(ref _requestsStarted);
        var originKey = new RequestOriginKey(origin.RequestSite, origin.DiagnosticScope);
        var originMeasurements = _requestOriginMeasurements.GetOrAdd(originKey, static _ => new RequestOriginMeasurements());
        originMeasurements.RecordStarted(origin.IconBoxId);
        _requestDemandStates.TryAdd(requestId, new RequestDemandState(originMeasurements));
        IconLoadEventSource.Log.RequestStarted(Id, requestId, (int)reason, scale);
        IconLoadEventSource.Log.RequestOrigin(
            Id,
            requestId,
            origin.IconBoxId,
            (int)origin.RequestSite,
            origin.DiagnosticScope);
        return new IconRequestMeasurement(this, requestId, Stopwatch.GetTimestamp());
    }

    public IconLoadMeasurement CreateLoad(IconLoadInputKind inputKind, double width, double height, double scale)
    {
        var loadId = Interlocked.Increment(ref _nextLoadId);
        Interlocked.Increment(ref _loadsCreated);
        Interlocked.Increment(ref _inputKinds[(int)inputKind]);
        _loadDemandStates.TryAdd(loadId, new LoadDemandState(this, loadId, inputKind));
        IconLoadEventSource.Log.LoadCreated(Id, loadId, (int)inputKind, width, height, scale);
        return new IconLoadMeasurement(this, loadId, inputKind);
    }

    public void RecordProviderResolution(long requestId, long loadId, IconProviderResolution resolution)
    {
        Interlocked.Increment(ref _providerResolutions[(int)resolution]);
        if (_requestDemandStates.TryGetValue(requestId, out var requestState))
        {
            lock (requestState.SyncRoot)
            {
                requestState.Resolution = resolution;
                requestState.LoadId = loadId;
                requestState.OriginMeasurements.RecordProviderResolution(resolution);

                if (loadId != 0 && _loadDemandStates.TryGetValue(loadId, out var demandState))
                {
                    var result = demandState.RecordResolution(
                        resolution,
                        requestState.Invalidated,
                        requestState.InvalidatedAt);
                    requestState.TracksLiveRequester = result.TracksLiveRequester;
                    if (result.RetainedResultCacheHit)
                    {
                        IconLoadEventSource.Log.RetainedLoadCacheHit(Id, loadId, result.CacheHitsAfterCompletion);
                    }

                    if (requestState.Invalidated && !requestState.InvalidationAttributed)
                    {
                        RecordInvalidationAttribution(requestId, loadId, result.Stage, result.RemainingLiveRequesters);
                        requestState.InvalidationAttributed = true;
                    }
                }
                else if (requestState.Invalidated && !requestState.InvalidationAttributed)
                {
                    RecordInvalidationAttribution(requestId, 0, IconLoadDemandStage.Unlinked, 0);
                    requestState.InvalidationAttributed = true;
                }
            }
        }

        IconLoadEventSource.Log.ProviderResolved(Id, requestId, loadId, (int)resolution);
    }

    public void InvalidateRequest(long requestId)
    {
        if (!_requestDemandStates.TryGetValue(requestId, out var requestState))
        {
            return;
        }

        lock (requestState.SyncRoot)
        {
            InvalidateRequest(requestId, requestState, Stopwatch.GetTimestamp());
        }
    }

    public void RegisterLoad(Task<IconSource?> task, IconLoadMeasurement load)
    {
        _loadsByTask.Add(task, load);
    }

    public IconLoadMeasurement? FindLoad(Task<IconSource?> task)
    {
        return _loadsByTask.TryGetValue(task, out var load) ? load : null;
    }

    public void CompleteRequest(long requestId, IconRequestStatus status, IconLoadResultKind resultKind, long elapsedTicks)
    {
        Interlocked.Increment(ref _requestStatuses[(int)status]);
        _requestLatency.Record(elapsedTicks);
        IconLoadEventSource.Log.RequestCompleted(Id, requestId, (int)status, ToMicroseconds(elapsedTicks));

        if (_requestDemandStates.TryRemove(requestId, out var requestState))
        {
            lock (requestState.SyncRoot)
            {
                requestState.OriginMeasurements.RecordCompleted(
                    status,
                    resultKind,
                    requestState.Resolution,
                    elapsedTicks);

                if (status == IconRequestStatus.Stale && !requestState.Invalidated)
                {
                    InvalidateRequest(requestId, requestState, Stopwatch.GetTimestamp());
                }

                if (requestState.TracksLiveRequester
                    && requestState.LoadId != 0
                    && _loadDemandStates.TryGetValue(requestState.LoadId, out var demandState))
                {
                    demandState.CompleteRequest();
                    requestState.TracksLiveRequester = false;
                }

                if (requestState.Invalidated && !requestState.InvalidationAttributed)
                {
                    RecordInvalidationAttribution(requestId, 0, IconLoadDemandStage.Unlinked, 0);
                    requestState.InvalidationAttributed = true;
                }

                if (requestState.Resolution is { } resolution)
                {
                    _requestLatencyByResolutionAndResult[(int)resolution][(int)resultKind].Record(elapsedTicks);
                    if (status == IconRequestStatus.Applied)
                    {
                        _appliedRequestLatencyByResolution[(int)resolution].Record(elapsedTicks);
                    }

                    IconLoadEventSource.Log.RequestAttributed(
                        Id,
                        requestId,
                        (int)resolution,
                        (int)resultKind,
                        ToMicroseconds(elapsedTicks));
                }
            }
        }
    }

    private void InvalidateRequest(long requestId, RequestDemandState requestState, long invalidatedAt)
    {
        if (requestState.Invalidated)
        {
            return;
        }

        requestState.Invalidated = true;
        requestState.InvalidatedAt = invalidatedAt;

        if (requestState.Resolution is null)
        {
            return;
        }

        if (requestState.LoadId != 0
            && _loadDemandStates.TryGetValue(requestState.LoadId, out var demandState))
        {
            var result = demandState.InvalidateRequest(requestState.TracksLiveRequester, invalidatedAt);
            requestState.TracksLiveRequester = false;
            RecordInvalidationAttribution(
                requestId,
                requestState.LoadId,
                result.Stage,
                result.RemainingLiveRequesters);
        }
        else
        {
            RecordInvalidationAttribution(requestId, 0, IconLoadDemandStage.Unlinked, 0);
        }

        requestState.InvalidationAttributed = true;
    }

    private void RecordInvalidationAttribution(
        long requestId,
        long loadId,
        IconLoadDemandStage stage,
        int remainingLiveRequesters)
    {
        Interlocked.Increment(ref _invalidatedRequestLoadStages[(int)stage]);
        IconLoadEventSource.Log.RequestInvalidated(
            Id,
            requestId,
            loadId,
            (int)stage,
            remainingLiveRequesters);
    }

    public void RecordLoadEnqueued(long loadId, IconLoadPriority priority, int workerCount)
    {
        ref var currentDepth = ref (priority == IconLoadPriority.High
            ? ref _currentHighQueueDepth
            : ref _currentLowQueueDepth);
        ref var maximumDepth = ref (priority == IconLoadPriority.High
            ? ref _maximumHighQueueDepth
            : ref _maximumLowQueueDepth);

        var depth = Interlocked.Increment(ref currentDepth);
        UpdateMaximum(ref maximumDepth, depth);
        if (_loadDemandStates.TryGetValue(loadId, out var demandState))
        {
            demandState.MarkEnqueued(workerCount);
        }

        IconLoadEventSource.Log.LoadEnqueued(Id, loadId, (int)priority, depth);
    }

    private DemandedQueueArrival? RecordDemandQueueEnqueued(
        long loadId,
        IconLoadInputKind inputKind,
        bool demanded,
        int workerCount)
    {
        long demandedDepth;
        long speculativeDepth;
        DemandedQueueArrival? demandArrival = null;
        lock (_queueDemandLock)
        {
            if (demanded)
            {
                _currentDemandedQueueDepth++;
                _maximumDemandedQueueDepth = Math.Max(_maximumDemandedQueueDepth, _currentDemandedQueueDepth);
                demandArrival = RecordDemandArrival(inputKind, workerCount);
            }
            else
            {
                _currentSpeculativeQueueDepth++;
                _maximumSpeculativeQueueDepth = Math.Max(_maximumSpeculativeQueueDepth, _currentSpeculativeQueueDepth);
            }

            demandedDepth = _currentDemandedQueueDepth;
            speculativeDepth = _currentSpeculativeQueueDepth;
        }

        var transition = demanded
            ? IconLoadQueueDemandTransition.EnqueuedDemanded
            : IconLoadQueueDemandTransition.EnqueuedSpeculative;
        IconLoadEventSource.Log.LoadQueueDemandChanged(
            Id,
            loadId,
            (int)transition,
            demandedDepth,
            speculativeDepth);
        return demandArrival;
    }

    private void RecordDemandQueueAbandoned(bool demanded)
    {
        lock (_queueDemandLock)
        {
            if (demanded)
            {
                Debug.Assert(_currentDemandedQueueDepth > 0, "An abandoned demanded load must still be queued.");
                if (_currentDemandedQueueDepth > 0)
                {
                    _currentDemandedQueueDepth--;
                }
            }
            else
            {
                Debug.Assert(_currentSpeculativeQueueDepth > 0, "An abandoned speculative load must still be queued.");
                if (_currentSpeculativeQueueDepth > 0)
                {
                    _currentSpeculativeQueueDepth--;
                }
            }
        }
    }

    private DemandedQueueArrival? RecordQueuedDemandTransition(
        long loadId,
        IconLoadInputKind inputKind,
        bool becameDemanded,
        int workerCount)
    {
        long demandedDepth;
        long speculativeDepth;
        DemandedQueueArrival? demandArrival = null;
        lock (_queueDemandLock)
        {
            if (becameDemanded)
            {
                Debug.Assert(_currentSpeculativeQueueDepth > 0, "A queued promotion requires a speculative load.");
                _currentSpeculativeQueueDepth--;
                _currentDemandedQueueDepth++;
                _queuedDemandPromotions++;
                _maximumDemandedQueueDepth = Math.Max(_maximumDemandedQueueDepth, _currentDemandedQueueDepth);
                demandArrival = RecordDemandArrival(inputKind, workerCount);
            }
            else
            {
                Debug.Assert(_currentDemandedQueueDepth > 0, "A queued demotion requires a demanded load.");
                _currentDemandedQueueDepth--;
                _currentSpeculativeQueueDepth++;
                _queuedDemandDemotions++;
                _maximumSpeculativeQueueDepth = Math.Max(_maximumSpeculativeQueueDepth, _currentSpeculativeQueueDepth);
            }

            demandedDepth = _currentDemandedQueueDepth;
            speculativeDepth = _currentSpeculativeQueueDepth;
        }

        IconLoadEventSource.Log.LoadQueueDemandChanged(
            Id,
            loadId,
            (int)(becameDemanded ? IconLoadQueueDemandTransition.Promoted : IconLoadQueueDemandTransition.Demoted),
            demandedDepth,
            speculativeDepth);
        return demandArrival;
    }

    private DemandedQueueArrival RecordDemandArrival(IconLoadInputKind inputKind, int workerCount)
    {
        Debug.Assert(Monitor.IsEntered(_queueDemandLock), "Demand arrival accounting requires the queue-demand lock.");

        var activeSpeculativeWorkers = _currentActiveSpeculativeWorkers;
        var activeDemandedWorkers = _currentActiveDemandedWorkers;
        var remainingWorkerCapacity = Math.Max(
            0,
            workerCount - activeDemandedWorkers - activeSpeculativeWorkers);
        var capacityWithoutSpeculativeWorkers = Math.Max(0, workerCount - activeDemandedWorkers);
        var directlyBlockedBySpeculativeCapacity = activeSpeculativeWorkers > 0
            && _currentDemandedQueueDepth > remainingWorkerCapacity
            && _currentDemandedQueueDepth <= capacityWithoutSpeculativeWorkers;

        _demandedQueueArrivals++;
        if (activeSpeculativeWorkers > 0)
        {
            _demandedArrivalsWithActiveSpeculativeWorkers++;
            _speculativeWorkerOccupancyAtDemandArrivals += activeSpeculativeWorkers;
            _maximumSpeculativeWorkersAtDemandArrival = Math.Max(
                _maximumSpeculativeWorkersAtDemandArrival,
                activeSpeculativeWorkers);

            for (var i = 0; i < _currentActiveSpeculativeWorkersByInputKind.Length; i++)
            {
                _speculativeWorkerOccupancyAtDemandArrivalsByInputKind[i] +=
                    _currentActiveSpeculativeWorkersByInputKind[i];
            }
        }

        if (directlyBlockedBySpeculativeCapacity)
        {
            _demandedArrivalsDirectlyBlockedBySpeculativeCapacity++;
            _directlyBlockedDemandArrivalsByInputKind[(int)inputKind]++;
        }

        return new DemandedQueueArrival(
            Stopwatch.GetTimestamp(),
            activeSpeculativeWorkers,
            directlyBlockedBySpeculativeCapacity);
    }

    private void RecordDemandWorkerStarted(
        long loadId,
        IconLoadInputKind inputKind,
        bool demanded,
        long startedAt,
        long queueTicks,
        long activeWorkers,
        int workerCount,
        DemandedQueueArrival? demandArrival)
    {
        long demandedDepth;
        long speculativeDepth;
        long demandedBeyondCapacity;
        lock (_queueDemandLock)
        {
            if (demanded)
            {
                Debug.Assert(_currentDemandedQueueDepth > 0, "A demanded worker start requires a demanded queued load.");
                _currentDemandedQueueDepth--;
                _demandedWorkerStarts++;
                _currentActiveDemandedWorkers++;
            }
            else
            {
                Debug.Assert(_currentSpeculativeQueueDepth > 0, "A speculative worker start requires a speculative queued load.");
                _currentSpeculativeQueueDepth--;
                _speculativeWorkerStarts++;
                _currentActiveSpeculativeWorkers++;
                _currentActiveSpeculativeWorkersByInputKind[(int)inputKind]++;
                _maximumActiveSpeculativeWorkers = Math.Max(
                    _maximumActiveSpeculativeWorkers,
                    _currentActiveSpeculativeWorkers);
            }

            demandedDepth = _currentDemandedQueueDepth;
            speculativeDepth = _currentSpeculativeQueueDepth;
            var remainingWorkerCapacity = Math.Max(0, workerCount - activeWorkers);
            demandedBeyondCapacity = Math.Max(0, demandedDepth - remainingWorkerCapacity);

            if (!demanded && demandedDepth > 0)
            {
                _speculativeStartsWithDemandedLoadsQueued++;
            }

            if (!demanded && demandedBeyondCapacity > 0)
            {
                _capacityInterferingSpeculativeStarts++;
                _demandedLoadsBeyondCapacityAtSpeculativeStarts += demandedBeyondCapacity;
                _maximumDemandedLoadsBeyondCapacityAtSpeculativeStart = Math.Max(
                    _maximumDemandedLoadsBeyondCapacityAtSpeculativeStart,
                    demandedBeyondCapacity);
                _capacityInterferingSpeculativeStartsByInputKind[(int)inputKind]++;
            }
        }

        if (demanded)
        {
            _demandedQueueLatency.Record(queueTicks);
            _inputKindMeasurements[(int)inputKind].DemandedQueueLatency.Record(queueTicks);
        }
        else
        {
            _speculativeQueueLatency.Record(queueTicks);
            _inputKindMeasurements[(int)inputKind].SpeculativeQueueLatency.Record(queueTicks);
        }

        if (demanded && demandArrival is { SpeculativeWorkersAtArrival: > 0 } arrival)
        {
            var demandArrivalToWorkerStartTicks = Math.Max(0, startedAt - arrival.ArrivedAt);
            _demandArrivalToWorkerStartWithActiveSpeculative.Record(demandArrivalToWorkerStartTicks);
            if (arrival.DirectlyBlockedBySpeculativeCapacity)
            {
                _directlyBlockedDemandArrivalToWorkerStart.Record(demandArrivalToWorkerStartTicks);
            }
        }

        IconLoadEventSource.Log.LoadDemandAtWorkerStart(
            Id,
            loadId,
            demanded ? 1 : 0,
            demandedDepth,
            speculativeDepth,
            activeWorkers,
            workerCount,
            demandedBeyondCapacity);
    }

    private void RecordActiveWorkerDemandTransition(IconLoadInputKind inputKind, bool becameDemanded)
    {
        lock (_queueDemandLock)
        {
            if (becameDemanded)
            {
                Debug.Assert(_currentActiveSpeculativeWorkers > 0, "An active promotion requires a speculative worker.");
                if (_currentActiveSpeculativeWorkers > 0)
                {
                    _currentActiveSpeculativeWorkers--;
                }

                if (_currentActiveSpeculativeWorkersByInputKind[(int)inputKind] > 0)
                {
                    _currentActiveSpeculativeWorkersByInputKind[(int)inputKind]--;
                }

                _currentActiveDemandedWorkers++;
            }
            else
            {
                Debug.Assert(_currentActiveDemandedWorkers > 0, "An active demotion requires a demanded worker.");
                if (_currentActiveDemandedWorkers > 0)
                {
                    _currentActiveDemandedWorkers--;
                }

                _currentActiveSpeculativeWorkers++;
                _currentActiveSpeculativeWorkersByInputKind[(int)inputKind]++;
                _maximumActiveSpeculativeWorkers = Math.Max(
                    _maximumActiveSpeculativeWorkers,
                    _currentActiveSpeculativeWorkers);
            }
        }
    }

    private void RecordActiveWorkerCompleted(IconLoadInputKind inputKind, bool demanded)
    {
        lock (_queueDemandLock)
        {
            if (demanded)
            {
                Debug.Assert(_currentActiveDemandedWorkers > 0, "A demanded completion requires an active demanded worker.");
                if (_currentActiveDemandedWorkers > 0)
                {
                    _currentActiveDemandedWorkers--;
                }
            }
            else
            {
                Debug.Assert(_currentActiveSpeculativeWorkers > 0, "A speculative completion requires an active speculative worker.");
                if (_currentActiveSpeculativeWorkers > 0)
                {
                    _currentActiveSpeculativeWorkers--;
                }

                if (_currentActiveSpeculativeWorkersByInputKind[(int)inputKind] > 0)
                {
                    _currentActiveSpeculativeWorkersByInputKind[(int)inputKind]--;
                }
            }
        }
    }

    public void RecordLoadRejected(long loadId)
    {
        Interlocked.Increment(ref _loadsRejected);
        if (_loadDemandStates.TryGetValue(loadId, out var demandState))
        {
            demandState.MarkRejected();
        }

        IconLoadEventSource.Log.LoadRejected(Id, loadId);
    }

    public void RecordLoadAbandoned(long loadId, IconLoadPriority priority)
    {
        ref var currentDepth = ref (priority == IconLoadPriority.High
            ? ref _currentHighQueueDepth
            : ref _currentLowQueueDepth);
        var remainingDepth = Interlocked.Decrement(ref currentDepth);
        Debug.Assert(remainingDepth >= 0, "An abandoned icon load must have been counted as queued.");
        Interlocked.Increment(ref _loadsAbandonedBeforeStart);
        if (_loadDemandStates.TryGetValue(loadId, out var demandState))
        {
            demandState.MarkAbandoned();
        }
    }

    public void RecordWorkerStarted(
        long loadId,
        IconLoadInputKind inputKind,
        IconLoadPriority priority,
        long queueTicks,
        int workerCount)
    {
        if (priority == IconLoadPriority.High)
        {
            Interlocked.Decrement(ref _currentHighQueueDepth);
        }
        else
        {
            Interlocked.Decrement(ref _currentLowQueueDepth);
        }

        _queueLatency.Record(queueTicks);
        _inputKindMeasurements[(int)inputKind].QueueLatency.Record(queueTicks);
        var activeWorkers = Interlocked.Increment(ref _activeWorkers);
        UpdateMaximum(ref _maximumActiveWorkers, activeWorkers);
        if (_loadDemandStates.TryGetValue(loadId, out var demandState))
        {
            var demandResult = demandState.MarkWorkerStarted(
                Stopwatch.GetTimestamp(),
                queueTicks,
                activeWorkers,
                Math.Max(1, workerCount));
            if (demandResult.StartedWithoutLiveRequester)
            {
                IconLoadEventSource.Log.LoadStartedWithoutRequester(
                    Id,
                    loadId,
                    ToMicroseconds(demandResult.WithoutRequesterElapsedTicks));
            }
        }

        IconLoadEventSource.Log.LoadStarted(Id, loadId, ToMicroseconds(queueTicks), activeWorkers);
    }

    public void RecordWorkerReleased(long loadId)
    {
        var activeWorkers = Interlocked.Decrement(ref _activeWorkers);
        Debug.Assert(activeWorkers >= 0, "An icon worker can only be released after it starts.");
        if (_loadDemandStates.TryGetValue(loadId, out var demandState))
        {
            demandState.MarkWorkerReleased();
        }

        IconLoadEventSource.Log.LoadWorkerReleased(Id, loadId, activeWorkers);
    }

    public void RecordBackgroundPreparation(long loadId, IconLoadInputKind inputKind, long elapsedTicks)
    {
        _backgroundPreparationLatency.Record(elapsedTicks);
        _inputKindMeasurements[(int)inputKind].BackgroundPreparationLatency.Record(elapsedTicks);
        IconLoadEventSource.Log.BackgroundPreparationCompleted(Id, loadId, ToMicroseconds(elapsedTicks));
    }

    public void RecordDispatcherEnqueued(
        long loadId,
        IconLoadInputKind inputKind,
        IconDispatcherMaterializationKind materializationKind,
        bool isDemanded)
    {
        _ = loadId;
        _ = inputKind;
        IncrementDemandCount(
            isDemanded,
            ref _dispatcherEnqueuedDemanded,
            ref _dispatcherEnqueuedSpeculative);
        _dispatcherMaterializationMeasurements[(int)materializationKind].RecordEnqueued(isDemanded);
        var currentWaits = Interlocked.Increment(ref _currentDispatcherWaits);
        UpdateMaximum(ref _maximumDispatcherWaits, currentWaits);
    }

    public void RecordDispatcherWait(
        long loadId,
        IconLoadInputKind inputKind,
        IconDispatcherMaterializationKind materializationKind,
        bool isDemanded,
        long startedAt,
        long elapsedTicks)
    {
        Interlocked.Decrement(ref _currentDispatcherWaits);
        var currentCallbacks = Interlocked.Increment(ref _currentDispatcherCallbacks);
        UpdateMaximum(ref _maximumDispatcherCallbacks, currentCallbacks);
        IncrementDemandCount(
            isDemanded,
            ref _dispatcherStartedDemanded,
            ref _dispatcherStartedSpeculative);
        _dispatcherWaitLatency.Record(elapsedTicks);
        _dispatcherWaitLatencyByDemand[DemandIndex(isDemanded)].Record(elapsedTicks);
        _inputKindMeasurements[(int)inputKind].DispatcherWaitLatency.Record(elapsedTicks);
        _dispatcherMaterializationMeasurements[(int)materializationKind].RecordStarted(isDemanded, elapsedTicks);
        RecordDispatcherOutlier(
            loadId,
            inputKind,
            materializationKind,
            DispatcherOutlierPhase.QueueWait,
            isDemanded,
            startedAt,
            elapsedTicks);
        IconLoadEventSource.Log.DispatcherWaitCompleted(Id, loadId, ToMicroseconds(elapsedTicks));
    }

    public void RecordDispatcherWaitFailed(
        long loadId,
        IconLoadInputKind inputKind,
        IconDispatcherMaterializationKind materializationKind,
        bool isDemanded,
        long startedAt,
        long elapsedTicks)
    {
        Interlocked.Decrement(ref _currentDispatcherWaits);
        Interlocked.Increment(ref _dispatcherWaitFailures);
        _dispatcherWaitLatency.Record(elapsedTicks);
        _dispatcherWaitLatencyByDemand[DemandIndex(isDemanded)].Record(elapsedTicks);
        _inputKindMeasurements[(int)inputKind].DispatcherWaitLatency.Record(elapsedTicks);
        _dispatcherMaterializationMeasurements[(int)materializationKind].RecordWaitFailed(isDemanded, elapsedTicks);
        RecordDispatcherOutlier(
            loadId,
            inputKind,
            materializationKind,
            DispatcherOutlierPhase.QueueWaitFailed,
            isDemanded,
            startedAt,
            elapsedTicks);
        IconLoadEventSource.Log.DispatcherWaitFailed(Id, loadId, ToMicroseconds(elapsedTicks));
    }

    public void RecordDispatcherUiSlice(
        long loadId,
        IconLoadInputKind inputKind,
        IconDispatcherMaterializationKind materializationKind,
        IconDispatcherUiSliceKind sliceKind,
        bool isDemanded,
        long startedAt,
        long elapsedTicks)
    {
        _dispatcherUiExecutionLatency.Record(elapsedTicks);
        _dispatcherUiExecutionLatencyByDemand[DemandIndex(isDemanded)].Record(elapsedTicks);
        _dispatcherUiExecutionLatencyBySliceKind[(int)sliceKind].Record(elapsedTicks);
        _inputKindMeasurements[(int)inputKind].DispatcherUiExecutionLatency.Record(elapsedTicks);
        _dispatcherMaterializationMeasurements[(int)materializationKind].RecordUiExecution(isDemanded, elapsedTicks);
        var outlierPhase = sliceKind == IconDispatcherUiSliceKind.AsyncContinuation
            ? DispatcherOutlierPhase.UiContinuation
            : DispatcherOutlierPhase.UiEntry;
        RecordDispatcherOutlier(
            loadId,
            inputKind,
            materializationKind,
            outlierPhase,
            isDemanded,
            startedAt,
            elapsedTicks);
        IconLoadEventSource.Log.DispatcherUiSliceCompleted(
            Id,
            loadId,
            (int)materializationKind,
            (int)sliceKind,
            isDemanded,
            ToMicroseconds(elapsedTicks));
    }

    public void RecordDispatcherAsyncSuspension(
        long loadId,
        IconLoadInputKind inputKind,
        IconDispatcherMaterializationKind materializationKind,
        bool isDemanded,
        long startedAt,
        long elapsedTicks)
    {
        _dispatcherAsyncSuspensionLatency.Record(elapsedTicks);
        _dispatcherAsyncSuspensionLatencyByDemand[DemandIndex(isDemanded)].Record(elapsedTicks);
        _inputKindMeasurements[(int)inputKind].DispatcherAsyncSuspensionLatency.Record(elapsedTicks);
        _dispatcherMaterializationMeasurements[(int)materializationKind].RecordAsyncSuspension(isDemanded, elapsedTicks);
        RecordDispatcherOutlier(
            loadId,
            inputKind,
            materializationKind,
            DispatcherOutlierPhase.AsyncSuspension,
            isDemanded,
            startedAt,
            elapsedTicks);
        IconLoadEventSource.Log.DispatcherAsyncSuspensionCompleted(
            Id,
            loadId,
            (int)materializationKind,
            isDemanded,
            ToMicroseconds(elapsedTicks));
    }

    public void RecordDispatcherWork(
        long loadId,
        IconLoadInputKind inputKind,
        IconDispatcherMaterializationKind materializationKind,
        bool isDemanded,
        long startedAt,
        long elapsedTicks)
    {
        Interlocked.Decrement(ref _currentDispatcherCallbacks);
        IncrementDemandCount(
            isDemanded,
            ref _dispatcherCompletedDemanded,
            ref _dispatcherCompletedSpeculative);
        _dispatcherWorkLatency.Record(elapsedTicks);
        _dispatcherWorkLatencyByDemand[DemandIndex(isDemanded)].Record(elapsedTicks);
        _inputKindMeasurements[(int)inputKind].DispatcherWorkLatency.Record(elapsedTicks);
        _dispatcherMaterializationMeasurements[(int)materializationKind].RecordCompleted(isDemanded, elapsedTicks);
        RecordDispatcherOutlier(
            loadId,
            inputKind,
            materializationKind,
            DispatcherOutlierPhase.CallbackWindow,
            isDemanded,
            startedAt,
            elapsedTicks);
        IconLoadEventSource.Log.DispatcherWorkCompleted(Id, loadId, ToMicroseconds(elapsedTicks));
    }

    public void RecordLoadCompleted(long loadId, IconLoadInputKind inputKind, IconLoadResultKind resultKind, long elapsedTicks)
    {
        Interlocked.Increment(ref _resultKinds[(int)resultKind]);
        _loadLatency.Record(elapsedTicks);
        _inputKindMeasurements[(int)inputKind].LoadLatency.Record(elapsedTicks);
        RecordDemandCompletion(loadId, resultKind);
        IconLoadEventSource.Log.LoadCompleted(Id, loadId, (int)resultKind, ToMicroseconds(elapsedTicks));
    }

    public void RecordDirectGlyphCompleted(long loadId, IconLoadInputKind inputKind, IconLoadResultKind resultKind, long elapsedTicks)
    {
        Interlocked.Increment(ref _directGlyphLoads);
        Interlocked.Increment(ref _resultKinds[(int)resultKind]);
        _directGlyphLatency.Record(elapsedTicks);
        _directGlyphLatencyByResultKind[(int)resultKind].Record(elapsedTicks);
        _inputKindMeasurements[(int)inputKind].DirectGlyphLatency.Record(elapsedTicks);
        RecordDemandCompletion(loadId, resultKind);
        IconLoadEventSource.Log.DirectGlyphLoadCompleted(Id, loadId, (int)resultKind, ToMicroseconds(elapsedTicks));
    }

    private void RecordDemandCompletion(long loadId, IconLoadResultKind resultKind)
    {
        if (!_loadDemandStates.TryGetValue(loadId, out var demandState))
        {
            return;
        }

        var demandResult = demandState.MarkCompleted(Stopwatch.GetTimestamp(), resultKind);
        if (demandResult.CompletedWithoutLiveRequester)
        {
            IconLoadEventSource.Log.LoadCompletedWithoutRequester(
                Id,
                loadId,
                ToMicroseconds(demandResult.WithoutRequesterElapsedTicks));
        }
    }

    public void RecordElementUpdate(bool reused, IconLoadResultKind resultKind, long elapsedTicks)
    {
        var measurements = _elementKindMeasurements[(int)resultKind];
        if (reused)
        {
            Interlocked.Increment(ref _elementsReused);
        }
        else
        {
            Interlocked.Increment(ref _elementsCreated);
        }

        measurements.Record(reused);
        _elementUpdateLatency.Record(elapsedTicks);
        measurements.UpdateLatency.Record(elapsedTicks);
        IconLoadEventSource.Log.ElementUpdated(Id, (int)resultKind, reused, ToMicroseconds(elapsedTicks));
    }

    public void Stop()
    {
        if (Volatile.Read(ref _stoppedAt) != 0)
        {
            return;
        }

        lock (_stopLock)
        {
            if (_stoppedAt == 0)
            {
                _stoppedUtc = DateTimeOffset.UtcNow;
                var stoppedAt = Stopwatch.GetTimestamp();
                _uiResponsivenessProbe?.Stop();
                _processCpuStoppedTicks = GetProcessCpuTicks();
                _managedAllocatedBytesStopped = GC.GetTotalAllocatedBytes(precise: false);
                _gcPauseStoppedTicks = GC.GetTotalPauseDuration().Ticks;
                _gen0CollectionsStopped = GC.CollectionCount(0);
                _gen1CollectionsStopped = GC.CollectionCount(1);
                _gen2CollectionsStopped = GC.CollectionCount(2);
                _workingSetStoppedBytes = GetWorkingSetBytes();
                Volatile.Write(ref _stoppedAt, stoppedAt);
            }
        }
    }

    public IconLoadDiagnosticsReport CreateReport()
    {
        Stop();
        var stoppedAt = Volatile.Read(ref _stoppedAt);
        var duration = TimeSpan.FromSeconds((stoppedAt - _startedAt) / (double)Stopwatch.Frequency);

        var builder = new StringBuilder(4096);
        builder.AppendLine("CmdPal icon diagnostics");
        builder.Append("Session: ").AppendLine(Id.ToString(CultureInfo.InvariantCulture));
        builder.Append("Started UTC: ").AppendLine(_startedUtc.ToString("O", CultureInfo.InvariantCulture));
        builder.Append("Ended UTC: ").AppendLine(_stoppedUtc.ToString("O", CultureInfo.InvariantCulture));
        builder.Append("Duration: ").Append(FormatMilliseconds(stoppedAt - _startedAt)).AppendLine(" ms");
        builder.AppendLine();

        builder.AppendLine("Process work during session");
        AppendProcessWorkMeasurements(builder, stoppedAt - _startedAt);
        builder.AppendLine();

        builder.AppendLine("UI responsiveness probe");
        AppendUiResponsivenessMeasurements(builder);
        builder.AppendLine();

        builder.AppendLine("Requests");
        AppendValue(builder, "Started", Volatile.Read(ref _requestsStarted));
        AppendEnumCounts<IconRequestStatus>(builder, _requestStatuses);
        AppendValue(builder, "Outstanding at stop", Math.Max(0, Volatile.Read(ref _requestsStarted) - Sum(_requestStatuses)));
        _requestLatency.Append(builder, "Request to completion");
        builder.AppendLine();

        builder.AppendLine("Provider resolution");
        AppendEnumCounts<IconProviderResolution>(builder, _providerResolutions);
        AppendRequestMeasurements(builder);
        builder.AppendLine();

        builder.AppendLine("Icon caches");
        AppendCacheMeasurements(builder);
        builder.AppendLine();

        builder.AppendLine("Shell item identity and reuse");
        AppendShellIconMeasurements(builder);
        builder.AppendLine();

        builder.AppendLine("Request origins");
        AppendRequestOriginMeasurements(builder);
        builder.AppendLine();

        builder.AppendLine("Loads");
        AppendValue(builder, "Created", Volatile.Read(ref _loadsCreated));
        AppendValue(builder, "Rejected", Volatile.Read(ref _loadsRejected));
        AppendValue(builder, "Abandoned before worker start", Volatile.Read(ref _loadsAbandonedBeforeStart));
        AppendValue(builder, "Direct glyph loads", Volatile.Read(ref _directGlyphLoads));
        AppendValue(builder, "Active at stop", Math.Max(0, Volatile.Read(ref _activeWorkers)));
        AppendValue(builder, "Maximum active workers", Volatile.Read(ref _maximumActiveWorkers));
        AppendValue(builder, "Maximum high queue depth", Volatile.Read(ref _maximumHighQueueDepth));
        AppendValue(builder, "Maximum low queue depth", Volatile.Read(ref _maximumLowQueueDepth));
        _loadLatency.Append(builder, "Enqueue to completion");
        _directGlyphLatency.Append(builder, "Direct glyph construction");
        AppendDirectGlyphResultMeasurements(builder);
        _queueLatency.Append(builder, "Queue wait");
        _backgroundPreparationLatency.Append(builder, "Background preparation");
        _dispatcherWaitLatency.Append(builder, "Dispatcher wait");
        _dispatcherWorkLatency.Append(builder, "Dispatcher callback wall time");
        builder.AppendLine();

        builder.AppendLine("Dispatcher materialization");
        AppendDispatcherMeasurements(builder);
        builder.AppendLine();

        builder.AppendLine("Scheduler coordination");
        AppendSchedulerMeasurements(builder);
        builder.AppendLine();

        builder.AppendLine("Load demand");
        AppendLoadDemandMeasurements(builder);
        builder.AppendLine();

        builder.AppendLine("Input kinds");
        AppendInputKindMeasurements(builder);
        builder.AppendLine();

        builder.AppendLine("New-load result kinds");
        AppendEnumCounts<IconLoadResultKind>(builder, _resultKinds);
        builder.AppendLine();

        builder.AppendLine("Icon elements");
        AppendValue(builder, "Created", Volatile.Read(ref _elementsCreated));
        AppendValue(builder, "Reused", Volatile.Read(ref _elementsReused));
        _elementUpdateLatency.Append(builder, "Update wall time");
        AppendElementKindMeasurements(builder);
        builder.AppendLine();
        builder.AppendLine("No icon strings, paths, glyphs, application identifiers, or item data are included. Diagnostic scopes are static developer labels.");

        return new IconLoadDiagnosticsReport(Id, _startedUtc, _stoppedUtc, duration, builder.ToString());
    }

    private void AppendProcessWorkMeasurements(StringBuilder builder, long durationTicks)
    {
        builder.AppendLine("  Definition: process-wide measurements include all CmdPal work during the session, not only icon loading.");

        if (_processCpuStartedTicks < 0 || _processCpuStoppedTicks < 0)
        {
            builder.AppendLine("  Process CPU time: unavailable");
        }
        else
        {
            var cpuTicks = Math.Max(0, _processCpuStoppedTicks - _processCpuStartedTicks);
            var cpuMilliseconds = TimeSpan.FromTicks(cpuTicks).TotalMilliseconds;
            var durationMilliseconds = durationTicks * 1000D / Stopwatch.Frequency;
            var equivalentCoreUtilization = durationMilliseconds <= 0
                ? 0
                : cpuMilliseconds * 100D / durationMilliseconds;
            builder.Append("  Process CPU time: ")
                .Append(cpuMilliseconds.ToString("0.###", CultureInfo.InvariantCulture))
                .AppendLine(" ms");
            builder.Append("  Equivalent logical-core utilization (100% = one fully busy logical core): ")
                .Append(equivalentCoreUtilization.ToString("0.###", CultureInfo.InvariantCulture))
                .AppendLine(" %");
        }

        var allocatedBytes = Math.Max(0, _managedAllocatedBytesStopped - _managedAllocatedBytesStarted);
        builder.Append("  Managed allocations: ")
            .Append(allocatedBytes.ToString(CultureInfo.InvariantCulture))
            .Append(" bytes (")
            .Append(FormatMebibytes(allocatedBytes))
            .AppendLine(" MiB)");
        AppendValue(builder, "Gen 0 collections", Math.Max(0, _gen0CollectionsStopped - _gen0CollectionsStarted));
        AppendValue(builder, "Gen 1 collections", Math.Max(0, _gen1CollectionsStopped - _gen1CollectionsStarted));
        AppendValue(builder, "Gen 2 collections", Math.Max(0, _gen2CollectionsStopped - _gen2CollectionsStarted));
        builder.Append("  GC pause time: ")
            .Append(TimeSpan.FromTicks(Math.Max(0, _gcPauseStoppedTicks - _gcPauseStartedTicks))
                .TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture))
            .AppendLine(" ms");

        if (_workingSetStartedBytes < 0 || _workingSetStoppedBytes < 0)
        {
            builder.AppendLine("  Working set: unavailable");
        }
        else
        {
            builder.Append("  Working set at start: ").Append(FormatMebibytes(_workingSetStartedBytes)).AppendLine(" MiB");
            builder.Append("  Working set at stop: ").Append(FormatMebibytes(_workingSetStoppedBytes)).AppendLine(" MiB");
            builder.Append("  Working set change: ")
                .Append(FormatSignedMebibytes(_workingSetStoppedBytes - _workingSetStartedBytes))
                .AppendLine(" MiB");
        }
    }

    private void AppendUiResponsivenessMeasurements(StringBuilder builder)
    {
        builder.AppendLine("  Definition: a background clock posts at most one minimal normal-priority callback every 50 ms.");
        builder.AppendLine("  Its delay measures how long executing dispatcher work blocks normal-priority responsiveness; it intentionally runs before queued low-priority icon callbacks.");
        builder.AppendLine("  Use Dispatcher wait measurements for low-priority icon queue depth. This is a coarse signal, not frame time.");
        builder.Append("  Enabled: ").AppendLine(_uiResponsivenessProbe is null ? "no" : "yes");
        var enqueued = Volatile.Read(ref _uiProbeEnqueued);
        var completed = Volatile.Read(ref _uiProbeCompleted);
        var rejected = Volatile.Read(ref _uiProbeRejected);
        AppendValue(builder, "Callbacks enqueued", enqueued);
        AppendValue(builder, "Callbacks completed", completed);
        AppendValue(builder, "Callbacks outstanding at stop", Math.Max(0, enqueued - completed - rejected));
        AppendValue(builder, "Timer ticks skipped while a callback was pending", Volatile.Read(ref _uiProbeSkipped));
        AppendValue(builder, "Callbacks rejected by DispatcherQueue", rejected);
        _uiProbeWaitLatency.Append(builder, "Normal-priority queue wait");
    }

    private void AppendDispatcherMeasurements(StringBuilder builder)
    {
        builder.AppendLine("  Definitions:");
        builder.AppendLine("    Queue wait is from publishing low-priority icon work until its dispatcher callback starts.");
        builder.AppendLine("    Callback wall time includes asynchronous suspension; it is worker-slot occupancy, not STA CPU time.");
        builder.AppendLine("    Measured STA execution slices cover the loader's managed callback entry and outer continuation work around asynchronous operations.");
        builder.AppendLine("    Framework work, nested async-helper continuations, and native rendering may occur inside a suspension window or continue outside the measured slices.");
        builder.AppendLine("    Later XAML layout, rasterization, and rendering of the created source are outside this section; the responsiveness probe can still expose resulting dispatcher stalls.");
        builder.AppendLine("    Queue-wait demand is sampled when the wait ends: at callback start or enqueue failure.");
        builder.AppendLine("    Cumulative times sum all loads and can overlap across workers. Demand is sampled independently at enqueue, callback start, and completion.");

        builder.AppendLine("  Phase counts");
        AppendValue(builder, "Enqueued demanded", Volatile.Read(ref _dispatcherEnqueuedDemanded), "    ");
        AppendValue(builder, "Enqueued speculative", Volatile.Read(ref _dispatcherEnqueuedSpeculative), "    ");
        AppendValue(builder, "Callbacks started demanded", Volatile.Read(ref _dispatcherStartedDemanded), "    ");
        AppendValue(builder, "Callbacks started speculative", Volatile.Read(ref _dispatcherStartedSpeculative), "    ");
        AppendValue(builder, "Callbacks completed demanded", Volatile.Read(ref _dispatcherCompletedDemanded), "    ");
        AppendValue(builder, "Callbacks completed speculative", Volatile.Read(ref _dispatcherCompletedSpeculative), "    ");
        AppendValue(builder, "Dispatcher enqueue failures", Volatile.Read(ref _dispatcherWaitFailures), "    ");
        AppendValue(builder, "Waits outstanding at stop", Math.Max(0, Volatile.Read(ref _currentDispatcherWaits)), "    ");
        AppendValue(builder, "Maximum simultaneous waits", Volatile.Read(ref _maximumDispatcherWaits), "    ");
        AppendValue(builder, "Callback windows outstanding at stop", Math.Max(0, Volatile.Read(ref _currentDispatcherCallbacks)), "    ");
        AppendValue(builder, "Maximum simultaneous callback windows", Volatile.Read(ref _maximumDispatcherCallbacks), "    ");

        var preparationTicks = _backgroundPreparationLatency.SumTicks;
        var waitTicks = _dispatcherWaitLatency.SumTicks;
        var callbackTicks = _dispatcherWorkLatency.SumTicks;
        var uiHandoffTicks = waitTicks + callbackTicks;
        var postWorkerStartTicks = preparationTicks + uiHandoffTicks;
        builder.AppendLine("  Cumulative worker-path time");
        AppendCumulativeTime(builder, "Background preparation", preparationTicks);
        AppendCumulativeTime(builder, "Low-priority dispatcher wait", waitTicks);
        AppendCumulativeTime(builder, "Dispatcher callback wall windows", callbackTicks);
        AppendCumulativeTime(builder, "UI handoff total", uiHandoffTicks);
        AppendCumulativeTime(builder, "Post-worker-start materialization total", postWorkerStartTicks);
        builder.Append("    UI handoff share of post-worker-start time: ")
            .Append(postWorkerStartTicks == 0
                ? "n/a"
                : (uiHandoffTicks * 100D / postWorkerStartTicks).ToString("0.###", CultureInfo.InvariantCulture) + " %")
            .AppendLine();
        AppendCumulativeTime(builder, "Measured managed STA execution", _dispatcherUiExecutionLatency.SumTicks);
        AppendCumulativeTime(builder, "Asynchronous materialization suspension", _dispatcherAsyncSuspensionLatency.SumTicks);

        var measuredUiThreadTicks = _directGlyphLatency.SumTicks +
            _dispatcherUiExecutionLatency.SumTicks +
            _elementUpdateLatency.SumTicks;
        builder.AppendLine("  Measured UI-thread work in instrumented icon paths");
        builder.AppendLine("    Definition: a lower bound composed of direct glyph construction, loader-managed STA slices, and IconBox element updates. It excludes later XAML rendering and unrelated UI work.");
        AppendCumulativeTime(builder, "Direct glyph construction", _directGlyphLatency.SumTicks);
        AppendCumulativeTime(builder, "Loader-managed STA slices", _dispatcherUiExecutionLatency.SumTicks);
        AppendCumulativeTime(builder, "IconBox element updates", _elementUpdateLatency.SumTicks);
        AppendCumulativeTime(builder, "Measured icon UI-thread total", measuredUiThreadTicks);

        builder.AppendLine("  Overall timing");
        _dispatcherWaitLatency.Append(builder, "Low-priority dispatcher wait", "    ");
        _dispatcherWorkLatency.Append(builder, "Dispatcher callback wall time", "    ");
        _dispatcherUiExecutionLatency.Append(builder, "Measured STA execution slices", "    ");
        _dispatcherAsyncSuspensionLatency.Append(builder, "Asynchronous materialization suspension", "    ");

        builder.AppendLine("  By demand at measured phase");
        AppendDispatcherDemandMeasurements(builder, "Speculative", 0);
        AppendDispatcherDemandMeasurements(builder, "Demanded", 1);

        builder.AppendLine("  Measured STA execution by slice kind");
        var sliceKinds = Enum.GetValues<IconDispatcherUiSliceKind>();
        for (var i = 0; i < sliceKinds.Length; i++)
        {
            _dispatcherUiExecutionLatencyBySliceKind[i].Append(builder, sliceKinds[i].ToString(), "    ");
        }

        builder.AppendLine("  By materialization kind");
        var materializationKinds = Enum.GetValues<IconDispatcherMaterializationKind>();
        var wroteMaterialization = false;
        for (var i = 0; i < materializationKinds.Length; i++)
        {
            var measurements = _dispatcherMaterializationMeasurements[i];
            if (!measurements.HasSamples)
            {
                continue;
            }

            wroteMaterialization = true;
            builder.Append("    ").AppendLine(materializationKinds[i].ToString());
            AppendValue(builder, "Enqueued demanded", measurements.EnqueuedDemanded, "      ");
            AppendValue(builder, "Enqueued speculative", measurements.EnqueuedSpeculative, "      ");
            AppendValue(builder, "Callbacks started demanded", measurements.StartedDemanded, "      ");
            AppendValue(builder, "Callbacks started speculative", measurements.StartedSpeculative, "      ");
            AppendValue(builder, "Callbacks completed demanded", measurements.CompletedDemanded, "      ");
            AppendValue(builder, "Callbacks completed speculative", measurements.CompletedSpeculative, "      ");
            AppendValue(builder, "Dispatcher enqueue failures", measurements.WaitFailures, "      ");
            measurements.DispatcherWaitLatency.Append(builder, "Low-priority dispatcher wait", "      ");
            measurements.CallbackWallLatency.Append(builder, "Dispatcher callback wall time", "      ");
            measurements.UiExecutionLatency.Append(builder, "Measured STA execution slices", "      ");
            measurements.AsyncSuspensionLatency.Append(builder, "Asynchronous materialization suspension", "      ");
            measurements.AppendDemandTimings(builder, "Speculative", 0);
            measurements.AppendDemandTimings(builder, "Demanded", 1);
        }

        if (!wroteMaterialization)
        {
            builder.AppendLine("    no samples");
        }

        var outliers = _dispatcherOutliers.ToArray();
        Array.Sort(outliers, static (left, right) => right.ElapsedTicks.CompareTo(left.ElapsedTicks));
        builder.AppendLine("  Dispatcher outliers (>=16 ms, top 10 by duration)");
        AppendValue(builder, "Samples captured", outliers.Length, "    ");
        if (outliers.Length == 0)
        {
            builder.AppendLine("    no samples");
        }
        else
        {
            for (var i = 0; i < Math.Min(10, outliers.Length); i++)
            {
                var sample = outliers[i];
                builder.Append("    Load ").Append(sample.LoadId.ToString(CultureInfo.InvariantCulture))
                    .Append(": phase=").Append(sample.Phase)
                    .Append(", input=").Append(sample.InputKind)
                    .Append(", materialization=").Append(sample.MaterializationKind)
                    .Append(", demand=").Append(sample.IsDemanded ? "Demanded" : "Speculative")
                    .Append(", session offset=").Append(FormatMilliseconds(Math.Max(0, sample.StartedAt - _startedAt))).Append(" ms")
                    .Append(", duration=").Append(FormatMilliseconds(sample.ElapsedTicks)).AppendLine(" ms");
            }
        }
    }

    private void AppendDispatcherDemandMeasurements(StringBuilder builder, string name, int index)
    {
        builder.Append("    ").AppendLine(name);
        _dispatcherWaitLatencyByDemand[index].Append(builder, "Low-priority dispatcher wait", "      ");
        _dispatcherWorkLatencyByDemand[index].Append(builder, "Dispatcher callback wall time", "      ");
        _dispatcherUiExecutionLatencyByDemand[index].Append(builder, "Measured STA execution slices", "      ");
        _dispatcherAsyncSuspensionLatencyByDemand[index].Append(builder, "Asynchronous materialization suspension", "      ");
    }

    private static void AppendCumulativeTime(StringBuilder builder, string name, long stopwatchTicks)
    {
        builder.Append("    ").Append(name).Append(": ").Append(FormatMilliseconds(stopwatchTicks)).AppendLine(" ms");
    }

    private void AppendCacheMeasurements(StringBuilder builder)
    {
        builder.AppendLine("  Definition: each entry is a cached IconSource task; counts are approximate concurrent observations. Eviction only drops the cache reference.");
        builder.AppendLine("  A request coalesced with an in-flight load is a cache miss; see Provider resolution for in-flight reuse.");
        builder.AppendLine("  Capacity means the cache was over its limit when removal was attempted and takes precedence over LowScore; LowScore means score alone caused removal.");
        if (_cacheMeasurements.IsEmpty)
        {
            builder.AppendLine("  No cache activity was observed during this session.");
            return;
        }

        var caches = _cacheMeasurements.ToArray();
        Array.Sort(
            caches,
            static (left, right) =>
            {
                var width = left.Key.Width.CompareTo(right.Key.Width);
                if (width != 0)
                {
                    return width;
                }

                var height = left.Key.Height.CompareTo(right.Key.Height);
                if (height != 0)
                {
                    return height;
                }

                var partition = left.Key.Partition.CompareTo(right.Key.Partition);
                return partition != 0 ? partition : left.Key.Capacity.CompareTo(right.Key.Capacity);
            });

        foreach (var (descriptor, measurements) in caches)
        {
            var snapshot = measurements.CreateSnapshot();
            builder
                .Append("  ")
                .Append(descriptor.Width)
                .Append('x')
                .Append(descriptor.Height)
                .Append(' ')
                .Append(descriptor.Partition)
                .Append(" cache, capacity ")
                .AppendLine(descriptor.Capacity.ToString(CultureInfo.InvariantCulture));
            AppendValue(builder, "Lookups", snapshot.Hits + snapshot.Misses, "    ");
            AppendValue(builder, "Hits", snapshot.Hits, "    ");
            AppendValue(builder, "Misses", snapshot.Misses, "    ");
            builder.Append("    Hit rate: ")
                .Append(snapshot.Hits + snapshot.Misses == 0
                    ? "n/a"
                    : (snapshot.Hits * 100D / (snapshot.Hits + snapshot.Misses)).ToString("0.###", CultureInfo.InvariantCulture) + " %")
                .AppendLine();
            AppendValue(builder, "First observed entries", snapshot.FirstObservedCount, "    ");
            AppendValue(builder, "Last observed entries", snapshot.LastObservedCount, "    ");
            AppendValue(builder, "Maximum observed entries", snapshot.MaximumObservedCount, "    ");
            AppendValue(builder, "Entries added during session", snapshot.EntriesAdded, "    ");
            AppendValue(builder, "Entries removed during session", snapshot.EntriesRemoved, "    ");
            builder.AppendLine("    Removal reasons");
            AppendEnumCounts<AdaptiveCacheRemovalReason>(builder, snapshot.RemovalsByReason, "      ");
        }
    }

    private void AppendShellIconMeasurements(StringBuilder builder)
    {
        var requestCount = Sum(_shellIconRequestKinds);
        var canonicalCacheHits = Volatile.Read(ref _shellIconCanonicalCacheHits);
        var canonicalInFlightJoins = Volatile.Read(ref _shellIconCanonicalInFlightJoins);
        var canonicalNewLoads = Volatile.Read(ref _shellIconCanonicalNewLoads);
        var canonicalOutcomes = canonicalCacheHits + canonicalInFlightJoins + canonicalNewLoads;
        var extractionCount = Volatile.Read(ref _shellIconExtractionsSucceeded)
            + Volatile.Read(ref _shellIconExtractionsEmpty)
            + Volatile.Read(ref _shellIconExtractionsFailed);
        var imageListExtractionCount = Sum(_shellImageListSizes);
        var imageListSourceSizeSamples = Volatile.Read(ref _shellImageListSourceSizeSamples);

        builder.AppendLine("  Definition: location aliases map submitted paths to non-sensitive Shell identities; canonical outcomes describe materialized source reuse after that mapping.");
        builder.AppendLine("  The same identity has independent materialized entries for each icon size and scale.");
        AppendValue(builder, "Requests", requestCount);
        builder.AppendLine("  Requests by kind");
        AppendEnumCounts<ShellIconRequestKind>(builder, _shellIconRequestKinds, "    ");
        builder.AppendLine("  Location invalidation");
        AppendValue(builder, "Association-change notifications received", Volatile.Read(ref _shellIconAssociationChangedNotifications), "    ");
        builder.AppendLine("    Invalidations by reason");
        AppendEnumCounts<ShellIconCacheInvalidationReason>(builder, _shellIconCacheInvalidationReasons, "      ");
        builder.AppendLine("  Location aliases");
        AppendValue(builder, "Cache hits", Volatile.Read(ref _shellIconLocationCacheHits), "    ");
        AppendValue(builder, "Cache misses", Volatile.Read(ref _shellIconLocationCacheMisses), "    ");
        AppendValue(builder, "Raw in-flight joins before identity resolution", Volatile.Read(ref _shellIconRawInFlightJoins), "    ");
        AppendValue(builder, "Identity resolutions", Sum(_shellIconIdentityKinds), "    ");
        _shellIconIdentityResolutionLatency.Append(builder, "Identity resolution", "    ");
        builder.AppendLine("    Resolved identity kinds");
        AppendEnumCounts<ShellIconIdentityKind>(builder, _shellIconIdentityKinds, "      ");
        builder.AppendLine("  Canonical source outcomes");
        AppendValue(builder, "Cache hits", canonicalCacheHits, "    ");
        AppendValue(builder, "In-flight joins", canonicalInFlightJoins, "    ");
        AppendValue(builder, "New loads", canonicalNewLoads, "    ");
        AppendPercentage(builder, "Reuse rate", canonicalCacheHits + canonicalInFlightJoins, canonicalOutcomes, "    ");
        builder.AppendLine("  Shell extraction");
        AppendValue(builder, "Started", extractionCount, "    ");
        AppendValue(builder, "Succeeded", Volatile.Read(ref _shellIconExtractionsSucceeded), "    ");
        AppendValue(builder, "Empty", Volatile.Read(ref _shellIconExtractionsEmpty), "    ");
        AppendValue(builder, "Failed", Volatile.Read(ref _shellIconExtractionsFailed), "    ");
        _shellIconExtractionLatency.Append(builder, "Extraction", "    ");
        builder.AppendLine("    Extraction routes");
        AppendEnumCounts<ShellIconIdentityKind>(builder, _shellIconExtractionKinds, "      ");
        AppendPercentage(builder, "Requests avoiding extraction", Math.Max(0, requestCount - extractionCount), requestCount, "    ");
        builder.AppendLine("    Direct system image-list extraction");
        AppendValue(builder, "Attempts", imageListExtractionCount, "      ");
        builder.AppendLine("      Image-list levels used");
        AppendEnumCounts<ShellImageListSize>(builder, _shellImageListSizes, "        ");
        AppendAveragePixels(
            builder,
            "Requested physical edge",
            Volatile.Read(ref _shellImageListRequestedPixelTotal),
            imageListExtractionCount,
            Volatile.Read(ref _shellImageListMaximumRequestedPixels),
            "      ");
        AppendAverageDimensions(
            builder,
            "Source image-list dimensions",
            Volatile.Read(ref _shellImageListSourceWidthTotal),
            Volatile.Read(ref _shellImageListSourceHeightTotal),
            imageListSourceSizeSamples,
            Volatile.Read(ref _shellImageListMaximumSourcePixels),
            "      ");
        AppendValue(builder, "Source smaller than request", Volatile.Read(ref _shellImageListSourceSmallerThanRequest), "      ");
        AppendValue(builder, "Source equal to request", Volatile.Read(ref _shellImageListSourceEqualToRequest), "      ");
        AppendValue(builder, "Source larger than request", Volatile.Read(ref _shellImageListSourceLargerThanRequest), "      ");
        _shellHIconConversionLatency.Append(builder, "HICON to SoftwareBitmap", "      ");
    }

    private static void AppendAveragePixels(
        StringBuilder builder,
        string name,
        long total,
        long count,
        long maximum,
        string indentation)
    {
        builder.Append(indentation).Append(name).Append(": ");
        if (count == 0)
        {
            builder.AppendLine("no samples");
            return;
        }

        builder
            .Append("count=").Append(count.ToString(CultureInfo.InvariantCulture))
            .Append(", avg=").Append((total / (double)count).ToString("0.###", CultureInfo.InvariantCulture)).Append(" px")
            .Append(", max=").Append(maximum.ToString(CultureInfo.InvariantCulture)).AppendLine(" px");
    }

    private static void AppendAverageDimensions(
        StringBuilder builder,
        string name,
        long totalWidth,
        long totalHeight,
        long count,
        long maximumEdge,
        string indentation)
    {
        builder.Append(indentation).Append(name).Append(": ");
        if (count == 0)
        {
            builder.AppendLine("no samples");
            return;
        }

        builder
            .Append("count=").Append(count.ToString(CultureInfo.InvariantCulture))
            .Append(", avg=").Append((totalWidth / (double)count).ToString("0.###", CultureInfo.InvariantCulture))
            .Append('x').Append((totalHeight / (double)count).ToString("0.###", CultureInfo.InvariantCulture)).Append(" px")
            .Append(", max edge=").Append(maximumEdge.ToString(CultureInfo.InvariantCulture)).AppendLine(" px");
    }

    private static void AppendPercentage(
        StringBuilder builder,
        string name,
        long numerator,
        long denominator,
        string indentation)
    {
        builder.Append(indentation).Append(name).Append(": ");
        if (denominator == 0)
        {
            builder.AppendLine("n/a");
            return;
        }

        builder.Append((100d * numerator / denominator).ToString("0.###", CultureInfo.InvariantCulture)).AppendLine("%");
    }

    private CacheMeasurements GetCacheMeasurements(
        Size iconSize,
        IconCachePartition partition,
        int capacity)
    {
        var descriptor = new CacheDescriptor(
            NormalizeCacheDimension(iconSize.Width),
            NormalizeCacheDimension(iconSize.Height),
            partition,
            capacity);
        return _cacheMeasurements.GetOrAdd(descriptor, static _ => new CacheMeasurements());
    }

    private static int NormalizeCacheDimension(double value)
    {
        return double.IsFinite(value) && value >= 0
            ? (int)Math.Round(value)
            : 0;
    }

    private void AppendSchedulerMeasurements(StringBuilder builder)
    {
        builder.AppendLine("  Definition: the command backlog is work published to the lock-free coordinator but not yet processed during this diagnostic session.");
        builder.AppendLine("  Commands published by kind");
        AppendEnumCounts<IconLoadQueue.QueueCommandKind>(builder, _schedulerCommandsPublished, "    ");
        builder.AppendLine("  Commands processed by kind");
        AppendEnumCounts<IconLoadQueue.QueueCommandKind>(builder, _schedulerCommandsProcessed, "    ");
        AppendValue(
            builder,
            "Commands outstanding at stop",
            Math.Max(0, Volatile.Read(ref _currentSchedulerCommandBacklog)));
        AppendValue(builder, "Maximum command backlog", Volatile.Read(ref _maximumSchedulerCommandBacklog));
        builder.AppendLine("  Publish to coordinator processing by command kind");

        var commandKinds = Enum.GetValues<IconLoadQueue.QueueCommandKind>();
        for (var i = 0; i < commandKinds.Length; i++)
        {
            _schedulerCommandLatency[i].Append(builder, commandKinds[i].ToString(), "    ");
        }

        builder.AppendLine("  Coordinator wake and batch processing");
        builder.AppendLine("    Definition: the first command to complete the current signal triggers the next coordinator pass; later pulses coalesce. If the coordinator is still processing, latency includes the remainder of that pass. Drain time excludes worker dispatch.");
        builder.AppendLine("    Empty batches occur when the preceding pass already consumed the triggering command; their signal-to-pass-start latency is reported separately.");
        _schedulerSignalToWakeLatency.Append(builder, "Signal to coordinator pass start for non-empty batches", "    ");
        _schedulerEmptyBatchSignalToWakeLatency.Append(builder, "Signal to coordinator pass start for empty coalesced batches", "    ");
        builder.AppendLine("    Non-empty batch signal to coordinator pass start by triggering command kind");
        for (var i = 0; i < commandKinds.Length; i++)
        {
            _schedulerSignalToWakeLatencyByCommandKind[i].Append(
                builder,
                commandKinds[i].ToString(),
                "      ");
        }

        var batchesCompleted = Volatile.Read(ref _schedulerBatchesCompleted);
        var emptyBatches = Volatile.Read(ref _schedulerEmptyBatches);
        var nonEmptyBatches = Math.Max(0, batchesCompleted - emptyBatches);
        var commandsDrained = Volatile.Read(ref _schedulerCommandsDrained);
        var workItemsDispatched = Volatile.Read(ref _schedulerWorkItemsDispatched);
        AppendValue(builder, "Batches completed", batchesCompleted, "    ");
        AppendValue(builder, "Empty batches", emptyBatches, "    ");
        AppendValue(builder, "Commands drained", commandsDrained, "    ");
        AppendAverage(builder, "Average commands per non-empty batch", commandsDrained, nonEmptyBatches, "    ");
        AppendValue(builder, "Maximum commands in one batch", Volatile.Read(ref _maximumSchedulerBatchSize), "    ");
        AppendValue(builder, "Work items dispatched", workItemsDispatched, "    ");
        AppendAverage(builder, "Average work items dispatched per non-empty batch", workItemsDispatched, nonEmptyBatches, "    ");
        AppendValue(builder, "Maximum work items dispatched in one batch", Volatile.Read(ref _maximumSchedulerDispatchCount), "    ");
        _schedulerBatchDrainLatency.Append(builder, "Non-empty batch command drain wall time", "    ");
        _schedulerPassLatency.Append(builder, "Non-empty batch pass-start-to-dispatch-complete wall time", "    ");

        builder.AppendLine("  Worker handoff");
        builder.AppendLine("    Definition: measured from a worker publishing readiness until the coordinator assigns work to that worker slot; this includes ordinary idle time before work arrives.");
        _workerReadyToDispatchLatency.Append(builder, "Ready to work dispatch", "    ");
        _workerReadyToDemandedDispatchLatency.Append(builder, "Ready to demanded work dispatch", "    ");
        _workerReadyToSpeculativeDispatchLatency.Append(builder, "Ready to speculative work dispatch", "    ");
        builder.AppendLine("  Demanded work queued while worker capacity was available");
        builder.AppendLine("    Definition: a coordinator-state interval with at least one demanded queued load and at least one worker-ready slot. It normally spans command draining before dispatch.");
        AppendValue(
            builder,
            "Intervals started",
            Volatile.Read(ref _demandedIdleCapacityIntervalsStarted),
            "    ");
        AppendValue(
            builder,
            "Intervals active at stop",
            Math.Max(0, Volatile.Read(ref _currentDemandedIdleCapacityIntervals)),
            "    ");
        AppendValue(
            builder,
            "Maximum demanded queue depth during an interval",
            Volatile.Read(ref _maximumDemandedQueueDepthWithIdleCapacity),
            "    ");
        AppendValue(
            builder,
            "Maximum available worker slots during an interval",
            Volatile.Read(ref _maximumAvailableWorkerSlotsWithDemandedWork),
            "    ");
        _demandedIdleCapacityDuration.Append(builder, "Interval duration", "    ");
        builder.AppendLine("  Speculative dispatch deferred by the demand reserve");
        builder.AppendLine("    Definition: a coordinator-state interval with speculative work queued, no demanded work queued, and a worker-ready slot deliberately retained for a future live request.");
        AppendValue(
            builder,
            "Intervals started",
            Volatile.Read(ref _speculativeDispatchDeferralIntervalsStarted),
            "    ");
        AppendValue(
            builder,
            "Intervals active at stop",
            Math.Max(0, Volatile.Read(ref _currentSpeculativeDispatchDeferralIntervals)),
            "    ");
        AppendValue(
            builder,
            "Maximum speculative queue depth during an interval",
            Volatile.Read(ref _maximumSpeculativeQueueDepthDuringDeferral),
            "    ");
        AppendValue(
            builder,
            "Maximum configured worker count during an interval",
            Volatile.Read(ref _maximumWorkerCountDuringSpeculativeDispatchDeferral),
            "    ");
        AppendValue(
            builder,
            "Maximum worker-ready slots retained during an interval",
            Volatile.Read(ref _maximumReservedWorkerSlotsDuringDeferral),
            "    ");
        _speculativeDispatchDeferralDuration.Append(builder, "Interval duration", "    ");
    }

    private void AppendLoadDemandMeasurements(StringBuilder builder)
    {
        var linkedRequests = 0L;
        var loadsWithMultipleRequesters = 0L;
        var maximumLiveRequesters = 0L;
        var loadsWithLiveRequesters = 0L;
        var liveRequesters = 0L;
        var lostBeforeEnqueue = 0L;
        var lostWhileQueued = 0L;
        var lostWhileWorkerActive = 0L;
        var lostWhileAwaitingSharedLoad = 0L;
        var loadsWhereDemandReturned = 0L;
        var workersStartedWithoutRequester = 0L;
        var loadsCompletedWithoutRequester = 0L;
        var retainedLoadsLaterCacheHit = 0L;
        var retainedResultCacheHits = 0L;
        var unrequestedCompletionsByInputKind = new long[Enum.GetValues<IconLoadInputKind>().Length];
        var unrequestedCompletionsByResultKind = new long[Enum.GetValues<IconLoadResultKind>().Length];
        DiagnosticHistogram withoutRequesterToWorkerStart = new();
        DiagnosticHistogram withoutRequesterToCompletion = new();

        foreach (var demandState in _loadDemandStates.Values)
        {
            var snapshot = demandState.CreateSnapshot();
            linkedRequests += snapshot.LinkedRequests;
            if (snapshot.MaximumLiveRequesters > 1)
            {
                loadsWithMultipleRequesters++;
            }

            maximumLiveRequesters = Math.Max(maximumLiveRequesters, snapshot.MaximumLiveRequesters);
            if (snapshot.LiveRequesters > 0)
            {
                loadsWithLiveRequesters++;
                liveRequesters += snapshot.LiveRequesters;
            }

            lostBeforeEnqueue += snapshot.LostLastRequesterBeforeEnqueue;
            lostWhileQueued += snapshot.LostLastRequesterWhileQueued;
            lostWhileWorkerActive += snapshot.LostLastRequesterWhileWorkerActive;
            lostWhileAwaitingSharedLoad += snapshot.LostLastRequesterWhileAwaitingSharedLoad;
            if (snapshot.DemandReturnedBeforeCompletion)
            {
                loadsWhereDemandReturned++;
            }

            if (snapshot.WorkerStartedWithoutLiveRequester)
            {
                workersStartedWithoutRequester++;
                withoutRequesterToWorkerStart.Record(snapshot.WithoutRequesterToWorkerStartTicks);
            }

            if (snapshot.CompletedWithoutLiveRequester)
            {
                loadsCompletedWithoutRequester++;
                unrequestedCompletionsByInputKind[(int)snapshot.InputKind]++;
                if (snapshot.ResultKind is { } resultKind)
                {
                    unrequestedCompletionsByResultKind[(int)resultKind]++;
                }

                withoutRequesterToCompletion.Record(snapshot.WithoutRequesterToCompletionTicks);
            }

            if (snapshot.CacheHitsAfterUnrequestedCompletion > 0)
            {
                retainedLoadsLaterCacheHit++;
                retainedResultCacheHits += snapshot.CacheHitsAfterUnrequestedCompletion;
            }
        }

        AppendValue(builder, "Requests linked to session loads", linkedRequests);
        AppendValue(builder, "Loads with multiple simultaneous requesters", loadsWithMultipleRequesters);
        AppendValue(builder, "Maximum simultaneous requesters per load", maximumLiveRequesters);
        AppendValue(builder, "Loads with live requesters at stop", loadsWithLiveRequesters);
        AppendValue(builder, "Live requesters at stop", liveRequesters);
        AppendDemandQueueMeasurements(builder);
        builder.AppendLine("  Invalidated requests by load stage");
        AppendEnumCounts<IconLoadDemandStage>(builder, _invalidatedRequestLoadStages, "    ");
        builder.AppendLine("  Demand-loss events after the last requester was invalidated");
        AppendValue(builder, "Before enqueue", lostBeforeEnqueue, "    ");
        AppendValue(builder, "Queued", lostWhileQueued, "    ");
        AppendValue(builder, "Worker active", lostWhileWorkerActive, "    ");
        AppendValue(builder, "Awaiting shared load", lostWhileAwaitingSharedLoad, "    ");
        AppendValue(builder, "Loads where demand returned before completion", loadsWhereDemandReturned);
        AppendValue(builder, "Workers started with no live requester", workersStartedWithoutRequester);
        AppendValue(builder, "Loads completed with no live requester", loadsCompletedWithoutRequester);
        builder.AppendLine("  Loads completed with no live requester by input kind");
        AppendEnumCounts<IconLoadInputKind>(builder, unrequestedCompletionsByInputKind, "    ");
        builder.AppendLine("  Loads completed with no live requester by result kind");
        AppendEnumCounts<IconLoadResultKind>(builder, unrequestedCompletionsByResultKind, "    ");
        AppendValue(builder, "Completed-without-requester loads later cache-hit", retainedLoadsLaterCacheHit);
        AppendValue(builder, "Later cache-hit requests", retainedResultCacheHits);
        withoutRequesterToWorkerStart.Append(builder, "No-requester time before worker start");
        withoutRequesterToCompletion.Append(builder, "No-requester time before load completion");
        builder.AppendLine("  Scope: UI IconBox requests and IconLoader work only. Installed Apps icon extraction enters this pipeline as SpecializedAppIcon work. Other extension-side icon-data preloading before this pipeline is not classified as unused work.");
    }

    private void AppendDemandQueueMeasurements(StringBuilder builder)
    {
        long currentDemandedQueueDepth;
        long currentSpeculativeQueueDepth;
        long maximumDemandedQueueDepth;
        long maximumSpeculativeQueueDepth;
        long queuedDemandDemotions;
        long queuedDemandPromotions;
        long demandedWorkerStarts;
        long speculativeWorkerStarts;
        long speculativeStartsWithDemandedLoadsQueued;
        long capacityInterferingSpeculativeStarts;
        long demandedLoadsBeyondCapacityAtSpeculativeStarts;
        long maximumDemandedLoadsBeyondCapacityAtSpeculativeStart;
        long currentActiveDemandedWorkers;
        long currentActiveSpeculativeWorkers;
        long maximumActiveSpeculativeWorkers;
        long demandedQueueArrivals;
        long demandedArrivalsWithActiveSpeculativeWorkers;
        long speculativeWorkerOccupancyAtDemandArrivals;
        long maximumSpeculativeWorkersAtDemandArrival;
        long demandedArrivalsDirectlyBlockedBySpeculativeCapacity;
        long[] capacityInterferingSpeculativeStartsByInputKind;
        long[] speculativeWorkerOccupancyAtDemandArrivalsByInputKind;
        long[] directlyBlockedDemandArrivalsByInputKind;

        lock (_queueDemandLock)
        {
            currentDemandedQueueDepth = _currentDemandedQueueDepth;
            currentSpeculativeQueueDepth = _currentSpeculativeQueueDepth;
            maximumDemandedQueueDepth = _maximumDemandedQueueDepth;
            maximumSpeculativeQueueDepth = _maximumSpeculativeQueueDepth;
            queuedDemandDemotions = _queuedDemandDemotions;
            queuedDemandPromotions = _queuedDemandPromotions;
            demandedWorkerStarts = _demandedWorkerStarts;
            speculativeWorkerStarts = _speculativeWorkerStarts;
            speculativeStartsWithDemandedLoadsQueued = _speculativeStartsWithDemandedLoadsQueued;
            capacityInterferingSpeculativeStarts = _capacityInterferingSpeculativeStarts;
            demandedLoadsBeyondCapacityAtSpeculativeStarts = _demandedLoadsBeyondCapacityAtSpeculativeStarts;
            maximumDemandedLoadsBeyondCapacityAtSpeculativeStart = _maximumDemandedLoadsBeyondCapacityAtSpeculativeStart;
            currentActiveDemandedWorkers = _currentActiveDemandedWorkers;
            currentActiveSpeculativeWorkers = _currentActiveSpeculativeWorkers;
            maximumActiveSpeculativeWorkers = _maximumActiveSpeculativeWorkers;
            demandedQueueArrivals = _demandedQueueArrivals;
            demandedArrivalsWithActiveSpeculativeWorkers = _demandedArrivalsWithActiveSpeculativeWorkers;
            speculativeWorkerOccupancyAtDemandArrivals = _speculativeWorkerOccupancyAtDemandArrivals;
            maximumSpeculativeWorkersAtDemandArrival = _maximumSpeculativeWorkersAtDemandArrival;
            demandedArrivalsDirectlyBlockedBySpeculativeCapacity = _demandedArrivalsDirectlyBlockedBySpeculativeCapacity;
            capacityInterferingSpeculativeStartsByInputKind = [.. _capacityInterferingSpeculativeStartsByInputKind];
            speculativeWorkerOccupancyAtDemandArrivalsByInputKind = [.. _speculativeWorkerOccupancyAtDemandArrivalsByInputKind];
            directlyBlockedDemandArrivalsByInputKind = [.. _directlyBlockedDemandArrivalsByInputKind];
        }

        builder.AppendLine("  Demand-aware queue view");
        builder.AppendLine("    Definition: demanded means at least one live IconBox request; speculative means none. Queue scheduling prefers demanded work; worker count is unchanged.");
        AppendValue(builder, "Demanded loads queued at stop", currentDemandedQueueDepth, "    ");
        AppendValue(builder, "Speculative loads queued at stop", currentSpeculativeQueueDepth, "    ");
        AppendValue(builder, "Maximum demanded queue depth", maximumDemandedQueueDepth, "    ");
        AppendValue(builder, "Maximum speculative queue depth", maximumSpeculativeQueueDepth, "    ");
        AppendValue(builder, "Queued demotions after demand loss", queuedDemandDemotions, "    ");
        AppendValue(builder, "Queued promotions after demand returned", queuedDemandPromotions, "    ");
        AppendValue(builder, "Workers started demanded", demandedWorkerStarts, "    ");
        AppendValue(builder, "Workers started speculative", speculativeWorkerStarts, "    ");
        AppendValue(builder, "Active demanded workers at stop", currentActiveDemandedWorkers, "    ");
        AppendValue(builder, "Active speculative workers at stop", currentActiveSpeculativeWorkers, "    ");
        AppendValue(builder, "Maximum active speculative workers", maximumActiveSpeculativeWorkers, "    ");
        AppendValue(builder, "Speculative starts with demanded loads queued", speculativeStartsWithDemandedLoadsQueued, "    ");
        AppendValue(builder, "Speculative starts leaving demanded loads beyond remaining worker capacity", capacityInterferingSpeculativeStarts, "    ");
        AppendValue(builder, "Demanded loads beyond remaining capacity across those starts", demandedLoadsBeyondCapacityAtSpeculativeStarts, "    ");
        AppendValue(builder, "Maximum demanded loads beyond remaining capacity at one start", maximumDemandedLoadsBeyondCapacityAtSpeculativeStart, "    ");
        builder.AppendLine("    Capacity-interfering speculative starts by input kind");
        AppendEnumCounts<IconLoadInputKind>(builder, capacityInterferingSpeculativeStartsByInputKind, "      ");
        builder.AppendLine("    Demanded arrivals and active speculative capacity");
        builder.AppendLine("      Directly blocked means the arriving load's queue position could use capacity occupied by speculative workers and would fit if those workers were absent.");
        AppendValue(builder, "Demanded queue arrivals", demandedQueueArrivals, "      ");
        AppendValue(builder, "Arrivals with active speculative workers", demandedArrivalsWithActiveSpeculativeWorkers, "      ");
        AppendValue(builder, "Sum of active speculative workers observed at those arrivals", speculativeWorkerOccupancyAtDemandArrivals, "      ");
        AppendValue(builder, "Maximum speculative workers active at one demanded arrival", maximumSpeculativeWorkersAtDemandArrival, "      ");
        AppendValue(builder, "Arrivals directly blocked by speculative worker capacity", demandedArrivalsDirectlyBlockedBySpeculativeCapacity, "      ");
        builder.AppendLine("      Speculative worker occupancy observed at demanded arrivals by speculative input kind");
        AppendEnumCounts<IconLoadInputKind>(builder, speculativeWorkerOccupancyAtDemandArrivalsByInputKind, "        ");
        builder.AppendLine("      Directly blocked demanded arrivals by demanded input kind");
        AppendEnumCounts<IconLoadInputKind>(builder, directlyBlockedDemandArrivalsByInputKind, "        ");
        builder.AppendLine("      Timing samples include affected arrivals that were still demanded when their worker started.");
        _demandArrivalToWorkerStartWithActiveSpeculative.Append(
            builder,
            "Demand arrival to worker start with speculative workers active",
            "      ");
        _directlyBlockedDemandArrivalToWorkerStart.Append(
            builder,
            "Directly blocked demand arrival to worker start",
            "      ");
        _demandedQueueLatency.Append(builder, "Demanded queue wait", "    ");
        _speculativeQueueLatency.Append(builder, "Speculative queue wait", "    ");
    }

    private void AppendInputKindMeasurements(StringBuilder builder)
    {
        var values = Enum.GetValues<IconLoadInputKind>();
        for (var i = 0; i < values.Length; i++)
        {
            var count = Volatile.Read(ref _inputKinds[i]);
            builder.Append("  ").Append(values[i]).Append(": ").AppendLine(count.ToString(CultureInfo.InvariantCulture));
            if (count == 0)
            {
                continue;
            }

            var measurements = _inputKindMeasurements[i];
            measurements.LoadLatency.Append(builder, "Enqueue to completion", "    ");
            measurements.DirectGlyphLatency.Append(builder, "Direct glyph construction", "    ");
            measurements.QueueLatency.Append(builder, "Queue wait", "    ");
            measurements.DemandedQueueLatency.Append(builder, "Demanded queue wait", "    ");
            measurements.SpeculativeQueueLatency.Append(builder, "Speculative queue wait", "    ");
            measurements.BackgroundPreparationLatency.Append(builder, "Background preparation", "    ");
            measurements.DispatcherWaitLatency.Append(builder, "Dispatcher wait", "    ");
            measurements.DispatcherWorkLatency.Append(builder, "Dispatcher callback wall time", "    ");
            measurements.DispatcherUiExecutionLatency.Append(builder, "Measured STA execution slices", "    ");
            measurements.DispatcherAsyncSuspensionLatency.Append(builder, "Asynchronous materialization suspension", "    ");
        }
    }

    private void AppendDirectGlyphResultMeasurements(StringBuilder builder)
    {
        var resultKinds = Enum.GetValues<IconLoadResultKind>();
        var wroteHeader = false;
        for (var i = 0; i < resultKinds.Length; i++)
        {
            var measurement = _directGlyphLatencyByResultKind[i];
            if (measurement.Count == 0)
            {
                continue;
            }

            if (!wroteHeader)
            {
                builder.AppendLine("  Direct glyph construction by result kind");
                wroteHeader = true;
            }

            measurement.Append(builder, resultKinds[i].ToString(), "    ");
        }
    }

    private void AppendRequestMeasurements(StringBuilder builder)
    {
        builder.AppendLine("  Request to completion by resolution and result");
        var resolutions = Enum.GetValues<IconProviderResolution>();
        var resultKinds = Enum.GetValues<IconLoadResultKind>();
        var attributedRequests = 0L;

        for (var resolutionIndex = 0; resolutionIndex < resolutions.Length; resolutionIndex++)
        {
            builder.Append("    ").AppendLine(resolutions[resolutionIndex].ToString());
            var hasSamples = false;
            for (var resultIndex = 0; resultIndex < resultKinds.Length; resultIndex++)
            {
                var histogram = _requestLatencyByResolutionAndResult[resolutionIndex][resultIndex];
                var count = histogram.Count;
                if (count == 0)
                {
                    continue;
                }

                hasSamples = true;
                attributedRequests += count;
                histogram.Append(builder, resultKinds[resultIndex].ToString(), "      ");
            }

            if (!hasSamples)
            {
                builder.AppendLine("      no completed requests");
            }
        }

        var unattributedRequests = Math.Max(0, Sum(_requestStatuses) - attributedRequests);
        if (unattributedRequests > 0)
        {
            builder.Append("    Unattributed completed requests: ")
                .AppendLine(unattributedRequests.ToString(CultureInfo.InvariantCulture));
        }

        builder.AppendLine("  Applied request to completion by provider resolution");
        AppendResolutionMeasurements(builder, _appliedRequestLatencyByResolution, "    ");
    }

    private void AppendRequestOriginMeasurements(StringBuilder builder)
    {
        var origins = _requestOriginMeasurements.ToArray();
        Array.Sort(
            origins,
            static (left, right) =>
            {
                var siteComparison = left.Key.RequestSite.CompareTo(right.Key.RequestSite);
                return siteComparison != 0
                    ? siteComparison
                    : StringComparer.Ordinal.Compare(left.Key.DiagnosticScope, right.Key.DiagnosticScope);
            });

        if (origins.Length == 0)
        {
            builder.AppendLine("  no requests");
            return;
        }

        foreach (var origin in origins)
        {
            builder.Append("  ").Append(origin.Key.RequestSite);
            if (!string.IsNullOrEmpty(origin.Key.DiagnosticScope))
            {
                builder.Append(" / ").Append(origin.Key.DiagnosticScope);
            }

            builder.AppendLine();
            origin.Value.Append(builder);
        }

        builder.AppendLine("  Individual process-local IconBox IDs are available in RequestOrigin ETW events.");
    }

    private void AppendElementKindMeasurements(StringBuilder builder)
    {
        var resultKinds = Enum.GetValues<IconLoadResultKind>();
        var wroteHeader = false;
        for (var i = 0; i < resultKinds.Length; i++)
        {
            var measurements = _elementKindMeasurements[i];
            var created = measurements.Created;
            var reused = measurements.Reused;
            if (created + reused == 0)
            {
                continue;
            }

            if (!wroteHeader)
            {
                builder.AppendLine("  By source kind");
                wroteHeader = true;
            }

            builder.Append("    ").Append(resultKinds[i])
                .Append(": created=").Append(created.ToString(CultureInfo.InvariantCulture))
                .Append(", reused=").AppendLine(reused.ToString(CultureInfo.InvariantCulture));
            measurements.UpdateLatency.Append(builder, "Update wall time", "      ");
        }
    }

    private static InputKindMeasurements[] CreateInputKindMeasurements()
    {
        var measurements = new InputKindMeasurements[Enum.GetValues<IconLoadInputKind>().Length];
        for (var i = 0; i < measurements.Length; i++)
        {
            measurements[i] = new InputKindMeasurements();
        }

        return measurements;
    }

    private static DiagnosticHistogram[] CreateDemandMeasurements()
    {
        return [new DiagnosticHistogram(), new DiagnosticHistogram()];
    }

    private static DiagnosticHistogram[] CreateDispatcherUiSliceMeasurements()
    {
        var measurements = new DiagnosticHistogram[Enum.GetValues<IconDispatcherUiSliceKind>().Length];
        for (var i = 0; i < measurements.Length; i++)
        {
            measurements[i] = new DiagnosticHistogram();
        }

        return measurements;
    }

    private static DispatcherMaterializationMeasurements[] CreateDispatcherMaterializationMeasurements()
    {
        var measurements = new DispatcherMaterializationMeasurements[Enum.GetValues<IconDispatcherMaterializationKind>().Length];
        for (var i = 0; i < measurements.Length; i++)
        {
            measurements[i] = new DispatcherMaterializationMeasurements();
        }

        return measurements;
    }

    private static DiagnosticHistogram[][] CreateRequestMeasurements()
    {
        var measurements = new DiagnosticHistogram[Enum.GetValues<IconProviderResolution>().Length][];
        var resultKindCount = Enum.GetValues<IconLoadResultKind>().Length;
        for (var resolutionIndex = 0; resolutionIndex < measurements.Length; resolutionIndex++)
        {
            measurements[resolutionIndex] = new DiagnosticHistogram[resultKindCount];
            for (var resultIndex = 0; resultIndex < resultKindCount; resultIndex++)
            {
                measurements[resolutionIndex][resultIndex] = new DiagnosticHistogram();
            }
        }

        return measurements;
    }

    private static DiagnosticHistogram[] CreateResultMeasurements()
    {
        var measurements = new DiagnosticHistogram[Enum.GetValues<IconLoadResultKind>().Length];
        for (var i = 0; i < measurements.Length; i++)
        {
            measurements[i] = new DiagnosticHistogram();
        }

        return measurements;
    }

    private static DiagnosticHistogram[] CreateResolutionMeasurements()
    {
        var measurements = new DiagnosticHistogram[Enum.GetValues<IconProviderResolution>().Length];
        for (var i = 0; i < measurements.Length; i++)
        {
            measurements[i] = new DiagnosticHistogram();
        }

        return measurements;
    }

    private static DiagnosticHistogram[] CreateSchedulerCommandMeasurements()
    {
        var measurements = new DiagnosticHistogram[Enum.GetValues<IconLoadQueue.QueueCommandKind>().Length];
        for (var i = 0; i < measurements.Length; i++)
        {
            measurements[i] = new DiagnosticHistogram();
        }

        return measurements;
    }

    private static ElementKindMeasurements[] CreateElementKindMeasurements()
    {
        var measurements = new ElementKindMeasurements[Enum.GetValues<IconLoadResultKind>().Length];
        for (var i = 0; i < measurements.Length; i++)
        {
            measurements[i] = new ElementKindMeasurements();
        }

        return measurements;
    }

    private static void AppendEnumCounts<TEnum>(StringBuilder builder, long[] counts, string indentation = "  ")
        where TEnum : struct, Enum
    {
        var values = Enum.GetValues<TEnum>();
        for (var i = 0; i < values.Length; i++)
        {
            AppendValue(builder, values[i].ToString(), Volatile.Read(ref counts[i]), indentation);
        }
    }

    private static void AppendResolutionMeasurements(
        StringBuilder builder,
        DiagnosticHistogram[] measurements,
        string indentation)
    {
        var resolutions = Enum.GetValues<IconProviderResolution>();
        for (var i = 0; i < resolutions.Length; i++)
        {
            measurements[i].Append(builder, resolutions[i].ToString(), indentation);
        }
    }

    private static void AppendValue(StringBuilder builder, string name, long value, string indentation = "  ")
    {
        builder.Append(indentation).Append(name).Append(": ").AppendLine(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendAverage(
        StringBuilder builder,
        string name,
        long total,
        long count,
        string indentation)
    {
        builder.Append(indentation).Append(name).Append(": ");
        if (count == 0)
        {
            builder.AppendLine("n/a");
            return;
        }

        builder.AppendLine((total / (double)count).ToString("0.###", CultureInfo.InvariantCulture));
    }

    private static long Sum(long[] values)
    {
        var total = 0L;
        for (var i = 0; i < values.Length; i++)
        {
            total += Volatile.Read(ref values[i]);
        }

        return total;
    }

    private static long[] SnapshotCounts(long[] values)
    {
        var snapshot = new long[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            snapshot[i] = Volatile.Read(ref values[i]);
        }

        return snapshot;
    }

    private static void UpdateMaximum(ref long maximum, long value)
    {
        var current = Volatile.Read(ref maximum);
        while (value > current)
        {
            var previous = Interlocked.CompareExchange(ref maximum, value, current);
            if (previous == current)
            {
                return;
            }

            current = previous;
        }
    }

    private void RecordDispatcherOutlier(
        long loadId,
        IconLoadInputKind inputKind,
        IconDispatcherMaterializationKind materializationKind,
        DispatcherOutlierPhase phase,
        bool isDemanded,
        long startedAt,
        long elapsedTicks)
    {
        if (elapsedTicks < Stopwatch.Frequency * 16L / 1000L)
        {
            return;
        }

        _dispatcherOutliers.Enqueue(new DispatcherOutlierSample(
            loadId,
            inputKind,
            materializationKind,
            phase,
            isDemanded,
            startedAt,
            elapsedTicks));
    }

    private static int DemandIndex(bool isDemanded) => isDemanded ? 1 : 0;

    private static void IncrementDemandCount(bool isDemanded, ref long demanded, ref long speculative)
    {
        if (isDemanded)
        {
            Interlocked.Increment(ref demanded);
        }
        else
        {
            Interlocked.Increment(ref speculative);
        }
    }

    private static long GetProcessCpuTicks()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.TotalProcessorTime.Ticks;
        }
        catch
        {
            return -1;
        }
    }

    private static long GetWorkingSetBytes()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.WorkingSet64;
        }
        catch
        {
            return -1;
        }
    }

    private static string FormatMebibytes(long bytes) =>
        (bytes / (1024D * 1024D)).ToString("0.###", CultureInfo.InvariantCulture);

    private static string FormatSignedMebibytes(long bytes) =>
        (bytes / (1024D * 1024D)).ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture);

    private static long ToMicroseconds(long ticks) => (long)(ticks * 1_000_000D / Stopwatch.Frequency);

    private static string FormatMilliseconds(long ticks) => (ticks * 1000D / Stopwatch.Frequency).ToString("0.###", CultureInfo.InvariantCulture);

    private enum DispatcherOutlierPhase
    {
        QueueWait,
        QueueWaitFailed,
        UiEntry,
        AsyncSuspension,
        UiContinuation,
        CallbackWindow,
    }

    private readonly record struct DispatcherOutlierSample(
        long LoadId,
        IconLoadInputKind InputKind,
        IconDispatcherMaterializationKind MaterializationKind,
        DispatcherOutlierPhase Phase,
        bool IsDemanded,
        long StartedAt,
        long ElapsedTicks);

    private readonly record struct CacheDescriptor(
        int Width,
        int Height,
        IconCachePartition Partition,
        int Capacity);

    private readonly record struct CacheMeasurementsSnapshot(
        long Hits,
        long Misses,
        long FirstObservedCount,
        long LastObservedCount,
        long MaximumObservedCount,
        long EntriesAdded,
        long EntriesRemoved,
        long[] RemovalsByReason);

    private sealed class CacheMeasurements
    {
        private readonly long[] _removalsByReason = new long[Enum.GetValues<AdaptiveCacheRemovalReason>().Length];
        private long _hits;
        private long _misses;
        private long _firstObservedCount = -1;
        private long _lastObservedCount;
        private long _maximumObservedCount;
        private long _entriesAdded;
        private long _entriesRemoved;

        public void RecordLookup(bool hit)
        {
            if (hit)
            {
                Interlocked.Increment(ref _hits);
            }
            else
            {
                Interlocked.Increment(ref _misses);
            }
        }

        public void RecordAdded(int entryCount)
        {
            RecordObservation(entryCount);
            Interlocked.Increment(ref _entriesAdded);
        }

        public void RecordRemoved(int entryCount, AdaptiveCacheRemovalReason reason)
        {
            RecordObservation(entryCount);
            Interlocked.Increment(ref _entriesRemoved);
            Interlocked.Increment(ref _removalsByReason[(int)reason]);
        }

        public CacheMeasurementsSnapshot CreateSnapshot()
        {
            return new CacheMeasurementsSnapshot(
                Volatile.Read(ref _hits),
                Volatile.Read(ref _misses),
                Math.Max(0, Volatile.Read(ref _firstObservedCount)),
                Math.Max(0, Volatile.Read(ref _lastObservedCount)),
                Math.Max(0, Volatile.Read(ref _maximumObservedCount)),
                Volatile.Read(ref _entriesAdded),
                Volatile.Read(ref _entriesRemoved),
                SnapshotCounts(_removalsByReason));
        }

        private void RecordObservation(int entryCount)
        {
            var normalizedCount = Math.Max(0, entryCount);
            Interlocked.CompareExchange(ref _firstObservedCount, normalizedCount, -1);
            Interlocked.Exchange(ref _lastObservedCount, normalizedCount);
            UpdateMaximum(ref _maximumObservedCount, normalizedCount);
        }
    }

    private readonly record struct LoadResolutionResult(
        bool TracksLiveRequester,
        bool RetainedResultCacheHit,
        int CacheHitsAfterCompletion,
        IconLoadDemandStage Stage,
        int RemainingLiveRequesters);

    private readonly record struct LoadRequestInvalidationResult(
        IconLoadDemandStage Stage,
        int RemainingLiveRequesters);

    private readonly record struct LoadWorkerDemandResult(
        bool StartedWithoutLiveRequester,
        long WithoutRequesterElapsedTicks);

    private readonly record struct DemandedQueueArrival(
        long ArrivedAt,
        long SpeculativeWorkersAtArrival,
        bool DirectlyBlockedBySpeculativeCapacity);

    private readonly record struct LoadCompletionDemandResult(
        bool CompletedWithoutLiveRequester,
        long WithoutRequesterElapsedTicks);

    private readonly record struct LoadDemandSnapshot(
        IconLoadInputKind InputKind,
        IconLoadResultKind? ResultKind,
        int LinkedRequests,
        int MaximumLiveRequesters,
        int LiveRequesters,
        int LostLastRequesterBeforeEnqueue,
        int LostLastRequesterWhileQueued,
        int LostLastRequesterWhileWorkerActive,
        int LostLastRequesterWhileAwaitingSharedLoad,
        bool DemandReturnedBeforeCompletion,
        bool WorkerStartedWithoutLiveRequester,
        long WithoutRequesterToWorkerStartTicks,
        bool CompletedWithoutLiveRequester,
        long WithoutRequesterToCompletionTicks,
        int CacheHitsAfterUnrequestedCompletion);

    private readonly record struct RequestOriginKey(
        IconRequestSite RequestSite,
        string DiagnosticScope);

    private sealed class RequestOriginMeasurements
    {
        private readonly long[] _requestStatuses = new long[Enum.GetValues<IconRequestStatus>().Length];
        private readonly long[] _providerResolutions = new long[Enum.GetValues<IconProviderResolution>().Length];
        private readonly long[] _resultKinds = new long[Enum.GetValues<IconLoadResultKind>().Length];
        private readonly DiagnosticHistogram[] _requestLatencyByStatus = CreateStatusMeasurements();
        private readonly DiagnosticHistogram[] _appliedRequestLatencyByResolution = CreateResolutionMeasurements();
        private readonly DiagnosticHistogram _requestLatency = new();
        private readonly ConcurrentDictionary<long, byte> _iconBoxIds = new();
        private long _requestsStarted;

        public void RecordStarted(long iconBoxId)
        {
            Interlocked.Increment(ref _requestsStarted);
            if (iconBoxId > 0)
            {
                _iconBoxIds.TryAdd(iconBoxId, 0);
            }
        }

        public void RecordProviderResolution(IconProviderResolution resolution)
        {
            Interlocked.Increment(ref _providerResolutions[(int)resolution]);
        }

        public void RecordCompleted(
            IconRequestStatus status,
            IconLoadResultKind resultKind,
            IconProviderResolution? resolution,
            long elapsedTicks)
        {
            Interlocked.Increment(ref _requestStatuses[(int)status]);
            Interlocked.Increment(ref _resultKinds[(int)resultKind]);
            _requestLatency.Record(elapsedTicks);
            _requestLatencyByStatus[(int)status].Record(elapsedTicks);
            if (status == IconRequestStatus.Applied && resolution is { } appliedResolution)
            {
                _appliedRequestLatencyByResolution[(int)appliedResolution].Record(elapsedTicks);
            }
        }

        public void Append(StringBuilder builder)
        {
            var requestsStarted = Volatile.Read(ref _requestsStarted);
            AppendValue(builder, "Icon boxes", _iconBoxIds.Count, "    ");
            AppendValue(builder, "Started", requestsStarted, "    ");
            AppendEnumCounts<IconRequestStatus>(builder, _requestStatuses, "    ");
            AppendValue(builder, "Outstanding at stop", Math.Max(0, requestsStarted - Sum(_requestStatuses)), "    ");
            builder.AppendLine("    Provider resolution");
            AppendEnumCounts<IconProviderResolution>(builder, _providerResolutions, "      ");
            builder.AppendLine("    Result kinds");
            AppendNonzeroResultKinds(builder);
            _requestLatency.Append(builder, "Request to completion", "    ");
            builder.AppendLine("    Request to completion by status");

            var statuses = Enum.GetValues<IconRequestStatus>();
            var wroteStatus = false;
            for (var i = 0; i < statuses.Length; i++)
            {
                var histogram = _requestLatencyByStatus[i];
                if (histogram.Count == 0)
                {
                    continue;
                }

                wroteStatus = true;
                histogram.Append(builder, statuses[i].ToString(), "      ");
            }

            if (!wroteStatus)
            {
                builder.AppendLine("      no completed requests");
            }

            builder.AppendLine("    Applied request to completion by provider resolution");
            AppendResolutionMeasurements(builder, _appliedRequestLatencyByResolution, "      ");
        }

        private void AppendNonzeroResultKinds(StringBuilder builder)
        {
            var resultKinds = Enum.GetValues<IconLoadResultKind>();
            var wroteResult = false;
            for (var i = 0; i < resultKinds.Length; i++)
            {
                var count = Volatile.Read(ref _resultKinds[i]);
                if (count == 0)
                {
                    continue;
                }

                wroteResult = true;
                AppendValue(builder, resultKinds[i].ToString(), count, "      ");
            }

            if (!wroteResult)
            {
                builder.AppendLine("      no completed requests");
            }
        }

        private static DiagnosticHistogram[] CreateStatusMeasurements()
        {
            var measurements = new DiagnosticHistogram[Enum.GetValues<IconRequestStatus>().Length];
            for (var i = 0; i < measurements.Length; i++)
            {
                measurements[i] = new DiagnosticHistogram();
            }

            return measurements;
        }
    }

    private sealed class RequestDemandState
    {
        public RequestDemandState(RequestOriginMeasurements originMeasurements)
        {
            OriginMeasurements = originMeasurements;
        }

        public object SyncRoot { get; } = new();

        public RequestOriginMeasurements OriginMeasurements { get; }

        public IconProviderResolution? Resolution { get; set; }

        public long LoadId { get; set; }

        public bool TracksLiveRequester { get; set; }

        public bool Invalidated { get; set; }

        public long InvalidatedAt { get; set; }

        public bool InvalidationAttributed { get; set; }
    }

    private sealed class LoadDemandState
    {
        private readonly object _lock = new();
        private readonly IconLoadDiagnosticsSession _session;
        private readonly long _loadId;
        private readonly IconLoadInputKind _inputKind;
        private IconLoadDemandStage _stage = IconLoadDemandStage.BeforeEnqueue;
        private IconLoadResultKind? _resultKind;
        private int _linkedRequests;
        private int _liveRequesters;
        private int _maximumLiveRequesters;
        private int _lostLastRequesterBeforeEnqueue;
        private int _lostLastRequesterWhileQueued;
        private int _lostLastRequesterWhileWorkerActive;
        private int _lostLastRequesterWhileAwaitingSharedLoad;
        private bool _workerActive;
        private bool _activeWorkerDemanded;
        private bool _withoutRequester;
        private long _withoutRequesterAt;
        private bool _demandReturnedBeforeCompletion;
        private bool _workerStartedWithoutLiveRequester;
        private long _withoutRequesterToWorkerStartTicks = -1;
        private bool _completedWithoutLiveRequester;
        private long _withoutRequesterToCompletionTicks = -1;
        private int _cacheHitsAfterUnrequestedCompletion;
        private int _workerCount = 1;
        private DemandedQueueArrival? _pendingDemandArrival;

        public LoadDemandState(IconLoadDiagnosticsSession session, long loadId, IconLoadInputKind inputKind)
        {
            _session = session;
            _loadId = loadId;
            _inputKind = inputKind;
        }

        // Dispatcher diagnostics only need a point-in-time attribution. Keep this read lock-free
        // so recording a callback phase can never block the WinUI STA on a demand-state writer.
        public bool IsDemanded => Volatile.Read(ref _liveRequesters) > 0;

        public LoadResolutionResult RecordResolution(
            IconProviderResolution resolution,
            bool requesterInvalidated,
            long invalidatedAt)
        {
            lock (_lock)
            {
                _linkedRequests++;
                var wasDemanded = _liveRequesters > 0;

                var retainedResultCacheHit = false;
                if (resolution == IconProviderResolution.CacheHit && _completedWithoutLiveRequester)
                {
                    _cacheHitsAfterUnrequestedCompletion++;
                    retainedResultCacheHit = true;
                }

                var tracksLiveRequester = false;
                if (resolution is IconProviderResolution.NewLoad or IconProviderResolution.InFlight
                    && _stage is not IconLoadDemandStage.Completed
                        and not IconLoadDemandStage.Rejected
                        and not IconLoadDemandStage.Abandoned)
                {
                    if (requesterInvalidated)
                    {
                        BeginWithoutRequester(invalidatedAt);
                    }
                    else
                    {
                        if (_liveRequesters == 0 && _withoutRequester)
                        {
                            _withoutRequester = false;
                            _withoutRequesterAt = 0;
                            _demandReturnedBeforeCompletion = true;
                        }

                        _liveRequesters++;
                        _maximumLiveRequesters = Math.Max(_maximumLiveRequesters, _liveRequesters);
                        tracksLiveRequester = true;
                    }
                }

                if (!wasDemanded && _liveRequesters > 0)
                {
                    if (_stage == IconLoadDemandStage.Queued)
                    {
                        _pendingDemandArrival = _session.RecordQueuedDemandTransition(
                            _loadId,
                            _inputKind,
                            becameDemanded: true,
                            _workerCount);
                    }
                    else if (_stage == IconLoadDemandStage.WorkerActive && _workerActive)
                    {
                        _session.RecordActiveWorkerDemandTransition(_inputKind, becameDemanded: true);
                        _activeWorkerDemanded = true;
                    }
                }

                return new LoadResolutionResult(
                    tracksLiveRequester,
                    retainedResultCacheHit,
                    _cacheHitsAfterUnrequestedCompletion,
                    _stage,
                    _liveRequesters);
            }
        }

        public LoadRequestInvalidationResult InvalidateRequest(bool tracksLiveRequester, long invalidatedAt)
        {
            lock (_lock)
            {
                var wasDemanded = _liveRequesters > 0;
                if (tracksLiveRequester && _liveRequesters > 0)
                {
                    _liveRequesters--;
                }

                if (tracksLiveRequester)
                {
                    BeginWithoutRequester(invalidatedAt);
                }

                if (wasDemanded && _liveRequesters == 0)
                {
                    if (_stage == IconLoadDemandStage.Queued)
                    {
                        _pendingDemandArrival = null;
                        _session.RecordQueuedDemandTransition(
                            _loadId,
                            _inputKind,
                            becameDemanded: false,
                            _workerCount);
                    }
                    else if (_stage == IconLoadDemandStage.WorkerActive && _workerActive)
                    {
                        _session.RecordActiveWorkerDemandTransition(_inputKind, becameDemanded: false);
                        _activeWorkerDemanded = false;
                    }
                }

                return new LoadRequestInvalidationResult(_stage, _liveRequesters);
            }
        }

        public void CompleteRequest()
        {
            lock (_lock)
            {
                var wasDemanded = _liveRequesters > 0;
                if (_liveRequesters > 0)
                {
                    _liveRequesters--;
                }

                if (wasDemanded
                    && _liveRequesters == 0
                    && _stage == IconLoadDemandStage.WorkerActive
                    && _workerActive)
                {
                    _session.RecordActiveWorkerDemandTransition(_inputKind, becameDemanded: false);
                    _activeWorkerDemanded = false;
                }
            }
        }

        private void BeginWithoutRequester(long invalidatedAt)
        {
            if (_liveRequesters != 0
                || _withoutRequester
                || _stage is IconLoadDemandStage.Completed
                    or IconLoadDemandStage.Rejected
                    or IconLoadDemandStage.Abandoned)
            {
                return;
            }

            _withoutRequester = true;
            _withoutRequesterAt = invalidatedAt;
            switch (_stage)
            {
                case IconLoadDemandStage.BeforeEnqueue:
                    _lostLastRequesterBeforeEnqueue++;
                    break;
                case IconLoadDemandStage.Queued:
                    _lostLastRequesterWhileQueued++;
                    break;
                case IconLoadDemandStage.WorkerActive:
                    _lostLastRequesterWhileWorkerActive++;
                    break;
                case IconLoadDemandStage.AwaitingSharedLoad:
                    _lostLastRequesterWhileAwaitingSharedLoad++;
                    break;
            }
        }

        public void MarkEnqueued(int workerCount)
        {
            lock (_lock)
            {
                if (_stage == IconLoadDemandStage.BeforeEnqueue)
                {
                    _workerCount = Math.Max(1, workerCount);
                    _stage = IconLoadDemandStage.Queued;
                    _pendingDemandArrival = _session.RecordDemandQueueEnqueued(
                        _loadId,
                        _inputKind,
                        _liveRequesters > 0,
                        _workerCount);
                }
            }
        }

        public void MarkRejected()
        {
            lock (_lock)
            {
                Debug.Assert(_stage == IconLoadDemandStage.BeforeEnqueue, "Only a load that was never queued can be rejected.");
                if (_stage == IconLoadDemandStage.BeforeEnqueue)
                {
                    _stage = IconLoadDemandStage.Rejected;
                }
            }
        }

        public void MarkAbandoned()
        {
            lock (_lock)
            {
                Debug.Assert(_stage == IconLoadDemandStage.Queued, "Only accepted queued work can be abandoned.");
                if (_stage == IconLoadDemandStage.Queued)
                {
                    _session.RecordDemandQueueAbandoned(_liveRequesters > 0);
                    _pendingDemandArrival = null;
                    _stage = IconLoadDemandStage.Abandoned;
                }
            }
        }

        public LoadWorkerDemandResult MarkWorkerStarted(
            long startedAt,
            long queueTicks,
            long activeWorkers,
            int workerCount)
        {
            lock (_lock)
            {
                if (_stage == IconLoadDemandStage.Queued)
                {
                    _session.RecordDemandWorkerStarted(
                        _loadId,
                        _inputKind,
                        _liveRequesters > 0,
                        startedAt,
                        queueTicks,
                        activeWorkers,
                        workerCount,
                        _pendingDemandArrival);
                    _pendingDemandArrival = null;
                }

                if (_stage is not IconLoadDemandStage.Completed
                    and not IconLoadDemandStage.Rejected
                    and not IconLoadDemandStage.Abandoned)
                {
                    _stage = IconLoadDemandStage.WorkerActive;
                    _workerActive = true;
                    _activeWorkerDemanded = _liveRequesters > 0;
                }

                if (_withoutRequester && _liveRequesters == 0)
                {
                    _workerStartedWithoutLiveRequester = true;
                    _withoutRequesterToWorkerStartTicks = Math.Max(0, startedAt - _withoutRequesterAt);
                }

                return new LoadWorkerDemandResult(
                    _workerStartedWithoutLiveRequester,
                    _withoutRequesterToWorkerStartTicks);
            }
        }

        public void MarkWorkerReleased()
        {
            lock (_lock)
            {
                if (!_workerActive)
                {
                    return;
                }

                _session.RecordActiveWorkerCompleted(_inputKind, _activeWorkerDemanded);
                _workerActive = false;
                if (_stage == IconLoadDemandStage.WorkerActive)
                {
                    _stage = IconLoadDemandStage.AwaitingSharedLoad;
                }
            }
        }

        public LoadCompletionDemandResult MarkCompleted(long completedAt, IconLoadResultKind resultKind)
        {
            lock (_lock)
            {
                _stage = IconLoadDemandStage.Completed;
                _resultKind = resultKind;
                if (_withoutRequester && _liveRequesters == 0)
                {
                    _completedWithoutLiveRequester = true;
                    _withoutRequesterToCompletionTicks = Math.Max(0, completedAt - _withoutRequesterAt);
                }

                return new LoadCompletionDemandResult(
                    _completedWithoutLiveRequester,
                    _withoutRequesterToCompletionTicks);
            }
        }

        public LoadDemandSnapshot CreateSnapshot()
        {
            lock (_lock)
            {
                return new LoadDemandSnapshot(
                    _inputKind,
                    _resultKind,
                    _linkedRequests,
                    _maximumLiveRequesters,
                    _liveRequesters,
                    _lostLastRequesterBeforeEnqueue,
                    _lostLastRequesterWhileQueued,
                    _lostLastRequesterWhileWorkerActive,
                    _lostLastRequesterWhileAwaitingSharedLoad,
                    _demandReturnedBeforeCompletion,
                    _workerStartedWithoutLiveRequester,
                    _withoutRequesterToWorkerStartTicks,
                    _completedWithoutLiveRequester,
                    _withoutRequesterToCompletionTicks,
                    _cacheHitsAfterUnrequestedCompletion);
            }
        }
    }

    private sealed class InputKindMeasurements
    {
        public DiagnosticHistogram LoadLatency { get; } = new();

        public DiagnosticHistogram DirectGlyphLatency { get; } = new();

        public DiagnosticHistogram QueueLatency { get; } = new();

        public DiagnosticHistogram DemandedQueueLatency { get; } = new();

        public DiagnosticHistogram SpeculativeQueueLatency { get; } = new();

        public DiagnosticHistogram BackgroundPreparationLatency { get; } = new();

        public DiagnosticHistogram DispatcherWaitLatency { get; } = new();

        public DiagnosticHistogram DispatcherWorkLatency { get; } = new();

        public DiagnosticHistogram DispatcherUiExecutionLatency { get; } = new();

        public DiagnosticHistogram DispatcherAsyncSuspensionLatency { get; } = new();
    }

    private sealed class DispatcherMaterializationMeasurements
    {
        private long _enqueuedDemanded;
        private long _enqueuedSpeculative;
        private long _startedDemanded;
        private long _startedSpeculative;
        private long _completedDemanded;
        private long _completedSpeculative;
        private long _waitFailures;

        public long EnqueuedDemanded => Volatile.Read(ref _enqueuedDemanded);

        public long EnqueuedSpeculative => Volatile.Read(ref _enqueuedSpeculative);

        public long StartedDemanded => Volatile.Read(ref _startedDemanded);

        public long StartedSpeculative => Volatile.Read(ref _startedSpeculative);

        public long CompletedDemanded => Volatile.Read(ref _completedDemanded);

        public long CompletedSpeculative => Volatile.Read(ref _completedSpeculative);

        public long WaitFailures => Volatile.Read(ref _waitFailures);

        public DiagnosticHistogram DispatcherWaitLatency { get; } = new();

        public DiagnosticHistogram CallbackWallLatency { get; } = new();

        public DiagnosticHistogram UiExecutionLatency { get; } = new();

        public DiagnosticHistogram AsyncSuspensionLatency { get; } = new();

        private DiagnosticHistogram[] DispatcherWaitLatencyByDemand { get; } = CreateDemandMeasurements();

        private DiagnosticHistogram[] CallbackWallLatencyByDemand { get; } = CreateDemandMeasurements();

        private DiagnosticHistogram[] UiExecutionLatencyByDemand { get; } = CreateDemandMeasurements();

        private DiagnosticHistogram[] AsyncSuspensionLatencyByDemand { get; } = CreateDemandMeasurements();

        public bool HasSamples => EnqueuedDemanded + EnqueuedSpeculative > 0;

        public void RecordEnqueued(bool isDemanded)
        {
            if (isDemanded)
            {
                Interlocked.Increment(ref _enqueuedDemanded);
            }
            else
            {
                Interlocked.Increment(ref _enqueuedSpeculative);
            }
        }

        public void RecordStarted(bool isDemanded, long elapsedTicks)
        {
            if (isDemanded)
            {
                Interlocked.Increment(ref _startedDemanded);
            }
            else
            {
                Interlocked.Increment(ref _startedSpeculative);
            }

            DispatcherWaitLatency.Record(elapsedTicks);
            DispatcherWaitLatencyByDemand[DemandIndex(isDemanded)].Record(elapsedTicks);
        }

        public void RecordWaitFailed(bool isDemanded, long elapsedTicks)
        {
            Interlocked.Increment(ref _waitFailures);
            DispatcherWaitLatency.Record(elapsedTicks);
            DispatcherWaitLatencyByDemand[DemandIndex(isDemanded)].Record(elapsedTicks);
        }

        public void RecordCompleted(bool isDemanded, long elapsedTicks)
        {
            if (isDemanded)
            {
                Interlocked.Increment(ref _completedDemanded);
            }
            else
            {
                Interlocked.Increment(ref _completedSpeculative);
            }

            CallbackWallLatency.Record(elapsedTicks);
            CallbackWallLatencyByDemand[DemandIndex(isDemanded)].Record(elapsedTicks);
        }

        public void RecordUiExecution(bool isDemanded, long elapsedTicks)
        {
            UiExecutionLatency.Record(elapsedTicks);
            UiExecutionLatencyByDemand[DemandIndex(isDemanded)].Record(elapsedTicks);
        }

        public void RecordAsyncSuspension(bool isDemanded, long elapsedTicks)
        {
            AsyncSuspensionLatency.Record(elapsedTicks);
            AsyncSuspensionLatencyByDemand[DemandIndex(isDemanded)].Record(elapsedTicks);
        }

        public void AppendDemandTimings(StringBuilder builder, string name, int index)
        {
            if (DispatcherWaitLatencyByDemand[index].Count == 0 &&
                CallbackWallLatencyByDemand[index].Count == 0 &&
                UiExecutionLatencyByDemand[index].Count == 0 &&
                AsyncSuspensionLatencyByDemand[index].Count == 0)
            {
                return;
            }

            builder.Append("      ").Append(name).AppendLine(" timing");
            DispatcherWaitLatencyByDemand[index].Append(builder, "Low-priority dispatcher wait", "        ");
            CallbackWallLatencyByDemand[index].Append(builder, "Dispatcher callback wall time", "        ");
            UiExecutionLatencyByDemand[index].Append(builder, "Measured STA execution slices", "        ");
            AsyncSuspensionLatencyByDemand[index].Append(builder, "Asynchronous materialization suspension", "        ");
        }
    }

    private sealed class ElementKindMeasurements
    {
        private long _created;
        private long _reused;

        public long Created => Volatile.Read(ref _created);

        public long Reused => Volatile.Read(ref _reused);

        public DiagnosticHistogram UpdateLatency { get; } = new();

        public void Record(bool reused)
        {
            if (reused)
            {
                Interlocked.Increment(ref _reused);
            }
            else
            {
                Interlocked.Increment(ref _created);
            }
        }
    }

    private sealed class DiagnosticHistogram
    {
        private static readonly double[] BucketUpperBoundsMilliseconds =
        [
            0.1,
            0.25,
            0.5,
            1,
            2,
            4,
            8,
            16,
            33,
            50,
            100,
            250,
            500,
            1000,
            2500,
            5000,
        ];

        private readonly long[] _buckets = new long[BucketUpperBoundsMilliseconds.Length + 1];
        private long _count;
        private long _sumTicks;
        private long _maximumTicks;

        public long Count => Volatile.Read(ref _count);

        public long SumTicks => Volatile.Read(ref _sumTicks);

        public void Record(long elapsedTicks)
        {
            if (elapsedTicks < 0)
            {
                return;
            }

            var elapsedMilliseconds = elapsedTicks * 1000D / Stopwatch.Frequency;
            var bucket = 0;
            while (bucket < BucketUpperBoundsMilliseconds.Length && elapsedMilliseconds > BucketUpperBoundsMilliseconds[bucket])
            {
                bucket++;
            }

            Interlocked.Increment(ref _buckets[bucket]);
            Interlocked.Increment(ref _count);
            Interlocked.Add(ref _sumTicks, elapsedTicks);
            UpdateMaximum(ref _maximumTicks, elapsedTicks);
        }

        public void Append(StringBuilder builder, string name, string indentation = "  ")
        {
            var count = Volatile.Read(ref _count);
            builder.Append(indentation).Append(name).Append(": ");
            if (count == 0)
            {
                builder.AppendLine("no samples");
                return;
            }

            var average = Volatile.Read(ref _sumTicks) * 1000D / Stopwatch.Frequency / count;
            builder
                .Append("count=").Append(count.ToString(CultureInfo.InvariantCulture))
                .Append(", avg=").Append(average.ToString("0.###", CultureInfo.InvariantCulture)).Append(" ms")
                .Append(", p50=").Append(FormatPercentile(count, 0.50))
                .Append(", p95=").Append(FormatPercentile(count, 0.95))
                .Append(", p99=").Append(FormatPercentile(count, 0.99))
                .Append(", max=").Append(FormatMilliseconds(Volatile.Read(ref _maximumTicks))).AppendLine(" ms");
        }

        private string FormatPercentile(long count, double percentile)
        {
            var target = (long)Math.Ceiling(count * percentile);
            var cumulative = 0L;
            for (var i = 0; i < _buckets.Length; i++)
            {
                cumulative += Volatile.Read(ref _buckets[i]);
                if (cumulative < target)
                {
                    continue;
                }

                return i < BucketUpperBoundsMilliseconds.Length
                    ? $"<={BucketUpperBoundsMilliseconds[i].ToString("0.###", CultureInfo.InvariantCulture)} ms"
                    : $">{BucketUpperBoundsMilliseconds[^1].ToString("0.###", CultureInfo.InvariantCulture)} ms";
            }

            return "n/a";
        }
    }
}
