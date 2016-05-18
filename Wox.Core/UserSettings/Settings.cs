using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Wox.Core.Plugin;
using Wox.Plugin;
using Newtonsoft.Json;

namespace Wox.Core.UserSettings
{
    public class Settings
    {
        public string Hotkey { get; set; } = "Alt + Space";
        public string Language { get; set; } = "en";
        public string Theme { get; set; } = "Dark";
        public string QueryBoxFont { get; set; } = FontFamily.GenericSansSerif.Name;
        public string QueryBoxFontStyle { get; set; }
        public string QueryBoxFontWeight { get; set; }
        public string QueryBoxFontStretch { get; set; }
        public string ResultFont { get; set; } = FontFamily.GenericSansSerif.Name;
        public string ResultFontStyle { get; set; }
        public string ResultFontWeight { get; set; }
        public string ResultFontStretch { get; set; }

        public bool AutoUpdates { get; set; } = true;

        public double WindowLeft { get; set; }
        public double WindowTop { get; set; }
        public int MaxResultsToShow { get; set; } = 6;
        public int ActivateTimes { get; set; }

        // Order defaults to 0 or -1, so 1 will let this property appear last
        [JsonProperty(Order = 1)]
        public PluginsSettings PluginSettings { get; set; } = new PluginsSettings();
        public List<CustomPluginHotkey> CustomPluginHotkeys { get; set; } = new List<CustomPluginHotkey>();

        [Obsolete]
        public double Opacity { get; set; } = 1;

        [Obsolete]
        public OpacityMode OpacityMode { get; set; } = OpacityMode.Normal;

        public bool DontPromptUpdateMsg { get; set; }
        public bool EnableUpdateLog { get; set; }

        public bool StartWoxOnSystemStartup { get; set; } = true;
        public bool HideOnStartup { get; set; }
        public bool LeaveCmdOpen { get; set; }
        public bool HideWhenDeactive { get; set; }
        public bool RememberLastLaunchLocation { get; set; }
        public bool IgnoreHotkeysOnFullscreen { get; set; }

        public string ProxyServer { get; set; }
        public bool ProxyEnabled { get; set; }
        public int ProxyPort { get; set; }
        public string ProxyUserName { get; set; }
        public string ProxyPassword { get; set; }
    }

    [Obsolete]
    public enum OpacityMode
    {
        Normal = 0,
        LayeredWindow = 1,
        DWM = 2
    }
}