using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Text;

namespace Microsoft.PowerLauncher.Telemetry
{
    /// <summary>
    /// ETW event for when a result is actioned.
    /// </summary>
    [EventData]
    public class ResultActionEvent : IEvent
    {
        public string EventName { get; } = "PowerLauncher_Result_ActionEvent";

        public enum TriggerType
        {
            Click,
            KeyboardShortcut
        }

        public TriggerType Trigger { get; set; }
        public string PluginName { get; set; }
        public string ActionName { get; set; }
    }
}
