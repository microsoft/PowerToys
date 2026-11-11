// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Dispatching;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[OSCondition(OperatingSystems.Windows)]
[TestClass]
[DoNotParallelize]
public class IconLoadDiagnosticsTests
{
    [TestCleanup]
    public void Cleanup()
    {
        IconLoadDiagnostics.Reset();
    }

    [TestMethod]
    public void RecordingProducesAnonymousAggregateReport()
    {
        var sessionId = IconLoadDiagnostics.Start();
        var request = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.5);
        var load = IconLoadDiagnostics.CreateLoad(
            request,
            @"C:\private\secret.exe,0",
            hasStream: false,
            width: 20,
            height: 20,
            scale: 1.5);

        Assert.IsNotNull(load);
        request.RecordProviderResolution(IconProviderResolution.NewLoad, load);
        load.Enqueued(IconLoadPriority.Low);
        StartWorker(load);
        var preparationStartedAt = load.BeginBackgroundPreparation();
        load.CompleteBackgroundPreparation(preparationStartedAt);
        var dispatcherEnqueuedAt = load.BeginDispatcherWait(IconDispatcherMaterializationKind.Binary);
        var dispatcherStartedAt = load.DispatcherStarted(dispatcherEnqueuedAt);
        load.DispatcherUiSliceCompleted(
            dispatcherStartedAt,
            IconDispatcherUiSliceKind.SynchronousCallback);
        load.DispatcherCompleted(dispatcherStartedAt);
        load.SetResult(null);
        load.Complete();
        load.WorkerReleased();
        request.Complete(IconRequestStatus.Stale);
        var elementStartedAt = IconLoadDiagnostics.BeginElementUpdate();
        IconLoadDiagnostics.RecordElementUpdate(reused: false, source: null, elementStartedAt);
        elementStartedAt = IconLoadDiagnostics.BeginElementUpdate();
        IconLoadDiagnostics.RecordElementUpdate(reused: true, source: null, elementStartedAt);

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        Assert.AreEqual(sessionId, report.SessionId);
        Assert.IsGreaterThanOrEqualTo(report.StartedUtc, report.EndedUtc);
        Assert.IsGreaterThanOrEqualTo(TimeSpan.Zero, report.Duration);
        Assert.Contains($"Session: {sessionId}", report.Text);
        Assert.Contains("Ended UTC:", report.Text);
        Assert.Contains("Process work during session", report.Text);
        Assert.Contains("Managed allocations:", report.Text);
        Assert.Contains("UI responsiveness probe", report.Text);
        Assert.Contains("  Enabled: no", report.Text);
        Assert.Contains("Started: 1", report.Text);
        Assert.Contains("Stale: 1", report.Text);
        Assert.Contains("NewLoad: 1", report.Text);
        Assert.Contains("Request to completion by resolution and result", report.Text);
        Assert.Contains("      Empty: count=1", report.Text);
        Assert.Contains("ShellBinary: 1", report.Text);
        Assert.Contains("    Enqueue to completion: count=1", report.Text);
        Assert.Contains("    Dispatcher wait: count=1", report.Text);
        Assert.Contains("Empty: 1", report.Text);
        Assert.Contains("Maximum low queue depth: 1", report.Text);
        Assert.Contains("Dispatcher wait: count=1", report.Text);
        Assert.Contains("Dispatcher materialization", report.Text);
        Assert.Contains("Measured STA execution slices: count=1", report.Text);
        Assert.Contains("    Binary", report.Text);
        Assert.Contains("Load demand", report.Text);
        Assert.Contains("Requests linked to session loads: 1", report.Text);
        Assert.Contains("    Completed: 1", report.Text);
        Assert.Contains("Loads completed with no live requester: 0", report.Text);
        Assert.Contains("Installed Apps icon extraction enters this pipeline as SpecializedAppIcon work", report.Text);
        Assert.Contains("Created: 1", report.Text);
        Assert.Contains("Reused: 1", report.Text);
        Assert.Contains("Update wall time: count=2", report.Text);
        Assert.Contains("Empty: created=1, reused=1", report.Text);
        Assert.IsFalse(report.Text.Contains("secret", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(report.Text.Contains(@"C:\private", StringComparison.OrdinalIgnoreCase));

        var reports = IconLoadDiagnostics.GetReports();
        Assert.HasCount(1, reports);
        Assert.AreSame(report, reports[0]);
    }

    [TestMethod]
    public void UppercaseShellExtensionUsesBinaryInputKind()
    {
        IconLoadDiagnostics.Start();
        var request = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
        var load = IconLoadDiagnostics.CreateLoad(
            request,
            @"C:\Windows\APP.EXE,0",
            hasStream: false,
            width: 20,
            height: 20,
            scale: 1.0);

        Assert.IsNotNull(load);
        load.Rejected();
        request.Complete(IconRequestStatus.Failed);

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        var inputKindsStart = report.Text.IndexOf("Input kinds", StringComparison.Ordinal);
        var inputKindsEnd = report.Text.IndexOf("New-load result kinds", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, inputKindsStart);
        Assert.IsGreaterThan(inputKindsStart, inputKindsEnd);
        var inputKinds = report.Text[inputKindsStart..inputKindsEnd];
        Assert.Contains("  String: 0", inputKinds);
        Assert.Contains("  ShellBinary: 1", inputKinds);
    }

    [TestMethod]
    public void UiResponsivenessProbeTracksCompletedSkippedRejectedAndOutstandingCallbacks()
    {
        var session = new IconLoadDiagnosticsSession(1);
        var acceptCallback = true;
        DispatcherQueueHandler? pendingCallback = null;
        var observedPriorities = new List<DispatcherQueuePriority>();
        var probe = new IconUiResponsivenessProbe(TryEnqueue, session);

        probe.OnTimerTick();
        Assert.IsNotNull(pendingCallback);
        probe.OnTimerTick();

        var completedCallback = pendingCallback;
        pendingCallback = null;
        completedCallback!();

        acceptCallback = false;
        probe.OnTimerTick();

        acceptCallback = true;
        probe.OnTimerTick();
        Assert.IsNotNull(pendingCallback);

        probe.Stop();
        var report = session.CreateReport();

        Assert.AreSequenceEqual(
            new[]
            {
                DispatcherQueuePriority.Normal,
                DispatcherQueuePriority.Normal,
                DispatcherQueuePriority.Normal,
            },
            observedPriorities);
        Assert.Contains("Callbacks enqueued: 3", report.Text);
        Assert.Contains("Callbacks completed: 1", report.Text);
        Assert.Contains("Callbacks outstanding at stop: 1", report.Text);
        Assert.Contains("Timer ticks skipped while a callback was pending: 1", report.Text);
        Assert.Contains("Callbacks rejected by DispatcherQueue: 1", report.Text);
        Assert.Contains("Normal-priority queue wait: count=1", report.Text);

        bool TryEnqueue(DispatcherQueuePriority priority, DispatcherQueueHandler callback)
        {
            observedPriorities.Add(priority);
            if (!acceptCallback)
            {
                return false;
            }

            Assert.IsNull(pendingCallback);
            pendingCallback = callback;
            return true;
        }
    }

    [TestMethod]
    public void DirectGlyphLoadDoesNotCountAsActiveWorker()
    {
        IconLoadDiagnostics.Start();
        var request = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
        var load = IconLoadDiagnostics.CreateLoad(
            request,
            "\uE700",
            hasStream: false,
            width: 20,
            height: 20,
            scale: 1.0);

        Assert.IsNotNull(load);
        request.RecordProviderResolution(IconProviderResolution.NewLoad, load);
        load.CompleteDirectGlyph(result: null);
        request.Complete(IconRequestStatus.Empty);

        var report = IconLoadDiagnostics.StopAndCreateReport();
        var directGlyphResults =
            $"  Direct glyph construction by result kind{Environment.NewLine}" +
            $"    Empty: count=1";

        Assert.IsNotNull(report);
        Assert.Contains("Direct glyph loads: 1", report.Text);
        Assert.Contains("Direct glyph construction: count=1", report.Text);
        Assert.Contains(directGlyphResults, report.Text);
        Assert.Contains("Active at stop: 0", report.Text);
        Assert.Contains("Maximum active workers: 0", report.Text);
        Assert.Contains("Enqueue to completion: no samples", report.Text);
        Assert.Contains("New-load result kinds", report.Text);
        Assert.Contains("Empty: 1", report.Text);
    }

    [TestMethod]
    public void AppIconProtocolUsesSpecializedInputKind()
    {
        IconLoadDiagnostics.Start();
        var request = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
        var load = IconLoadDiagnostics.CreateLoad(
            request,
            AppIconProtocol.Create("C:\\Windows\\System32\\shell32.dll,1"),
            hasStream: false,
            width: 20,
            height: 20,
            scale: 1.0);

        Assert.IsNotNull(load);
        request.RecordProviderResolution(IconProviderResolution.NewLoad, load);
        load.SetResult(null);
        load.Complete();
        load.WorkerReleased();
        request.Complete(IconRequestStatus.Empty);

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        Assert.Contains("  SpecializedAppIcon: 1", report.Text);
        Assert.IsFalse(report.Text.Contains("shell32", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    [DataRow("|Swatch|#FF0067C0|", "GeneratedSwatch")]
    [DataRow("|Initials|CP|#FF005FB8|square|", "GeneratedInitials")]
    [DataRow("|Svg|C:\\Icons\\plain.svg", "SvgFile")]
    [DataRow("|Svg|<svg xmlns=\"http://www.w3.org/2000/svg\"/>", "SvgInline")]
    [DataRow("|ThemedSvg|warning|C:\\Icons\\themed.svg", "ThemedSvgFile")]
    [DataRow("|ThemedSvg|#7A3E9D|<svg xmlns=\"http://www.w3.org/2000/svg\"/>", "ThemedSvgInline")]
    [DataRow("|ShellItemIcon|v1;1:a", "ShellItemIcon")]
    [DataRow("C:\\Files\\report.txt", "ShellItemIcon")]
    public void SemanticIconInputsUseSpecificInputKind(string icon, string expectedKind)
    {
        IconLoadDiagnostics.Start();
        var request = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
        var load = IconLoadDiagnostics.CreateLoad(
            request,
            icon,
            hasStream: false,
            width: 20,
            height: 20,
            scale: 1.0);

        Assert.IsNotNull(load);
        request.RecordProviderResolution(IconProviderResolution.NewLoad, load);
        load.SetResult(null);
        load.Complete();
        load.WorkerReleased();
        request.Complete(IconRequestStatus.Empty);

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        Assert.Contains($"  {expectedKind}: 1", report.Text);
        Assert.IsFalse(report.Text.Contains(icon, StringComparison.Ordinal));
    }

    [TestMethod]
    [Timeout(5_000, CooperativeCancellation = true)]
    public async Task SchedulerReportCapturesCoordinatorAndWorkerHandoff()
    {
        using var coordinatorThreadListener = new CoordinatorThreadListener();
        IconLoadDiagnostics.Start();
        var queue = new IconLoadQueue(workerCount: 1);
        var work = new TestOperation();

        var dequeue = queue.DequeueAsync().AsTask();
        Assert.IsTrue(queue.TryEnqueue(
            work,
            IconLoadPriority.Low,
            IconLoadDemand.CreateDemanded(),
            out _));

        Assert.AreSame(work, await dequeue);
        Assert.IsFalse(await coordinatorThreadListener.IsThreadPoolThread);
        queue.Complete();
        await queue.Completion;

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        var publishedCommands =
            $"  Commands published by kind{Environment.NewLine}" +
            $"    Enqueue: 1{Environment.NewLine}" +
            $"    DemandChanged: 0{Environment.NewLine}" +
            $"    WorkerReady: 1{Environment.NewLine}" +
            $"    Complete: 1";
        var processedCommands =
            $"  Commands processed by kind{Environment.NewLine}" +
            $"    Enqueue: 1{Environment.NewLine}" +
            $"    DemandChanged: 0{Environment.NewLine}" +
            $"    WorkerReady: 1{Environment.NewLine}" +
            $"    Complete: 1";

        Assert.Contains("Scheduler coordination", report.Text);
        Assert.Contains(publishedCommands, report.Text);
        Assert.Contains(processedCommands, report.Text);
        Assert.Contains("Commands outstanding at stop: 0", report.Text);
        Assert.Contains("    Enqueue: count=1", report.Text);
        Assert.Contains("    WorkerReady: count=1", report.Text);
        Assert.Contains("  Coordinator wake and batch processing", report.Text);
        Assert.Contains("    Signal to coordinator pass start for non-empty batches: count=", report.Text);

        // Complete may be drained by a batch whose wake was triggered by another command.
        // Its publication and processing are asserted above instead of its trigger attribution.
        Assert.Contains("    Commands drained: 3", report.Text);
        Assert.Contains("    Work items dispatched: 1", report.Text);
        Assert.Contains("    Non-empty batch command drain wall time: count=", report.Text);
        Assert.Contains("    Non-empty batch pass-start-to-dispatch-complete wall time: count=", report.Text);
        Assert.Contains("    Ready to work dispatch: count=1", report.Text);
        Assert.Contains("    Ready to demanded work dispatch: count=1", report.Text);
        Assert.Contains("    Ready to speculative work dispatch: no samples", report.Text);
        Assert.Contains("    Intervals started: 1", report.Text);
        Assert.Contains("    Intervals active at stop: 0", report.Text);
        Assert.Contains("    Maximum demanded queue depth during an interval: 1", report.Text);
        Assert.Contains("    Maximum available worker slots during an interval: 1", report.Text);
        Assert.Contains("    Interval duration: count=1", report.Text);
    }

    [TestMethod]
    [Timeout(5_000, CooperativeCancellation = true)]
    public async Task SchedulerReportCapturesSpeculativeDemandReserve()
    {
        IconLoadDiagnostics.Start();
        var queue = new IconLoadQueue(workerCount: 4);
        var speculativeWork = new TestOperation();
        var demandedWork = new TestOperation();
        var speculativeDemand = IconLoadDemand.CreateDemanded();
        speculativeDemand.RemoveRequester();

        Assert.IsTrue(queue.TryEnqueue(
            speculativeWork,
            IconLoadPriority.Low,
            speculativeDemand,
            out _));

        var reservedDequeue = queue.DequeueAsync().AsTask();
        Assert.IsTrue(queue.TryEnqueue(
            demandedWork,
            IconLoadPriority.Low,
            IconLoadDemand.CreateDemanded(),
            out _));
        Assert.AreSame(demandedWork, await reservedDequeue);

        var firstReadyWorker = queue.DequeueAsync().AsTask();
        var secondReadyWorker = queue.DequeueAsync().AsTask();
        var speculativeDequeue = await Task.WhenAny(firstReadyWorker, secondReadyWorker);
        Assert.AreSame(speculativeWork, await speculativeDequeue);

        queue.Complete();
        var remainingDequeue = ReferenceEquals(speculativeDequeue, firstReadyWorker)
            ? secondReadyWorker
            : firstReadyWorker;
        Assert.IsNull(await remainingDequeue);
        await queue.Completion;

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        var reserveBlock =
            $"  Speculative dispatch deferred by the demand reserve{Environment.NewLine}" +
            $"    Definition: a coordinator-state interval with speculative work queued, no demanded work queued, and a worker-ready slot deliberately retained for a future live request.{Environment.NewLine}" +
            $"    Intervals started: 2{Environment.NewLine}" +
            $"    Intervals active at stop: 0{Environment.NewLine}" +
            $"    Maximum speculative queue depth during an interval: 1{Environment.NewLine}" +
            $"    Maximum configured worker count during an interval: 4{Environment.NewLine}" +
            $"    Maximum worker-ready slots retained during an interval: 1{Environment.NewLine}" +
            $"    Interval duration: count=2";
        Assert.Contains(reserveBlock, report.Text);
    }

    [TestMethod]
    public void SchedulerReportSeparatesEmptyCoalescedBatchWakeLatency()
    {
        IconLoadDiagnostics.Start();
        var command = IconLoadDiagnostics.BeginSchedulerCommand(IconLoadQueue.QueueCommandKind.Enqueue);

        Assert.IsNotNull(command);
        var wake = command.CreateWakeMeasurement();
        command.Processed();
        wake.Woke(System.Diagnostics.Stopwatch.GetTimestamp());
        wake.BatchCompleted(
            commandCount: 0,
            dispatchedWorkItemCount: 0,
            drainTicks: 0,
            passTicks: 0);

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        Assert.Contains("    Signal to coordinator pass start for non-empty batches: no samples", report.Text);
        Assert.Contains("    Signal to coordinator pass start for empty coalesced batches: count=1", report.Text);
        Assert.Contains("    Batches completed: 1", report.Text);
        Assert.Contains("    Empty batches: 1", report.Text);
        Assert.Contains("    Commands drained: 0", report.Text);
    }

    [TestMethod]
    [Timeout(5_000, CooperativeCancellation = true)]
    public async Task SchedulerMeasurementsRemainPairedWithConcurrentPublishersAndWorkers()
    {
        const int WorkerCount = 4;
        const int WorkItemCount = 128;

        IconLoadDiagnostics.Start();
        var queue = new IconLoadQueue(WorkerCount);
        var work = new TestOperation[WorkItemCount];
        var accepted = 0;

        Parallel.For(0, WorkItemCount, i =>
        {
            work[i] = new TestOperation();
            if (queue.TryEnqueue(
                work[i],
                IconLoadPriority.Low,
                IconLoadDemand.CreateDemanded(),
                out _))
            {
                Interlocked.Increment(ref accepted);
            }
        });

        Assert.AreEqual(WorkItemCount, accepted);
        for (var i = 0; i < WorkItemCount; i += WorkerCount)
        {
            var dequeued = await Task.WhenAll(
                queue.DequeueAsync().AsTask(),
                queue.DequeueAsync().AsTask(),
                queue.DequeueAsync().AsTask(),
                queue.DequeueAsync().AsTask());
            Assert.IsTrue(dequeued.All(item => item is not null));
        }

        queue.Complete();
        await queue.Completion;

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        Assert.Contains($"    Enqueue: {WorkItemCount}", report.Text);
        Assert.Contains($"    WorkerReady: {WorkItemCount}", report.Text);
        Assert.Contains("    Complete: 1", report.Text);
        Assert.Contains("Commands outstanding at stop: 0", report.Text);
        Assert.Contains($"    Commands drained: {(WorkItemCount * 2) + 1}", report.Text);
        Assert.Contains($"    Work items dispatched: {WorkItemCount}", report.Text);
        Assert.Contains($"    Ready to work dispatch: count={WorkItemCount}", report.Text);
        Assert.Contains($"    Ready to demanded work dispatch: count={WorkItemCount}", report.Text);
    }

    [TestMethod]
    public void CacheReportTracksLookupsOccupancyAndRemovalReasons()
    {
        IconLoadDiagnostics.Start();
        var size = new global::Windows.Foundation.Size(20, 20);
        IconLoadDiagnostics.RecordCacheLookup(size, IconCachePartition.Glyph, capacity: 16, hit: false);
        IconLoadDiagnostics.RecordCacheEntryAdded(size, IconCachePartition.Glyph, capacity: 16, entryCount: 1);
        IconLoadDiagnostics.RecordCacheLookup(size, IconCachePartition.Glyph, capacity: 16, hit: true);
        IconLoadDiagnostics.RecordCacheEntryRemoved(
            size,
            IconCachePartition.Glyph,
            capacity: 16,
            entryCount: 0,
            AdaptiveCacheRemovalReason.Explicit);

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        var expectedHeader =
            $"Icon caches{Environment.NewLine}" +
            $"  Definition: each entry is a cached IconSource task; counts are approximate concurrent observations. Eviction only drops the cache reference.{Environment.NewLine}" +
            $"  A request coalesced with an in-flight load is a cache miss; see Provider resolution for in-flight reuse.{Environment.NewLine}" +
            $"  Capacity means the cache was over its limit when removal was attempted and takes precedence over LowScore; LowScore means score alone caused removal.{Environment.NewLine}" +
            "  20x20 Glyph cache, capacity 16";
        Assert.Contains(expectedHeader, report.Text);
        Assert.Contains("    Lookups: 2", report.Text);
        Assert.Contains("    Hits: 1", report.Text);
        Assert.Contains("    Misses: 1", report.Text);
        Assert.Contains("    Hit rate: 50 %", report.Text);
        Assert.Contains("    Maximum observed entries: 1", report.Text);
        var expectedRemovalReason =
            $"    Removal reasons{Environment.NewLine}" +
            "      Explicit: 1";
        Assert.Contains(expectedRemovalReason, report.Text);
    }

    [TestMethod]
    public void ShellIconReportTracksIdentityAndExtractionReuseWithoutPaths()
    {
        IconLoadDiagnostics.Start();
        var firstRequest = new ShellItemIconRequest(
            ShellItemIconProtocol.Create(@"C:\Windows\System32\first.txt"),
            @"C:\Windows\System32\first.txt",
            false);
        var first = IconLoadDiagnostics.BeginShellIconRequest(firstRequest);
        first.LocationCacheMiss();
        var identityStartedAt = first.BeginIdentityResolution();
        first.IdentityResolved(ShellIconIdentityKind.SystemImageList, identityStartedAt);
        first.CanonicalNewLoad();
        var extractionStartedAt = first.BeginExtraction();
        first.ExtractionCompleted(
            extractionStartedAt,
            ShellIconIdentityKind.SystemImageList,
            hasContent: true);
        first.SystemImageListExtracted(
            ShellImageListSize.Large,
            requestedPixelSize: 20,
            sourceWidth: 32,
            sourceHeight: 32,
            hIconConversionTicks: Stopwatch.Frequency / 1_000);
        var typeFallbackStartedAt = first.BeginTypeFallback();
        first.TypeFallbackCompleted(typeFallbackStartedAt, hasContent: true, dispatchAccepted: true);
        first.IntermediatePresentationCompleted(typeFallbackStartedAt, applied: true);
        first.ExactRefinementCompleted(sameSource: true);
        IconLoadDiagnostics.RecordShellAssociationChangedNotification();
        IconLoadDiagnostics.RecordShellIconCacheInvalidation(ShellIconCacheInvalidationReason.AssociationChanged);
        IconLoadDiagnostics.RecordShellIconCacheInvalidation(ShellIconCacheInvalidationReason.ShellRestarted);

        var secondRequest = new ShellItemIconRequest(@"C:\Windows\System32\second.txt", false);
        var second = IconLoadDiagnostics.BeginShellIconRequest(secondRequest);
        second.LocationCacheHit();
        second.CanonicalCacheHit();

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        var requestBlock =
            $"Shell item identity and reuse{Environment.NewLine}" +
            $"  Definition: location aliases map submitted paths to non-sensitive Shell identities; canonical outcomes describe materialized source reuse after that mapping.{Environment.NewLine}" +
            $"  The same identity has independent materialized entries for each icon size and scale.{Environment.NewLine}" +
            $"  Requests: 2{Environment.NewLine}" +
            $"  Requests by kind{Environment.NewLine}" +
            $"    Protocol: 1{Environment.NewLine}" +
            $"    FileUri: 0{Environment.NewLine}" +
            "    LegacyPath: 1";
        Assert.Contains(requestBlock, report.Text);
        var invalidationBlock =
            $"  Location invalidation{Environment.NewLine}" +
            $"    Association-change notifications received: 1{Environment.NewLine}" +
            $"    Invalidations by reason{Environment.NewLine}" +
            $"      AssociationChanged: 1{Environment.NewLine}" +
            "      ShellRestarted: 1";
        Assert.Contains(invalidationBlock, report.Text);
        var progressiveBlock =
            $"  Progressive type fallback{Environment.NewLine}" +
            $"    Succeeded: 1{Environment.NewLine}" +
            $"    Empty: 0{Environment.NewLine}" +
            "    Failed: 0";
        Assert.Contains(progressiveBlock, report.Text);
        Assert.Contains("    Request to type fallback: count=1", report.Text);
        var progressiveOutcomeBlock =
            $"    Intermediate dispatches accepted: 1{Environment.NewLine}" +
            $"    Intermediate dispatches rejected: 0{Environment.NewLine}" +
            $"    Intermediate UI updates applied: 1{Environment.NewLine}" +
            $"    Intermediate UI updates skipped: 0{Environment.NewLine}" +
            $"    Request to applied intermediate: count=1";
        Assert.Contains(progressiveOutcomeBlock, report.Text);
        var refinementBlock =
            $"    Exact refinement outcomes{Environment.NewLine}" +
            "      Same source: 1";
        Assert.Contains(refinementBlock, report.Text);
        Assert.Contains($"  Location aliases{Environment.NewLine}    Cache hits: 1{Environment.NewLine}    Cache misses: 1", report.Text);
        Assert.Contains("    Identity resolutions: 1", report.Text);
        Assert.Contains($"    Resolved identity kinds{Environment.NewLine}      SystemImageList: 1", report.Text);
        Assert.Contains($"  Canonical source outcomes{Environment.NewLine}    Cache hits: 1{Environment.NewLine}    In-flight joins: 0{Environment.NewLine}    New loads: 1{Environment.NewLine}    Reuse rate: 50%", report.Text);
        Assert.Contains($"  Shell extraction{Environment.NewLine}    Started: 1{Environment.NewLine}    Succeeded: 1{Environment.NewLine}    Empty: 0{Environment.NewLine}    Failed: 0", report.Text);
        Assert.Contains($"    Extraction routes{Environment.NewLine}      SystemImageList: 1", report.Text);
        Assert.Contains("    Requests avoiding extraction: 50%", report.Text);
        var imageListBlock =
            $"    Direct system image-list extraction{Environment.NewLine}" +
            $"      Attempts: 1{Environment.NewLine}" +
            $"      Image-list levels used{Environment.NewLine}" +
            "        Large: 1";
        Assert.Contains(imageListBlock, report.Text);
        Assert.Contains("      Requested physical edge: count=1, avg=20 px, max=20 px", report.Text);
        Assert.Contains("      Source image-list dimensions: count=1, avg=32x32 px, max edge=32 px", report.Text);
        Assert.Contains("      Source larger than request: 1", report.Text);
        Assert.Contains("      HICON to SoftwareBitmap: count=1", report.Text);
        Assert.IsFalse(report.Text.Contains("first.txt", StringComparison.Ordinal));
        Assert.IsFalse(report.Text.Contains("second.txt", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DispatcherReportSeparatesUiExecutionFromAsyncSuspensionAndSamplesLiveDemand()
    {
        IconLoadDiagnostics.Start();
        var request = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
        var load = IconLoadDiagnostics.CreateLoad(
            request,
            "bitmap.png",
            hasStream: false,
            width: 20,
            height: 20,
            scale: 1.0);

        Assert.IsNotNull(load);
        request.RecordProviderResolution(IconProviderResolution.NewLoad, load);
        load.Enqueued(IconLoadPriority.Low);
        StartWorker(load);
        var dispatcherEnqueuedAt = load.BeginDispatcherWait(IconDispatcherMaterializationKind.BitmapStream);

        // The enqueue is demanded, but callback phases should observe the invalidated request
        // without taking the demand-state lock on the dispatcher thread.
        request.Invalidate();
        var dispatcherStartedAt = load.DispatcherStarted(dispatcherEnqueuedAt);
        var artificialOutlierStart = Stopwatch.GetTimestamp() - (Stopwatch.Frequency / 50);
        _ = load.DispatcherUiSliceCompleted(
            artificialOutlierStart,
            IconDispatcherUiSliceKind.BeforeAsyncSuspension);
        var continuationStartedAt = load.DispatcherAsyncSuspensionCompleted(artificialOutlierStart);
        load.DispatcherUiSliceCompleted(
            continuationStartedAt,
            IconDispatcherUiSliceKind.AsyncContinuation);
        load.DispatcherCompleted(dispatcherStartedAt);
        load.SetResult(null);
        load.Complete();
        load.WorkerReleased();
        request.Complete(IconRequestStatus.Stale);

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        Assert.Contains("Dispatcher materialization", report.Text);
        Assert.Contains("    Enqueued demanded: 1", report.Text);
        Assert.Contains("    Callbacks started speculative: 1", report.Text);
        Assert.Contains("    Callbacks completed speculative: 1", report.Text);
        Assert.Contains("    Measured STA execution slices: count=2", report.Text);
        Assert.Contains("    Asynchronous materialization suspension: count=1", report.Text);
        Assert.Contains("    BitmapStream", report.Text);
        Assert.Contains("phase=UiEntry", report.Text);
        Assert.Contains("phase=AsyncSuspension", report.Text);
        Assert.Contains("materialization=BitmapStream", report.Text);
        Assert.Contains("demand=Speculative", report.Text);
    }

    [TestMethod]
    public void DispatcherEnqueueFailureClosesTheWaitAtCurrentDemand()
    {
        IconLoadDiagnostics.Start();
        var request = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
        var load = IconLoadDiagnostics.CreateLoad(
            request,
            "failed.png",
            hasStream: false,
            width: 20,
            height: 20,
            scale: 1.0);

        Assert.IsNotNull(load);
        request.RecordProviderResolution(IconProviderResolution.NewLoad, load);
        load.Enqueued(IconLoadPriority.Low);
        StartWorker(load);
        var dispatcherEnqueuedAt = load.BeginDispatcherWait(IconDispatcherMaterializationKind.BitmapUri);
        request.Invalidate();
        load.DispatcherWaitFailed(dispatcherEnqueuedAt);
        load.Fail();
        load.WorkerReleased();
        request.Complete(IconRequestStatus.Failed);

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        Assert.Contains("    Enqueued demanded: 1", report.Text);
        Assert.Contains("    Dispatcher enqueue failures: 1", report.Text);
        Assert.Contains(
            $"    Speculative{Environment.NewLine}      Low-priority dispatcher wait: count=1",
            report.Text);
    }

    [TestMethod]
    public void StaleQueuedRequestTracksRetainedCacheUse()
    {
        IconLoadDiagnostics.Start();
        var request = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
        var load = IconLoadDiagnostics.CreateLoad(
            request,
            "bitmap.png",
            hasStream: false,
            width: 20,
            height: 20,
            scale: 1.0);

        Assert.IsNotNull(load);
        var task = Task.FromResult<Microsoft.UI.Xaml.Controls.IconSource?>(null);
        load.RegisterTask(task);
        request.RecordProviderResolution(IconProviderResolution.NewLoad, load);
        load.Enqueued(IconLoadPriority.Low);
        request.Invalidate();
        request.Complete(IconRequestStatus.Stale);
        StartWorker(load);
        load.SetResult(null);
        load.Complete();
        load.WorkerReleased();

        var cacheRequest = IconLoadDiagnostics.BeginRequest(IconRequestReason.Loaded, 1.0);
        cacheRequest.RecordProviderResolution(IconProviderResolution.CacheHit, task);
        cacheRequest.Complete(IconRequestStatus.Empty);

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        Assert.Contains("Requests linked to session loads: 2", report.Text);
        Assert.Contains("    Queued: 1", report.Text);
        Assert.Contains("  Invalidated requests by load stage", report.Text);
        Assert.Contains("  Demand-loss events after the last requester was invalidated", report.Text);
        Assert.Contains("    Queued: 1", report.Text);
        Assert.Contains("Workers started with no live requester: 1", report.Text);
        Assert.Contains("Loads completed with no live requester: 1", report.Text);
        Assert.Contains("Loads completed with no live requester by input kind", report.Text);
        Assert.Contains("Loads completed with no live requester by result kind", report.Text);
        Assert.Contains("Completed-without-requester loads later cache-hit: 1", report.Text);
        Assert.Contains("Later cache-hit requests: 1", report.Text);
        Assert.Contains("No-requester time before worker start: count=1", report.Text);
        Assert.Contains("No-requester time before load completion: count=1", report.Text);
    }

    [TestMethod]
    public void ReturnedInFlightDemandPreventsFalseAbandonment()
    {
        IconLoadDiagnostics.Start();
        var firstRequest = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
        var load = IconLoadDiagnostics.CreateLoad(
            firstRequest,
            "bitmap.png",
            hasStream: false,
            width: 20,
            height: 20,
            scale: 1.0);

        Assert.IsNotNull(load);
        firstRequest.RecordProviderResolution(IconProviderResolution.NewLoad, load);
        var secondRequest = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
        secondRequest.RecordProviderResolution(IconProviderResolution.InFlight, load);
        load.Enqueued(IconLoadPriority.Low);

        firstRequest.Invalidate();
        firstRequest.Complete(IconRequestStatus.Stale);
        secondRequest.Invalidate();
        secondRequest.Complete(IconRequestStatus.Stale);

        var returnedRequest = IconLoadDiagnostics.BeginRequest(IconRequestReason.Loaded, 1.0);
        returnedRequest.RecordProviderResolution(IconProviderResolution.InFlight, load);
        StartWorker(load);
        load.SetResult(null);
        load.Complete();
        load.WorkerReleased();
        returnedRequest.Complete(IconRequestStatus.Empty);

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        Assert.Contains("Requests linked to session loads: 3", report.Text);
        Assert.Contains("Loads with multiple simultaneous requesters: 1", report.Text);
        Assert.Contains("Maximum simultaneous requesters per load: 2", report.Text);
        Assert.Contains("    Queued: 2", report.Text);
        Assert.Contains("    Queued: 1", report.Text);
        Assert.Contains("Loads where demand returned before completion: 1", report.Text);
        Assert.Contains("Queued demotions after demand loss: 1", report.Text);
        Assert.Contains("Queued promotions after demand returned: 1", report.Text);
        Assert.Contains("Workers started demanded: 1", report.Text);
        Assert.Contains("Workers started speculative: 0", report.Text);
        Assert.Contains("Workers started with no live requester: 0", report.Text);
        Assert.Contains("Loads completed with no live requester: 0", report.Text);
    }

    [TestMethod]
    public void DemandQueueReportSeparatesQueuedDemandFromCapacityInterference()
    {
        IconLoadDiagnostics.Start();

        var firstSpeculativeRequest = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
        var firstSpeculativeLoad = IconLoadDiagnostics.CreateLoad(
            firstSpeculativeRequest,
            "bitmap.png",
            hasStream: false,
            width: 20,
            height: 20,
            scale: 1.0);
        Assert.IsNotNull(firstSpeculativeLoad);
        firstSpeculativeRequest.RecordProviderResolution(IconProviderResolution.NewLoad, firstSpeculativeLoad);
        firstSpeculativeLoad.Enqueued(IconLoadPriority.Low);
        firstSpeculativeRequest.Invalidate();
        firstSpeculativeRequest.Complete(IconRequestStatus.Stale);

        var firstDemandedRequest = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
        var firstDemandedLoad = IconLoadDiagnostics.CreateLoad(
            firstDemandedRequest,
            "bitmap.png",
            hasStream: false,
            width: 20,
            height: 20,
            scale: 1.0);
        Assert.IsNotNull(firstDemandedLoad);
        firstDemandedRequest.RecordProviderResolution(IconProviderResolution.NewLoad, firstDemandedLoad);
        firstDemandedLoad.Enqueued(IconLoadPriority.Low);

        StartWorker(firstSpeculativeLoad, workerCount: 1);
        firstSpeculativeLoad.SetResult(null);
        firstSpeculativeLoad.Complete();
        firstSpeculativeLoad.WorkerReleased();
        StartWorker(firstDemandedLoad, workerCount: 1);
        firstDemandedLoad.SetResult(null);
        firstDemandedLoad.Complete();
        firstDemandedLoad.WorkerReleased();
        firstDemandedRequest.Complete(IconRequestStatus.Empty);

        var secondSpeculativeRequest = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
        var secondSpeculativeLoad = IconLoadDiagnostics.CreateLoad(
            secondSpeculativeRequest,
            "bitmap.png",
            hasStream: false,
            width: 20,
            height: 20,
            scale: 1.0);
        Assert.IsNotNull(secondSpeculativeLoad);
        secondSpeculativeRequest.RecordProviderResolution(IconProviderResolution.NewLoad, secondSpeculativeLoad);
        secondSpeculativeLoad.Enqueued(IconLoadPriority.Low);
        secondSpeculativeRequest.Invalidate();
        secondSpeculativeRequest.Complete(IconRequestStatus.Stale);

        var secondDemandedRequest = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
        var secondDemandedLoad = IconLoadDiagnostics.CreateLoad(
            secondDemandedRequest,
            "bitmap.png",
            hasStream: false,
            width: 20,
            height: 20,
            scale: 1.0);
        Assert.IsNotNull(secondDemandedLoad);
        secondDemandedRequest.RecordProviderResolution(IconProviderResolution.NewLoad, secondDemandedLoad);
        secondDemandedLoad.Enqueued(IconLoadPriority.Low);

        StartWorker(secondSpeculativeLoad, workerCount: 4);
        secondSpeculativeLoad.SetResult(null);
        secondSpeculativeLoad.Complete();
        secondSpeculativeLoad.WorkerReleased();
        StartWorker(secondDemandedLoad, workerCount: 4);
        secondDemandedLoad.SetResult(null);
        secondDemandedLoad.Complete();
        secondDemandedLoad.WorkerReleased();
        secondDemandedRequest.Complete(IconRequestStatus.Empty);

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        Assert.Contains("Maximum demanded queue depth: 1", report.Text);
        Assert.Contains("Maximum speculative queue depth: 1", report.Text);
        Assert.Contains("Queued demotions after demand loss: 2", report.Text);
        Assert.Contains("Queued promotions after demand returned: 0", report.Text);
        Assert.Contains("Workers started demanded: 2", report.Text);
        Assert.Contains("Workers started speculative: 2", report.Text);
        Assert.Contains("Speculative starts with demanded loads queued: 2", report.Text);
        Assert.Contains("Speculative starts leaving demanded loads beyond remaining worker capacity: 1", report.Text);
        Assert.Contains("Demanded loads beyond remaining capacity across those starts: 1", report.Text);
        Assert.Contains("Maximum demanded loads beyond remaining capacity at one start: 1", report.Text);
        Assert.Contains("Capacity-interfering speculative starts by input kind", report.Text);
        Assert.Contains("      String: 1", report.Text);
        Assert.Contains("Demanded queue wait: count=2", report.Text);
        Assert.Contains("Speculative queue wait: count=2", report.Text);

        var stringInputMeasurements = GetTextBetween(
            report.Text,
            "  String: 4",
            "  ShellBinary: 0");
        Assert.Contains("    Demanded queue wait: count=2", stringInputMeasurements);
        Assert.Contains("    Speculative queue wait: count=2", stringInputMeasurements);
    }

    [TestMethod]
    public void DemandArrivalReportCapturesActiveSpeculativeCapacity()
    {
        IconLoadDiagnostics.Start();

        var activeRequest = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
        var activeLoad = IconLoadDiagnostics.CreateLoad(
            activeRequest,
            "active.png",
            hasStream: false,
            width: 20,
            height: 20,
            scale: 1.0);
        Assert.IsNotNull(activeLoad);
        activeRequest.RecordProviderResolution(IconProviderResolution.NewLoad, activeLoad);
        activeLoad.Enqueued(IconLoadPriority.Low, workerCount: 1);
        StartWorker(activeLoad, workerCount: 1);

        activeRequest.Invalidate();
        activeRequest.Complete(IconRequestStatus.Stale);

        var demandedRequest = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
        var demandedLoad = IconLoadDiagnostics.CreateLoad(
            demandedRequest,
            "demanded.png",
            hasStream: false,
            width: 20,
            height: 20,
            scale: 1.0);
        Assert.IsNotNull(demandedLoad);
        demandedRequest.RecordProviderResolution(IconProviderResolution.NewLoad, demandedLoad);
        demandedLoad.Enqueued(IconLoadPriority.Low, workerCount: 1);

        activeLoad.SetResult(null);
        activeLoad.Complete();
        activeLoad.WorkerReleased();
        StartWorker(demandedLoad, workerCount: 1);
        demandedLoad.SetResult(null);
        demandedLoad.Complete();
        demandedLoad.WorkerReleased();
        demandedRequest.Complete(IconRequestStatus.Empty);

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        var speculativeOccupancyBlock =
            $"      Speculative worker occupancy observed at demanded arrivals by speculative input kind{Environment.NewLine}" +
            $"        Empty: 0{Environment.NewLine}" +
            $"        String: 1";
        var directlyBlockedBlock =
            $"      Directly blocked demanded arrivals by demanded input kind{Environment.NewLine}" +
            $"        Empty: 0{Environment.NewLine}" +
            $"        String: 1";

        Assert.Contains("Active demanded workers at stop: 0", report.Text);
        Assert.Contains("Active speculative workers at stop: 0", report.Text);
        Assert.Contains("Maximum active speculative workers: 1", report.Text);
        Assert.Contains("Demanded queue arrivals: 2", report.Text);
        Assert.Contains("Arrivals with active speculative workers: 1", report.Text);
        Assert.Contains("Sum of active speculative workers observed at those arrivals: 1", report.Text);
        Assert.Contains("Maximum speculative workers active at one demanded arrival: 1", report.Text);
        Assert.Contains("Arrivals directly blocked by speculative worker capacity: 1", report.Text);
        Assert.Contains(speculativeOccupancyBlock, report.Text);
        Assert.Contains(directlyBlockedBlock, report.Text);
        Assert.Contains("Demand arrival to worker start with speculative workers active: count=1", report.Text);
        Assert.Contains("Directly blocked demand arrival to worker start: count=1", report.Text);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "MSTEST0045:Use cooperative cancellation for timeout", Justification = "Synchronous and native operations require the independent MSTest timeout watchdog.")]
    [TestMethod]
    [Timeout(5_000, CooperativeCancellation = false)]
    public void ConcurrentActiveInvalidationAndCompletionDoNotLeakWorkerDemand()
    {
        IconLoadDiagnostics.Start();

        for (var i = 0; i < 500; i++)
        {
            var request = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
            var load = IconLoadDiagnostics.CreateLoad(
                request,
                "bitmap.png",
                hasStream: false,
                width: 20,
                height: 20,
                scale: 1.0);
            Assert.IsNotNull(load);
            request.RecordProviderResolution(IconProviderResolution.NewLoad, load);
            load.Enqueued(IconLoadPriority.Low, workerCount: 1);
            StartWorker(load, workerCount: 1);
            load.SetResult(null);

            Parallel.Invoke(request.Invalidate, load.Complete);
            load.WorkerReleased();
            request.Complete(IconRequestStatus.Stale);
        }

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        Assert.Contains("Active demanded workers at stop: 0", report.Text);
        Assert.Contains("Active speculative workers at stop: 0", report.Text);
    }

    [TestMethod]
    public void InvalidationBeforeResolutionStillTracksLoadWithoutDemand()
    {
        IconLoadDiagnostics.Start();
        var request = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
        request.Invalidate();

        var load = IconLoadDiagnostics.CreateLoad(
            request,
            "bitmap.png",
            hasStream: false,
            width: 20,
            height: 20,
            scale: 1.0);

        Assert.IsNotNull(load);
        request.RecordProviderResolution(IconProviderResolution.NewLoad, load);
        load.Enqueued(IconLoadPriority.Low);
        StartWorker(load);
        load.SetResult(null);
        load.Complete();
        load.WorkerReleased();
        request.Complete(IconRequestStatus.Stale);

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        Assert.Contains("    BeforeEnqueue: 1", report.Text);
        Assert.Contains("Workers started with no live requester: 1", report.Text);
        Assert.Contains("Loads completed with no live requester: 1", report.Text);
    }

    [TestMethod]
    public void RequestLatencyIsAttributedToEveryProviderResolution()
    {
        IconLoadDiagnostics.Start();

        foreach (var resolution in Enum.GetValues<IconProviderResolution>())
        {
            var request = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
            request.RecordProviderResolution(resolution, load: null);
            request.Complete(IconRequestStatus.Empty);
        }

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        foreach (var resolution in Enum.GetValues<IconProviderResolution>())
        {
            Assert.Contains($"    {resolution}", report.Text);
        }

        Assert.AreEqual(
            Enum.GetValues<IconProviderResolution>().Length,
            CountOccurrences(report.Text, "      Empty: count=1"));
        Assert.IsFalse(report.Text.Contains("Unattributed completed requests", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RequestOriginsAggregateBySiteAndStaticScope()
    {
        IconLoadDiagnostics.Start();
        var firstOrigin = new IconRequestOrigin(101, IconRequestSite.ListItem, "SingleRow");
        var secondOrigin = new IconRequestOrigin(102, IconRequestSite.ListItem, "SingleRow");

        var firstRequest = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0, firstOrigin);
        firstRequest.RecordProviderResolution(IconProviderResolution.CacheHit, load: null);
        firstRequest.Complete(IconRequestStatus.Applied);

        var staleRequest = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0, firstOrigin);
        staleRequest.RecordProviderResolution(IconProviderResolution.NewLoad, load: null);
        staleRequest.Complete(IconRequestStatus.Stale);

        var secondRequest = IconLoadDiagnostics.BeginRequest(IconRequestReason.Loaded, 1.0, secondOrigin);
        secondRequest.RecordProviderResolution(IconProviderResolution.CacheHit, load: null);
        secondRequest.Complete(IconRequestStatus.Applied);

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        Assert.Contains("Request origins", report.Text);
        Assert.Contains("  ListItem / SingleRow", report.Text);
        Assert.Contains("    Icon boxes: 2", report.Text);
        Assert.Contains("    Started: 3", report.Text);
        Assert.Contains("    Applied: 2", report.Text);
        Assert.Contains("    Stale: 1", report.Text);
        Assert.Contains("      NewLoad: 1", report.Text);
        Assert.Contains("      CacheHit: 2", report.Text);
        Assert.Contains("    Result kinds", report.Text);
        Assert.Contains("      Empty: 3", report.Text);
        Assert.Contains("      Applied: count=2", report.Text);
        Assert.Contains("      Stale: count=1", report.Text);
        var globalAppliedResolutionBlock =
            $"  Applied request to completion by provider resolution{Environment.NewLine}" +
            $"    NewLoad: no samples{Environment.NewLine}" +
            $"    CacheHit: count=2";
        var originAppliedResolutionBlock =
            $"    Applied request to completion by provider resolution{Environment.NewLine}" +
            $"      NewLoad: no samples{Environment.NewLine}" +
            $"      CacheHit: count=2";
        Assert.Contains(globalAppliedResolutionBlock, report.Text);
        Assert.Contains(originAppliedResolutionBlock, report.Text);
        Assert.Contains("Individual process-local IconBox IDs are available in RequestOrigin ETW events.", report.Text);
    }

    [TestMethod]
    public void DiagnosticScopeRejectsPathsAndReportInjection()
    {
        IconLoadDiagnostics.Start();
        var origin = new IconRequestOrigin(101, IconRequestSite.Settings, "C:\\private\\secret.exe\r\nInjected: 1");
        var request = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0, origin);
        request.Complete(IconRequestStatus.Empty);

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        Assert.Contains("  Settings", report.Text);
        Assert.IsFalse(report.Text.Contains("secret", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(report.Text.Contains("Injected", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(report.Text.Contains("C:\\private", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    [Timeout(5_000, CooperativeCancellation = true)]
    public async Task WorkerArrivalBeforeEnqueueDoesNotBlockAndResumesAfterCommit()
    {
        IconLoadDiagnostics.Start();
        var load = IconLoadDiagnostics.CreateLoad(
            default,
            "bitmap.png",
            hasStream: false,
            width: 20,
            height: 20,
            scale: 1.0);

        Assert.IsNotNull(load);
        var workerStart = load.WorkerStartingAsync().AsTask();

        Assert.IsFalse(workerStart.IsCompleted);
        load.Enqueued(IconLoadPriority.Low);
        Assert.IsTrue(await workerStart.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken));
        load.SetResult(null);
        load.Complete();
        load.WorkerReleased();

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        Assert.Contains("Maximum low queue depth: 1", report.Text);
        Assert.Contains("Active at stop: 0", report.Text);
        Assert.Contains("Enqueue to completion: count=1", report.Text);
    }

    [TestMethod]
    [Timeout(5_000, CooperativeCancellation = true)]
    public async Task RejectionReleasesWorkerWaitingForEnqueueCommit()
    {
        IconLoadDiagnostics.Start();
        var load = IconLoadDiagnostics.CreateLoad(
            default,
            "bitmap.png",
            hasStream: false,
            width: 20,
            height: 20,
            scale: 1.0);

        Assert.IsNotNull(load);
        var workerStart = load.WorkerStartingAsync().AsTask();

        Assert.IsFalse(workerStart.IsCompleted);
        load.Rejected();
        Assert.IsFalse(await workerStart.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken));
        load.WorkerReleased();

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        Assert.Contains("Rejected: 1", report.Text);
        Assert.Contains("Active at stop: 0", report.Text);
        Assert.Contains("Enqueue to completion: no samples", report.Text);
    }

    [TestMethod]
    public void WorkerReleaseEndsOccupancyBeforeForwardedLoadCompletes()
    {
        IconLoadDiagnostics.Start();
        var request = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
        var load = IconLoadDiagnostics.CreateLoad(
            request,
            "bitmap.png",
            hasStream: false,
            width: 20,
            height: 20,
            scale: 1.0);

        Assert.IsNotNull(load);
        request.RecordProviderResolution(IconProviderResolution.NewLoad, load);
        load.Enqueued(IconLoadPriority.Low, workerCount: 4);
        StartWorker(load, workerCount: 4);

        // A raw Shell request can hand its result off to an existing canonical load.
        // Its queue worker is free immediately even though this logical load remains pending.
        load.WorkerReleased();
        request.Invalidate();
        request.Complete(IconRequestStatus.Stale);

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        Assert.Contains("Active at stop: 0", report.Text);
        Assert.Contains("Active demanded workers at stop: 0", report.Text);
        Assert.Contains("Active speculative workers at stop: 0", report.Text);
        Assert.Contains("    AwaitingSharedLoad: 1", report.Text);
        Assert.Contains("    Awaiting shared load: 1", report.Text);
        Assert.Contains("Enqueue to completion: no samples", report.Text);

        load.Fail();
    }

    [TestMethod]
    public void AbandonedQueuedLoadRetiresQueueAndDemandAccounting()
    {
        IconLoadDiagnostics.Start();
        var request = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
        var load = IconLoadDiagnostics.CreateLoad(
            request,
            "bitmap.png",
            hasStream: false,
            width: 20,
            height: 20,
            scale: 1.0);

        Assert.IsNotNull(load);
        request.RecordProviderResolution(IconProviderResolution.NewLoad, load);
        load.Enqueued(IconLoadPriority.Low);
        load.Fail();
        load.WorkerReleased();
        request.Invalidate();
        request.Complete(IconRequestStatus.Failed);

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        Assert.Contains("Abandoned before worker start: 1", report.Text);
        Assert.Contains("Active at stop: 0", report.Text);
        Assert.Contains("Demanded loads queued at stop: 0", report.Text);
        Assert.Contains("Speculative loads queued at stop: 0", report.Text);
        Assert.Contains("    Abandoned: 1", report.Text);
        Assert.Contains("Enqueue to completion: no samples", report.Text);
    }

    [TestMethod]
    public void CompletionWithoutEnqueueDoesNotRecordAbsoluteTimestamp()
    {
        IconLoadDiagnostics.Start();
        var load = IconLoadDiagnostics.CreateLoad(
            default,
            "bitmap.png",
            hasStream: false,
            width: 20,
            height: 20,
            scale: 1.0);

        Assert.IsNotNull(load);
        load.SetResult(null);
        load.Complete();
        load.WorkerReleased();

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        Assert.Contains("Active at stop: 0", report.Text);
        Assert.Contains("Enqueue to completion: no samples", report.Text);
    }

    [TestMethod]
    public void ReportsRemainAvailableUntilReset()
    {
        var firstSessionId = IconLoadDiagnostics.Start();
        var firstReport = IconLoadDiagnostics.StopAndCreateReport();
        var secondSessionId = IconLoadDiagnostics.Start();
        var secondReport = IconLoadDiagnostics.StopAndCreateReport();

        var reports = IconLoadDiagnostics.GetReports();

        Assert.HasCount(2, reports);
        Assert.AreEqual(firstSessionId, firstReport?.SessionId);
        Assert.AreEqual(secondSessionId, secondReport?.SessionId);
        Assert.AreSame(firstReport, reports[0]);
        Assert.AreSame(secondReport, reports[1]);

        IconLoadDiagnostics.Start();

        IconLoadDiagnostics.Reset();

        Assert.IsFalse(IconLoadDiagnostics.IsRecording);
        Assert.IsNull(IconLoadDiagnostics.StopAndCreateReport());
        Assert.IsEmpty(IconLoadDiagnostics.GetReports());
    }

    [TestMethod]
    public void ExternalEtwListenerActivatesMeasurementsWithoutCreatingATextReport()
    {
        using (var listener = new EnablingEventListener())
        {
            Assert.IsFalse(IconLoadDiagnostics.IsRecording);
            Assert.IsNull(IconLoadDiagnostics.ActiveSessionId);

            var request = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
            var load = IconLoadDiagnostics.CreateLoad(
                request,
                "icon.png",
                hasStream: false,
                width: 20,
                height: 20,
                scale: 1.0);

            Assert.IsNotNull(request.Session);
            Assert.IsNotNull(load);
            request.RecordProviderResolution(IconProviderResolution.NewLoad, load);
            request.Invalidate();
            request.Complete(IconRequestStatus.Stale);

            var schedulerCommand = IconLoadDiagnostics.BeginSchedulerCommand(IconLoadQueue.QueueCommandKind.Enqueue);
            Assert.IsNotNull(schedulerCommand);
            var wake = schedulerCommand.CreateWakeMeasurement();
            schedulerCommand.Processed();
            schedulerCommand.WorkerDispatched(demanded: true);
            wake.Woke(Stopwatch.GetTimestamp());
            wake.BatchCompleted(commandCount: 1, dispatchedWorkItemCount: 1, drainTicks: 1, passTicks: 2);

            var idleCapacity = IconLoadDiagnostics.BeginDemandedIdleCapacity(
                demandedQueueDepth: 1,
                availableWorkerSlots: 1);
            Assert.IsNotNull(idleCapacity);
            Assert.IsTrue(idleCapacity.IsForActiveSession);
            idleCapacity.Complete();

            var shellRequest = new ShellItemIconRequest(@"C:\Windows\System32\example.txt", false);
            var shellMeasurement = IconLoadDiagnostics.BeginShellIconRequest(shellRequest);
            shellMeasurement.LocationCacheMiss();

            Assert.Contains(1, listener.EventIds.ToArray());
            Assert.Contains(4, listener.EventIds.ToArray());
            Assert.Contains(22, listener.EventIds.ToArray());
            Assert.Contains(23, listener.EventIds.ToArray());
            Assert.Contains(24, listener.EventIds.ToArray());
            Assert.Contains(25, listener.EventIds.ToArray());
            Assert.Contains(26, listener.EventIds.ToArray());
            Assert.Contains(39, listener.EventIds.ToArray());
        }

        var inactiveRequest = IconLoadDiagnostics.BeginRequest(IconRequestReason.SourceChanged, 1.0);
        Assert.IsNull(inactiveRequest.Session);
        Assert.IsNull(IconLoadDiagnostics.StopAndCreateReport());
        Assert.IsEmpty(IconLoadDiagnostics.GetReports());
    }

    private static int CountOccurrences(string value, string text)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(text, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += text.Length;
        }

        return count;
    }

    private static void StartWorker(IconLoadMeasurement load, int workerCount = 1)
    {
        var workerStart = load.WorkerStartingAsync(workerCount).AsTask();
        Assert.IsTrue(workerStart.IsCompletedSuccessfully);
        Assert.IsTrue(workerStart.GetAwaiter().GetResult());
    }

    private sealed class EnablingEventListener : EventListener
    {
        internal ConcurrentQueue<int> EventIds { get; } = new();

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "Microsoft.PowerToys.CmdPal.IconLoading")
            {
                EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            EventIds.Enqueue(eventData.EventId);
        }
    }

    private static string GetTextBetween(string value, string start, string end)
    {
        var startIndex = value.IndexOf(start, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, startIndex, $"Missing report section start: {start}");
        var endIndex = value.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        Assert.IsGreaterThan(startIndex, endIndex, $"Missing report section end: {end}");
        return value[startIndex..endIndex];
    }

    private sealed class CoordinatorThreadListener : EventListener
    {
        private readonly TaskCompletionSource<bool> _isThreadPoolThread = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<bool> IsThreadPoolThread => _isThreadPoolThread.Task;

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "Microsoft.PowerToys.CmdPal.IconLoading")
            {
                EnableEvents(eventSource, EventLevel.Informational);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventName == nameof(IconLoadEventSource.SchedulerCoordinatorWoke))
            {
                _isThreadPoolThread.TrySetResult(Thread.CurrentThread.IsThreadPoolThread);
            }
        }
    }

    private sealed class TestOperation : IconLoadQueue.Operation
    {
        public override Task ExecuteAsync() => Task.CompletedTask;

        public override void Fail(Exception failure)
        {
        }
    }

    public TestContext TestContext { get; set; } = null!;
}
