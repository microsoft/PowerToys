// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Contracts;

/// <summary>
/// One row in the <c>profiles</c> list: a saved profile's name, how many monitors it
/// targets, and when it was last modified.
/// </summary>
public sealed class CliProfileInfo
{
    public string Name { get; init; } = string.Empty;

    public int MonitorCount { get; init; }

    /// <summary>Last-modified timestamp in ISO 8601 round-trip format, or null if unknown.</summary>
    public string? LastModified { get; init; }
}
