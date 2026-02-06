// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace SettingsSearchEvaluation;

internal sealed class QueryEvaluationResult
{
    public required string Query { get; init; }

    public required IReadOnlyList<string> ExpectedIds { get; init; }

    public required IReadOnlyList<string> TopResultIds { get; init; }

    public required int BestRank { get; init; }

    public required bool HitAtK { get; init; }

    public string? Notes { get; init; }
}
