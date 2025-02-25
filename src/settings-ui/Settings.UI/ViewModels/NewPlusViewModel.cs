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
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Windows.ApplicationModel.VoiceCommands;
using Windows.System;

using static Microsoft.PowerToys.Settings.UI.Helpers.ShellGetFolder;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class NewPlusViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ISettingsUtils _settingsUtils;

        private NewPlusSettings Settings { get; set; }

        private const string ModuleName = NewPlusSettings.ModuleName;

        public NewPlusViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));

            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            Settings = LoadSettings(settingsUtils);

            // Initialize properties
            _hideFileExtension = Settings.Properties.HideFileExtension.Value;
            _hideStartingDigits = Settings.Properties.HideStartingDigits.Value;
            _templateLocation = Settings.Properties.TemplateLocation.Value;
            InitializeEnabledValue();
            InitializeGpoValues();

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredNewPlusEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isNewPlusEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isNewPlusEnabled = GeneralSettingsConfig.Enabled.NewPlus;
            }
        }

        private void InitializeGpoValues()
        {
            // Policy for hide file extension setting
            _hideFileExtensionGpoRuleConfiguration = GPOWrapper.GetConfiguredNewPlusHideTemplateFilenameExtensionValue();
            _hideFileExtensionIsGPOConfigured = _hideFileExtensionGpoRuleConfiguration == GpoRuleConfigured.Disabled || _hideFileExtensionGpoRuleConfiguration == GpoRuleConfigured.Enabled;
        }

        public bool IsEnabled
        {
            get => _isNewPlusEnabled;
            set
            {
                if (_isNewPlusEnabled != value)
                {
                    _isNewPlusEnabled = value;

                    GeneralSettingsConfig.Enabled.NewPlus = value;
                    OnPropertyChanged(nameof(IsEnabled));
                    OnPropertyChanged(nameof(IsHideFileExtSettingsCardEnabled));
                    OnPropertyChanged(nameof(IsHideFileExtSettingGPOConfigured));

                    OutGoingGeneralSettings outgoingMessage = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoingMessage.ToString());

                    NotifySettingsChanged();

                    if (_isNewPlusEnabled == true)
                    {
                        CopyTemplateExamples(_templateLocation);
                    }
                }
            }
        }

        public bool IsWin10OrLower
        {
            get => !OSVersionHelper.IsWindows11();
        }

        public string TemplateLocation
        {
            get => _templateLocation;
            set
            {
                if (_templateLocation != value)
                {
                    _templateLocation = value;
                    Settings.Properties.TemplateLocation.Value = value;
                    OnPropertyChanged(nameof(TemplateLocation));

                    NotifySettingsChanged();
                }
            }
        }

        public bool HideFileExtension
        {
            get
            {
                if (_hideFileExtensionIsGPOConfigured)
                {
                    return _hideFileExtensionGpoRuleConfiguration == GpoRuleConfigured.Enabled;
                }

                return _hideFileExtension;
            }

            set
            {
                if (_hideFileExtension != value && !_hideFileExtensionIsGPOConfigured)
                {
                    _hideFileExtension = value;
                    Settings.Properties.HideFileExtension.Value = value;
                    OnPropertyChanged(nameof(HideFileExtension));

                    NotifySettingsChanged();
                }
            }
        }

        public bool IsHideFileExtSettingsCardEnabled => _isNewPlusEnabled && !_hideFileExtensionIsGPOConfigured;

        public bool IsHideFileExtSettingGPOConfigured => _isNewPlusEnabled && _hideFileExtensionIsGPOConfigured;

        public bool HideStartingDigits
        {
            get => _hideStartingDigits;
            set
            {
                if (_hideStartingDigits != value)
                {
                    _hideStartingDigits = value;
                    Settings.Properties.HideStartingDigits.Value = value;
                    OnPropertyChanged(nameof(HideStartingDigits));

                    NotifySettingsChanged();
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
                       JsonSerializer.Serialize(Settings, SourceGenerationContextContext.Default.NewPlusSettings)));
        }

        private Func<string, int> SendConfigMSG { get; }

        public static NewPlusSettings LoadSettings(ISettingsUtils settingsUtils)
        {
            NewPlusSettings settings = null;

            try
            {
                settings = settingsUtils.GetSettingsOrDefault<NewPlusSettings>(NewPlusSettings.ModuleName);

                if (string.IsNullOrEmpty(settings.Properties.TemplateLocation.Value))
                {
                    // This can happen when running the DEBUG Settings application without first letting the runner create the default settings file.
                    settings.Properties.TemplateLocation.Value = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "NewPlus", "Templates");
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception encountered while reading {NewPlusSettings.ModuleName} settings.", e);
            }

            return settings;
        }

        public static void CopyTemplateExamples(string templateLocation)
        {
            if (!Directory.Exists(templateLocation))
            {
                Directory.CreateDirectory(templateLocation);
            }

            if (Directory.GetFiles(templateLocation).Length == 0 && Directory.GetDirectories(templateLocation).Length == 0)
            {
                // No files in templateLocation directory
                // Copy over examples files from <Program Files>\PowerToys\WinUI3Apps\Assets\NewPlus\Templates
                var example_templates = Path.Combine(Helper.GetPowerToysInstallationWinUI3AppsAssetsFolder(), "NewPlus", "Templates");
                Helper.CopyDirectory(example_templates, templateLocation, true);
            }
        }

        private bool _isNewPlusEnabled;
        private string _templateLocation;
        private bool _hideFileExtension;
        private bool _hideStartingDigits;

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private GpoRuleConfigured _hideFileExtensionGpoRuleConfiguration;
        private bool _hideFileExtensionIsGPOConfigured;

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }

        private void OpenNewTemplateFolder()
        {
            try
            {
                CopyTemplateExamples(_templateLocation);

                var process = new ProcessStartInfo()
                {
                    FileName = _templateLocation,
                    UseShellExecute = true,
                };
                Process.Start(process);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to show NewPlus template folder.", ex);
            }
        }

        private async void PickNewTemplateFolder()
        {
            var newPath = await PickFolderDialog();
            if (!string.IsNullOrEmpty(newPath))
            {
                TemplateLocation = newPath;
            }
        }

        private async Task<string> PickFolderDialog()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.GetSettingsWindow());
            return await Task.FromResult(GetFolderDialogWithFlags(hwnd, FolderDialogFlags._BIF_NEWDIALOGSTYLE));
        }
    }
}
