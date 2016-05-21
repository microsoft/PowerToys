using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Core.UserSettings;
using Wox.Infrastructure.Storage;

namespace Wox.ViewModel
{
    public class SettingWindowViewModel
    {
        private const string StartupPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

        private readonly JsonStrorage<Settings> _storage;
        public Settings Settings { get; set; }
        public List<Language> Languages => InternationalizationManager.Instance.LoadAvailableLanguages();
        public IEnumerable<int> MaxResultsRange => Enumerable.Range(2, 16);
        public Tab SelectedTab { get; set; } = Tab.General;

        public SettingWindowViewModel()
        {
            _storage = new JsonStrorage<Settings>();
            Settings = _storage.Load();
        }

        public void Save()
        {
            _storage.Save();
        }

        public enum Tab
        {
            General = 0,
            Plugin = 1,
            Theme = 2,
            Hotkey = 3,
            Proxy = 4,
            About = 5
        }
    }
}
