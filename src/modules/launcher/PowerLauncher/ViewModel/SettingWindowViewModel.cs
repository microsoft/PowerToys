// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Wox.Core.Resource;
using Wox.Infrastructure.Storage;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;

namespace PowerLauncher.ViewModel
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

        private static Internationalization Translater => InternationalizationManager.Instance;

        public string ActivatedTimes => string.Format(CultureInfo.InvariantCulture, Translater.GetTranslation("about_activate_times"), Settings.ActivateTimes);
    }
}
