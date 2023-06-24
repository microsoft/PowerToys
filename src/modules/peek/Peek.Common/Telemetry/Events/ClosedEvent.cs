// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace Peek.UI.Telemetry.Events
{
    [EventData]
    public class ClosedEvent : EventBase, IEvent
    {
        public ClosedEvent()
        {
            EventName = "Peek_Closed";
        }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
