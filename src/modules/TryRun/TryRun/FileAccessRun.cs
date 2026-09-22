// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerToys.TryRun.Core;

namespace PowerToys.TryRun;

internal sealed record FileAccessRun(ExecutionRequest Request, string[] InputPaths, string Description)
{
    public string? WorkloadFile { get; init; }
}
