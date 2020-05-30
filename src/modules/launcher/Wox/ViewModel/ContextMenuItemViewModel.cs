using Microsoft.PowerLauncher.Telemetry;
using Microsoft.PowerToys.Telemetry;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Input;
using Wox.Plugin;

namespace Wox.ViewModel
{
    public class ContextMenuItemViewModel : BaseModel
    {
        public string PluginName { get; set; }
        public string Title { get; set; }
        public string Glyph { get; set; }
        public string FontFamily { get; set; }
        public ICommand Command { get; set; }
        public Key AcceleratorKey { get; set; }
        public ModifierKeys AcceleratorModifiers { get; set; }
        public bool IsAcceleratorKeyEnabled { get; set; }

        public void SendTelemetryEvent(LauncherResultActionEvent.TriggerType triggerType)
        {
            var eventData = new LauncherResultActionEvent()
            {
                PluginName = PluginName,
                Trigger = triggerType.ToString(),
                ActionName = Title

            };
            PowerToysTelemetry.Log.WriteEvent(eventData);
        }
    }
}