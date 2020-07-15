using Wox.Core.Resource;
using Wox.Infrastructure.Storage;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;

namespace Wox.ViewModel
{
    public class SettingWindowViewModel : BaseModel
    {
        private readonly WoxJsonStorage<Settings> _storage;

        public SettingWindowViewModel()
        {
            _storage = new WoxJsonStorage<Settings>();
            Settings = _storage.Load();
            Settings.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Settings.ActivateTimes))
                {
                    OnPropertyChanged(nameof(ActivatedTimes));
                }
            };
        }

        public Settings Settings { get; set; }

        public void Save()
        {
            _storage.Save();
        }

        #region general      

        private Internationalization _translater => InternationalizationManager.Instance;

        #endregion

        #region about

        public string ActivatedTimes => string.Format(_translater.GetTranslation("about_activate_times"), Settings.ActivateTimes);
        
        #endregion
    }
}
