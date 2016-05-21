using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wox.Core.Resource;
using Wox.Core.UserSettings;
using Wox.Infrastructure.Storage;

namespace Wox.ViewModel
{
    public class SettingWindowViewModel
    {
        private readonly JsonStrorage<Settings> _storage;
        public Settings Settings { get; set; }
        public List<Language> Languages => InternationalizationManager.Instance.LoadAvailableLanguages();
        public IEnumerable<int> MaxResultsRange => Enumerable.Range(2, 16);
        public SettingWindowViewModel()
        {
            _storage = new JsonStrorage<Settings>();
            Settings = _storage.Load();
        }

        public void Save()
        {
            _storage.Save();
        }
    }
}
