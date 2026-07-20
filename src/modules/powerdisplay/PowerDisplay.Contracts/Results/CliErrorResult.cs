// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Contracts;

public sealed class CliErrorResult
{
    // Response discriminator (see CliResponseHeader): always true on an error envelope.
    public bool IsError { get; init; } = true;

    public string Version { get; init; } = CliSchema.Version;

    public string Command { get; init; } = string.Empty;

    public CliError Error { get; init; } = new();

    public CliMonitorRef? Monitor { get; init; }
}
