using System.Diagnostics.Tracing;
using Telemetry.Events;

namespace Microsoft.PowerLauncher.Telemetry
{
    [EventData]
    public class LauncherShowEvent : IEvent
    {
        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
