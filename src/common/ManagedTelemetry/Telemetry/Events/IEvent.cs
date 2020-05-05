using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PowerToys.Telemetry
{
    public interface IEvent
    {
        string EventName { get; }
    }
}
