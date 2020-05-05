using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Text;
using Microsoft.PowerToys.Telemetry;

namespace Microsoft.PowerLauncher.Telemetry
{
    [EventData]
    public class FirstDeleteEvent : IEvent
    {
        public string EventName { get; } = "PowerLauncher_FirstDelete_Event";
    }
}
