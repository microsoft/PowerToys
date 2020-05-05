using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;

namespace Microsoft.PowerToys.Settings.Telemetry
{
    [EventData]
    public class EnabledModuleEvent : IEvent
    {
        public string EventName { get; } = "Settings_EnableModule";

        public string ModuleName { get; set; }

        public bool Value { get; set; }
    }
}
