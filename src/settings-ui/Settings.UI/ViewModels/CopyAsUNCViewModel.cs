// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.Json;

using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.SerializationContext;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class CopyAsUNCViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly SettingsUtils _settingsUtils;

        private CopyAsUNCSettings Settings { get; set; }

        private const string ModuleName = CopyAsUNCSettings.ModuleName;

        private string _settingsConfigFileFolder = string.Empty;

        public CopyAsUNCViewModel(SettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc, string configFileSubfolder = "")
        {
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));

            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            try
            {
                CopyAsUNCLocalProperties localSettings = _settingsUtils.GetSettingsOrDefault<CopyAsUNCLocalProperties>(GetSettingsSubPath(), "copy-as-unc-settings.json");
                Settings = new CopyAsUNCSettings(localSettings);
            }
            catch (Exception)
            {
                CopyAsUNCLocalProperties localSettings = new CopyAsUNCLocalProperties();
                Settings = new CopyAsUNCSettings(localSettings);
                _settingsUtils.SaveSettings(localSettings.ToJsonString(), GetSettingsSubPath(), "copy-as-unc-settings.json");
            }

            InitializeEnabledValue();

            SendConfigMSG = ipcMSGCallBackFunc;

            _copyAsUNCEnabledOnContextExtendedMenu = Settings.Properties.ExtendedContextMenuOnly.Value;
        }

        public string GetSettingsSubPath()
        {
            return _settingsConfigFileFolder + "\\" + ModuleName;
        }

        private void InitializeEnabledValue()
        {
            // TODO: Replace with GPOWrapper.GetConfiguredCopyAsUNCEnabledValue() once GPO entry is added
            _enabledStateIsGPOConfigured = false;
            _isCopyAsUNCEnabled = GeneralSettingsConfig.Enabled.CopyAsUNC;
        }

        public bool IsCopyAsUNCEnabled
        {
            get => _isCopyAsUNCEnabled;
            set
            {
                if (_enabledStateIsGPOConfigured)
                {
                    return;
                }

                if (_isCopyAsUNCEnabled != value)
                {
                    _isCopyAsUNCEnabled = value;

                    GeneralSettingsConfig.Enabled.CopyAsUNC = value;
                    OnPropertyChanged(nameof(IsCopyAsUNCEnabled));

                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());

                    NotifySettingsChanged();
                }
            }
        }

        public bool EnabledOnContextExtendedMenu
        {
            get => _copyAsUNCEnabledOnContextExtendedMenu;
            set
            {
                if (value != _copyAsUNCEnabledOnContextExtendedMenu)
                {
                    _copyAsUNCEnabledOnContextExtendedMenu = value;
                    Settings.Properties.ExtendedContextMenuOnly.Value = value;
                    OnPropertyChanged(nameof(EnabledOnContextExtendedMenu));
                    NotifySettingsChanged();
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        private void NotifySettingsChanged()
        {
            SendConfigMSG(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                    CopyAsUNCSettings.ModuleName,
                    JsonSerializer.Serialize(Settings, SourceGenerationContextContext.Default.CopyAsUNCSettings)));
        }

        private Func<string, int> SendConfigMSG { get; }

        private bool _enabledStateIsGPOConfigured;
        private bool _isCopyAsUNCEnabled;
        private bool _copyAsUNCEnabledOnContextExtendedMenu;

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsCopyAsUNCEnabled));
        }
    }
}
