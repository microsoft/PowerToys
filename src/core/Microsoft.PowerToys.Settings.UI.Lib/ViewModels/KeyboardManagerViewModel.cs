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
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;
using Microsoft.PowerToys.Settings.UI.Lib.ViewModels.Commands;

namespace Microsoft.PowerToys.Settings.UI.Lib.ViewModels
{
    public class KeyboardManagerViewModel : Observable
    {
        private const string PowerToyName = "Keyboard Manager";
        private const string RemapKeyboardActionName = "RemapKeyboard";
        private const string RemapKeyboardActionValue = "Open Remap Keyboard Window";
        private const string EditShortcutActionName = "EditShortcut";
        private const string EditShortcutActionValue = "Open Edit Shortcut Window";
        private const string JsonFileType = ".json";
        private const string ProfileFileMutexName = "PowerToys.KeyboardManager.ConfigMutex";
        private const int ProfileFileMutexWaitTimeoutMilliseconds = 1000;

        private readonly FileSystemWatcher watcher;

        private ICommand remapKeyboardCommand;
        private ICommand editShortcutCommand;
        public KeyboardManagerSettings settings;
        private KeyboardManagerProfile profile;
        private GeneralSettings generalSettings;

        private Func<string, int> SendConfigMSG { get; }

        private Func<List<KeysDataModel>, int> FilterRemapKeysList { get; }

        public KeyboardManagerViewModel(Func<string, int> ipcMSGCallBackFunc, Func<List<KeysDataModel>, int> filterRemapKeysList)
        {
            // set the callback functions value to hangle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
            FilterRemapKeysList = filterRemapKeysList;

            if (SettingsUtils.SettingsExists(PowerToyName))
            {
                // Todo: Be more resilient while reading and saving settings.
                settings = SettingsUtils.GetSettings<KeyboardManagerSettings>(PowerToyName);

                // Load profile.
                if (!LoadProfile())
                {
                    profile = new KeyboardManagerProfile();
                }
            }
            else
            {
                settings = new KeyboardManagerSettings(PowerToyName);
                SettingsUtils.SaveSettings(settings.ToJsonString(), PowerToyName);
            }

            if (SettingsUtils.SettingsExists())
            {
                generalSettings = SettingsUtils.GetSettings<GeneralSettings>(string.Empty);
            }
            else
            {
                generalSettings = new GeneralSettings();
                SettingsUtils.SaveSettings(generalSettings.ToJsonString(), string.Empty);
            }
        }

        public bool Enabled
        {
            get
            {
                return generalSettings.Enabled.KeyboardManager;
            }

            set
            {
                if (generalSettings.Enabled.KeyboardManager != value)
                {
                    generalSettings.Enabled.KeyboardManager = value;
                    OnPropertyChanged(nameof(Enabled));
                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(generalSettings);

                    SendConfigMSG(outgoing.ToString());
                }
            }
        }

        // store remappings
        public List<KeysDataModel> RemapKeys
        {
            get
            {
                if (profile != null)
                {
                    return profile.RemapKeys.InProcessRemapKeys;
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
                if (profile != null)
                {
                    return CombineShortcutLists(profile.RemapShortcuts.GlobalRemapShortcuts, profile.RemapShortcuts.AppSpecificRemapShortcuts);
                }
                else
                {
                    return new List<AppSpecificKeysDataModel>();
                }
            }
        }

        public ICommand RemapKeyboardCommand => remapKeyboardCommand ?? (remapKeyboardCommand = new RelayCommand(OnRemapKeyboard));

        public ICommand EditShortcutCommand => editShortcutCommand ?? (editShortcutCommand = new RelayCommand(OnEditShortcut));

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
                            profile = SettingsUtils.GetSettings<KeyboardManagerProfile>(PowerToyName, settings.Properties.ActiveConfiguration.Value + JsonFileType);
                            FilterRemapKeysList(profile.RemapKeys.InProcessRemapKeys);
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
