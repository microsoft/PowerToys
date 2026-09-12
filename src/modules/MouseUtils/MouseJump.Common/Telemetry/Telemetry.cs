// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseJump.Common.Telemetry;

/// <summary>
/// Holds the process-wide ambient <see cref="TelemetryContext"/> - kept separate from
/// <see cref="TelemetryContext"/> itself so that type can stay a plain instance class (easy to
/// construct more than one of, e.g. in tests) without also carrying global static state.
/// </summary>
public static class Telemetry
{
    private static TelemetryContext? current;

    /// <summary>
    /// Gets the ambient <see cref="TelemetryContext"/> for the whole process - defaults to one
    /// that hasn't been started, so any call site can use this safely (and for free - no
    /// <see cref="System.Diagnostics.ActivityListener"/>, no <see cref="System.Diagnostics.Activity"/> objects) even
    /// if nothing has called <see cref="SetCurrent"/> yet, e.g. a component under test that
    /// touches telemetry incidentally. Call <see cref="SetCurrent"/> at startup to route
    /// somewhere real instead - see <see cref="NullTelemetryWriter"/> for an explicit "started
    /// but discards everything" writer, if that's ever preferable to just not starting.
    /// </summary>
    public static TelemetryContext Current
        => Telemetry.current ??= TelemetryContext.Create(nameof(Telemetry));

    public static void SetCurrent(TelemetryContext context)
        => Telemetry.current = context ?? throw new ArgumentNullException(nameof(context));
}
