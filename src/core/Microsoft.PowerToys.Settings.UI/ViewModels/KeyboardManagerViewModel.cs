// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;
using Microsoft.PowerToys.Settings.UI.Views;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class KeyboardManagerViewModel : Observable
    {
        private const string PowerToyName = "Keyboard Manager";
        private const string RemapKeyboardActionName = "RemapKeyboard";
        private const string RemapKeyboardActionValue = "Open Remap Keyboard Window";
        private const string EditShortcutActionName = "EditShortcut";
        private const string EditShortcutActionValue = "Open Edit Shortcut Window";
        private const string JsonFileType = ".json";
        private const string ConfigFileMutexName = "PowerToys.KeyboardManager.ConfigMutex";
        private const int ConfigFileMutexWaitTimeoutMiliSeconds = 1000;

        private ICommand remapKeyboardCommand;
        private ICommand editShortcutCommand;
        private FileSystemWatcher watcher;
        private KeyboardManagerSettings settings;

        public ICommand RemapKeyboardCommand => remapKeyboardCommand ?? (remapKeyboardCommand = new RelayCommand(OnRemapKeyboard));

        public ICommand EditShortcutCommand => editShortcutCommand ?? (editShortcutCommand = new RelayCommand(OnEditShortcut));

        public KeyboardManagerViewModel()
        {
            if (SettingsUtils.SettingsExists(PowerToyName))
            {
                // Todo: Be more resillent while reading and saving settings.
                settings = SettingsUtils.GetSettings<KeyboardManagerSettings>(PowerToyName);
            }
            else
            {
                settings = new KeyboardManagerSettings(PowerToyName);
                SettingsUtils.SaveSettings(settings.ToJsonString(), PowerToyName);
            }

            watcher = Helper.GetFileWatcher(PowerToyName, settings.Properties.ActiveConfiguration.Value + JsonFileType, OnConfigFileUpdate);
        }

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
            ShellPage.DefaultSndMSGCallback(Helper.GetSerializedCustomAction(PowerToyName, RemapKeyboardActionName, RemapKeyboardActionValue));
            await Task.CompletedTask;
        }

        private async Task OnEditShortcutBackground()
        {
            Helper.AllowRunnerToForeground();
            ShellPage.DefaultSndMSGCallback(Helper.GetSerializedCustomAction(PowerToyName, EditShortcutActionName, EditShortcutActionValue));
            await Task.CompletedTask;
        }

        private void OnConfigFileUpdate()
        {
            // Note: FileSystemWatcher raise notification mutiple times for single update operation.
            // Todo: Handle duplicate events either by somehow supress them or re-read the configuration everytime since we will be updating the UI only if something is changed.
            GetKeyboardManagerConfigFile();
        }

        private void GetKeyboardManagerConfigFile()
        {
            try
            {
                using (var configFileMutex = Mutex.OpenExisting(ConfigFileMutexName))
                {
                    if (configFileMutex.WaitOne(ConfigFileMutexWaitTimeoutMiliSeconds))
                    {
                        // update the UI element here.
                        try
                        {
                            var config = SettingsUtils.GetSettings<KeyboadManagerConfigModel>(PowerToyName, settings.Properties.ActiveConfiguration.Value + JsonFileType);
                        }
                        finally
                        {
                            // Make sure to release the mutex.
                            configFileMutex.ReleaseMutex();
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Failed to load the configuration.
            }
        }
    }
}
