// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace SettingsSearchEvaluation;

internal sealed class LatencySummary
{
    public int Samples { get; init; }

    public double MinMs { get; init; }

    public double P50Ms { get; init; }

    public double P95Ms { get; init; }

    public double MaxMs { get; init; }

    public double AverageMs { get; init; }

    public static LatencySummary Empty { get; } = new();
}
