using System.Diagnostics.Tracing;

namespace Microsoft.PowerToys.Settings.Telemetry
{
    [EventData]
    public class SettingsEnabledModuleEvent
    {
        public string ModuleName { get; set; }

        public bool Value { get; set; }
    }
}
