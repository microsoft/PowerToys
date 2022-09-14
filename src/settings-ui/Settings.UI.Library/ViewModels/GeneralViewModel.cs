// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.VisualBasic;
using Settings.UI.Library;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace Microsoft.PowerToys.Settings.UI.Library.ViewModels
{
    public class GeneralViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private UpdatingSettings UpdatingSettingsConfig { get; set; }

        private Action HideBackupAndRestoreMessageAreaAction { get; set; }

        public ButtonClickCommand CheckForUpdatesEventHandler { get; set; }

        public ButtonClickCommand BackupConfigsEventHandler { get; set; }

        public ButtonClickCommand RestoreConfigsEventHandler { get; set; }

        public ButtonClickCommand SelectSettingBackupDirEventHandler { get; set; }

        public ButtonClickCommand RestartElevatedButtonEventHandler { get; set; }

        public ButtonClickCommand RestartButtonEventHandler { get; set; }

        public ButtonClickCommand UpdateNowButtonEventHandler { get; set; }

        public Func<string, int> UpdateUIThemeCallBack { get; }

        public Func<string, int> SendConfigMSG { get; }

        public Func<string, int> SendRestartAsAdminConfigMSG { get; }

        public Func<string, int> SendCheckForUpdatesConfigMSG { get; }

        public string RunningAsUserDefaultText { get; set; }

        public string RunningAsAdminDefaultText { get; set; }

        private string _settingsConfigFileFolder = string.Empty;

        private IFileSystemWatcher _fileWatcher;

        public object ResourceLoader { get; set; }

        public GeneralViewModel(ISettingsRepository<GeneralSettings> settingsRepository, string runAsAdminText, string runAsUserText, bool isElevated, bool isAdmin, Func<string, int> updateTheme, Func<string, int> ipcMSGCallBackFunc, Func<string, int> ipcMSGRestartAsAdminMSGCallBackFunc, Func<string, int> ipcMSGCheckForUpdatesCallBackFunc, string configFileSubfolder = "", Action dispatcherAction = null, Action hideBackupAndRestoreMessageAreaAction = null, object resourceLoader = null)
        {
            CheckForUpdatesEventHandler = new ButtonClickCommand(CheckForUpdatesClick);
            RestartElevatedButtonEventHandler = new ButtonClickCommand(RestartElevated);
            UpdateNowButtonEventHandler = new ButtonClickCommand(UpdateNowClick);
            BackupConfigsEventHandler = new ButtonClickCommand(BackupConfigsClick);
            SelectSettingBackupDirEventHandler = new ButtonClickCommand(SelectSettingBackupDir);
            RestoreConfigsEventHandler = new ButtonClickCommand(RestoreConfigsClick);
            RestartButtonEventHandler = new ButtonClickCommand(Restart);
            HideBackupAndRestoreMessageAreaAction = hideBackupAndRestoreMessageAreaAction;

            ResourceLoader = resourceLoader;

            // To obtain the general settings configuration of PowerToys if it exists, else to create a new file and return the default configurations.
            if (settingsRepository == null)
            {
                throw new ArgumentNullException(nameof(settingsRepository));
            }

            GeneralSettingsConfig = settingsRepository.SettingsConfig;
            UpdatingSettingsConfig = UpdatingSettings.LoadSettings();
            if (UpdatingSettingsConfig == null)
            {
                UpdatingSettingsConfig = new UpdatingSettings();
            }

            // set the callback functions value to hangle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
            SendCheckForUpdatesConfigMSG = ipcMSGCheckForUpdatesCallBackFunc;
            SendRestartAsAdminConfigMSG = ipcMSGRestartAsAdminMSGCallBackFunc;

            // set the callback function value to update the UI theme.
            UpdateUIThemeCallBack = updateTheme;

            UpdateUIThemeCallBack(GeneralSettingsConfig.Theme);

            // Update Settings file folder:
            _settingsConfigFileFolder = configFileSubfolder;

            // Using Invariant here as these are internal strings and the analyzer
            // expects strings to be normalized to uppercase. While the theme names
            // are represented in lowercase everywhere else, we'll use uppercase
            // normalization for switch statements
            switch (GeneralSettingsConfig.Theme.ToUpperInvariant())
            {
                case "DARK":
                    _themeIndex = 0;
                    break;
                case "LIGHT":
                    _themeIndex = 1;
                    break;
                case "SYSTEM":
                    _themeIndex = 2;
                    break;
            }

            _startup = GeneralSettingsConfig.Startup;
            _autoDownloadUpdates = GeneralSettingsConfig.AutoDownloadUpdates;

            // _settingsBackupAndRestoreDir = GeneralSettingsConfig.SettingsBackupAndRestoreDir;
            _isElevated = isElevated;
            _runElevated = GeneralSettingsConfig.RunElevated;

            RunningAsUserDefaultText = runAsUserText;
            RunningAsAdminDefaultText = runAsAdminText;

            _isAdmin = isAdmin;

            _updatingState = UpdatingSettingsConfig.State;
            _newAvailableVersion = UpdatingSettingsConfig.NewVersion;
            _newAvailableVersionLink = UpdatingSettingsConfig.ReleasePageLink;
            _updateCheckedDate = UpdatingSettingsConfig.LastCheckedDateLocalized;

            if (dispatcherAction != null)
            {
                _fileWatcher = Helper.GetFileWatcher(string.Empty, UpdatingSettings.SettingsFile, dispatcherAction);
            }
        }

        private bool _startup;
        private bool _isElevated;
        private bool _runElevated;
        private bool _isAdmin;
        private int _themeIndex;

        private bool _autoDownloadUpdates;

        // private string _settingsBackupAndRestoreDir;
        private UpdatingSettings.UpdatingState _updatingState = UpdatingSettings.UpdatingState.UpToDate;
        private string _newAvailableVersion = string.Empty;
        private string _newAvailableVersionLink = string.Empty;
        private string _updateCheckedDate = string.Empty;

        private bool _isNewVersionDownloading;
        private bool _isNewVersionChecked;
        private bool _settingsBackupWasSuccessful;
        private bool _settingsBackupWasUnsuccessful;

        private string _settingsBackupMessage;
        private string _settingsBackupRestoreInfo;

        // Gets or sets a value indicating whether run powertoys on start-up.
        public bool Startup
        {
            get
            {
                return _startup;
            }

            set
            {
                if (_startup != value)
                {
                    _startup = value;
                    GeneralSettingsConfig.Startup = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string RunningAsText
        {
            get
            {
                if (!IsElevated)
                {
                    return RunningAsUserDefaultText;
                }
                else
                {
                    return RunningAsAdminDefaultText;
                }
            }

            set
            {
                OnPropertyChanged("RunningAsAdminText");
            }
        }

        // Gets or sets a value indicating whether the powertoy elevated.
        public bool IsElevated
        {
            get
            {
                return _isElevated;
            }

            set
            {
                if (_isElevated != value)
                {
                    _isElevated = value;
                    OnPropertyChanged(nameof(IsElevated));
                    OnPropertyChanged(nameof(IsAdminButtonEnabled));
                    OnPropertyChanged("RunningAsAdminText");
                }
            }
        }

        public bool IsAdminButtonEnabled
        {
            get
            {
                return !IsElevated;
            }

            set
            {
                OnPropertyChanged(nameof(IsAdminButtonEnabled));
            }
        }

        // Gets or sets a value indicating whether powertoys should run elevated.
        public bool RunElevated
        {
            get
            {
                return _runElevated;
            }

            set
            {
                if (_runElevated != value)
                {
                    _runElevated = value;
                    GeneralSettingsConfig.RunElevated = value;
                    NotifyPropertyChanged();
                }
            }
        }

        // Gets a value indicating whether the user is part of administrators group.
        public bool IsAdmin
        {
            get
            {
                return _isAdmin;
            }
        }

        public bool AutoDownloadUpdates
        {
            get
            {
                return _autoDownloadUpdates;
            }

            set
            {
                if (_autoDownloadUpdates != value)
                {
                    _autoDownloadUpdates = value;
                    GeneralSettingsConfig.AutoDownloadUpdates = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public static bool AutoUpdatesEnabled
        {
            get
            {
                return Helper.GetProductVersion() != "v0.0.1";
            }
        }

        public string SettingsBackupAndRestoreDir
        {
            get
            {
                // return _settingsBackupAndRestoreDir;
                return SettingsBackupAndRestoreUtils.GetRegSettingsBackupAndRestoreRegItem("SettingsBackupAndRestoreDir");
            }

            set
            {
                if (SettingsBackupAndRestoreUtils.GetRegSettingsBackupAndRestoreRegItem("SettingsBackupAndRestoreDir") != value)
                {
                    // _settingsBackupAndRestoreDir = value;
                    // GeneralSettingsConfig.SettingsBackupAndRestoreDir = value;
                    SettingsBackupAndRestoreUtils.SetRegSettingsBackupAndRestoreItem("SettingsBackupAndRestoreDir", value);
                    NotifyPropertyChanged();
                }
            }
        }

        public int ThemeIndex
        {
            get
            {
                return _themeIndex;
            }

            set
            {
                if (_themeIndex != value)
                {
                    switch (value)
                    {
                        case 0: GeneralSettingsConfig.Theme = "dark"; break;
                        case 1: GeneralSettingsConfig.Theme = "light"; break;
                        case 2: GeneralSettingsConfig.Theme = "system"; break;
                    }

                    _themeIndex = value;

                    try
                    {
                        UpdateUIThemeCallBack(GeneralSettingsConfig.Theme);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError("Exception encountered when changing Settings theme", e);
                    }

                    NotifyPropertyChanged();
                }
            }
        }

        public string SettingsBackupAndRestoreLocationHeader
        {
            get
            {
                return "Location";
            }
        }

        public string PowerToysVersion
        {
            get
            {
                return Helper.GetProductVersion();
            }
        }

        public string UpdateCheckedDate
        {
            get
            {
                RequestUpdateCheckedDate();
                return _updateCheckedDate;
            }

            set
            {
                if (_updateCheckedDate != value)
                {
                    _updateCheckedDate = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string LastSettingsBackupDate
        {
            get
            {
                var manifest = SettingsBackupAndRestoreUtils.GetLatestSettingsBackupManifest();
                if (manifest != null)
                {
                    if (manifest["CreateDateTime"] != null)
                    {
                        if (DateTime.TryParse(manifest["CreateDateTime"].ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var theDateTime))
                        {
                            return theDateTime.ToString(CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            return "Failed to parse time";
                        }
                    }
                    else
                    {
                        return "Unknown";
                    }
                }
                else
                {
                    return "Non Found";
                }
            }
        }

        public string LastSettingsBackupSource
        {
            get
            {
                var manifest = SettingsBackupAndRestoreUtils.GetLatestSettingsBackupManifest();
                if (manifest != null)
                {
                    if (manifest["BackupSource"] != null)
                    {
                        return manifest["BackupSource"].ToString();
                    }
                    else
                    {
                        return "Unknown";
                    }
                }
                else
                {
                    return "Non Found";
                }
            }
        }

        public string LastSettingsRestoreDate
        {
            get
            {
                var date = SettingsBackupAndRestoreUtils.GetRegSettingsBackupAndRestoreRegItem("LastSettingsRestoreDate");
                if (date != null)
                {
                    if (DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var theDateTime))
                    {
                        return theDateTime.ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        return "Failed to parse time";
                    }
                }
                else
                {
                    return "Never restored";
                }
            }
        }

        public UpdatingSettings.UpdatingState PowerToysUpdatingState
        {
            get
            {
                return _updatingState;
            }

            private set
            {
                if (value != _updatingState)
                {
                    _updatingState = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string PowerToysNewAvailableVersion
        {
            get
            {
                return _newAvailableVersion;
            }

            private set
            {
                if (value != _newAvailableVersion)
                {
                    _newAvailableVersion = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string PowerToysNewAvailableVersionLink
        {
            get
            {
                return _newAvailableVersionLink;
            }

            private set
            {
                if (value != _newAvailableVersionLink)
                {
                    _newAvailableVersionLink = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsNewVersionDownloading
        {
            get
            {
                return _isNewVersionDownloading;
            }

            set
            {
                if (value != _isNewVersionDownloading)
                {
                    _isNewVersionDownloading = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsNewVersionCheckedAndUpToDate
        {
            get
            {
                return _isNewVersionChecked;
            }
        }

        public bool SettingsBackupWasUnsuccessful
        {
            get
            {
                return _settingsBackupWasUnsuccessful;
            }
        }

        public bool SettingsBackupWasSuccessful
        {
            get
            {
                return _settingsBackupWasSuccessful;
            }
        }

        public string SettingsBackupMessage
        {
            get
            {
                return _settingsBackupMessage;
            }
        }

        public string SettingsBackupRestoreInfo
        {
            get
            {
                return _settingsBackupRestoreInfo;
            }
        }

        public bool IsDownloadAllowed
        {
            get
            {
                return AutoUpdatesEnabled && !IsNewVersionDownloading;
            }
        }

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Notify UI of property change
            OnPropertyChanged(propertyName);
            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(GeneralSettingsConfig);

            SendConfigMSG(outsettings.ToString());
        }

        private void SelectSettingBackupDir()
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                // Debugger.Launch();
                var currentDir = SettingsBackupAndRestoreUtils.GetRegSettingsBackupAndRestoreRegItem("SettingsBackupAndRestoreDir");

                if (!string.IsNullOrEmpty(currentDir) && Directory.Exists(currentDir))
                {
                    dialog.InitialDirectory = currentDir;
                }

                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    SettingsBackupAndRestoreDir = dialog.SelectedPath;
                    SettingsBackupAndRestoreUtils.SetRegSettingsBackupAndRestoreItem("SettingsBackupAndRestoreDir", dialog.SelectedPath);
                    NotifyPropertyChanged(nameof(SettingsBackupAndRestoreDir));
                    NotifyPropertyChanged(nameof(LastSettingsBackupDate));
                    NotifyPropertyChanged(nameof(LastSettingsBackupSource));
                }
            }
        }

        private void RestoreConfigsClick()
        {
            var results = new SettingsUtils().RestoreSettings(SettingsBackupAndRestoreUtils.GetRegSettingsBackupAndRestoreRegItem("SettingsBackupAndRestoreDir"));

            if (!results.success)
            {
                _settingsBackupWasSuccessful = results.success;
                _settingsBackupMessage = GetResourceString(results.message);
                _settingsBackupWasUnsuccessful = !_settingsBackupWasSuccessful;
                _settingsBackupMessage = GetResourceString(results.message);

                NotifyPropertyChanged(nameof(SettingsBackupMessage));
                NotifyPropertyChanged(nameof(SettingsBackupWasSuccessful));
                NotifyPropertyChanged(nameof(SettingsBackupWasUnsuccessful));

                HideBackupAndRestoreMessageAreaAction();
            }
            else
            {
                // make sure not to do NotifyPropertyChanged here, else it will persist the configs from memory and
                // undo the settings restore.
                SettingsBackupAndRestoreUtils.SetRegSettingsBackupAndRestoreItem("LastSettingsRestoreDate", DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture));

                Restart();
            }
        }

        private void BackupConfigsClick()
        {
            var results = new SettingsUtils().BackupSettings(SettingsBackupAndRestoreUtils.GetRegSettingsBackupAndRestoreRegItem("SettingsBackupAndRestoreDir"));

            _settingsBackupWasSuccessful = results.success;
            _settingsBackupMessage = GetResourceString(results.message);
            _settingsBackupWasUnsuccessful = !_settingsBackupWasSuccessful;
            _settingsBackupMessage = GetResourceString(results.message);

            NotifyPropertyChanged(nameof(LastSettingsBackupDate));
            NotifyPropertyChanged(nameof(LastSettingsBackupSource));
            NotifyPropertyChanged(nameof(SettingsBackupMessage));
            NotifyPropertyChanged(nameof(SettingsBackupWasSuccessful));
            NotifyPropertyChanged(nameof(SettingsBackupWasUnsuccessful));

            if (_settingsBackupWasSuccessful)
            {
                _settingsBackupRestoreInfo = "GOOD";
            }
            else
            {
                _settingsBackupRestoreInfo = "BAD";
            }

            NotifyPropertyChanged(nameof(SettingsBackupRestoreInfo));

            HideBackupAndRestoreMessageAreaAction();
        }

        private void HideBackupAndRestoreMessage()
        {
            _settingsBackupWasSuccessful = false;
            _settingsBackupWasUnsuccessful = false;
            NotifyPropertyChanged(nameof(SettingsBackupWasSuccessful));
            NotifyPropertyChanged(nameof(SettingsBackupWasUnsuccessful));
        }

        // callback function to launch the URL to check for updates.
        private void CheckForUpdatesClick()
        {
            RefreshUpdatingState();
            IsNewVersionDownloading = string.IsNullOrEmpty(UpdatingSettingsConfig.DownloadedInstallerFilename);
            NotifyPropertyChanged(nameof(IsDownloadAllowed));

            if (_isNewVersionChecked)
            {
                _isNewVersionChecked = !IsNewVersionDownloading;
                NotifyPropertyChanged(nameof(IsNewVersionCheckedAndUpToDate));
            }

            GeneralSettingsConfig.CustomActionName = "check_for_updates";

            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(GeneralSettingsConfig);
            GeneralSettingsCustomAction customaction = new GeneralSettingsCustomAction(outsettings);

            SendCheckForUpdatesConfigMSG(customaction.ToString());
        }

        private void UpdateNowClick()
        {
            IsNewVersionDownloading = string.IsNullOrEmpty(UpdatingSettingsConfig.DownloadedInstallerFilename);
            NotifyPropertyChanged(nameof(IsDownloadAllowed));

            Process.Start(new ProcessStartInfo(Helper.GetPowerToysInstallationFolder() + "\\PowerToys.exe") { Arguments = "powertoys://update_now/" });
        }

        public string GetResourceString(string resource)
        {
            if (ResourceLoader != null)
            {
                var type = ResourceLoader.GetType();
                MethodInfo methodInfo = type.GetMethod("GetString");
                object classInstance = Activator.CreateInstance(type, null);
                object[] parametersArray = new object[] { resource };
                var result = (string)methodInfo.Invoke(ResourceLoader, parametersArray);
                if (string.IsNullOrEmpty(result))
                {
                    return resource.ToUpperInvariant() + "!!!";
                }
                else
                {
                    return result;
                }
            }
            else
            {
                return resource;
            }
        }

        public void RequestUpdateCheckedDate()
        {
            GeneralSettingsConfig.CustomActionName = "request_update_state_date";

            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(GeneralSettingsConfig);
            GeneralSettingsCustomAction customaction = new GeneralSettingsCustomAction(outsettings);

            SendCheckForUpdatesConfigMSG(customaction.ToString());
        }

        public void RestartElevated()
        {
            GeneralSettingsConfig.CustomActionName = "restart_elevation";

            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(GeneralSettingsConfig);
            GeneralSettingsCustomAction customaction = new GeneralSettingsCustomAction(outsettings);

            SendRestartAsAdminConfigMSG(customaction.ToString());
        }

        public void Restart()
        {
            // Debugger.Launch();
            GeneralSettingsConfig.CustomActionName = "restart_maintain_elevation";

            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(GeneralSettingsConfig);
            GeneralSettingsCustomAction customaction = new GeneralSettingsCustomAction(outsettings);

            var dataToSend = customaction.ToString();
            dataToSend = JsonSerializer.Serialize(new { action = new { general = new { action_name = "restart_maintain_elevation" } } });
            SendRestartAsAdminConfigMSG(dataToSend);
        }

        public void HideBackupAndRestoreMessageArea()
        {
            _settingsBackupWasSuccessful = false;
            _settingsBackupWasUnsuccessful = false;

            NotifyPropertyChanged(nameof(SettingsBackupWasSuccessful));
            NotifyPropertyChanged(nameof(SettingsBackupWasUnsuccessful));
        }

        public void RefreshUpdatingState()
        {
            object oLock = new object();
            lock (oLock)
            {
                var config = UpdatingSettings.LoadSettings();

                // Retry loading if failed
                for (int i = 0; i < 3 && config == null; i++)
                {
                    System.Threading.Thread.Sleep(100);
                    config = UpdatingSettings.LoadSettings();
                }

                if (config == null)
                {
                    return;
                }

                UpdatingSettingsConfig = config;

                if (PowerToysUpdatingState != config.State)
                {
                    IsNewVersionDownloading = false;
                }
                else
                {
                    bool dateChanged = UpdateCheckedDate == UpdatingSettingsConfig.LastCheckedDateLocalized;
                    bool fileDownloaded = string.IsNullOrEmpty(UpdatingSettingsConfig.DownloadedInstallerFilename);
                    IsNewVersionDownloading = !(dateChanged || fileDownloaded);
                }

                PowerToysUpdatingState = UpdatingSettingsConfig.State;
                PowerToysNewAvailableVersion = UpdatingSettingsConfig.NewVersion;
                PowerToysNewAvailableVersionLink = UpdatingSettingsConfig.ReleasePageLink;
                UpdateCheckedDate = UpdatingSettingsConfig.LastCheckedDateLocalized;

                _isNewVersionChecked = PowerToysUpdatingState == UpdatingSettings.UpdatingState.UpToDate && !IsNewVersionDownloading;
                NotifyPropertyChanged(nameof(IsNewVersionCheckedAndUpToDate));

                NotifyPropertyChanged(nameof(IsDownloadAllowed));
            }
        }
    }
}
