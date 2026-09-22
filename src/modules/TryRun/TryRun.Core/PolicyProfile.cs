// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public sealed record PolicyProfile
{
    public int SchemaVersion { get; init; } = 1;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name { get; init; } = string.Empty;

    public bool Linux { get; init; }

    public string Revision { get; init; } = string.Empty;

    public PolicySettings Policy { get; init; } = new();
}
