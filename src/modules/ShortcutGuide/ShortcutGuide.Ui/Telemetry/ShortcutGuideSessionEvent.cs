// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace ShortcutGuide.Telemetry;

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class ShortcutGuideSessionEvent : EventBase, IEvent
{
    public long DurationInMs { get; }

    public string CloseType { get; }

    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServicePerformance;

    public ShortcutGuideSessionEvent(long durationInMs, string closeType)
    {
        DurationInMs = durationInMs;
        CloseType = closeType;
    }
}
