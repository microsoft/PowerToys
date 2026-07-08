// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace Microsoft.CmdPal.UI.Events;

/// <summary>
/// Tracks which main-page search result the user selected (invoked).
/// Purpose: measure whether the ranking overhaul surfaces the wanted result near the top.
/// Privacy: only non-identifying aggregates are captured. The selected item's title,
/// subtitle, id, and the raw query text are never logged - only the query character
/// length, the zero-based rank (index) of the selected result, and its ranker tier name.
/// Emission goes through <see cref="PowerToysTelemetry"/>, which respects the existing
/// PowerToys data-diagnostics consent gate.
/// </summary>
[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class CmdPalSearchResultSelected : EventBase, IEvent
{
    /// <summary>
    /// Gets or sets the character length of the query at selection (never the query text).
    /// </summary>
    public int QueryLength { get; set; }

    /// <summary>
    /// Gets or sets the zero-based rank of the selected result within the visible results.
    /// </summary>
    public int SelectedIndex { get; set; }

    /// <summary>
    /// Gets or sets the ranker tier name of the selected result (e.g. "ExactTitle").
    /// This is a fixed, non-identifying enum name, not user content.
    /// </summary>
    public string SelectedTier { get; set; }

    public CmdPalSearchResultSelected(int queryLength, int selectedIndex, string selectedTier)
    {
        EventName = "CmdPal_SearchResultSelected";
        QueryLength = queryLength;
        SelectedIndex = selectedIndex;
        SelectedTier = selectedTier;
    }

    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
