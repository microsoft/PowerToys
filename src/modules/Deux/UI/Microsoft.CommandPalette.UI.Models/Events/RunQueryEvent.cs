// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace Microsoft.CommandPalette.UI.Models.Events;

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class RunQueryEvent : EventBase, IEvent
{
    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;

    public string Query { get; set; }

    public int ResultCount { get; set; }

    public ulong DurationMs { get; set; }

    public RunQueryEvent(string query, int resultCount, ulong durationMs)
    {
        EventName = "CmdPal_RunQuery";
        Query = query;
        ResultCount = resultCount;
        DurationMs = durationMs;
    }
}
