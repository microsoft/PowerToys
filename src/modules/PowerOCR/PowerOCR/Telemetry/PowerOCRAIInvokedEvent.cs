// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace PowerOCR.Telemetry
{
    [EventData]
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
    public class PowerOCRAIInvokedEvent : EventBase, IEvent
    {
        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;

        // Name of backend model (placeholder until AI model integrated)
        public string Backend { get; set; } = "AI";

        // Duration in milliseconds
        public int DurationMs { get; set; }

        // Whether operation succeeded without fallback
        public bool Success { get; set; }
    }
}
