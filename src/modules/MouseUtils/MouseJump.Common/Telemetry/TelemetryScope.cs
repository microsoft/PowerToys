// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace MouseJump.Common.Telemetry;

/// <summary>
/// A pure ambient-context scope opened via <see cref="TelemetryContext.BeginScope"/> - carries
/// key-value properties for anything recorded while it's open, but records no duration of its
/// own and writes nothing when disposed beyond closing the underlying <see cref="Activity"/>.
/// Deliberately a distinct type from <see cref="TelemetryTimer"/> (rather than both just
/// returning <see cref="IDisposable"/>) so "this carries context but isn't itself a measured
/// span" is visible at the call site, not just a naming convention.
/// </summary>
public sealed class TelemetryScope : IDisposable
{
    internal TelemetryScope(Activity? activity)
    {
        this.Activity = activity;
    }

    private Activity? Activity
    {
        get;
    }

    public void Dispose()
        => this.Activity?.Dispose();
}
