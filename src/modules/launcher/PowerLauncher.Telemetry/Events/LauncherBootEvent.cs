using System.Diagnostics.Tracing;

namespace Microsoft.PowerLauncher.Telemetry
{
    [EventData]
    public class LauncherBootEvent 
    {
        public double BootTimeMs { get; set; }
    }
}
