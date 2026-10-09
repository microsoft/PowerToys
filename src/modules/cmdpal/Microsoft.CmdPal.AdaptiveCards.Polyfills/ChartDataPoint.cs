// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills;

/// <summary>A labeled value, such as a donut slice, a bar, or a gauge segment.</summary>
internal readonly record struct ChartDataPoint(string? Label, double Value, string? Color);
