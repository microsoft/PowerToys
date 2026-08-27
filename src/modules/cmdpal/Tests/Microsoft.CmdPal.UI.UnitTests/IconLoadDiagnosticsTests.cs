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
        Assert.IsTrue(report.EndedUtc >= report.StartedUtc);
        Assert.IsTrue(report.Duration >= TimeSpan.Zero);
        StringAssert.Contains(report.Text, $"Session: {sessionId}");
        StringAssert.Contains(report.Text, "Ended UTC:");
        StringAssert.Contains(report.Text, "Process work during session");
        StringAssert.Contains(report.Text, "Managed allocations:");
        StringAssert.Contains(report.Text, "UI responsiveness probe");
        StringAssert.Contains(report.Text, "  Enabled: no");
        StringAssert.Contains(report.Text, "Started: 1");
        StringAssert.Contains(report.Text, "Stale: 1");
        StringAssert.Contains(report.Text, "NewLoad: 1");
        StringAssert.Contains(report.Text, "Request to completion by resolution and result");
        StringAssert.Contains(report.Text, "      Empty: count=1");
        StringAssert.Contains(report.Text, "ShellBinary: 1");
        StringAssert.Contains(report.Text, "    Enqueue to completion: count=1");
        StringAssert.Contains(report.Text, "    Dispatcher wait: count=1");
        StringAssert.Contains(report.Text, "Empty: 1");
        StringAssert.Contains(report.Text, "Maximum low queue depth: 1");
        StringAssert.Contains(report.Text, "Dispatcher wait: count=1");
        StringAssert.Contains(report.Text, "Dispatcher materialization");
        StringAssert.Contains(report.Text, "Measured STA execution slices: count=1");
        StringAssert.Contains(report.Text, "    Binary");
        StringAssert.Contains(report.Text, "Load demand");
        StringAssert.Contains(report.Text, "Requests linked to session loads: 1");
        StringAssert.Contains(report.Text, "    Completed: 1");
        StringAssert.Contains(report.Text, "Loads completed with no live requester: 0");
        StringAssert.Contains(report.Text, "Installed Apps icon extraction enters this pipeline as SpecializedAppIcon work");
        StringAssert.Contains(report.Text, "Created: 1");
        StringAssert.Contains(report.Text, "Reused: 1");
        StringAssert.Contains(report.Text, "Update wall time: count=2");
        StringAssert.Contains(report.Text, "Empty: created=1, reused=1");
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
        Assert.IsTrue(inputKindsStart >= 0);
        Assert.IsTrue(inputKindsEnd > inputKindsStart);
        var inputKinds = report.Text[inputKindsStart..inputKindsEnd];
        StringAssert.Contains(inputKinds, "  String: 0");
        StringAssert.Contains(inputKinds, "  ShellBinary: 1");
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

        CollectionAssert.AreEqual(
            new[]
            {
                DispatcherQueuePriority.Normal,
                DispatcherQueuePriority.Normal,
                DispatcherQueuePriority.Normal,
            },
            observedPriorities);
        StringAssert.Contains(report.Text, "Callbacks enqueued: 3");
        StringAssert.Contains(report.Text, "Callbacks completed: 1");
        StringAssert.Contains(report.Text, "Callbacks outstanding at stop: 1");
        StringAssert.Contains(report.Text, "Timer ticks skipped while a callback was pending: 1");
        StringAssert.Contains(report.Text, "Callbacks rejected by DispatcherQueue: 1");
        StringAssert.Contains(report.Text, "Normal-priority queue wait: count=1");

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
        StringAssert.Contains(report.Text, "Direct glyph loads: 1");
        StringAssert.Contains(report.Text, "Direct glyph construction: count=1");
        StringAssert.Contains(report.Text, directGlyphResults);
        StringAssert.Contains(report.Text, "Active at stop: 0");
        StringAssert.Contains(report.Text, "Maximum active workers: 0");
        StringAssert.Contains(report.Text, "Enqueue to completion: no samples");
        StringAssert.Contains(report.Text, "New-load result kinds");
        StringAssert.Contains(report.Text, "Empty: 1");
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
        StringAssert.Contains(report.Text, "  SpecializedAppIcon: 1");
        Assert.IsFalse(report.Text.Contains("shell32", StringComparison.OrdinalIgnoreCase));
    }

    [DataTestMethod]
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
        StringAssert.Contains(report.Text, $"  {expectedKind}: 1");
        Assert.IsFalse(report.Text.Contains(icon, StringComparison.Ordinal));
    }

    [TestMethod]
    [Timeout(5_000)]
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

        StringAssert.Contains(report.Text, "Scheduler coordination");
        StringAssert.Contains(report.Text, publishedCommands);
        StringAssert.Contains(report.Text, processedCommands);
        StringAssert.Contains(report.Text, "Commands outstanding at stop: 0");
        StringAssert.Contains(report.Text, "    Enqueue: count=1");
        StringAssert.Contains(report.Text, "    WorkerReady: count=1");
        StringAssert.Contains(report.Text, "  Coordinator wake and batch processing");
        StringAssert.Contains(report.Text, "    Signal to coordinator pass start for non-empty batches: count=");

        // Complete may be drained by a batch whose wake was triggered by another command.
        // Its publication and processing are asserted above instead of its trigger attribution.
        StringAssert.Contains(report.Text, "    Commands drained: 3");
        StringAssert.Contains(report.Text, "    Work items dispatched: 1");
        StringAssert.Contains(report.Text, "    Non-empty batch command drain wall time: count=");
        StringAssert.Contains(report.Text, "    Non-empty batch pass-start-to-dispatch-complete wall time: count=");
        StringAssert.Contains(report.Text, "    Ready to work dispatch: count=1");
        StringAssert.Contains(report.Text, "    Ready to demanded work dispatch: count=1");
        StringAssert.Contains(report.Text, "    Ready to speculative work dispatch: no samples");
        StringAssert.Contains(report.Text, "    Intervals started: 1");
        StringAssert.Contains(report.Text, "    Intervals active at stop: 0");
        StringAssert.Contains(report.Text, "    Maximum demanded queue depth during an interval: 1");
        StringAssert.Contains(report.Text, "    Maximum available worker slots during an interval: 1");
        StringAssert.Contains(report.Text, "    Interval duration: count=1");
    }

    [TestMethod]
    [Timeout(5_000)]
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
        StringAssert.Contains(report.Text, reserveBlock);
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
        StringAssert.Contains(report.Text, "    Signal to coordinator pass start for non-empty batches: no samples");
        StringAssert.Contains(report.Text, "    Signal to coordinator pass start for empty coalesced batches: count=1");
        StringAssert.Contains(report.Text, "    Batches completed: 1");
        StringAssert.Contains(report.Text, "    Empty batches: 1");
        StringAssert.Contains(report.Text, "    Commands drained: 0");
    }

    [TestMethod]
    [Timeout(5_000)]
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
        StringAssert.Contains(report.Text, $"    Enqueue: {WorkItemCount}");
        StringAssert.Contains(report.Text, $"    WorkerReady: {WorkItemCount}");
        StringAssert.Contains(report.Text, "    Complete: 1");
        StringAssert.Contains(report.Text, "Commands outstanding at stop: 0");
        StringAssert.Contains(report.Text, $"    Commands drained: {(WorkItemCount * 2) + 1}");
        StringAssert.Contains(report.Text, $"    Work items dispatched: {WorkItemCount}");
        StringAssert.Contains(report.Text, $"    Ready to work dispatch: count={WorkItemCount}");
        StringAssert.Contains(report.Text, $"    Ready to demanded work dispatch: count={WorkItemCount}");
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
        StringAssert.Contains(report.Text, expectedHeader);
        StringAssert.Contains(report.Text, "    Lookups: 2");
        StringAssert.Contains(report.Text, "    Hits: 1");
        StringAssert.Contains(report.Text, "    Misses: 1");
        StringAssert.Contains(report.Text, "    Hit rate: 50 %");
        StringAssert.Contains(report.Text, "    Maximum observed entries: 1");
        var expectedRemovalReason =
            $"    Removal reasons{Environment.NewLine}" +
            "      Explicit: 1";
        StringAssert.Contains(report.Text, expectedRemovalReason);
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
        StringAssert.Contains(report.Text, requestBlock);
        var invalidationBlock =
            $"  Location invalidation{Environment.NewLine}" +
            $"    Association-change notifications received: 1{Environment.NewLine}" +
            $"    Invalidations by reason{Environment.NewLine}" +
            $"      AssociationChanged: 1{Environment.NewLine}" +
            "      ShellRestarted: 1";
        StringAssert.Contains(report.Text, invalidationBlock);
        StringAssert.Contains(report.Text, $"  Location aliases{Environment.NewLine}    Cache hits: 1{Environment.NewLine}    Cache misses: 1");
        StringAssert.Contains(report.Text, "    Identity resolutions: 1");
        StringAssert.Contains(report.Text, $"    Resolved identity kinds{Environment.NewLine}      SystemImageList: 1");
        StringAssert.Contains(report.Text, $"  Canonical source outcomes{Environment.NewLine}    Cache hits: 1{Environment.NewLine}    In-flight joins: 0{Environment.NewLine}    New loads: 1{Environment.NewLine}    Reuse rate: 50%");
        StringAssert.Contains(report.Text, $"  Shell extraction{Environment.NewLine}    Started: 1{Environment.NewLine}    Succeeded: 1{Environment.NewLine}    Empty: 0{Environment.NewLine}    Failed: 0");
        StringAssert.Contains(report.Text, $"    Extraction routes{Environment.NewLine}      SystemImageList: 1");
        StringAssert.Contains(report.Text, "    Requests avoiding extraction: 50%");
        var imageListBlock =
            $"    Direct system image-list extraction{Environment.NewLine}" +
            $"      Attempts: 1{Environment.NewLine}" +
            $"      Image-list levels used{Environment.NewLine}" +
            "        Large: 1";
        StringAssert.Contains(report.Text, imageListBlock);
        StringAssert.Contains(report.Text, "      Requested physical edge: count=1, avg=20 px, max=20 px");
        StringAssert.Contains(report.Text, "      Source image-list dimensions: count=1, avg=32x32 px, max edge=32 px");
        StringAssert.Contains(report.Text, "      Source larger than request: 1");
        StringAssert.Contains(report.Text, "      HICON to SoftwareBitmap: count=1");
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
        StringAssert.Contains(report.Text, "Dispatcher materialization");
        StringAssert.Contains(report.Text, "    Enqueued demanded: 1");
        StringAssert.Contains(report.Text, "    Callbacks started speculative: 1");
        StringAssert.Contains(report.Text, "    Callbacks completed speculative: 1");
        StringAssert.Contains(report.Text, "    Measured STA execution slices: count=2");
        StringAssert.Contains(report.Text, "    Asynchronous materialization suspension: count=1");
        StringAssert.Contains(report.Text, "    BitmapStream");
        StringAssert.Contains(report.Text, "phase=UiEntry");
        StringAssert.Contains(report.Text, "phase=AsyncSuspension");
        StringAssert.Contains(report.Text, "materialization=BitmapStream");
        StringAssert.Contains(report.Text, "demand=Speculative");
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
        StringAssert.Contains(report.Text, "    Enqueued demanded: 1");
        StringAssert.Contains(report.Text, "    Dispatcher enqueue failures: 1");
        StringAssert.Contains(
            report.Text,
            $"    Speculative{Environment.NewLine}      Low-priority dispatcher wait: count=1");
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
        StringAssert.Contains(report.Text, "Requests linked to session loads: 2");
        StringAssert.Contains(report.Text, "    Queued: 1");
        StringAssert.Contains(report.Text, "  Invalidated requests by load stage");
        StringAssert.Contains(report.Text, "  Demand-loss events after the last requester was invalidated");
        StringAssert.Contains(report.Text, "    Queued: 1");
        StringAssert.Contains(report.Text, "Workers started with no live requester: 1");
        StringAssert.Contains(report.Text, "Loads completed with no live requester: 1");
        StringAssert.Contains(report.Text, "Loads completed with no live requester by input kind");
        StringAssert.Contains(report.Text, "Loads completed with no live requester by result kind");
        StringAssert.Contains(report.Text, "Completed-without-requester loads later cache-hit: 1");
        StringAssert.Contains(report.Text, "Later cache-hit requests: 1");
        StringAssert.Contains(report.Text, "No-requester time before worker start: count=1");
        StringAssert.Contains(report.Text, "No-requester time before load completion: count=1");
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
        StringAssert.Contains(report.Text, "Requests linked to session loads: 3");
        StringAssert.Contains(report.Text, "Loads with multiple simultaneous requesters: 1");
        StringAssert.Contains(report.Text, "Maximum simultaneous requesters per load: 2");
        StringAssert.Contains(report.Text, "    Queued: 2");
        StringAssert.Contains(report.Text, "    Queued: 1");
        StringAssert.Contains(report.Text, "Loads where demand returned before completion: 1");
        StringAssert.Contains(report.Text, "Queued demotions after demand loss: 1");
        StringAssert.Contains(report.Text, "Queued promotions after demand returned: 1");
        StringAssert.Contains(report.Text, "Workers started demanded: 1");
        StringAssert.Contains(report.Text, "Workers started speculative: 0");
        StringAssert.Contains(report.Text, "Workers started with no live requester: 0");
        StringAssert.Contains(report.Text, "Loads completed with no live requester: 0");
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
        StringAssert.Contains(report.Text, "Maximum demanded queue depth: 1");
        StringAssert.Contains(report.Text, "Maximum speculative queue depth: 1");
        StringAssert.Contains(report.Text, "Queued demotions after demand loss: 2");
        StringAssert.Contains(report.Text, "Queued promotions after demand returned: 0");
        StringAssert.Contains(report.Text, "Workers started demanded: 2");
        StringAssert.Contains(report.Text, "Workers started speculative: 2");
        StringAssert.Contains(report.Text, "Speculative starts with demanded loads queued: 2");
        StringAssert.Contains(report.Text, "Speculative starts leaving demanded loads beyond remaining worker capacity: 1");
        StringAssert.Contains(report.Text, "Demanded loads beyond remaining capacity across those starts: 1");
        StringAssert.Contains(report.Text, "Maximum demanded loads beyond remaining capacity at one start: 1");
        StringAssert.Contains(report.Text, "Capacity-interfering speculative starts by input kind");
        StringAssert.Contains(report.Text, "      String: 1");
        StringAssert.Contains(report.Text, "Demanded queue wait: count=2");
        StringAssert.Contains(report.Text, "Speculative queue wait: count=2");

        var stringInputMeasurements = GetTextBetween(
            report.Text,
            "  String: 4",
            "  ShellBinary: 0");
        StringAssert.Contains(stringInputMeasurements, "    Demanded queue wait: count=2");
        StringAssert.Contains(stringInputMeasurements, "    Speculative queue wait: count=2");
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

        StringAssert.Contains(report.Text, "Active demanded workers at stop: 0");
        StringAssert.Contains(report.Text, "Active speculative workers at stop: 0");
        StringAssert.Contains(report.Text, "Maximum active speculative workers: 1");
        StringAssert.Contains(report.Text, "Demanded queue arrivals: 2");
        StringAssert.Contains(report.Text, "Arrivals with active speculative workers: 1");
        StringAssert.Contains(report.Text, "Sum of active speculative workers observed at those arrivals: 1");
        StringAssert.Contains(report.Text, "Maximum speculative workers active at one demanded arrival: 1");
        StringAssert.Contains(report.Text, "Arrivals directly blocked by speculative worker capacity: 1");
        StringAssert.Contains(report.Text, speculativeOccupancyBlock);
        StringAssert.Contains(report.Text, directlyBlockedBlock);
        StringAssert.Contains(report.Text, "Demand arrival to worker start with speculative workers active: count=1");
        StringAssert.Contains(report.Text, "Directly blocked demand arrival to worker start: count=1");
    }

    [TestMethod]
    [Timeout(5_000)]
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
        StringAssert.Contains(report.Text, "Active demanded workers at stop: 0");
        StringAssert.Contains(report.Text, "Active speculative workers at stop: 0");
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
        StringAssert.Contains(report.Text, "    BeforeEnqueue: 1");
        StringAssert.Contains(report.Text, "Workers started with no live requester: 1");
        StringAssert.Contains(report.Text, "Loads completed with no live requester: 1");
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
            StringAssert.Contains(report.Text, $"    {resolution}");
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
        StringAssert.Contains(report.Text, "Request origins");
        StringAssert.Contains(report.Text, "  ListItem / SingleRow");
        StringAssert.Contains(report.Text, "    Icon boxes: 2");
        StringAssert.Contains(report.Text, "    Started: 3");
        StringAssert.Contains(report.Text, "    Applied: 2");
        StringAssert.Contains(report.Text, "    Stale: 1");
        StringAssert.Contains(report.Text, "      NewLoad: 1");
        StringAssert.Contains(report.Text, "      CacheHit: 2");
        StringAssert.Contains(report.Text, "    Result kinds");
        StringAssert.Contains(report.Text, "      Empty: 3");
        StringAssert.Contains(report.Text, "      Applied: count=2");
        StringAssert.Contains(report.Text, "      Stale: count=1");
        var globalAppliedResolutionBlock =
            $"  Applied request to completion by provider resolution{Environment.NewLine}" +
            $"    NewLoad: no samples{Environment.NewLine}" +
            $"    CacheHit: count=2";
        var originAppliedResolutionBlock =
            $"    Applied request to completion by provider resolution{Environment.NewLine}" +
            $"      NewLoad: no samples{Environment.NewLine}" +
            $"      CacheHit: count=2";
        StringAssert.Contains(report.Text, globalAppliedResolutionBlock);
        StringAssert.Contains(report.Text, originAppliedResolutionBlock);
        StringAssert.Contains(report.Text, "Individual process-local IconBox IDs are available in RequestOrigin ETW events.");
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
        StringAssert.Contains(report.Text, "  Settings");
        Assert.IsFalse(report.Text.Contains("secret", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(report.Text.Contains("Injected", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(report.Text.Contains("C:\\private", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    [Timeout(5_000)]
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
        Assert.IsTrue(await workerStart.WaitAsync(TimeSpan.FromSeconds(5)));
        load.SetResult(null);
        load.Complete();
        load.WorkerReleased();

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        StringAssert.Contains(report.Text, "Maximum low queue depth: 1");
        StringAssert.Contains(report.Text, "Active at stop: 0");
        StringAssert.Contains(report.Text, "Enqueue to completion: count=1");
    }

    [TestMethod]
    [Timeout(5_000)]
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
        Assert.IsFalse(await workerStart.WaitAsync(TimeSpan.FromSeconds(5)));
        load.WorkerReleased();

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        StringAssert.Contains(report.Text, "Rejected: 1");
        StringAssert.Contains(report.Text, "Active at stop: 0");
        StringAssert.Contains(report.Text, "Enqueue to completion: no samples");
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
        StringAssert.Contains(report.Text, "Active at stop: 0");
        StringAssert.Contains(report.Text, "Active demanded workers at stop: 0");
        StringAssert.Contains(report.Text, "Active speculative workers at stop: 0");
        StringAssert.Contains(report.Text, "    AwaitingSharedLoad: 1");
        StringAssert.Contains(report.Text, "    Awaiting shared load: 1");
        StringAssert.Contains(report.Text, "Enqueue to completion: no samples");

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
        StringAssert.Contains(report.Text, "Abandoned before worker start: 1");
        StringAssert.Contains(report.Text, "Active at stop: 0");
        StringAssert.Contains(report.Text, "Demanded loads queued at stop: 0");
        StringAssert.Contains(report.Text, "Speculative loads queued at stop: 0");
        StringAssert.Contains(report.Text, "    Abandoned: 1");
        StringAssert.Contains(report.Text, "Enqueue to completion: no samples");
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
        StringAssert.Contains(report.Text, "Active at stop: 0");
        StringAssert.Contains(report.Text, "Enqueue to completion: no samples");
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

            CollectionAssert.Contains(listener.EventIds.ToArray(), 1);
            CollectionAssert.Contains(listener.EventIds.ToArray(), 4);
            CollectionAssert.Contains(listener.EventIds.ToArray(), 22);
            CollectionAssert.Contains(listener.EventIds.ToArray(), 23);
            CollectionAssert.Contains(listener.EventIds.ToArray(), 24);
            CollectionAssert.Contains(listener.EventIds.ToArray(), 25);
            CollectionAssert.Contains(listener.EventIds.ToArray(), 26);
            CollectionAssert.Contains(listener.EventIds.ToArray(), 39);
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
        Assert.IsTrue(startIndex >= 0, $"Missing report section start: {start}");
        var endIndex = value.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        Assert.IsTrue(endIndex > startIndex, $"Missing report section end: {end}");
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
}
