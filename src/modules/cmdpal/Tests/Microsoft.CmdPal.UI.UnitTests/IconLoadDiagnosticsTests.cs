// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.Helpers;
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
        var dispatcherEnqueuedAt = load.BeginDispatcherWait();
        var dispatcherStartedAt = load.DispatcherStarted(dispatcherEnqueuedAt);
        load.DispatcherCompleted(dispatcherStartedAt);
        load.SetResult(null);
        load.Complete();
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
        StringAssert.Contains(report.Text, "Load demand");
        StringAssert.Contains(report.Text, "Requests linked to session loads: 1");
        StringAssert.Contains(report.Text, "    Completed: 1");
        StringAssert.Contains(report.Text, "Loads completed with no live requester: 0");
        StringAssert.Contains(report.Text, "CommandItemViewModel.InitializeProperties reading AppListItem.Icon");
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
    public void UppercaseShellExtensionRemainsStringInput()
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
        StringAssert.Contains(inputKinds, "  String: 1");
        StringAssert.Contains(inputKinds, "  ShellBinary: 0");
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

        Assert.IsNotNull(report);
        StringAssert.Contains(report.Text, "Direct glyph loads: 1");
        StringAssert.Contains(report.Text, "Direct glyph construction: count=1");
        StringAssert.Contains(report.Text, "Active at stop: 0");
        StringAssert.Contains(report.Text, "Maximum active workers: 0");
        StringAssert.Contains(report.Text, "Enqueue to completion: no samples");
        StringAssert.Contains(report.Text, "New-load result kinds");
        StringAssert.Contains(report.Text, "Empty: 1");
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
        StartWorker(firstDemandedLoad, workerCount: 1);
        firstDemandedLoad.SetResult(null);
        firstDemandedLoad.Complete();
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
        StartWorker(secondDemandedLoad, workerCount: 4);
        secondDemandedLoad.SetResult(null);
        secondDemandedLoad.Complete();
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

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        StringAssert.Contains(report.Text, "Maximum low queue depth: 1");
        StringAssert.Contains(report.Text, "Active at stop: 0");
        StringAssert.Contains(report.Text, "Enqueue to completion: count=1");
    }

    [TestMethod]
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

        var report = IconLoadDiagnostics.StopAndCreateReport();

        Assert.IsNotNull(report);
        StringAssert.Contains(report.Text, "Rejected: 1");
        StringAssert.Contains(report.Text, "Active at stop: 0");
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
}
