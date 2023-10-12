// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace MouseJumpUI.Helpers;

internal class SettingsHelper
{
    public SettingsHelper()
    {
        this.LockObject = new();
        this.CurrentSettings = this.LoadSettings();

        // delay loading settings on change by some time to avoid file in use exception
        var throttledActionInvoker = new ThrottledActionInvoker();
        this.FileSystemWatcher = Helper.GetFileWatcher(
            moduleName: MouseJumpSettings.ModuleName,
            fileName: "settings.json",
            onChangedCallback: () => throttledActionInvoker.ScheduleAction(this.ReloadSettings, 250));
    }

    private IFileSystemWatcher FileSystemWatcher
    {
        get;
    }

    private object LockObject
    {
        get;
    }

    public MouseJumpSettings CurrentSettings
    {
        get;
        private set;
    }

    private MouseJumpSettings LoadSettings()
    {
        lock (this.LockObject)
        {
            {
                var settingsUtils = new SettingsUtils();

                // set this to 1 to disable retries
                var remainingRetries = 5;

                while (remainingRetries > 0)
                {
                    try
                    {
                        if (!settingsUtils.SettingsExists(MouseJumpSettings.ModuleName))
                        {
                            Logger.LogInfo("MouseJump settings.json was missing, creating a new one");
                            var defaultSettings = new MouseJumpSettings();
                            defaultSettings.Save(settingsUtils);
                        }

                        var settings = settingsUtils.GetSettingsOrDefault<MouseJumpSettings>(MouseJumpSettings.ModuleName);
                        return settings;
                    }
                    catch (IOException ex)
                    {
                        Logger.LogError("Failed to read changed settings", ex);
                        Thread.Sleep(250);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to read changed settings", ex);
                        Thread.Sleep(250);
                    }

                    remainingRetries--;
                }
            }
        }

        const string message = "Failed to read changed settings - ran out of retries";
        Logger.LogError(message);
        throw new InvalidOperationException(message);
    }

    public void ReloadSettings()
    {
        this.CurrentSettings = this.LoadSettings();
    }
}
