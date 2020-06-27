using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Text;

namespace Microsoft.PowerToys.Telemetry.Events
{
    [EventData]
    public class DebugEvent : EventBase, IEvent
    {
        public string Message { get; set; }
        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServicePerformance;
    }
}
