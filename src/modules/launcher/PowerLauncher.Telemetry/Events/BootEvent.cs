using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Text;

namespace Microsoft.PowerLauncher.Telemetry
{
    [EventData]
    public class BootEvent : IEvent
    {
        public string EventName { get; } = "PowerLauncher_Boot_Event";

        public double BootTimeMs { get; set; }
    }
}
