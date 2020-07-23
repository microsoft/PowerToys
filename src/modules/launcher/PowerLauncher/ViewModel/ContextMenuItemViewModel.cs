using Microsoft.PowerLauncher.Telemetry;
using Microsoft.PowerToys.Telemetry;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Input;
using Wox.Plugin;

namespace PowerLauncher.ViewModel
{
    public class ContextMenuItemViewModel : BaseModel
    {
        private ICommand _command;

        public string PluginName { get; set; }
        public string Title { get; set; }
        public string Glyph { get; set; }
        public string FontFamily { get; set; }
        public ICommand Command
        {
            get
            {
                return this._command;
            }

            set
            {
                // ICommand does not implement the INotifyPropertyChanged interface and must call OnPropertyChanged() to prevent memory leaks
                if (value != this._command)
                {
                    this._command = value;
                    OnPropertyChanged();
                }
            }
        }

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