// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace SettingsSearchEvaluation;

internal sealed class EngineEvaluationReport
{
    public required SearchEngineKind Engine { get; init; }

    public required bool IsAvailable { get; init; }

    public string? AvailabilityError { get; init; }

    public string? CapabilitiesSummary { get; init; }

    public int IndexedEntries { get; init; }

    public int QueryCount { get; init; }

    public double IndexingTimeMs { get; init; }

    public double RecallAtK { get; init; }

    public double Mrr { get; init; }

    public required LatencySummary SearchLatencyMs { get; init; }

    public required IReadOnlyList<QueryEvaluationResult> CaseResults { get; init; }
}
