// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>One sample. A missing <see cref="Y"/> leaves a gap in the line.</summary>
internal readonly record struct LineChartPoint(string? Label, double? Y);
