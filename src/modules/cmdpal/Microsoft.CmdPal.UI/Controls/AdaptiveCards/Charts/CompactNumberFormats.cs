// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>
/// The formats that shorten large chart values, where <c>{0}</c> is the scaled number: for
/// example, <c>{0}K</c> shows 12,345 as 12.3K.
/// </summary>
/// <param name="Thousands">The format for units of 10^3.</param>
/// <param name="Millions">The format for units of 10^6.</param>
/// <param name="Billions">The format for units of 10^9.</param>
/// <param name="Trillions">The format for units of 10^12.</param>
internal sealed record CompactNumberFormats(
    CompositeFormat Thousands,
    CompositeFormat Millions,
    CompositeFormat Billions,
    CompositeFormat Trillions)
{
    /// <summary>Gets the English formats: K, M, B, and T.</summary>
    public static CompactNumberFormats English { get; } = new(
        CompositeFormat.Parse("{0}K"),
        CompositeFormat.Parse("{0}M"),
        CompositeFormat.Parse("{0}B"),
        CompositeFormat.Parse("{0}T"));
}
