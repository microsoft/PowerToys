using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using Wox.Core.Plugin;
using Wox.Infrastructure.Storage;
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

        public double WindowLeft { get; set; }
        public double WindowTop { get; set; }
        public int MaxResultsToShow { get; set; } = 6;
        public int ActivateTimes { get; set; }

        // Order defaults to 0 or -1, so 1 will let this property appear last
        [JsonProperty(Order = 1)]
        public Dictionary<string, PluginSettings> PluginSettings { get; set; } = new Dictionary<string, PluginSettings>();
        public List<CustomPluginHotkey> CustomPluginHotkeys { get; set; } = new List<CustomPluginHotkey>();

        [Obsolete]
        public double Opacity { get; set; } = 1;

        [Obsolete]
        public OpacityMode OpacityMode { get; set; } = OpacityMode.Normal;

        public bool DontPromptUpdateMsg { get; set; }
        public bool EnableUpdateLog { get; set; }

        public bool StartWoxOnSystemStartup { get; set; }
        public bool LeaveCmdOpen { get; set; }
        public bool HideWhenDeactive { get; set; }
        public bool RememberLastLaunchLocation { get; set; }
        public bool IgnoreHotkeysOnFullscreen { get; set; }

        public string ProxyServer { get; set; }
        public bool ProxyEnabled { get; set; }
        public int ProxyPort { get; set; }
        public string ProxyUserName { get; set; }
        public string ProxyPassword { get; set; }

        public void UpdatePluginSettings()
        {
            var metadatas = PluginManager.AllPlugins.Select(p => p.Metadata);
            if (PluginSettings == null)
            {
                var configs = new Dictionary<string, PluginSettings>();
                foreach (var metadata in metadatas)
                {
                    addPluginMetadata(configs, metadata);
                }
                PluginSettings = configs;
            }
            else
            {
                var configs = PluginSettings;
                foreach (var metadata in metadatas)
                {
                    if (configs.ContainsKey(metadata.ID))
                    {
                        var config = configs[metadata.ID];
                        if (config.ActionKeywords?.Count > 0)
                        {
                            metadata.ActionKeywords = config.ActionKeywords;
                            metadata.ActionKeyword = config.ActionKeywords[0];
                        }
                    }
                    else
                    {
                        addPluginMetadata(configs, metadata);
                    }
                }
            }
        }


        private void addPluginMetadata(Dictionary<string, PluginSettings> configs, PluginMetadata metadata)
        {
            configs[metadata.ID] = new PluginSettings
            {
                ID = metadata.ID,
                Name = metadata.Name,
                ActionKeywords = metadata.ActionKeywords,
                Disabled = false
            };
        }

        public void UpdateActionKeyword(PluginMetadata metadata)
        {
            var config = PluginSettings[metadata.ID];
            config.ActionKeywords = metadata.ActionKeywords;
        }

    }

    public enum OpacityMode
    {
        Normal = 0,
        LayeredWindow = 1,
        DWM = 2
    }
}