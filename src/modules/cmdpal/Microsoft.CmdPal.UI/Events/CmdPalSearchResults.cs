// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace Microsoft.CmdPal.UI.Events;

/// <summary>
/// Tracks the outcome of a settled main-page search query.
/// Purpose: measure search relevance and perceived speed for the ranking overhaul.
/// Privacy: only non-identifying aggregates are captured. The raw query text is never
/// logged - only its character length. No titles, subtitles, paths, or app names are
/// captured. Emission goes through <see cref="PowerToysTelemetry"/>, which respects the
/// existing PowerToys data-diagnostics consent gate.
/// </summary>
[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class CmdPalSearchResults : EventBase, IEvent
{
    /// <summary>
    /// Gets or sets the character length of the query (never the query text itself).
    /// </summary>
    public int QueryLength { get; set; }

    /// <summary>
    /// Gets or sets the number of deterministic first-paint results (commands and apps)
    /// produced for the query.
    /// </summary>
    public int ResultCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the query produced no deterministic results.
    /// </summary>
    public bool NoResults { get; set; }

    /// <summary>
    /// Gets or sets the time in milliseconds to produce the deterministic first-paint results.
    /// </summary>
    public ulong LatencyMs { get; set; }

    public CmdPalSearchResults(int queryLength, int resultCount, bool noResults, ulong latencyMs)
    {
        EventName = "CmdPal_SearchResults";
        QueryLength = queryLength;
        ResultCount = resultCount;
        NoResults = noResults;
        LatencyMs = latencyMs;
    }

    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
