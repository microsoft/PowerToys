using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Text;

namespace Microsoft.PowerLauncher.Telemetry
{
    [EventData]
    public class HideEvent : IEvent
    {
        public string EventName { get; } = "PowerLauncher_Hide_Event";
    }
}
