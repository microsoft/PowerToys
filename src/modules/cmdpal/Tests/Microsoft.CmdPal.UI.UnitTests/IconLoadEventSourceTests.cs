// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed class IconLoadEventSourceTests
{
    [TestMethod]
    public void DemandStageEtwValuesRemainStable()
    {
        Assert.AreEqual(0, (int)IconLoadDemandStage.Unlinked);
        Assert.AreEqual(1, (int)IconLoadDemandStage.BeforeEnqueue);
        Assert.AreEqual(2, (int)IconLoadDemandStage.Queued);
        Assert.AreEqual(3, (int)IconLoadDemandStage.WorkerActive);
        Assert.AreEqual(4, (int)IconLoadDemandStage.Completed);
        Assert.AreEqual(5, (int)IconLoadDemandStage.Rejected);
        Assert.AreEqual(6, (int)IconLoadDemandStage.Abandoned);
        Assert.AreEqual(7, (int)IconLoadDemandStage.AwaitingSharedLoad);
    }

    [TestMethod]
    public void ShellIconStepEtwValuesRemainStable()
    {
        Assert.AreEqual(0, (int)ShellIconDiagnosticStep.Request);
        Assert.AreEqual(1, (int)ShellIconDiagnosticStep.LocationCacheHit);
        Assert.AreEqual(2, (int)ShellIconDiagnosticStep.LocationCacheMiss);
        Assert.AreEqual(3, (int)ShellIconDiagnosticStep.RawInFlightJoin);
        Assert.AreEqual(4, (int)ShellIconDiagnosticStep.IdentityResolved);
        Assert.AreEqual(5, (int)ShellIconDiagnosticStep.CanonicalCacheHit);
        Assert.AreEqual(6, (int)ShellIconDiagnosticStep.CanonicalInFlightJoin);
        Assert.AreEqual(7, (int)ShellIconDiagnosticStep.CanonicalNewLoad);
        Assert.AreEqual(8, (int)ShellIconDiagnosticStep.ExtractionSucceeded);
        Assert.AreEqual(9, (int)ShellIconDiagnosticStep.ExtractionEmpty);
        Assert.AreEqual(10, (int)ShellIconDiagnosticStep.ExtractionFailed);
        Assert.AreEqual(11, (int)ShellIconDiagnosticStep.AssociationChangedNotification);
        Assert.AreEqual(12, (int)ShellIconDiagnosticStep.LocationCacheInvalidated);
        Assert.AreEqual(13, (int)ShellIconDiagnosticStep.TypeFallbackSucceeded);
        Assert.AreEqual(14, (int)ShellIconDiagnosticStep.TypeFallbackEmpty);
        Assert.AreEqual(15, (int)ShellIconDiagnosticStep.TypeFallbackFailed);
        Assert.AreEqual(16, (int)ShellIconDiagnosticStep.IntermediateDispatchAccepted);
        Assert.AreEqual(17, (int)ShellIconDiagnosticStep.IntermediateDispatchRejected);
        Assert.AreEqual(18, (int)ShellIconDiagnosticStep.ExactRefinementSame);
        Assert.AreEqual(19, (int)ShellIconDiagnosticStep.ExactRefinementDifferent);
        Assert.AreEqual(20, (int)ShellIconDiagnosticStep.ExactRefinementFailed);
        Assert.AreEqual(21, (int)ShellIconDiagnosticStep.IntermediatePresentationApplied);
        Assert.AreEqual(22, (int)ShellIconDiagnosticStep.IntermediatePresentationSkipped);
    }

    [TestMethod]
    public void EventPayloadsPreserveDeclaredTypesAndOrder()
    {
        using var listener = new CollectingEventListener();
        var log = IconLoadEventSource.Log;

        Assert.AreEqual(new Guid("AA068BA3-1767-5F92-7A9B-8F5DA0397413"), log.Guid);

        log.RequestStarted(11, 12, 13, 1.5);
        log.ProviderResolved(11, 12, 14, 15);
        log.RequestCompleted(11, 12, 16, 17);
        log.LoadCreated(11, 14, 18, 19.5, 20.5, 1.25);
        log.LoadEnqueued(11, 14, 21, 22);
        log.LoadRejected(11, 14);
        log.LoadStarted(11, 14, 23, 24);
        log.LoadCompleted(11, 14, 25, 26);
        log.BackgroundPreparationCompleted(11, 14, 27);
        log.DispatcherWaitCompleted(11, 14, 28);
        log.DispatcherWorkCompleted(11, 14, 29);
        log.DirectGlyphLoadCompleted(11, 14, 30, 31);
        log.ElementUpdated(11, 32, reused: true, 33);
        log.RequestAttributed(11, 12, 34, 35, 36);
        log.RequestInvalidated(11, 12, 14, 37, 38);
        log.LoadStartedWithoutRequester(11, 14, 39);
        log.LoadCompletedWithoutRequester(11, 14, 40);
        log.RetainedLoadCacheHit(11, 14, 41);
        log.RequestOrigin(11, 12, 42, 43, "ListItem / SingleRow");
        log.LoadQueueDemandChanged(11, 14, 44, 45, 46);
        log.LoadDemandAtWorkerStart(11, 14, 1, 47, 48, 49, 4, 50);
        log.SchedulerCommandProcessed(11, 58, 59, 60);
        log.WorkerReadyToDispatchCompleted(11, 1, 61);
        log.DemandedIdleCapacityCompleted(11, 62);
        log.SchedulerCoordinatorWoke(11, 63, 64);
        log.SchedulerBatchCompleted(11, 65, 66, 67, 68);
        log.DispatcherWaitFailed(11, 14, 51);
        log.DispatcherUiSliceCompleted(11, 14, 52, 53, isDemanded: true, 54);
        log.DispatcherAsyncSuspensionCompleted(11, 14, 55, isDemanded: false, 56);
        log.UiResponsivenessProbeCompleted(11, 57);
        log.SpeculativeDispatchDeferralCompleted(11, 69);
        log.ShellIconStepCompleted(11, 70, 71, 72);
        log.ShellImageListExtractionCompleted(11, 73, 74, 75, 76, 77);
        log.LoadWorkerReleased(11, 14, 3);

        Assert.AreEqual(34, listener.Events.Count);
        Assert.IsFalse(listener.Events.Any(e => e.EventId == 0), listener.GetEventSourceErrors());

        CollectionAssert.AreEqual(
            new object?[] { 11L, 12L, 13, 1.5 },
            listener.GetEvent(1).Payload!.ToArray());
        CollectionAssert.AreEqual(
            new object?[] { 11L, 32, true, 33L },
            listener.GetEvent(13).Payload!.ToArray());
        CollectionAssert.AreEqual(
            new object?[] { 11L, 12L, 42L, 43, "ListItem / SingleRow" },
            listener.GetEvent(19).Payload!.ToArray());
        CollectionAssert.AreEqual(
            new object?[] { 11L, 14L, 1, 47L, 48L, 49L, 4, 50L },
            listener.GetEvent(21).Payload!.ToArray());
        CollectionAssert.AreEqual(
            new object?[] { 11L, 58, 59L, 60L },
            listener.GetEvent(22).Payload!.ToArray());
        CollectionAssert.AreEqual(
            new object?[] { 11L, 1, 61L },
            listener.GetEvent(23).Payload!.ToArray());
        CollectionAssert.AreEqual(
            new object?[] { 11L, 62L },
            listener.GetEvent(24).Payload!.ToArray());
        CollectionAssert.AreEqual(
            new object?[] { 11L, 63, 64L },
            listener.GetEvent(25).Payload!.ToArray());
        CollectionAssert.AreEqual(
            new object?[] { 11L, 65, 66, 67L, 68L },
            listener.GetEvent(26).Payload!.ToArray());
        CollectionAssert.AreEqual(
            new object?[] { 11L, 14L, 52, 53, true, 54L },
            listener.GetEvent(35).Payload!.ToArray());
        CollectionAssert.AreEqual(
            new object?[] { 11L, 14L, 55, false, 56L },
            listener.GetEvent(36).Payload!.ToArray());
        CollectionAssert.AreEqual(
            new object?[] { 11L, 69L },
            listener.GetEvent(38).Payload!.ToArray());
        CollectionAssert.AreEqual(
            new object?[] { 11L, 70, 71, 72L },
            listener.GetEvent(39).Payload!.ToArray());
        CollectionAssert.AreEqual(
            new object?[] { 11L, 73, 74, 75, 76, 77L },
            listener.GetEvent(40).Payload!.ToArray());
        CollectionAssert.AreEqual(
            new object?[] { 11L, 14L, 3L },
            listener.GetEvent(41).Payload!.ToArray());
    }

    private sealed class CollectingEventListener : EventListener
    {
        internal ConcurrentQueue<EventWrittenEventArgs> Events { get; } = new();

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "Microsoft.PowerToys.CmdPal.IconLoading")
            {
                EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            Events.Enqueue(eventData);
        }

        internal EventWrittenEventArgs GetEvent(int eventId)
        {
            return Events.Single(e => e.EventId == eventId);
        }

        internal string GetEventSourceErrors()
        {
            return string.Join(
                Environment.NewLine,
                Events.Where(e => e.EventId == 0).Select(e => string.Join(", ", e.Payload ?? [])));
        }
    }
}
