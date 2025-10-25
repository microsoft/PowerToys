// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace PowerDisplay.Telemetry.Events
{
    [EventData]
    public class PowerDisplayStartEvent : EventBase, IEvent
    {
        public new string EventName => "PowerDisplay_Start";

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
