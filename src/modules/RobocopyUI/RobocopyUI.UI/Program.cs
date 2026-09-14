// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using Application = Microsoft.UI.Xaml.Application;

namespace RobocopyUI
{
    public sealed class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Logger.InitializeLogger("\\RobocopyUI\\Logs");

            // The module interface passes: <powertoys_pid> [telemetry]
            if (args.Length >= 2 && args[1] == "telemetry")
            {
                Logger.LogInfo("Telemetry mode requested. Sending settings telemetry.");
                SendSettingsTelemetry();
                return;
            }

            if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredShortcutGuideEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
                return;
            }

            if (args.Length >= 1 && int.TryParse(args[0], out int runnerPID))
            {
                MonitorPowerToysRunner(runnerPID);
            }

            WinRT.ComWrappersSupport.InitializeComWrappers();

            var instanceKey = AppInstance.FindOrRegisterForKey("PowerToys_RobocopyUI_Instance");

            if (instanceKey.IsCurrent)
            {
                Application.Start((p) =>
                {
                    var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    _ = new App();
                    AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                });
            }
            else
            {
                Logger.LogWarning("Another instance of RobocopyUI is running. Exiting ShortcutGuide");
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            throw new NotImplementedException();
        }

        private static void MonitorPowerToysRunner(int runnerPID)
        {
            Process runnerProcess;
            try
            {
                runnerProcess = Process.GetProcessById(runnerPID);

                // Force the process handle to open synchronously so a Runner exit
                // during WinUI initialization cannot be missed or confused with PID reuse.
                _ = runnerProcess.Handle;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
            {
                Logger.LogWarning($"PowerToys runner process (PID={runnerPID}) is no longer available. Exiting RobocopyUI.");
                return;
            }

            var runnerWatcher = new Thread(() =>
            {
                try
                {
                    runnerProcess.WaitForExit();
                    Logger.LogInfo($"PowerToys runner process (PID={runnerPID}) exited. Exiting RobocopyUI.");
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
                {
                    Logger.LogWarning($"Failed while waiting for PowerToys runner process (PID={runnerPID}): {ex.Message}");
                }
                finally
                {
                    runnerProcess.Dispose();
                }
            })
            {
                IsBackground = true,
                Name = "RobocopyUI-RunnerWatcher",
            };
            runnerWatcher.Start();
        }

        private static void SendSettingsTelemetry()
        {
            try
            {
                var settingsUtils = SettingsUtils.Default;
                var settings = settingsUtils.GetSettingsOrDefault<ShortcutGuideSettings>(ShortcutGuideSettings.ModuleName);
                if (settings?.Properties != null)
                {
                    var props = settings.Properties;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to send settings telemetry.", ex);
            }
        }
    }
}
