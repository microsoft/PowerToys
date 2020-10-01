// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.PowerToys.Settings.UI.Lib.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib.Interface;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;
using Microsoft.PowerToys.Settings.UI.Lib.ViewModels.Commands;

namespace Microsoft.PowerToys.Settings.UI.Lib.ViewModels
{
    public class KeyboardManagerViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ISettingsUtils _settingsUtils;

        private const string PowerToyName = KeyboardManagerSettings.ModuleName;
        private const string RemapKeyboardActionName = "RemapKeyboard";
        private const string RemapKeyboardActionValue = "Open Remap Keyboard Window";
        private const string EditShortcutActionName = "EditShortcut";
        private const string EditShortcutActionValue = "Open Edit Shortcut Window";
        private const string JsonFileType = ".json";
        private const string ProfileFileMutexName = "PowerToys.KeyboardManager.ConfigMutex";
        private const int ProfileFileMutexWaitTimeoutMilliseconds = 1000;

        public KeyboardManagerSettings Settings { get; set; }

        private ICommand _remapKeyboardCommand;
        private ICommand _editShortcutCommand;
        private KeyboardManagerProfile _profile;

        private Func<string, int> SendConfigMSG { get; }

        private Func<List<KeysDataModel>, int> FilterRemapKeysList { get; }

        public KeyboardManagerViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc, Func<List<KeysDataModel>, int> filterRemapKeysList)
        {
            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            // set the callback functions value to hangle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
            FilterRemapKeysList = filterRemapKeysList;

            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));

            if (_settingsUtils.SettingsExists(PowerToyName))
            {
                // Todo: Be more resilient while reading and saving settings.
                Settings = _settingsUtils.GetSettings<KeyboardManagerSettings>(PowerToyName);

                // Load profile.
                if (!LoadProfile())
                {
                    _profile = new KeyboardManagerProfile();
                }
            }
            else
            {
                Settings = new KeyboardManagerSettings();
                _settingsUtils.SaveSettings(Settings.ToJsonString(), PowerToyName);
            }
        }

        public bool Enabled
        {
            get
            {
                return GeneralSettingsConfig.Enabled.KeyboardManager;
            }

            set
            {
                if (GeneralSettingsConfig.Enabled.KeyboardManager != value)
                {
                    GeneralSettingsConfig.Enabled.KeyboardManager = value;
                    OnPropertyChanged(nameof(Enabled));
                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);

                    SendConfigMSG(outgoing.ToString());
                }
            }
        }

        // store remappings
        public List<KeysDataModel> RemapKeys
        {
            get
            {
                if (_profile != null)
                {
                    return _profile.RemapKeys.InProcessRemapKeys;
                }
                else
                {
                    return new List<KeysDataModel>();
                }
            }
        }

        public static List<AppSpecificKeysDataModel> CombineShortcutLists(List<KeysDataModel> globalShortcutList, List<AppSpecificKeysDataModel> appSpecificShortcutList)
        {
            return globalShortcutList.ConvertAll(x => new AppSpecificKeysDataModel { OriginalKeys = x.OriginalKeys, NewRemapKeys = x.NewRemapKeys, TargetApp = "All Apps" }).Concat(appSpecificShortcutList).ToList();
        }

        public List<AppSpecificKeysDataModel> RemapShortcuts
        {
            get
            {
                if (_profile != null)
                {
                    return CombineShortcutLists(_profile.RemapShortcuts.GlobalRemapShortcuts, _profile.RemapShortcuts.AppSpecificRemapShortcuts);
                }
                else
                {
                    return new List<AppSpecificKeysDataModel>();
                }
            }
        }

        public ICommand RemapKeyboardCommand => _remapKeyboardCommand ?? (_remapKeyboardCommand = new RelayCommand(OnRemapKeyboard));

        public ICommand EditShortcutCommand => _editShortcutCommand ?? (_editShortcutCommand = new RelayCommand(OnEditShortcut));

        private async void OnRemapKeyboard()
        {
            await Task.Run(() => OnRemapKeyboardBackground());
        }

        private async void OnEditShortcut()
        {
            await Task.Run(() => OnEditShortcutBackground());
        }

        private async Task OnRemapKeyboardBackground()
        {
            Helper.AllowRunnerToForeground();
            SendConfigMSG(Helper.GetSerializedCustomAction(PowerToyName, RemapKeyboardActionName, RemapKeyboardActionValue));
            await Task.CompletedTask;
        }

        private async Task OnEditShortcutBackground()
        {
            Helper.AllowRunnerToForeground();
            SendConfigMSG(Helper.GetSerializedCustomAction(PowerToyName, EditShortcutActionName, EditShortcutActionValue));
            await Task.CompletedTask;
        }

        public void NotifyFileChanged()
        {
            OnPropertyChanged(nameof(RemapKeys));
            OnPropertyChanged(nameof(RemapShortcuts));
        }

        public bool LoadProfile()
        {
            var success = true;

            try
            {
                using (var profileFileMutex = Mutex.OpenExisting(ProfileFileMutexName))
                {
                    if (profileFileMutex.WaitOne(ProfileFileMutexWaitTimeoutMilliseconds))
                    {
                        // update the UI element here.
                        try
                        {
                            string fileName = Settings.Properties.ActiveConfiguration.Value + JsonFileType;

                            if (_settingsUtils.SettingsExists(PowerToyName, fileName))
                            {
                                _profile = _settingsUtils.GetSettings<KeyboardManagerProfile>(PowerToyName, fileName);
                            }
                            else
                            {
                                // The KBM process out of runner creates the default.json file if it does not exist.
                                success = false;
                            }

                            FilterRemapKeysList(_profile?.RemapKeys?.InProcessRemapKeys);
                        }
                        finally
                        {
                            // Make sure to release the mutex.
                            profileFileMutex.ReleaseMutex();
                        }
                    }
                    else
                    {
                        success = false;
                    }
                }
            }
            catch (Exception)
            {
                // Failed to load the configuration.
                success = false;
            }

            return success;
        }
    }
}
