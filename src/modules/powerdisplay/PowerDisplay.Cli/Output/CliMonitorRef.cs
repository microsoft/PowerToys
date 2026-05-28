// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Cli.Output;

/// <summary>
/// Compact identification of a monitor used inside every JSON response so
/// consumers can correlate the result back to a single physical device.
/// </summary>
public sealed class CliMonitorRef
{
    public int Number { get; init; }

    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}
