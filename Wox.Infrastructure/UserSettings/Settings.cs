using System;
using System.Collections.ObjectModel;
using System.Drawing;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Wox.Plugin;

namespace Wox.Infrastructure.UserSettings
{
    public class Settings : BaseModel
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
        public ObservableCollection<CustomPluginHotkey> CustomPluginHotkeys { get; set; } = new ObservableCollection<CustomPluginHotkey>();

        [Obsolete]
        public double Opacity { get; set; } = 1;

        [Obsolete]
        public OpacityMode OpacityMode { get; set; } = OpacityMode.Normal;

        public bool DontPromptUpdateMsg { get; set; }
        public bool EnableUpdateLog { get; set; }

        public bool StartWoxOnSystemStartup { get; set; } = true;
        public bool HideOnStartup { get; set; }
        bool _hideNotifyIcon { get; set; }
        public bool HideNotifyIcon
        {
            get { return _hideNotifyIcon; }
            set
            {
                _hideNotifyIcon = value;
                OnPropertyChanged();
            }
        }
        public bool LeaveCmdOpen { get; set; }
        public bool HideWhenDeactive { get; set; }
        public bool RememberLastLaunchLocation { get; set; }
        public bool IgnoreHotkeysOnFullscreen { get; set; }

        public HttpProxy Proxy { get; set; } = new HttpProxy();

        [JsonConverter(typeof(StringEnumConverter))]
        public LastQueryMode LastQueryMode { get; set; } = LastQueryMode.Selected;

    }

    public enum LastQueryMode
    {
        Selected,
        Empty,
        Preserved
    }

    [Obsolete]
    public enum OpacityMode
    {
        Normal = 0,
        LayeredWindow = 1,
        DWM = 2
    }
}