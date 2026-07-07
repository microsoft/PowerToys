// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Contracts;

public sealed class CliApplyProfileResult
{
    // Response discriminator (see CliResponseHeader): apply-profile is always a success envelope.
    // A missing or invalid profile is reported separately as a CliErrorResult, not here.
    public bool IsError { get; init; }

    public string Version { get; init; } = CliSchema.Version;

    public string Command { get; init; } = CliCommandNames.ApplyProfile;

    public int ProfileId { get; init; }

    public string Profile { get; init; } = string.Empty;
}
