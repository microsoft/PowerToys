using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WinAlfred.Helper
{
    public class Settings
    {
        private string configPath = Directory.GetCurrentDirectory() + "\\config.ini";
        private static readonly Settings settings = new Settings();
        IniParser parser;

        public string Theme { get; set; }
        public bool ReplaceWinR { get; set; }

        private Settings()
        {
            if (!File.Exists(configPath)) File.Create(configPath);
            parser = new IniParser(configPath);
            LoadSettings();
        }

        private void LoadSettings()
        {
            Theme = parser.GetSetting("ui", "theme");

            string replaceWinRStr = parser.GetSetting("hotkey", "replaceWinR");
            bool replace = true;
            if (bool.TryParse(replaceWinRStr, out replace))
            {
                ReplaceWinR = replace;
            }
        }

        public void SaveSettings()
        {
            parser.AddSetting("ui", "theme", Theme);
            parser.AddSetting("hotkey", "replaceWinR", ReplaceWinR.ToString());
            parser.SaveSettings();
        }

        public static Settings Instance
        {
            get
            {
                return settings;
            }
        }


    }
}
