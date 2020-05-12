using System.Diagnostics.Tracing;
using Telemetry.Events;

namespace Microsoft.PowerLauncher.Telemetry
{
    /// <summary>
    /// ETW event for when a result is actioned.
    /// </summary>
    [EventData]
    public class LauncherResultActionEvent : IEvent
    {

        public enum TriggerType
        {
            Click,
            KeyboardShortcut
        }

        public string Trigger { get; set; }
        public string PluginName { get; set; }
        public string ActionName { get; set; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
