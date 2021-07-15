// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Input;
using Microsoft.PowerLauncher.Telemetry;
using Microsoft.PowerToys.Telemetry;
using Wox.Plugin;

namespace PowerLauncher.ViewModel
{
    public class ContextMenuItemViewModel : BaseModel
    {
        private ICommand _command;

        public string PluginName { get; }

        public string Title { get; }

        public string Glyph { get; }

        public string FontFamily { get; }

        public ICommand Command
        {
            get
            {
                return _command;
            }

            set
            {
                // ICommand does not implement the INotifyPropertyChanged interface and must call OnPropertyChanged() to prevent memory leaks
                if (value != _command)
                {
                    _command = value;
                    OnPropertyChanged();
                }
            }
        }

        public Key AcceleratorKey { get; }

        public ModifierKeys AcceleratorModifiers { get; }

        public bool IsAcceleratorKeyEnabled { get; set; }

        public ContextMenuItemViewModel(string pluginName, string title, string glyph, string fontFamily, Key acceleratorKey, ModifierKeys acceleratorModifiers, ICommand command)
        {
            PluginName = pluginName;
            Title = title;
            Glyph = glyph;
            FontFamily = fontFamily;
            Command = command;
            AcceleratorKey = acceleratorKey;
            AcceleratorModifiers = acceleratorModifiers;
        }

        public void SendTelemetryEvent(LauncherResultActionEvent.TriggerType triggerType)
        {
            var eventData = new LauncherResultActionEvent()
            {
                PluginName = PluginName,
                Trigger = triggerType.ToString(),
                ActionName = Title,
            };
            PowerToysTelemetry.Log.WriteEvent(eventData);
        }

        public override string ToString()
        {
            return Title;
        }
    }
}
