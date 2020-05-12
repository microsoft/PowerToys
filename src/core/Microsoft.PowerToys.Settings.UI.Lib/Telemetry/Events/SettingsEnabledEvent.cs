using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;
using System.Diagnostics.Tracing;

namespace Microsoft.PowerToys.Settings.Telemetry
{
    [EventData]
    public class SettingsEnabledEvent : IEvent
    {
        public string Name { get; set; }

        public bool Value { get; set; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
