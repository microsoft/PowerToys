using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PowerLauncher.Telemetry
{
    public interface IEvent
    {
        string EventName { get; }
    }
}
