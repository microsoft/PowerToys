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
    public class PowerOCRAIFallbackEvent : EventBase, IEvent
    {
        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;

        // Reason bucket (LoadFailure, InferenceError, Timeout, NotUsable)
        public string Reason { get; set; } = string.Empty;
    }
}
