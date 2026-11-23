// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Helpers;

internal readonly struct IconRequestMeasurement
{
    private readonly long _startedAt;

    internal IconLoadDiagnosticsSession? Session { get; }

    internal long Id { get; }

    internal IconRequestMeasurement(IconLoadDiagnosticsSession session, long id, long startedAt)
    {
        Session = session;
        Id = id;
        _startedAt = startedAt;
    }

    public void RecordProviderResolution(IconProviderResolution resolution, IconLoadMeasurement? load)
    {
        if (Session is not { } session)
        {
            return;
        }

        var loadId = load is not null && ReferenceEquals(load.Session, session) ? load.Id : 0;
        session.RecordProviderResolution(Id, loadId, resolution);
    }

    public void RecordProviderResolution(IconProviderResolution resolution, Task<IconSource?> task)
    {
        RecordProviderResolution(resolution, Session?.FindLoad(task));
    }

    public void Invalidate()
    {
        Session?.InvalidateRequest(Id);
    }

    public void Complete(IconRequestStatus status, IconSource? result = null)
    {
        if (Session is not { } session)
        {
            return;
        }

        var resultKind = status == IconRequestStatus.Failed
            ? IconLoadResultKind.Failed
            : IconLoadDiagnostics.ClassifyResult(result);
        session.CompleteRequest(Id, status, resultKind, Stopwatch.GetTimestamp() - _startedAt);
    }
}
