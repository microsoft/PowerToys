// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Wox.Infrastructure.Storage;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace PowerLauncher.ViewModel
{
    public class SettingWindowViewModel : BaseModel
    {
        private readonly WoxJsonStorage<PowerToysRunSettings> _storage;

        public SettingWindowViewModel()
        {
            _storage = new WoxJsonStorage<PowerToysRunSettings>();
            Settings = _storage.Load();

            // Check information file for version mismatch
            try
            {
                PowerToysRunSettings fields = _storage.ExtractFields(Settings, string.Empty);
                if (fields != null)
                {
                    if (_storage.CheckVersionMismatch(fields))
                    {
                        Settings = JsonSerializer.Deserialize<PowerToysRunSettings>("{}", _storage.GetJsonSerializerOptions());
                        if (!_storage.CheckWithInformatonFiletoClear(Settings))
                        {
                            _storage.Clear();
                            _storage.SaveInformationFile(Settings);
                        }
                    }
                }
            }
            catch (JsonException e)
            {
                Log.Exception($"Error in Load of PowerToysRunSettings: {e.Message}", e, GetType());
            }
        }

        public PowerToysRunSettings Settings { get; }

        public void Save()
        {
            _storage.Save();
        }
    }
}
