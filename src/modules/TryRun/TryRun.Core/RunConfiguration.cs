// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public sealed record RunConfiguration
{
    public WorkloadKind Kind { get; init; }

    public string Script { get; init; } = string.Empty;

    public string? ApplicationPath { get; init; }

    public string? WorkloadFile { get; init; }

    public string? FileRelativePath { get; init; }

    public string? WorkingSubdirectory { get; init; }

    public string[] InputPaths { get; init; } = [];

    public string[] Arguments { get; init; } = [];

    public PolicySettings? Policy { get; init; }

    public int TimeoutSeconds { get; init; } = 60;

    public string Image { get; init; } = "alpine:3.22";

    public string? ImageTarPath { get; init; }

    public string? Interpreter { get; init; }

    public bool CaptureDenials { get; init; }

    public bool IsolationDemo { get; init; }

    public bool PrepareImage { get; init; }

    public PolicyProfileReference? Profile { get; init; }
}
