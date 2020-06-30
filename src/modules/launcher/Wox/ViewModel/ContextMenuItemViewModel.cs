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

        private string title;
        private string glyph;
        private string fontfamily;
        private ICommand command;

        public string Title {
            get
            {
                return this.title;
            }

            set
            {
                if (value != this.title)
                {
                    this.title = value;
                    OnPropertyChanged("Title");
                }
            }
        }
        public string Glyph {
            get
            {
                return this.glyph;
            }

            set
            {
                if (value != this.glyph)
                {
                    this.glyph = value;
                    OnPropertyChanged("Glyph");
                }
            }
        }
        public string FontFamily {
            get
            {
                return this.fontfamily;
            }

            set
            {
                if (value != this.fontfamily)
                {
                    this.fontfamily = value;
                    OnPropertyChanged("FontFamily");
                }
            }
        }
        public ICommand Command {
            get
            {
                return this.command;
            }

            set
            {
                if (value != this.command)
                {
                    this.command = value;
                    OnPropertyChanged("Command");
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