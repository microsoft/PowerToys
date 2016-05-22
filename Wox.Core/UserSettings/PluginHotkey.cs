using System;
using PropertyChanged;

namespace Wox.Core.UserSettings
{
    [ImplementPropertyChanged]
    public class CustomPluginHotkey
    {
        public string Hotkey { get; set; }
        public string ActionKeyword { get; set; }
    }
}
