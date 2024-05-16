// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Windows.ApplicationModel.VoiceCommands;
using Windows.System;
using static Microsoft.PowerToys.Settings.UI.Helpers.ShellGetFolder;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class NewViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ISettingsUtils _settingsUtils;

        private NewSettings Settings { get; set; }

        private const string ModuleName = NewSettings.ModuleName;

        public NewViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));

            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            try
            {
                Settings = _settingsUtils.GetSettings<NewSettings>(ModuleName);
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception encountered while reading {ModuleName} settings.", e);

                // This code path should never happen
                Settings = new NewSettings();
                Settings.InitializeWithDefaultSettings();
                SaveSettingsToJson();
            }

            // Initialize properties
            _hideFileExtension = Settings.HideFileExtension;
            _templateLocation = Settings.TemplateLocation;
            InitializeEnabledValue();

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredNewEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isNewEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isNewEnabled = GeneralSettingsConfig.Enabled.NewPlus;
            }
        }

        public bool IsEnabled
        {
            get => _isNewEnabled;
            set
            {
                if (_isNewEnabled != value)
                {
                    _isNewEnabled = value;

                    GeneralSettingsConfig.Enabled.NewPlus = value;
                    OnPropertyChanged(nameof(IsEnabled));

                    OutGoingGeneralSettings outgoingMessage = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoingMessage.ToString());

                    NotifySettingsChanged();
                }
            }
        }

        public string TemplateLocation
        {
            get => _templateLocation;
            set
            {
                if (_templateLocation != value)
                {
                    _templateLocation = value;
                    Settings.TemplateLocation = value;
                    OnPropertyChanged(nameof(TemplateLocation));

                    NotifySettingsChanged();

                    SaveSettingsToJson();
                }
            }
        }

        public bool HideFileExtension
        {
            get => _hideFileExtension;
            set
            {
                if (_hideFileExtension != value)
                {
                    _hideFileExtension = value;
                    Settings.HideFileExtension = value;
                    OnPropertyChanged(nameof(HideFileExtension));

                    NotifySettingsChanged();

                    SaveSettingsToJson();
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public ButtonClickCommand OpenCurrentNewTemplateFolder => new ButtonClickCommand(OpenNewTemplateFolder);

        public ButtonClickCommand PickAnotherNewTemplateFolder => new ButtonClickCommand(PickNewTemplateFolder);

        private void NotifySettingsChanged()
        {
            // Using InvariantCulture as this is an IPC message
            SendConfigMSG(
                   string.Format(
                       CultureInfo.InvariantCulture,
                       "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                       ModuleName,
                       JsonSerializer.Serialize(Settings)));
        }

        private Func<string, int> SendConfigMSG { get; }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isNewEnabled;
        private string _templateLocation;
        private bool _hideFileExtension;

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }

        private void OpenNewTemplateFolder()
        {
            var process = new ProcessStartInfo()
            {
                FileName = _templateLocation,
                UseShellExecute = true,
            };
            Process.Start(process);
        }

        private async void PickNewTemplateFolder()
        {
            var newPath = await PickAFolderDialog();
            if (newPath.Length > 1)
            {
                TemplateLocation = newPath;
            }
        }

        private async Task<string> PickAFolderDialog()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.GetSettingsWindow());
            string pathFolder = await Task.FromResult<string>(ShellGetFolder.GetFolderDialogWithFlags(hwnd, ShellGetFolder.FolderDialogFlags._BIF_NEWDIALOGSTYLE));
            return pathFolder;
        }

        private void SaveSettingsToJson()
        {
            _settingsUtils.SaveSettings(Settings.ToJsonString(), ModuleName);
        }
    }
}
