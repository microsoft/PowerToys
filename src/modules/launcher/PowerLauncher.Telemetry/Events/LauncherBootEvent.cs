using System.Diagnostics.Tracing;
using Telemetry.Events;

namespace Microsoft.PowerLauncher.Telemetry
{
    [EventData]
    public class LauncherBootEvent : IEvent
    {
        public double BootTimeMs { get; set; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServicePerformance;
    }
}
