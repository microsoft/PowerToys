// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using ManagedCommon;
using MouseJumpUI.HotKeys;
using MouseJumpUI.Models.Settings;

namespace MouseJumpUI.Helpers;

internal static class ConfigHelper
{
    private static readonly HotKeyManager _hotKeyManager;

    private static FileSystemWatcher? _appSettingsWatcher;

    private static AppSettings? _appSettings;
    private static EventHandler<HotKeyEventArgs>? _hotKeyPressed;

    static ConfigHelper()
    {
        ConfigHelper._hotKeyManager = new HotKeyManager();
    }

    public static string? AppSettingsPath
    {
        get;
        private set;
    }

    public static AppSettings? AppSettings
    {
        get
        {
            if (_appSettings is null)
            {
                ConfigHelper.LoadAppSettings();
            }

            return _appSettings;
        }
    }

    public static void SetAppSettingsPath(string appSettingsPath)
    {
        ConfigHelper.AppSettingsPath = appSettingsPath;
    }

    public static void SetHotKeyEventHandler(EventHandler<HotKeyEventArgs> eventHandler)
    {
        var evt = _hotKeyPressed;
        if (evt is not null)
        {
            _hotKeyManager.HotKeyPressed -= evt;
        }

        _hotKeyPressed = eventHandler;
        _hotKeyManager.HotKeyPressed += eventHandler;
    }

    public static void LoadAppSettings()
    {
        _hotKeyManager.SetHoKey(null);
        _appSettings = AppSettingsReader.ReadFile(ConfigHelper.AppSettingsPath
            ?? throw new InvalidOperationException("AppSettings cannot be null"));
        _hotKeyManager.SetHoKey(_appSettings?.Hotkey
            ?? throw new InvalidOperationException($"{nameof(_appSettings.Hotkey)} cannot be null"));
    }

    public static void StartWatcher()
    {
        // set up the filesystem watcher
        var path = Path.GetDirectoryName(ConfigHelper.AppSettingsPath) ?? throw new InvalidOperationException();
        var filter = Path.GetFileName(ConfigHelper.AppSettingsPath) ?? throw new InvalidOperationException();
        _appSettingsWatcher = new FileSystemWatcher(path, filter)
        {
            NotifyFilter = NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };
        _appSettingsWatcher.Changed += ConfigHelper.OnAppSettingsChanged;
    }

    private static void OnAppSettingsChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed)
        {
            return;
        }

        // the file might not have been released yet by the application that saved it
        // and caused the file system event (e.g. notepad) so we need to do a couple
        // of retries to give it a change to release the lock so we can load the file contents.
        for (var i = 0; i < 3; i++)
        {
            try
            {
                ConfigHelper.LoadAppSettings();
                break;
            }
            catch (IOException ex)
            {
                Console.WriteLine(ex.ToString());
                Thread.Sleep(250);
            }
        }
    }
}
