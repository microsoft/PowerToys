// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class PowerRenameViewModel : Observable, IAsyncInitializable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly SettingsUtils _settingsUtils;

        private const string ModuleName = PowerRenameSettings.ModuleName;

        private string _settingsConfigFileFolder = string.Empty;

        private PowerRenameSettings Settings { get; set; }

        private Func<string, int> SendConfigMSG { get; }

        public PowerRenameViewModel(SettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc, string configFileSubfolder = "")
        {
            // Update Settings file folder:
            _settingsConfigFileFolder = configFileSubfolder;
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));

            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            // Initialize with defaults - heavy I/O deferred to InitializeAsync
            Settings = new PowerRenameSettings(new PowerRenameLocalProperties());

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            // Initialize extension helpers
            HeifExtension = new StoreExtensionHelper(
                "Microsoft.HEIFImageExtension_8wekyb3d8bbwe",
                "ms-windows-store://pdp/?ProductId=9PMMSR1CGPWG",
                "HEIF");

            AvifExtension = new StoreExtensionHelper(
                "Microsoft.AV1VideoExtension_8wekyb3d8bbwe",
                "ms-windows-store://pdp/?ProductId=9MVZQVXJBQ9V",
                "AV1");

            InitializeEnabledValue();
        }

        /// <summary>
        /// Gets a value indicating whether the ViewModel has been initialized.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Gets a value indicating whether initialization is in progress.
        /// </summary>
        public bool IsLoading { get; private set; }

        /// <summary>
        /// Initializes the ViewModel asynchronously, loading settings from disk.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the async operation.</returns>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (IsInitialized)
            {
                return;
            }

            IsLoading = true;
            OnPropertyChanged(nameof(IsLoading));

            try
            {
                await Task.Run(
                    () =>
                    {
                        LoadSettingsFromDisk();
                    },
                    cancellationToken);

                IsInitialized = true;
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(IsLoading));
                OnPropertyChanged(nameof(IsInitialized));
            }
        }

        private void LoadSettingsFromDisk()
        {
            try
            {
                PowerRenameLocalProperties localSettings = _settingsUtils.GetSettingsOrDefault<PowerRenameLocalProperties>(GetSettingsSubPath(), "power-rename-settings.json");
                Settings = new PowerRenameSettings(localSettings);
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception encountered while reading {ModuleName} settings.", e);
#if DEBUG
                if (e is ArgumentException || e is ArgumentNullException || e is PathTooLongException)
                {
                    throw;
                }
#endif
                PowerRenameLocalProperties localSettings = new PowerRenameLocalProperties();
                Settings = new PowerRenameSettings(localSettings);
                _settingsUtils.SaveSettings(localSettings.ToJsonString(), GetSettingsSubPath(), "power-rename-settings.json");
            }

            _powerRenameEnabledOnContextMenu = Settings.Properties.ShowIcon.Value;
            _powerRenameEnabledOnContextExtendedMenu = Settings.Properties.ExtendedContextMenuOnly.Value;
            _powerRenameRestoreFlagsOnLaunch = Settings.Properties.PersistState.Value;
            _powerRenameMaxDispListNumValue = Settings.Properties.MaxMRUSize.Value;
            _autoComplete = Settings.Properties.MRUEnabled.Value;
            _powerRenameUseBoostLib = Settings.Properties.UseBoostLib.Value;

            // Notify UI of property changes
            OnPropertyChanged(nameof(EnabledOnContextMenu));
            OnPropertyChanged(nameof(EnabledOnContextExtendedMenu));
            OnPropertyChanged(nameof(RestoreFlagsOnLaunch));
            OnPropertyChanged(nameof(MaxDispListNum));
            OnPropertyChanged(nameof(MRUEnabled));
            OnPropertyChanged(nameof(UseBoostLib));
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredPowerRenameEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _powerRenameEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _powerRenameEnabled = GeneralSettingsConfig.Enabled.PowerRename;
            }
        }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _powerRenameEnabled;
        private bool _powerRenameEnabledOnContextMenu;
        private bool _powerRenameEnabledOnContextExtendedMenu;
        private bool _powerRenameRestoreFlagsOnLaunch;
        private int _powerRenameMaxDispListNumValue;
        private bool _autoComplete;
        private bool _powerRenameUseBoostLib;

        public bool IsEnabled
        {
            get
            {
                return _powerRenameEnabled;
            }

            set
            {
                if (_enabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (value != _powerRenameEnabled)
                {
                    GeneralSettingsConfig.Enabled.PowerRename = value;
                    OutGoingGeneralSettings snd = new OutGoingGeneralSettings(GeneralSettingsConfig);

                    SendConfigMSG(snd.ToString());

                    _powerRenameEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                    RaisePropertyChanged(nameof(GlobalAndMruEnabled));
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public bool MRUEnabled
        {
            get
            {
                return _autoComplete;
            }

            set
            {
                if (value != _autoComplete)
                {
                    _autoComplete = value;
                    Settings.Properties.MRUEnabled.Value = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(GlobalAndMruEnabled));
                }
            }
        }

        public bool GlobalAndMruEnabled
        {
            get
            {
                return _autoComplete && _powerRenameEnabled;
            }
        }

        public bool EnabledOnContextMenu
        {
            get
            {
                return _powerRenameEnabledOnContextMenu;
            }

            set
            {
                if (value != _powerRenameEnabledOnContextMenu)
                {
                    _powerRenameEnabledOnContextMenu = value;
                    Settings.Properties.ShowIcon.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool EnabledOnContextExtendedMenu
        {
            get
            {
                return _powerRenameEnabledOnContextExtendedMenu;
            }

            set
            {
                if (value != _powerRenameEnabledOnContextExtendedMenu)
                {
                    _powerRenameEnabledOnContextExtendedMenu = value;
                    Settings.Properties.ExtendedContextMenuOnly.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool RestoreFlagsOnLaunch
        {
            get
            {
                return _powerRenameRestoreFlagsOnLaunch;
            }

            set
            {
                if (value != _powerRenameRestoreFlagsOnLaunch)
                {
                    _powerRenameRestoreFlagsOnLaunch = value;
                    Settings.Properties.PersistState.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public int MaxDispListNum
        {
            get
            {
                return _powerRenameMaxDispListNumValue;
            }

            set
            {
                if (value != _powerRenameMaxDispListNumValue)
                {
                    _powerRenameMaxDispListNumValue = value;
                    Settings.Properties.MaxMRUSize.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool UseBoostLib
        {
            get
            {
                return _powerRenameUseBoostLib;
            }

            set
            {
                if (value != _powerRenameUseBoostLib)
                {
                    _powerRenameUseBoostLib = value;
                    Settings.Properties.UseBoostLib.Value = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string GetSettingsSubPath()
        {
            return _settingsConfigFileFolder + "\\" + ModuleName;
        }

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Notify UI of property change
            OnPropertyChanged(propertyName);

            if (SendConfigMSG != null)
            {
                SndPowerRenameSettings snd = new SndPowerRenameSettings(Settings);
                SndModuleSettings<SndPowerRenameSettings> ipcMessage = new SndModuleSettings<SndPowerRenameSettings>(snd);
                SendConfigMSG(ipcMessage.ToJsonString());
            }
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(GlobalAndMruEnabled));
        }

        // Store extension helpers
        public StoreExtensionHelper HeifExtension { get; private set; }

        public StoreExtensionHelper AvifExtension { get; private set; }

        // Convenience properties for XAML binding
        public bool IsHeifExtensionInstalled => HeifExtension.IsInstalled;

        public bool IsAvifExtensionInstalled => AvifExtension.IsInstalled;

        public ICommand InstallHeifExtensionCommand => HeifExtension.InstallCommand;

        public ICommand InstallAvifExtensionCommand => AvifExtension.InstallCommand;

        public void RefreshHeifExtensionStatus()
        {
            HeifExtension.RefreshStatus();
            OnPropertyChanged(nameof(IsHeifExtensionInstalled));
        }

        public void RefreshAvifExtensionStatus()
        {
            AvifExtension.RefreshStatus();
            OnPropertyChanged(nameof(IsAvifExtensionInstalled));
        }
    }
}
