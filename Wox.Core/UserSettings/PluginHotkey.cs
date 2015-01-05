using System;

namespace Wox.Core.UserSettings
{
    [Serializable]
    public class CustomPluginHotkey
    {
        public string Hotkey { get; set; }
        public string ActionKeyword { get; set; }
    }
}
