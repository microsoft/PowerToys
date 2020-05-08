using System.Diagnostics.Tracing;

namespace Microsoft.PowerToys.Settings.Telemetry
{
    [EventData]
    public class SettingsEnabledEvent
    {
        public string Name { get; set; }

        public bool Value { get; set; }
    }
}
