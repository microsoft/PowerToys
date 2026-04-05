// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class FileConverterViewModel : Observable
    {
        private readonly GeneralSettings _generalSettingsConfig;

        private readonly Func<string, int> _sendConfigMessage;

        private bool _isEnabled;

        public FileConverterViewModel(
            SettingsUtils settingsUtils,
            ISettingsRepository<GeneralSettings> settingsRepository,
            Func<string, int> ipcMessageCallback)
        {
            ArgumentNullException.ThrowIfNull(settingsUtils);
            ArgumentNullException.ThrowIfNull(settingsRepository);

            _generalSettingsConfig = settingsRepository.SettingsConfig;
            _sendConfigMessage = ipcMessageCallback;

            _isEnabled = _generalSettingsConfig.Enabled.FileConverter;
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    _generalSettingsConfig.Enabled.FileConverter = value;

                    OutGoingGeneralSettings outgoing = new(_generalSettingsConfig);
                    _sendConfigMessage?.Invoke(outgoing.ToString());

                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        public void RefreshEnabledState()
        {
            _isEnabled = _generalSettingsConfig.Enabled.FileConverter;
            OnPropertyChanged(nameof(IsEnabled));
        }
    }
}
