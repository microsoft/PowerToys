// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace SettingsSearchEvaluation;

internal sealed class RunnerOptions
{
    public required string IndexJsonPath { get; init; }

    public string? CasesJsonPath { get; init; }

    public required IReadOnlyList<SearchEngineKind> Engines { get; init; }

    public int MaxResults { get; init; } = 10;

    public int TopK { get; init; } = 5;

    public int Iterations { get; init; } = 5;

    public int WarmupIterations { get; init; } = 1;

    public TimeSpan SemanticIndexTimeout { get; init; } = TimeSpan.FromSeconds(15);

    public string? OutputJsonPath { get; init; }
}
