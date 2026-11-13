// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace Microsoft.CmdPal.UI.Events;

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class CmdPalHeartbeat : EventBase, IEvent
{
    public string Trigger { get; set; }

    public ulong UptimeMs { get; set; }

    public int LoadedExtensionCount { get; set; }

    public long WorkingSetBytes { get; set; }

    public long PrivateBytes { get; set; }

    public long ManagedHeapBytes { get; set; }

    public CmdPalHeartbeat(string trigger, ulong uptimeMs, int loadedExtensionCount, long workingSetBytes, long privateBytes, long managedHeapBytes)
    {
        EventName = "CmdPal_Heartbeat";
        Trigger = trigger;
        UptimeMs = uptimeMs;
        LoadedExtensionCount = loadedExtensionCount;
        WorkingSetBytes = workingSetBytes;
        PrivateBytes = privateBytes;
        ManagedHeapBytes = managedHeapBytes;
    }

    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServicePerformance;
}
