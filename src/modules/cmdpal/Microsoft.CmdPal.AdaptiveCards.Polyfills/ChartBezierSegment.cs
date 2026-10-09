// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>A cubic Bezier segment that starts where the previous segment ended.</summary>
internal readonly record struct ChartBezierSegment(ChartPoint Control1, ChartPoint Control2, ChartPoint End);
