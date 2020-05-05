using System.Diagnostics.Tracing;

namespace Microsoft.PowerLauncher.Telemetry
{
    /// <summary>
    /// ETW Event for when the user initiates a query
    /// </summary>
    [EventData]
    public class LauncherQueryEvent
    {
        public double QueryTimeMs { get; set; }
        public int QueryLength { get; set; }
        public int NumResults { get; set; }
    }

}
