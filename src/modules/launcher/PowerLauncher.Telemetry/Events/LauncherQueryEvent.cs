using System.Diagnostics.Tracing;
using Telemetry.Events;

namespace Microsoft.PowerLauncher.Telemetry
{
    /// <summary>
    /// ETW Event for when the user initiates a query
    /// </summary>
    [EventData]
    public class LauncherQueryEvent : IEvent
    {
        public double QueryTimeMs { get; set; }
        public int QueryLength { get; set; }
        public int NumResults { get; set; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServicePerformance;
    }

}
