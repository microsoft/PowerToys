// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Wox.Infrastructure.Storage;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;

namespace PowerLauncher.ViewModel
{
    public class SettingWindowViewModel : BaseModel
    {
        private readonly WoxJsonStorage<PowerToysRunSettings> _storage;

        public SettingWindowViewModel()
        {
            _storage = new WoxJsonStorage<PowerToysRunSettings>();
            Settings = _storage.Load();
        }

        public PowerToysRunSettings Settings { get; }

        public void Save()
        {
            _storage.Save();
        }
    }
}
