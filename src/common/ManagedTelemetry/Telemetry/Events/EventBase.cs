using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Text;

namespace Microsoft.PowerToys.Telemetry.Events
{
    /// <summary>
    /// A base class to implement properties that are common to all telemetry events. 
    /// </summary>
    [EventData]
    public class EventBase
    {
        public bool UTCReplace_AppSessionGuid => true;
    }
}
