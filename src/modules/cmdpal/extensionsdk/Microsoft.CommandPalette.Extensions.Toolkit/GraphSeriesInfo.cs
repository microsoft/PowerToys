// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Describes a graph series. Metadata is immutable after initialization and may
/// be shared by independent configuration reads.
/// </summary>
public sealed partial class GraphSeriesInfo : IGraphSeriesInfo
{
    public string Name { get; init; } = string.Empty;

    public OptionalColor Color { get; init; }

    public GraphLineStyle LineStyle { get; init; }

    public bool IsReadoutOnly { get; init; }

    public string ReadoutValueSuffix { get; init; } = string.Empty;
}
