using System.Diagnostics.Tracing;
using Telemetry.Events;

namespace Microsoft.PowerLauncher.Telemetry
{
    [EventData]
    public class LauncherHideEvent : IEvent
    {
        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
