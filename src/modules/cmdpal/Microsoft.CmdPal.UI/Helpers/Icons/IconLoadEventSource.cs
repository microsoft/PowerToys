// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;

namespace Microsoft.CmdPal.UI.Helpers;

[EventSource(Name = "Microsoft.PowerToys.CmdPal.IconLoading")]
internal sealed partial class IconLoadEventSource : EventSource
{
    public static IconLoadEventSource Log { get; } = new();

    private IconLoadEventSource()
    {
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
    [Event(37, Level = EventLevel.Informational)]
    public void UiResponsivenessProbeCompleted(long sessionId, long elapsedMicroseconds)
    {
        if (!IsEnabled())
        {
            return;
        }

        WriteEvent(37, sessionId, elapsedMicroseconds);
    }
}
