using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;
using System.Diagnostics.Tracing;

namespace Microsoft.PowerLauncher.Telemetry
{
    [EventData]
    public class LauncherBootEvent : EventBase, IEvent
    {
        public double BootTimeMs { get; set; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServicePerformance;
    }
}
