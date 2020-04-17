// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.UserDataTasks;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class KeyboardManagerViewModel : Observable
    {
        private const string PowerToyName = "Keyboard Manager";
        private const string RemapKeyboardActionName = "RemapKeyboard";
        private const string RemapKeyboardActionValue = "Open Remap Keyboard Window";
        private const string EditShortcutActionName = "EditShortcut";
        private const string EditShortcutActionValue = "Open Edit Shortcut Window";

        // This is an dummy file which will be updated by the Keyboard Manager Module after finish up writing to the configuration file.
        private const string WatchFileName = "settings-updated.json";

        // Time in miliseconds to group multiple fired events as one. Single file update can fire multiple events.
        private const double TriggerExpirationThreshold = 100;

        private DateTimeOffset lastTriggerTime = DateTimeOffset.MinValue;
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
                settings = SettingsUtils.GetSettings<KeyboardManagerSettings>(PowerToyName);
            }
            else
            {
                settings = new KeyboardManagerSettings(PowerToyName);
                SettingsUtils.SaveSettings(settings.ToJsonString(), PowerToyName);
            }

            this.watcher = Helper.GetFileWatcher(PowerToyName, WatchFileName, OnConfigFileUpdate);
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
            // Todo: update the UI data source here.
            var lastEventTimeDiff = (DateTimeOffset.UtcNow - lastTriggerTime).TotalMilliseconds;
            lastTriggerTime = DateTimeOffset.UtcNow;
            if (lastEventTimeDiff > TriggerExpirationThreshold)
            {
                var config = Helper.GetConfigFile<KeyboadManagerConfigModel>(PowerToyName, settings.Properties.ActiveConfiguration.Value);
            }
        }
    }
}
