using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;
using System.Diagnostics.Tracing;

namespace Microsoft.PowerLauncher.Telemetry
{
    [EventData]
    public class LauncherBootEvent : EventBase, IEvent
    {
        /// <summary>
        /// TODO: This should be replaced by a P/Invoke call to get_product_version
        /// </summary>
        public string Version => "v0.18.3";

        public double BootTimeMs { get; set; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServicePerformance;
    }
}
