// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Contracts;

public sealed class CliSetResult
{
    // Response discriminator (see CliResponseHeader): false on success DTOs, true only on CliErrorResult.
    public bool IsError { get; init; }

    public string Version { get; init; } = CliSchema.Version;

    public string Command { get; init; } = CliCommandNames.Set;

    public CliMonitorRef Monitor { get; init; } = new();

    public string Setting { get; init; } = string.Empty;

    public string? BeforeDisplay { get; init; }

    public string AfterDisplay { get; init; } = string.Empty;
}
