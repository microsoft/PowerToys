using System.Diagnostics.Tracing;

namespace Microsoft.PowerLauncher.Telemetry
{
    /// <summary>
    /// ETW event for when a result is actioned.
    /// </summary>
    [EventData]
    public class LauncherResultActionEvent 
    {

        public enum TriggerType
        {
            Click,
            KeyboardShortcut
        }

        public string Trigger { get; set; }
        public string PluginName { get; set; }
        public string ActionName { get; set; }
    }
}
