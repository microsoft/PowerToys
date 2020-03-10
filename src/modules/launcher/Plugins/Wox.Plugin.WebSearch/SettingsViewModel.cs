using Wox.Infrastructure.Storage;

namespace Wox.Plugin.WebSearch
{
    public class SettingsViewModel
    {
        private readonly PluginJsonStorage<Settings> _storage;

        public SettingsViewModel()
        {
            _storage = new PluginJsonStorage<Settings>();
            Settings = _storage.Load();
        }

        public Settings Settings { get; set; }

        public void Save()
        {
            _storage.Save();
        }
    }
}