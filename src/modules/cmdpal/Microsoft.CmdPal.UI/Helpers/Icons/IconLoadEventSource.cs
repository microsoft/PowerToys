// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;

namespace Microsoft.CmdPal.UI.Helpers;

[EventSource(
    Name = "Microsoft.PowerToys.CmdPal.IconLoading",
    Guid = "AA068BA3-1767-5F92-7A9B-8F5DA0397413")]
internal sealed partial class IconLoadEventSource : EventSource
{
    public static IconLoadEventSource Log { get; } = new();

    private IconLoadEventSource()
    {
    }

    protected override void OnEventCommand(EventCommandEventArgs command)
    {
        base.OnEventCommand(command);
        if (command.Command == EventCommand.Disable && !IsEnabled())
        {
            IconLoadDiagnostics.OnEtwDisabled();
        }
    }

    [Event(1, Level = EventLevel.Informational)]
    public void RequestStarted(long sessionId, long requestId, int reason, double scale)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(1, sessionId, requestId, reason, scale);
    }

    [Event(2, Level = EventLevel.Informational)]
    public void ProviderResolved(long sessionId, long requestId, long loadId, int resolution)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(2, sessionId, requestId, loadId, resolution);
    }

    [Event(3, Level = EventLevel.Informational)]
    public void RequestCompleted(long sessionId, long requestId, int status, long elapsedMicroseconds)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(3, sessionId, requestId, status, elapsedMicroseconds);
    }

    [Event(4, Level = EventLevel.Informational)]
    public void LoadCreated(long sessionId, long loadId, int inputKind, double width, double height, double scale)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(4, sessionId, loadId, inputKind, width, height, scale);
    }

    [Event(5, Level = EventLevel.Informational)]
    public void LoadEnqueued(long sessionId, long loadId, int priority, long queueDepth)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(5, sessionId, loadId, priority, queueDepth);
    }

    [Event(6, Level = EventLevel.Warning)]
    public void LoadRejected(long sessionId, long loadId)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(6, sessionId, loadId);
    }

    [Event(7, Level = EventLevel.Informational)]
    public void LoadStarted(long sessionId, long loadId, long queueMicroseconds, long activeWorkers)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(7, sessionId, loadId, queueMicroseconds, activeWorkers);
    }

    [Event(8, Level = EventLevel.Informational)]
    public void LoadCompleted(long sessionId, long loadId, int resultKind, long elapsedMicroseconds)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(8, sessionId, loadId, resultKind, elapsedMicroseconds);
    }

    [Event(9, Level = EventLevel.Informational)]
    public void BackgroundPreparationCompleted(long sessionId, long loadId, long elapsedMicroseconds)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(9, sessionId, loadId, elapsedMicroseconds);
    }

    [Event(10, Level = EventLevel.Informational)]
    public void DispatcherWaitCompleted(long sessionId, long loadId, long elapsedMicroseconds)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(10, sessionId, loadId, elapsedMicroseconds);
    }

    [Event(11, Level = EventLevel.Informational)]
    public void DispatcherWorkCompleted(long sessionId, long loadId, long elapsedMicroseconds)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(11, sessionId, loadId, elapsedMicroseconds);
    }

    [Event(12, Level = EventLevel.Informational)]
    public void DirectGlyphLoadCompleted(long sessionId, long loadId, int resultKind, long elapsedMicroseconds)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(12, sessionId, loadId, resultKind, elapsedMicroseconds);
    }

    [Event(13, Level = EventLevel.Informational)]
    public void ElementUpdated(long sessionId, int resultKind, bool reused, long elapsedMicroseconds)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(13, sessionId, resultKind, reused, elapsedMicroseconds);
    }

    [Event(14, Level = EventLevel.Informational)]
    public void RequestAttributed(long sessionId, long requestId, int resolution, int resultKind, long elapsedMicroseconds)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(14, sessionId, requestId, resolution, resultKind, elapsedMicroseconds);
    }

    [Event(15, Level = EventLevel.Informational)]
    public void RequestInvalidated(long sessionId, long requestId, long loadId, int loadStage, int remainingLiveRequesters)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(15, sessionId, requestId, loadId, loadStage, remainingLiveRequesters);
    }

    [Event(16, Level = EventLevel.Informational)]
    public void LoadStartedWithoutRequester(long sessionId, long loadId, long elapsedMicroseconds)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(16, sessionId, loadId, elapsedMicroseconds);
    }

    [Event(17, Level = EventLevel.Informational)]
    public void LoadCompletedWithoutRequester(long sessionId, long loadId, long elapsedMicroseconds)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(17, sessionId, loadId, elapsedMicroseconds);
    }

    [Event(18, Level = EventLevel.Informational)]
    public void RetainedLoadCacheHit(long sessionId, long loadId, int cacheHitRequests)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(18, sessionId, loadId, cacheHitRequests);
    }

    [Event(19, Level = EventLevel.Informational)]
    public void RequestOrigin(long sessionId, long requestId, long iconBoxId, int requestSite, string diagnosticScope)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(19, sessionId, requestId, iconBoxId, requestSite, diagnosticScope);
    }

    [Event(20, Level = EventLevel.Informational)]
    public void LoadQueueDemandChanged(
        long sessionId,
        long loadId,
        int transition,
        long demandedQueueDepth,
        long speculativeQueueDepth)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(20, sessionId, loadId, transition, demandedQueueDepth, speculativeQueueDepth);
    }

    [Event(21, Level = EventLevel.Informational)]
    public void LoadDemandAtWorkerStart(
        long sessionId,
        long loadId,
        int demanded,
        long demandedQueueDepth,
        long speculativeQueueDepth,
        long activeWorkers,
        int workerCount,
        long demandedBeyondCapacity)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(
            21,
            sessionId,
            loadId,
            demanded,
            demandedQueueDepth,
            speculativeQueueDepth,
            activeWorkers,
            workerCount,
            demandedBeyondCapacity);
    }

    // Event IDs follow the final grouped diagnostics schema and intentionally remain sparse so
    // independently reviewable layers can land without changing an event's published identity.
    [Event(22, Level = EventLevel.Informational)]
    public void SchedulerCommandProcessed(long sessionId, int commandKind, long elapsedMicroseconds, long backlog)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(22, sessionId, commandKind, elapsedMicroseconds, backlog);
    }

    [Event(23, Level = EventLevel.Informational)]
    public void WorkerReadyToDispatchCompleted(long sessionId, int demanded, long elapsedMicroseconds)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(23, sessionId, demanded, elapsedMicroseconds);
    }

    [Event(24, Level = EventLevel.Informational)]
    public void DemandedIdleCapacityCompleted(long sessionId, long elapsedMicroseconds)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(24, sessionId, elapsedMicroseconds);
    }

    [Event(25, Level = EventLevel.Informational)]
    public void SchedulerCoordinatorWoke(long sessionId, int triggerKind, long elapsedMicroseconds)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(25, sessionId, triggerKind, elapsedMicroseconds);
    }

    [Event(26, Level = EventLevel.Informational)]
    public void SchedulerBatchCompleted(
        long sessionId,
        int commandCount,
        int dispatchedWorkItemCount,
        long drainMicroseconds,
        long passMicroseconds)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(
            26,
            sessionId,
            commandCount,
            dispatchedWorkItemCount,
            drainMicroseconds,
            passMicroseconds);
    }

    [Event(34, Level = EventLevel.Warning)]
    public void DispatcherWaitFailed(long sessionId, long loadId, long elapsedMicroseconds)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(34, sessionId, loadId, elapsedMicroseconds);
    }

    [Event(35, Level = EventLevel.Informational)]
    public void DispatcherUiSliceCompleted(
        long sessionId,
        long loadId,
        int materializationKind,
        int sliceKind,
        bool isDemanded,
        long elapsedMicroseconds)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(35, sessionId, loadId, materializationKind, sliceKind, isDemanded, elapsedMicroseconds);
    }

    [Event(36, Level = EventLevel.Informational)]
    public void DispatcherAsyncSuspensionCompleted(
        long sessionId,
        long loadId,
        int materializationKind,
        bool isDemanded,
        long elapsedMicroseconds)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(36, sessionId, loadId, materializationKind, isDemanded, elapsedMicroseconds);
    }

    [Event(37, Level = EventLevel.Informational)]
    public void UiResponsivenessProbeCompleted(long sessionId, long elapsedMicroseconds)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(37, sessionId, elapsedMicroseconds);
    }

    [Event(38, Level = EventLevel.Informational)]
    public void SpeculativeDispatchDeferralCompleted(long sessionId, long elapsedMicroseconds)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(38, sessionId, elapsedMicroseconds);
    }

    // These exact overloads intentionally shadow EventSource.WriteEvent(params object?[]).
    // The params overload allocates an array and boxes values while ETW is enabled, which
    // would make the icon diagnostics measurably perturb the paths they are observing.
    [NonEvent]
    private new unsafe void WriteEvent(int eventId, long value1, long value2)
    {
        EventData* data = stackalloc EventData[2];
        SetEventData(&data[0], &value1, sizeof(long));
        SetEventData(&data[1], &value2, sizeof(long));
        WritePrimitiveEvent(eventId, 2, data);
    }

    [NonEvent]
    private unsafe void WriteEvent(int eventId, long value1, int value2, long value3)
    {
        EventData* data = stackalloc EventData[3];
        SetEventData(&data[0], &value1, sizeof(long));
        SetEventData(&data[1], &value2, sizeof(int));
        SetEventData(&data[2], &value3, sizeof(long));
        WritePrimitiveEvent(eventId, 3, data);
    }

    [NonEvent]
    private unsafe void WriteEvent(int eventId, long value1, long value2, int value3)
    {
        EventData* data = stackalloc EventData[3];
        SetEventData(&data[0], &value1, sizeof(long));
        SetEventData(&data[1], &value2, sizeof(long));
        SetEventData(&data[2], &value3, sizeof(int));
        WritePrimitiveEvent(eventId, 3, data);
    }

    [NonEvent]
    private new unsafe void WriteEvent(int eventId, long value1, long value2, long value3)
    {
        EventData* data = stackalloc EventData[3];
        SetEventData(&data[0], &value1, sizeof(long));
        SetEventData(&data[1], &value2, sizeof(long));
        SetEventData(&data[2], &value3, sizeof(long));
        WritePrimitiveEvent(eventId, 3, data);
    }

    [NonEvent]
    private unsafe void WriteEvent(int eventId, long value1, long value2, int value3, double value4)
    {
        EventData* data = stackalloc EventData[4];
        SetEventData(&data[0], &value1, sizeof(long));
        SetEventData(&data[1], &value2, sizeof(long));
        SetEventData(&data[2], &value3, sizeof(int));
        SetEventData(&data[3], &value4, sizeof(double));
        WritePrimitiveEvent(eventId, 4, data);
    }

    [NonEvent]
    private unsafe void WriteEvent(int eventId, long value1, long value2, long value3, int value4)
    {
        EventData* data = stackalloc EventData[4];
        SetEventData(&data[0], &value1, sizeof(long));
        SetEventData(&data[1], &value2, sizeof(long));
        SetEventData(&data[2], &value3, sizeof(long));
        SetEventData(&data[3], &value4, sizeof(int));
        WritePrimitiveEvent(eventId, 4, data);
    }

    [NonEvent]
    private unsafe void WriteEvent(int eventId, long value1, long value2, int value3, long value4)
    {
        EventData* data = stackalloc EventData[4];
        SetEventData(&data[0], &value1, sizeof(long));
        SetEventData(&data[1], &value2, sizeof(long));
        SetEventData(&data[2], &value3, sizeof(int));
        SetEventData(&data[3], &value4, sizeof(long));
        WritePrimitiveEvent(eventId, 4, data);
    }

    [NonEvent]
    private unsafe void WriteEvent(int eventId, long value1, long value2, long value3, long value4)
    {
        EventData* data = stackalloc EventData[4];
        SetEventData(&data[0], &value1, sizeof(long));
        SetEventData(&data[1], &value2, sizeof(long));
        SetEventData(&data[2], &value3, sizeof(long));
        SetEventData(&data[3], &value4, sizeof(long));
        WritePrimitiveEvent(eventId, 4, data);
    }

    [NonEvent]
    private unsafe void WriteEvent(int eventId, long value1, int value2, long value3, long value4)
    {
        EventData* data = stackalloc EventData[4];
        SetEventData(&data[0], &value1, sizeof(long));
        SetEventData(&data[1], &value2, sizeof(int));
        SetEventData(&data[2], &value3, sizeof(long));
        SetEventData(&data[3], &value4, sizeof(long));
        WritePrimitiveEvent(eventId, 4, data);
    }

    [NonEvent]
    private unsafe void WriteEvent(int eventId, long value1, int value2, bool value3, long value4)
    {
        var boolValue3 = value3 ? 1 : 0;
        EventData* data = stackalloc EventData[4];
        SetEventData(&data[0], &value1, sizeof(long));
        SetEventData(&data[1], &value2, sizeof(int));
        SetEventData(&data[2], &boolValue3, sizeof(int));
        SetEventData(&data[3], &value4, sizeof(long));
        WritePrimitiveEvent(eventId, 4, data);
    }

    [NonEvent]
    private unsafe void WriteEvent(int eventId, long value1, long value2, int value3, int value4, long value5)
    {
        EventData* data = stackalloc EventData[5];
        SetEventData(&data[0], &value1, sizeof(long));
        SetEventData(&data[1], &value2, sizeof(long));
        SetEventData(&data[2], &value3, sizeof(int));
        SetEventData(&data[3], &value4, sizeof(int));
        SetEventData(&data[4], &value5, sizeof(long));
        WritePrimitiveEvent(eventId, 5, data);
    }

    [NonEvent]
    private unsafe void WriteEvent(int eventId, long value1, long value2, long value3, int value4, int value5)
    {
        EventData* data = stackalloc EventData[5];
        SetEventData(&data[0], &value1, sizeof(long));
        SetEventData(&data[1], &value2, sizeof(long));
        SetEventData(&data[2], &value3, sizeof(long));
        SetEventData(&data[3], &value4, sizeof(int));
        SetEventData(&data[4], &value5, sizeof(int));
        WritePrimitiveEvent(eventId, 5, data);
    }

    [NonEvent]
    private unsafe void WriteEvent(int eventId, long value1, long value2, int value3, long value4, long value5)
    {
        EventData* data = stackalloc EventData[5];
        SetEventData(&data[0], &value1, sizeof(long));
        SetEventData(&data[1], &value2, sizeof(long));
        SetEventData(&data[2], &value3, sizeof(int));
        SetEventData(&data[3], &value4, sizeof(long));
        SetEventData(&data[4], &value5, sizeof(long));
        WritePrimitiveEvent(eventId, 5, data);
    }

    [NonEvent]
    private unsafe void WriteEvent(int eventId, long value1, int value2, int value3, long value4, long value5)
    {
        EventData* data = stackalloc EventData[5];
        SetEventData(&data[0], &value1, sizeof(long));
        SetEventData(&data[1], &value2, sizeof(int));
        SetEventData(&data[2], &value3, sizeof(int));
        SetEventData(&data[3], &value4, sizeof(long));
        SetEventData(&data[4], &value5, sizeof(long));
        WritePrimitiveEvent(eventId, 5, data);
    }

    [NonEvent]
    private unsafe void WriteEvent(int eventId, long value1, long value2, int value3, bool value4, long value5)
    {
        var boolValue4 = value4 ? 1 : 0;
        EventData* data = stackalloc EventData[5];
        SetEventData(&data[0], &value1, sizeof(long));
        SetEventData(&data[1], &value2, sizeof(long));
        SetEventData(&data[2], &value3, sizeof(int));
        SetEventData(&data[3], &boolValue4, sizeof(int));
        SetEventData(&data[4], &value5, sizeof(long));
        WritePrimitiveEvent(eventId, 5, data);
    }

    [NonEvent]
    private unsafe void WriteEvent(int eventId, long value1, long value2, int value3, double value4, double value5, double value6)
    {
        EventData* data = stackalloc EventData[6];
        SetEventData(&data[0], &value1, sizeof(long));
        SetEventData(&data[1], &value2, sizeof(long));
        SetEventData(&data[2], &value3, sizeof(int));
        SetEventData(&data[3], &value4, sizeof(double));
        SetEventData(&data[4], &value5, sizeof(double));
        SetEventData(&data[5], &value6, sizeof(double));
        WritePrimitiveEvent(eventId, 6, data);
    }

    [NonEvent]
    private unsafe void WriteEvent(int eventId, long value1, long value2, int value3, int value4, bool value5, long value6)
    {
        var boolValue5 = value5 ? 1 : 0;
        EventData* data = stackalloc EventData[6];
        SetEventData(&data[0], &value1, sizeof(long));
        SetEventData(&data[1], &value2, sizeof(long));
        SetEventData(&data[2], &value3, sizeof(int));
        SetEventData(&data[3], &value4, sizeof(int));
        SetEventData(&data[4], &boolValue5, sizeof(int));
        SetEventData(&data[5], &value6, sizeof(long));
        WritePrimitiveEvent(eventId, 6, data);
    }

    [NonEvent]
    private unsafe void WriteEvent(int eventId, long value1, long value2, long value3, int value4, string value5)
    {
        value5 ??= string.Empty;
        fixed (char* value5Pointer = value5)
        {
            EventData* data = stackalloc EventData[5];
            SetEventData(&data[0], &value1, sizeof(long));
            SetEventData(&data[1], &value2, sizeof(long));
            SetEventData(&data[2], &value3, sizeof(long));
            SetEventData(&data[3], &value4, sizeof(int));
            SetEventData(&data[4], value5Pointer, checked((value5.Length + 1) * sizeof(char)));
            WritePrimitiveEvent(eventId, 5, data);
        }
    }

    [NonEvent]
    private unsafe void WriteEvent(
        int eventId,
        long value1,
        long value2,
        int value3,
        long value4,
        long value5,
        long value6,
        int value7,
        long value8)
    {
        EventData* data = stackalloc EventData[8];
        SetEventData(&data[0], &value1, sizeof(long));
        SetEventData(&data[1], &value2, sizeof(long));
        SetEventData(&data[2], &value3, sizeof(int));
        SetEventData(&data[3], &value4, sizeof(long));
        SetEventData(&data[4], &value5, sizeof(long));
        SetEventData(&data[5], &value6, sizeof(long));
        SetEventData(&data[6], &value7, sizeof(int));
        SetEventData(&data[7], &value8, sizeof(long));
        WritePrimitiveEvent(eventId, 8, data);
    }

    [NonEvent]
    private static unsafe void SetEventData(EventData* eventData, void* value, int size)
    {
        eventData->DataPointer = (IntPtr)value;
        eventData->Size = size;
    }

    [NonEvent]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Payload descriptors reference only primitive values or an explicitly pinned string buffer; no object graph is serialized.")]
    private unsafe void WritePrimitiveEvent(int eventId, int eventDataCount, EventData* data)
    {
        WriteEventCore(eventId, eventDataCount, data);
    }
}
