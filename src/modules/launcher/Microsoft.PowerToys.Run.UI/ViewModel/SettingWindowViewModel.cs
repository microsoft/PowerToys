// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.PowerToys.Run.Infrastructure.Storage;
using Microsoft.PowerToys.Run.Infrastructure.UserSettings;
using Microsoft.PowerToys.Run.Plugin;

namespace Microsoft.PowerToys.Run.UI.ViewModel
{
    public class SettingWindowViewModel : BaseModel
    {
        private readonly WoxJsonStorage<PowerToysRunSettings> _storage;

        public SettingWindowViewModel()
        {
            _storage = new WoxJsonStorage<PowerToysRunSettings>();
            Settings = _storage.Load();
        }

        public PowerToysRunSettings Settings { get; set; }

        public void Save()
        {
            _storage.Save();
        }
    }
}
