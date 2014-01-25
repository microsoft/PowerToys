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
        IniParser parser = new IniParser("config.ini");

        public string Theme { get; set; }

        private Settings()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            if (!File.Exists(configPath)) File.Create(configPath);
            Theme = parser.GetSetting("ui", "theme");
        }

        public void SaveSettings()
        {
            parser.AddSetting("ui", "theme", Theme);
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
