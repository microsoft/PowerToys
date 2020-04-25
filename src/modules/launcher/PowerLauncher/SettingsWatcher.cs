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
        private FileSystemWatcher _watcher;
        private Settings _settings;
        public SettingsWatcher(Settings settings)
        {
            _settings = settings;
            // Set up watcher
            try
            {
                _watcher = Helper.GetFileWatcher("Launcher", "settings.json", OverloadSettings);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }

            // Load initial settings file
            OverloadSettings();
        }

        public void OverloadSettings()
        {
            Monitor.Enter(_watcher);
            var retry = true;
            for (int i = 0; retry && i < MAX_RETRIES; i++)
            {
                retry = false;
                try
                {
                    var overloadSettings = SettingsUtils.GetSettings<PowerLauncherSettings>("Launcher");

                    var openPowerlauncher = ConvertHotkey(overloadSettings.properties.open_powerlauncher);
                    if (_settings.Hotkey != openPowerlauncher)
                    {
                        _settings.Hotkey = ConvertHotkey(overloadSettings.properties.open_powerlauncher);
                    }

                    var shell = PluginManager.AllPlugins.Find(pp => pp.Metadata.Name == "Shell");
                    if (shell != null)
                    {
                        var shellSettings = shell.Plugin as ISettingProvider;
                        shellSettings.UpdateSettings(overloadSettings);
                    }

                    var indexer = PluginManager.AllPlugins.Find(pp => pp.Metadata.Name == "Windows Indexer Plugin");
                    if (indexer != null)
                    {
                        var indexerSettings = indexer.Plugin as ISettingProvider;
                        indexerSettings.UpdateSettings(overloadSettings);
                    }

                }
                catch (Exception e)
                {
                    retry = true;
                    Thread.Sleep(1000);
                    Debug.WriteLine(e.Message);
                }
            }
            Monitor.Exit(_watcher);
        }

        private string ConvertHotkey(HotkeySettings hotkey)
        {
            Key key = KeyInterop.KeyFromVirtualKey(hotkey.Code);
            HotkeyModel model = new HotkeyModel(hotkey.Alt, hotkey.Shift, hotkey.Win, hotkey.Ctrl, key);
            return model.ToString();
        }

    }
}
