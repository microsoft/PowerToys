using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Text;
using Microsoft.PowerToys.Telemetry;

namespace Microsoft.PowerLauncher.Telemetry
{
    /// <summary>
    /// ETW Event for when the user initiates a query
    /// </summary>
    [EventData]
    public class QueryEvent : IEvent
    {
        public string EventName { get; } = "PowerLauncher_Query_Event";
        public double QueryTimeMs { get; set; }
        public int QueryLength { get; set; }
        public int NumResults { get; set; }
    }

}
