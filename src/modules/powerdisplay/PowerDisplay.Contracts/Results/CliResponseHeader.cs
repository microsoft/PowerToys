// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Contracts;

/// <summary>
/// Minimal header the CLI dispatcher deserializes from any IPC response to read the
/// <see cref="IsError"/> discriminator before it knows the concrete result type. Every response
/// carries <c>isError</c>: success DTOs emit <see langword="false"/>, error envelopes
/// (<see cref="CliErrorResult"/>) emit <see langword="true"/>. This makes the success/error split
/// an explicit, app-set field rather than an inference over the response shape.
/// </summary>
public sealed class CliResponseHeader
{
    public bool IsError { get; init; }
}
