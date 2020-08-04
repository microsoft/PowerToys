// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using ColorPicker.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;

namespace ColorPicker.Settings
{
    [Export(typeof(IUserSettings))]
    public class UserSettings : IUserSettings
    {
        private const string ColorPickerModuleName = "ColorPicker";
        private const string DefaultActivationShortcut = "Ctrl + Break";
        private const int MaxNumberOfRetry = 5;
        private FileSystemWatcher _watcher;

        private object _loadingSettingsLock = new object();

        [ImportingConstructor]
        public UserSettings()
        {
            ChangeCursor = new SettingItem<bool>(true);
            ActivationShortcut = new SettingItem<string>(DefaultActivationShortcut);
            CopiedColorRepresentation = new SettingItem<ColorRepresentationType>(ColorRepresentationType.HEX);

            LoadSettingsFromJson();
            _watcher = Helper.GetFileWatcher(ColorPickerModuleName, "settings.json", LoadSettingsFromJson);
        }

        public SettingItem<string> ActivationShortcut { get; private set; }

        public SettingItem<bool> ChangeCursor { get; private set; }

        public SettingItem<ColorRepresentationType> CopiedColorRepresentation { get; set; }

        private void LoadSettingsFromJson()
        {
            // TODO this IO call should by Async, update GetFileWatcher helper to support async
            lock (_loadingSettingsLock)
            {
                {
                    var retry = true;
                    var retryCount = 0;

                    while (retry)
                    {
                        try
                        {
                            retryCount++;

                            if (!SettingsUtils.SettingsExists(ColorPickerModuleName))
                            {
                                Logger.LogInfo("ColorPicker settings.json was missing, creating a new one");
                                var defaultColorPickerSettings = new ColorPickerSettings();
                                defaultColorPickerSettings.Save();
                            }

                            var settings = SettingsUtils.GetSettings<ColorPickerSettings>(ColorPickerModuleName);
                            if (settings != null)
                            {
                                ChangeCursor.Value = settings.Properties.ChangeCursor;
                                ActivationShortcut.Value = settings.Properties.ActivationShortcut.ToString();
                                CopiedColorRepresentation.Value = settings.Properties.CopiedColorRepresentation;
                            }

                            retry = false;
                        }
                        catch (IOException ex)
                        {
                            if (retryCount > MaxNumberOfRetry)
                            {
                                retry = false;
                            }

                            Logger.LogError("Failed to read changed settings", ex);
                            Thread.Sleep(500);
                        }
                        catch (Exception ex)
                        {
                            if (retryCount > MaxNumberOfRetry)
                            {
                                retry = false;
                            }

                            Logger.LogError("Failed to read changed settings", ex);
                            Thread.Sleep(500);
                        }
                    }
                }
            }
        }
    }
}
