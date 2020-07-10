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
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.Toolkit.Uwp.Helpers;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;

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
        private const string ProfileFileMutexName = "PowerToys.KeyboardManager.ConfigMutex";
        private const int ProfileFileMutexWaitTimeoutMilliseconds = 1000;

        private readonly CoreDispatcher dispatcher;
        private readonly FileSystemWatcher watcher;

        private ICommand remapKeyboardCommand;
        private ICommand editShortcutCommand;
        private KeyboardManagerSettings settings;
        private KeyboardManagerProfile profile;
        private GeneralSettings generalSettings;

        public KeyboardManagerViewModel()
        {
            dispatcher = Window.Current.Dispatcher;
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

            watcher = Helper.GetFileWatcher(
                PowerToyName,
                settings.Properties.ActiveConfiguration.Value + JsonFileType,
                OnConfigFileUpdate);

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

                    ShellPage.DefaultSndMSGCallback(outgoing.ToString());
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

        public List<KeysDataModel> RemapShortcuts
        {
            get
            {
                if (profile != null)
                {
                    return profile.RemapShortcuts.GlobalRemapShortcuts;
                }
                else
                {
                    return new List<KeysDataModel>();
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
            ShellPage.DefaultSndMSGCallback(Helper.GetSerializedCustomAction(PowerToyName, RemapKeyboardActionName, RemapKeyboardActionValue));
            await Task.CompletedTask;
        }

        private async Task OnEditShortcutBackground()
        {
            Helper.AllowRunnerToForeground();
            ShellPage.DefaultSndMSGCallback(Helper.GetSerializedCustomAction(PowerToyName, EditShortcutActionName, EditShortcutActionValue));
            await Task.CompletedTask;
        }

        private async void OnConfigFileUpdate()
        {
            // Note: FileSystemWatcher raise notification multiple times for single update operation.
            // Todo: Handle duplicate events either by somehow suppress them or re-read the configuration everytime since we will be updating the UI only if something is changed.
            if (LoadProfile())
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    OnPropertyChanged(nameof(RemapKeys));
                    OnPropertyChanged(nameof(RemapShortcuts));
                });
            }
        }

        private bool LoadProfile()
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

        private void FilterRemapKeysList(List<KeysDataModel> remapKeysList)
        {
            CombineRemappings(remapKeysList, (uint)VirtualKey.LeftControl, (uint)VirtualKey.RightControl, (uint)VirtualKey.Control);
            CombineRemappings(remapKeysList, (uint)VirtualKey.LeftMenu, (uint)VirtualKey.RightMenu, (uint)VirtualKey.Menu);
            CombineRemappings(remapKeysList, (uint)VirtualKey.LeftShift, (uint)VirtualKey.RightShift, (uint)VirtualKey.Shift);
            CombineRemappings(remapKeysList, (uint)VirtualKey.LeftWindows, (uint)VirtualKey.RightWindows, Helper.VirtualKeyWindows);
        }

        private void CombineRemappings(List<KeysDataModel> remapKeysList, uint leftKey, uint rightKey, uint combinedKey)
        {
            KeysDataModel firstRemap = remapKeysList.Find(x => uint.Parse(x.OriginalKeys) == leftKey);
            KeysDataModel secondRemap = remapKeysList.Find(x => uint.Parse(x.OriginalKeys) == rightKey);
            if (firstRemap != null && secondRemap != null)
            {
                if (firstRemap.NewRemapKeys == secondRemap.NewRemapKeys)
                {
                    KeysDataModel combinedRemap = new KeysDataModel
                    {
                        OriginalKeys = combinedKey.ToString(),
                        NewRemapKeys = firstRemap.NewRemapKeys,
                    };
                    remapKeysList.Insert(remapKeysList.IndexOf(firstRemap), combinedRemap);
                    remapKeysList.Remove(firstRemap);
                    remapKeysList.Remove(secondRemap);
                }
            }
        }
    }
}
