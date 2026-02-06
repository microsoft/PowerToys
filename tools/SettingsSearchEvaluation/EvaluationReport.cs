// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace SettingsSearchEvaluation;

internal sealed class EvaluationReport
{
    public required DateTimeOffset GeneratedAtUtc { get; init; }

    public required string IndexJsonPath { get; init; }

    public required DatasetDiagnostics Dataset { get; init; }

    public required int CaseCount { get; init; }

    public required IReadOnlyList<EngineEvaluationReport> Engines { get; init; }
}
