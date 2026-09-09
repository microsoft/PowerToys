// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Describes an immutable display scale for graph measurements.
/// </summary>
public sealed partial class GraphValueScale : IGraphValueScale
{
    public double Divisor { get; init; }

    public string Suffix { get; init; } = string.Empty;
}
