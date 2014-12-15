using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Wox.Infrastructure.Storage.UserSettings
{
    public class UserSettingStorage : BaseStorage<UserSettingStorage>
    {
        [JsonProperty]
        public bool DontPromptUpdateMsg { get; set; }

        [JsonProperty]
        public string Hotkey { get; set; }

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
        public bool ReplaceWinR { get; set; }

        [JsonProperty]
        public List<WebSearch> WebSearches { get; set; }

        [JsonProperty]
        public double WindowLeft { get; set; }

        [JsonProperty]
        public double WindowTop { get; set; }

        [JsonProperty]
        public List<ProgramSource> ProgramSources { get; set; }

        [JsonProperty]
        public List<FolderLink> FolderLinks { get; set; }	//Aaron

        public List<CustomizedPluginConfig> CustomizedPluginConfigs { get; set; }

        [JsonProperty]
        public List<CustomPluginHotkey> CustomPluginHotkeys { get; set; }

        [JsonProperty]
        public bool StartWoxOnSystemStartup { get; set; }

        [JsonProperty]
        public double Opacity { get; set; }

        [JsonProperty]
        public string ProgramSuffixes { get; set; }

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

        protected override string ConfigName
        {
            get { return "config"; }
        }

        protected override UserSettingStorage LoadDefaultConfig()
        {
            DontPromptUpdateMsg = false;
            Theme = "Dark";
            ReplaceWinR = true;
            WebSearches = LoadDefaultWebSearches();
            ProgramSources = new List<ProgramSource>();
            CustomizedPluginConfigs = new List<CustomizedPluginConfig>();
            Hotkey = "Alt + Space";
            QueryBoxFont = FontFamily.GenericSansSerif.Name;
            ResultItemFont = FontFamily.GenericSansSerif.Name;
            Opacity = 1;
            OpacityMode = OpacityMode.Normal;
            LeaveCmdOpen = false;
            HideWhenDeactive = false;

            return this;
        }

        protected override void OnAfterLoadConfig(UserSettingStorage storage)
        {
            if (storage.CustomizedPluginConfigs == null)
            {
                storage.CustomizedPluginConfigs = new List<CustomizedPluginConfig>();
            }
            if (string.IsNullOrEmpty(storage.ProgramSuffixes))
            {
                storage.ProgramSuffixes = "lnk;exe;appref-ms;bat";
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
