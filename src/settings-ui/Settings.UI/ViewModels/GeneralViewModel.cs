// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class GeneralViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private UpdatingSettings UpdatingSettingsConfig { get; set; }

        public ButtonClickCommand CheckForUpdatesEventHandler { get; set; }

        public object ResourceLoader { get; set; }

        private Action HideBackupAndRestoreMessageAreaAction { get; set; }

        private Action<int> DoBackupAndRestoreDryRun { get; set; }

        public ButtonClickCommand BackupConfigsEventHandler { get; set; }

        public ButtonClickCommand RestoreConfigsEventHandler { get; set; }

        public ButtonClickCommand RefreshBackupStatusEventHandler { get; set; }

        public ButtonClickCommand SelectSettingBackupDirEventHandler { get; set; }

        public ButtonClickCommand RestartElevatedButtonEventHandler { get; set; }

        public ButtonClickCommand UpdateNowButtonEventHandler { get; set; }

        public Func<string, int> UpdateUIThemeCallBack { get; }

        public Func<string, int> SendConfigMSG { get; }

        public Func<string, int> SendRestartAsAdminConfigMSG { get; }

        public Func<string, int> SendCheckForUpdatesConfigMSG { get; }

        public string RunningAsUserDefaultText { get; set; }

        public string RunningAsAdminDefaultText { get; set; }

        private string _settingsConfigFileFolder = string.Empty;

        private IFileSystemWatcher _fileWatcher;

        private Func<Task<string>> PickSingleFolderDialog { get; }

        private SettingsBackupAndRestoreUtils settingsBackupAndRestoreUtils = SettingsBackupAndRestoreUtils.Instance;

        public GeneralViewModel(ISettingsRepository<GeneralSettings> settingsRepository, string runAsAdminText, string runAsUserText, bool isElevated, bool isAdmin, Func<string, int> updateTheme, Func<string, int> ipcMSGCallBackFunc, Func<string, int> ipcMSGRestartAsAdminMSGCallBackFunc, Func<string, int> ipcMSGCheckForUpdatesCallBackFunc, string configFileSubfolder = "", Action dispatcherAction = null, Action hideBackupAndRestoreMessageAreaAction = null, Action<int> doBackupAndRestoreDryRun = null, Func<Task<string>> pickSingleFolderDialog = null, object resourceLoader = null)
        {
            CheckForUpdatesEventHandler = new ButtonClickCommand(CheckForUpdatesClick);
            RestartElevatedButtonEventHandler = new ButtonClickCommand(RestartElevated);
            UpdateNowButtonEventHandler = new ButtonClickCommand(UpdateNowClick);
            BackupConfigsEventHandler = new ButtonClickCommand(BackupConfigsClick);
            SelectSettingBackupDirEventHandler = new ButtonClickCommand(SelectSettingBackupDir);
            RestoreConfigsEventHandler = new ButtonClickCommand(RestoreConfigsClick);
            RefreshBackupStatusEventHandler = new ButtonClickCommand(RefreshBackupStatusEventHandlerClick);
            HideBackupAndRestoreMessageAreaAction = hideBackupAndRestoreMessageAreaAction;
            DoBackupAndRestoreDryRun = doBackupAndRestoreDryRun;
            PickSingleFolderDialog = pickSingleFolderDialog;
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

        private UpdatingSettings.UpdatingState _updatingState = UpdatingSettings.UpdatingState.UpToDate;
        private string _newAvailableVersion = string.Empty;
        private string _newAvailableVersionLink = string.Empty;
        private string _updateCheckedDate = string.Empty;

        private bool _isNewVersionDownloading;
        private bool _isNewVersionChecked;
        private bool _isNoNetwork;

        private bool _settingsBackupRestoreMessageVisible;
        private string _settingsBackupMessage;
        private string _backupRestoreMessageSeverity;

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
                return settingsBackupAndRestoreUtils.GetSettingsBackupAndRestoreDir();
            }

            set
            {
                if (settingsBackupAndRestoreUtils.GetSettingsBackupAndRestoreDir() != value)
                {
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
                try
                {
                    var manifest = settingsBackupAndRestoreUtils.GetLatestSettingsBackupManifest();
                    if (manifest != null)
                    {
                        if (manifest["CreateDateTime"] != null)
                        {
                            if (DateTime.TryParse(manifest["CreateDateTime"].ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var theDateTime))
                            {
                                return theDateTime.ToString("G", CultureInfo.CurrentCulture);
                            }
                            else
                            {
                                Logger.LogError("Failed to parse time from backup");
                                return GetResourceString("General_SettingsBackupAndRestore_FailedToParseTime");
                            }
                        }
                        else
                        {
                            return GetResourceString("General_SettingsBackupAndRestore_UnknownBackupTime");
                        }
                    }
                    else
                    {
                        return GetResourceString("General_SettingsBackupAndRestore_NoBackupFound");
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError("Error getting LastSettingsBackupDate", e);
                    return GetResourceString("General_SettingsBackupAndRestore_UnknownBackupTime");
                }
            }
        }

        public string CurrentSettingMatchText
        {
            get
            {
                try
                {
                    var results = settingsBackupAndRestoreUtils.GetLastBackupSettingsResults();

                    var resultText = string.Empty;

                    if (!results.LastRan.HasValue)
                    {
                        // not ran since started.
                        return GetResourceString("General_SettingsBackupAndRestore_CurrentSettingsNoChecked"); // "Current Settings Unknown";
                    }
                    else
                    {
                        if (results.Success)
                        {
                            if (results.LastBackupExists)
                            {
                                // if true, it means a backup would have been made
                                resultText = GetResourceString("General_SettingsBackupAndRestore_CurrentSettingsDiffer"); // "Current Settings Differ";
                            }
                            else
                            {
                                // would have done the backup, but there also was not an existing one there.
                                resultText = GetResourceString("General_SettingsBackupAndRestore_NoBackupFound");
                            }
                        }
                        else
                        {
                            if (results.HadError)
                            {
                                // if false and error we don't really know
                                resultText = GetResourceString("General_SettingsBackupAndRestore_CurrentSettingsUnknown"); // "Current Settings Unknown";
                            }
                            else
                            {
                                // if false, it means a backup would not have been needed/made
                                resultText = GetResourceString("General_SettingsBackupAndRestore_CurrentSettingsMatch"); // "Current Settings Match";
                            }
                        }

                        return $"{resultText} {GetResourceString("General_SettingsBackupAndRestore_CurrentSettingsStatusAt")} {results.LastRan.Value.ToLocalTime().ToString("G", CultureInfo.CurrentCulture)}";
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError("Error getting CurrentSettingMatchText", e);
                    return string.Empty;
                }
            }
        }

        public string LastSettingsBackupSource
        {
            get
            {
                try
                {
                    var manifest = settingsBackupAndRestoreUtils.GetLatestSettingsBackupManifest();
                    if (manifest != null)
                    {
                        if (manifest["BackupSource"] != null)
                        {
                            if (manifest["BackupSource"].ToString().Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase))
                            {
                                return GetResourceString("General_SettingsBackupAndRestore_ThisMachine");
                            }
                            else
                            {
                                return manifest["BackupSource"].ToString();
                            }
                        }
                        else
                        {
                            return GetResourceString("General_SettingsBackupAndRestore_UnknownBackupSource");
                        }
                    }
                    else
                    {
                        return GetResourceString("General_SettingsBackupAndRestore_NoBackupFound");
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError("Error getting LastSettingsBackupSource", e);
                    return GetResourceString("General_SettingsBackupAndRestore_UnknownBackupSource");
                }
            }
        }

        public string LastSettingsBackupFileName
        {
            get
            {
                try
                {
                    var fileName = settingsBackupAndRestoreUtils.GetLatestBackupFileName();
                    return !string.IsNullOrEmpty(fileName) ? fileName : GetResourceString("General_SettingsBackupAndRestore_NoBackupFound");
                }
                catch (Exception e)
                {
                    Logger.LogError("Error getting LastSettingsBackupFileName", e);
                    return string.Empty;
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

        public bool IsNoNetwork
        {
            get
            {
                return _isNoNetwork;
            }
        }

        public bool SettingsBackupRestoreMessageVisible
        {
            get
            {
                return _settingsBackupRestoreMessageVisible;
            }
        }

        public string BackupRestoreMessageSeverity
        {
            get
            {
                return _backupRestoreMessageSeverity;
            }
        }

        public string SettingsBackupMessage
        {
            get
            {
                return _settingsBackupMessage;
            }
        }

        public bool IsDownloadAllowed
        {
            get
            {
                return AutoUpdatesEnabled && !IsNewVersionDownloading;
            }
        }

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null, bool reDoBackupDryRun = true)
        {
            // Notify UI of property change
            OnPropertyChanged(propertyName);

            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(GeneralSettingsConfig);

            SendConfigMSG(outsettings.ToString());

            if (reDoBackupDryRun && DoBackupAndRestoreDryRun != null)
            {
                DoBackupAndRestoreDryRun(500);
            }
        }

        /// <summary>
        /// Method <c>SelectSettingBackupDir</c> opens folder browser to select a backup and retore location.
        /// </summary>
        private async void SelectSettingBackupDir()
        {
            var currentDir = settingsBackupAndRestoreUtils.GetSettingsBackupAndRestoreDir();

            var newPath = await PickSingleFolderDialog();

            if (!string.IsNullOrEmpty(newPath))
            {
                SettingsBackupAndRestoreDir = newPath;
                NotifyAllBackupAndRestoreProperties();
            }
        }

        private void RefreshBackupStatusEventHandlerClick()
        {
            DoBackupAndRestoreDryRun(0);
        }

        /// <summary>
        /// Method <c>RestoreConfigsClick</c> starts the restore.
        /// </summary>
        private void RestoreConfigsClick()
        {
            string settingsBackupAndRestoreDir = settingsBackupAndRestoreUtils.GetSettingsBackupAndRestoreDir();

            if (string.IsNullOrEmpty(settingsBackupAndRestoreDir))
            {
                SelectSettingBackupDir();
            }

            var results = SettingsUtils.RestoreSettings();
            _backupRestoreMessageSeverity = results.Severity;

            if (!results.Success)
            {
                _settingsBackupRestoreMessageVisible = true;

                _settingsBackupMessage = GetResourceString(results.Message);

                NotifyAllBackupAndRestoreProperties();

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

        /// <summary>
        /// Method <c>BackupConfigsClick</c> starts the backup.
        /// </summary>
        private void BackupConfigsClick()
        {
            string settingsBackupAndRestoreDir = settingsBackupAndRestoreUtils.GetSettingsBackupAndRestoreDir();

            if (string.IsNullOrEmpty(settingsBackupAndRestoreDir))
            {
                SelectSettingBackupDir();
            }

            var results = SettingsUtils.BackupSettings();

            _settingsBackupRestoreMessageVisible = true;
            _backupRestoreMessageSeverity = results.Severity;
            _settingsBackupMessage = GetResourceString(results.Message);

            // now we do a dry run to get the results for "setting match"
            var settingsUtils = new SettingsUtils();
            var appBasePath = Path.GetDirectoryName(settingsUtils.GetSettingsFilePath());
            settingsBackupAndRestoreUtils.BackupSettings(appBasePath, settingsBackupAndRestoreDir, true);

            NotifyAllBackupAndRestoreProperties();

            HideBackupAndRestoreMessageAreaAction();
        }

        public void NotifyAllBackupAndRestoreProperties()
        {
            NotifyPropertyChanged(nameof(LastSettingsBackupDate), false);
            NotifyPropertyChanged(nameof(LastSettingsBackupSource), false);
            NotifyPropertyChanged(nameof(LastSettingsBackupFileName), false);
            NotifyPropertyChanged(nameof(CurrentSettingMatchText), false);
            NotifyPropertyChanged(nameof(SettingsBackupMessage), false);
            NotifyPropertyChanged(nameof(BackupRestoreMessageSeverity), false);
            NotifyPropertyChanged(nameof(SettingsBackupRestoreMessageVisible), false);
        }

        // callback function to launch the URL to check for updates.
        private void CheckForUpdatesClick()
        {
            // check if network is available
            bool isNetAvailable = IsNetworkAvailable();

            // check if the state changed
            bool prevState = _isNoNetwork;
            _isNoNetwork = !isNetAvailable;
            if (prevState != _isNoNetwork)
            {
                NotifyPropertyChanged(nameof(IsNoNetwork));
            }

            if (!isNetAvailable)
            {
                _isNewVersionDownloading = false;
                return;
            }

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

        /// <summary>
        /// Class <c>GetResourceString</c> gets a localized text.
        /// </summary>
        /// <remarks>
        /// To do: see if there is a betting way to do this, there should be. It does allow us to return missing localization in a way that makes it obvious they were missed.
        /// </remarks>
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

        /// <summary>
        /// Class <c>Restart</c> begin a restart and signal we want to maintain elevation
        /// </summary>
        /// <remarks>
        /// Other restarts either raised or lowered elevation
        /// </remarks>
        public void Restart()
        {
            GeneralSettingsConfig.CustomActionName = "restart_maintain_elevation";

            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(GeneralSettingsConfig);
            GeneralSettingsCustomAction customaction = new GeneralSettingsCustomAction(outsettings);

            var dataToSend = customaction.ToString();
            dataToSend = JsonSerializer.Serialize(new { action = new { general = new { action_name = "restart_maintain_elevation" } } });
            SendRestartAsAdminConfigMSG(dataToSend);
        }

        /// <summary>
        /// Class <c>HideBackupAndRestoreMessageArea</c> hides the backup/restore message area
        /// </summary>
        /// <remarks>
        /// We want to have it go away after a short period.
        /// </remarks>
        public void HideBackupAndRestoreMessageArea()
        {
            _settingsBackupRestoreMessageVisible = false;
            NotifyAllBackupAndRestoreProperties();
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

        /// <summary>
        /// Indicates whether any network connection is available
        /// Filter virtual network cards.
        /// </summary>
        /// <returns>
        ///     <c>true</c> if a network connection is available; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsNetworkAvailable()
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return false;
            }

            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                // discard because of standard reasons
                if ((ni.OperationalStatus != OperationalStatus.Up) ||
                    (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) ||
                    (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel))
                {
                    continue;
                }

                // discard virtual cards (virtual box, virtual pc, etc.)
                if (ni.Description.Contains("virtual", StringComparison.OrdinalIgnoreCase) ||
                    ni.Name.Contains("virtual", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // discard "Microsoft Loopback Adapter", it will not show as NetworkInterfaceType.Loopback but as Ethernet Card.
                if (ni.Description.Equals("Microsoft Loopback Adapter", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
