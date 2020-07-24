using System;
using System.Collections.Generic;
using System.Text;
using Wox.Plugin;

using Microsoft.PowerToys.Settings.UI.Lib;
using Wox.Infrastructure.UserSettings;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;
using System.Diagnostics;
using System.Threading;
using Wox.Infrastructure.Hotkey;
using System.Windows.Input;
using Wox.Core.Plugin;
using System.IO;

namespace PowerLauncher
{
    // Watch for /Local/Microsoft/PowerToys/Launcher/Settings.json changes
    public class SettingsWatcher : BaseModel
    {
        private static int MAX_RETRIES = 10;
        private static object _watcherSyncObject = new object();
        private FileSystemWatcher _watcher;
        private Settings _settings;
        public SettingsWatcher(Settings settings)
        {
            _settings = settings;
            // Set up watcher
            _watcher = Microsoft.PowerToys.Settings.UI.Lib.Utilities.Helper.GetFileWatcher(PowerLauncherSettings.ModuleName, "settings.json", OverloadSettings);

            // Load initial settings file
            OverloadSettings();
        }

        public void OverloadSettings()
        {
            Monitor.Enter(_watcherSyncObject);
            var retry = true;
            for (int i = 0; retry && i < MAX_RETRIES; i++)
            {
                retry = false;
                try
                {
                    var overloadSettings = SettingsUtils.GetSettings<PowerLauncherSettings>(PowerLauncherSettings.ModuleName);

                    var openPowerlauncher = ConvertHotkey(overloadSettings.Properties.OpenPowerLauncher);
                    if (_settings.Hotkey != openPowerlauncher)
                    {
                        _settings.Hotkey = openPowerlauncher;
                    }

                    var shell = PluginManager.AllPlugins.Find(pp => pp.Metadata.Name == "Shell");
                    if (shell != null)
                    {
                        var shellSettings = shell.Plugin as ISettingProvider;
                        shellSettings.UpdateSettings(overloadSettings);
                    }

                    if (_settings.MaxResultsToShow != overloadSettings.Properties.MaximumNumberOfResults)
                    {
                        _settings.MaxResultsToShow = overloadSettings.Properties.MaximumNumberOfResults;
                    }

                    if (_settings.IgnoreHotkeysOnFullscreen != overloadSettings.Properties.IgnoreHotkeysInFullscreen)
                    {
                        _settings.IgnoreHotkeysOnFullscreen = overloadSettings.Properties.IgnoreHotkeysInFullscreen;
                    }

                    var indexer = PluginManager.AllPlugins.Find(p => p.Metadata.Name.Equals("Windows Indexer Plugin", StringComparison.OrdinalIgnoreCase));
                    if (indexer != null)
                    {
                        var indexerSettings = indexer.Plugin as ISettingProvider;
                        indexerSettings.UpdateSettings(overloadSettings);
                    }

                    if (_settings.ClearInputOnLaunch != overloadSettings.Properties.ClearInputOnLaunch)
                    {
                        _settings.ClearInputOnLaunch = overloadSettings.Properties.ClearInputOnLaunch;
                    }
                }
                // the settings application can hold a lock on the settings.json file which will result in a IOException.  
                // This should be changed to properly synch with the settings app instead of retrying.
                catch (IOException e)
                {
                    retry = true;
                    Thread.Sleep(1000);
                    Debug.WriteLine(e.Message);
                }
            }
            Monitor.Exit(_watcherSyncObject);
        }

        private static string ConvertHotkey(HotkeySettings hotkey)
        {
            Key key = KeyInterop.KeyFromVirtualKey(hotkey.Code);
            HotkeyModel model = new HotkeyModel(hotkey.Alt, hotkey.Shift, hotkey.Win, hotkey.Ctrl, key);
            return model.ToString();
        }

    }
}
