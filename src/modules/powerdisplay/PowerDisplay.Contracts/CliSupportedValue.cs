// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Contracts;

/// <summary>
/// A discrete-value choice carried in error details so users can self-correct.
/// </summary>
public sealed class CliSupportedValue
{
    public string Name { get; init; } = string.Empty;

    public string Vcp { get; init; } = string.Empty;
}
