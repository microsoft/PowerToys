using System.Collections.Generic;
using System.IO;

namespace Wox.Infrastructure.UserSettings
{
    public class UserSetting
    {
        public string Hotkey { get; set; }
        public string Theme { get; set; }
        public bool ReplaceWinR { get; set; }
        public List<WebSearch> WebSearches { get; set; }
        public List<CustomPluginHotkey> CustomPluginHotkeys { get; set; }
        public bool StartWoxOnSystemStartup { get; set; }
        public bool EnablePythonPlugins { get; set; }

        public UserSetting()
        {
            EnablePythonPlugins = false;
        }

        public List<WebSearch> LoadDefaultWebSearches()
        {
            List<WebSearch> webSearches = new List<WebSearch>();

            WebSearch googleWebSearch = new WebSearch()
            {
                Title = "Google",
                ActionWord = "g",
                IconPath = Directory.GetCurrentDirectory() + @"\Images\websearch\google.png",
                Url = "https://www.google.com/search?q={q}",
                Enabled = true
            };
            webSearches.Add(googleWebSearch);

            
            WebSearch wikiWebSearch = new WebSearch()
            {
                Title = "Wikipedia",
                ActionWord = "wiki",
                IconPath = Directory.GetCurrentDirectory() + @"\Images\websearch\wiki.png",
                Url = "http://en.wikipedia.org/wiki/{q}",
                Enabled = true
            };
            webSearches.Add(wikiWebSearch);

            return webSearches;
        }
    }
}
