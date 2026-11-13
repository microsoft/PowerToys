// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills;

/// <summary>One sample: its optional <c>x</c> label and its <c>y</c> value.</summary>
internal readonly record struct LineChartPoint(string? Label, double Y);
