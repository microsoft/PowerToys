using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;
using Wox.Plugin;
using System.Drawing;

namespace Wox.Core.UserSettings
{
    public class UserSettingStorage : JsonStrorage<UserSettingStorage>
    {
        [JsonProperty]
        public bool DontPromptUpdateMsg { get; set; }

        [JsonProperty]
        public string Hotkey { get; set; }

        [JsonProperty]
        public string Language { get; set; }

        [JsonProperty]
        public string Theme { get; set; }

        [JsonProperty]
        public string QueryBoxFont { get; set; }

        [JsonProperty]
        public string QueryBoxFontStyle { get; set; }

        [JsonProperty]
        public string QueryBoxFontWeight { get; set; }

        [JsonProperty]
        public string QueryBoxFontStretch { get; set; }

        [JsonProperty]
        public string ResultItemFont { get; set; }

        [JsonProperty]
        public string ResultItemFontStyle { get; set; }

        [JsonProperty]
        public string ResultItemFontWeight { get; set; }

        [JsonProperty]
        public string ResultItemFontStretch { get; set; }

        [JsonProperty]
        public List<WebSearch> WebSearches { get; set; }

        [JsonProperty]
        public double WindowLeft { get; set; }

        [JsonProperty]
        public double WindowTop { get; set; }

        public List<CustomizedPluginConfig> CustomizedPluginConfigs { get; set; }

        [JsonProperty]
        public List<CustomPluginHotkey> CustomPluginHotkeys { get; set; }

        [JsonProperty]
        public bool StartWoxOnSystemStartup { get; set; }

        [JsonProperty]
        public double Opacity { get; set; }

        [JsonProperty]
        public OpacityMode OpacityMode { get; set; }

        [JsonProperty]
        public bool EnableWebSearchSuggestion { get; set; }

        [JsonProperty]
        public string WebSearchSuggestionSource { get; set; }

        [JsonProperty]
        public bool LeaveCmdOpen { get; set; }

        [JsonProperty]
        public bool HideWhenDeactive { get; set; }

        [JsonProperty]
        public string ProxyServer { get; set; }

        [JsonProperty]
        public bool ProxyEnabled { get; set; }

        [JsonProperty]
        public int ProxyPort { get; set; }

        [JsonProperty]
        public string ProxyUserName { get; set; }

        [JsonProperty]
        public string ProxyPassword { get; set; }

        public List<WebSearch> LoadDefaultWebSearches()
        {
            List<WebSearch> webSearches = new List<WebSearch>();

            WebSearch googleWebSearch = new WebSearch()
            {
                Title = "Google",
                ActionWord = "g",
                IconPath = Path.GetDirectoryName(Application.ExecutablePath) + @"\Images\websearch\google.png",
                Url = "https://www.google.com/search?q={q}",
                Enabled = true
            };
            webSearches.Add(googleWebSearch);


            WebSearch wikiWebSearch = new WebSearch()
            {
                Title = "Wikipedia",
                ActionWord = "wiki",
                IconPath = Path.GetDirectoryName(Application.ExecutablePath) + @"\Images\websearch\wiki.png",
                Url = "http://en.wikipedia.org/wiki/{q}",
                Enabled = true
            };
            webSearches.Add(wikiWebSearch);

            WebSearch findIcon = new WebSearch()
            {
                Title = "FindIcon",
                ActionWord = "findicon",
                IconPath = Path.GetDirectoryName(Application.ExecutablePath) + @"\Images\websearch\pictures.png",
                Url = "http://findicons.com/search/{q}",
                Enabled = true
            };
            webSearches.Add(findIcon);

            return webSearches;
        }

        protected override string ConfigFolder
        {
            get
            {
                string userProfilePath = Environment.GetEnvironmentVariable("USERPROFILE");
                if (userProfilePath == null)
                {
                    throw new ArgumentException("Environment variable USERPROFILE is empty");
                }
                return Path.Combine(Path.Combine(userProfilePath, ".Wox"), "Config");
            }
        }

        protected override string ConfigName
        {
            get { return "config"; }
        }

        protected override UserSettingStorage LoadDefault()
        {
            DontPromptUpdateMsg = false;
            Theme = "Dark";
            Language = "en";
            WebSearches = LoadDefaultWebSearches();
            CustomizedPluginConfigs = new List<CustomizedPluginConfig>();
            Hotkey = "Alt + Space";
            QueryBoxFont = FontFamily.GenericSansSerif.Name;
            ResultItemFont = FontFamily.GenericSansSerif.Name;
            Opacity = 1;
            OpacityMode = OpacityMode.Normal;
            LeaveCmdOpen = false;
            HideWhenDeactive = false;
            CustomPluginHotkeys = new List<CustomPluginHotkey>()
            {
                new CustomPluginHotkey()
                {
                    ActionKeyword = "history ",
                    Hotkey = "Alt + H"
                }
            };
            return this;
        }

        protected override void OnAfterLoad(UserSettingStorage storage)
        {
            if (storage.CustomizedPluginConfigs == null)
            {
                storage.CustomizedPluginConfigs = new List<CustomizedPluginConfig>();
            }
            if (storage.QueryBoxFont == null)
            {
                storage.QueryBoxFont = FontFamily.GenericSansSerif.Name;
            }
            if (storage.ResultItemFont == null)
            {
                storage.ResultItemFont = FontFamily.GenericSansSerif.Name;
            }
            if (storage.Language == null)
            {
                storage.Language = "en";
            }
        }
    }

    public enum OpacityMode
    {
        Normal = 0,
        LayeredWindow = 1,
        DWM = 2
    }
}
