// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace ShowDesktop
{
    internal sealed class Program
    {
        private const string TerminateEventName = "Local\\PowerToysShowDesktop-TerminateEvent-7b3a9c1e-2d4f-5a6b-8e7c-9f0d1a2b3c4e";
        private const string MutexName = "PowerToys.ShowDesktop.Mutex";

        private static DesktopPeek? _peek;
        private static FileSystemWatcher? _settingsWatcher;

        [STAThread]
        private static int Main(string[] args)
        {
            Logger.InitializeLogger("\\ShowDesktop\\Logs");

            if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredShowDesktopEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                Logger.LogWarning("ShowDesktop is disabled by GPO policy.");
                return 0;
            }

            // Parse runner PID
            int runnerPid = 0;
            if (args.Length > 0)
            {
                _ = int.TryParse(args[0], out runnerPid);
            }

            // Single instance
            using var mutex = new Mutex(true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                Logger.LogWarning("Another instance of ShowDesktop is already running.");
                return 0;
            }

            Logger.LogInfo($"ShowDesktop starting. Runner PID={runnerPid}");

            // Open the terminate event signaled by the runner when the module should exit
            using var terminateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, TerminateEventName);

            // Watch for runner exit
            if (runnerPid > 0)
            {
                try
                {
                    var runner = Process.GetProcessById(runnerPid);
                    runner.EnableRaisingEvents = true;
                    runner.Exited += (s, e) =>
                    {
                        Logger.LogInfo("Runner process exited — shutting down.");
                        NativeMethods.PostQuitMessage(0);
                    };
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to watch runner process: {ex.Message}");
                }
            }

            // Watch for terminate event from runner
            var registeredWait = ThreadPool.RegisterWaitForSingleObject(
                terminateEvent,
                (state, timedOut) =>
                {
                    Logger.LogInfo("Terminate event received — shutting down.");
                    NativeMethods.PostQuitMessage(0);
                },
                null,
                -1,
                true);

            // Load settings
            var settingsUtils = SettingsUtils.Default;
            ShowDesktopSettings settings;
            try
            {
                settings = settingsUtils.GetSettingsOrDefault<ShowDesktopSettings>(ShowDesktopSettings.ModuleName);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load settings, using defaults: {ex.Message}");
                settings = new ShowDesktopSettings();
            }

            // Create and start the core
            _peek = new DesktopPeek(settings);
            _peek.Start();

            // Watch for settings file changes
            SetupSettingsWatcher(settingsUtils);

            // Run the message loop (required for the low-level mouse hook)
            Win32MessageLoop.Run();

            // Cleanup
            Logger.LogInfo("ShowDesktop shutting down.");
            _peek.Stop();
            _peek.Dispose();
            _settingsWatcher?.Dispose();
            registeredWait.Unregister(null);

            return 0;
        }

        private static void SetupSettingsWatcher(SettingsUtils settingsUtils)
        {
            try
            {
                string settingsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft",
                    "PowerToys",
                    ShowDesktopSettings.ModuleName);

                if (!Directory.Exists(settingsDir))
                {
                    Directory.CreateDirectory(settingsDir);
                }

                _settingsWatcher = new FileSystemWatcher(settingsDir, "settings.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite,
                    EnableRaisingEvents = true,
                };

                _settingsWatcher.Changed += (s, e) =>
                {
                    try
                    {
                        Logger.LogInfo("Settings file changed, reloading.");

                        // Small delay to avoid reading a partially-written file
                        Thread.Sleep(200);

                        var newSettings = settingsUtils.GetSettingsOrDefault<ShowDesktopSettings>(ShowDesktopSettings.ModuleName);
                        _peek?.UpdateSettings(newSettings);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error reloading settings: {ex.Message}");
                    }
                };

                Logger.LogInfo($"Watching settings at: {settingsDir}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to set up settings watcher: {ex.Message}");
            }
        }
    }
}
