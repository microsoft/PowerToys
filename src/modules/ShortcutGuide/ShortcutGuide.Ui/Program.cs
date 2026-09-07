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
using ShortcutGuide.Helpers;
using ShortcutGuide.Telemetry;
using Application = Microsoft.UI.Xaml.Application;

namespace ShortcutGuide
{
    public sealed class Program
    {
        private static readonly ManualResetEvent _runnerExitEvent = new(false);

        public static Thread CopyAndIndexGenerationThread { get; private set; } = null!;

        public static nint ForegroundWindowHandle { get; set; } = nint.Zero;

        internal static WaitHandle RunnerExitEvent => _runnerExitEvent;

        [STAThread]
        public static void Main(string[] args)
        {
            ForegroundWindowHandle = NativeMethods.GetForegroundWindow();
            Logger.InitializeLogger("\\ShortcutGuide\\Logs");
            LogForegroundCapture(ForegroundWindowHandle);

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

            Directory.CreateDirectory(ManifestInterpreter.PathOfManifestFiles);

            // Copy every shipped manifest from the install directory to the per-user manifest folder.
            // Enumerating the source folder avoids drift between the deployed assets and a hard-coded list.
            // Todo: Only copy files after an update.
            string sourceManifestFolder = Path.Combine(
                Path.GetDirectoryName(Environment.ProcessPath)!,
                "Assets",
                "ShortcutGuide",
                "Manifests");

            CopyAndIndexGenerationThread = new Thread(() =>
            {
                try
                {
                    foreach (string sourceFile in Directory.EnumerateFiles(sourceManifestFolder, "*.yml"))
                    {
                        string destinationFile = Path.Combine(ManifestInterpreter.PathOfManifestFiles, Path.GetFileName(sourceFile));
                        File.Copy(sourceFile, destinationFile, true);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to copy bundled shortcut manifests from '{sourceManifestFolder}'.", ex);
                }

                string indexGeneratorPath = Path.Combine(
                    Path.GetDirectoryName(Environment.ProcessPath)!,
                    "PowerToys.ShortcutGuide.IndexYmlGenerator.exe");

                try
                {
                    using Process? indexGeneration = Process.Start(indexGeneratorPath);

                    if (indexGeneration is null)
                    {
                        Logger.LogError($"Failed to start index generation process '{indexGeneratorPath}'.");
                        return;
                    }

                    indexGeneration.WaitForExit();

                    if (indexGeneration.ExitCode != 0)
                    {
                        Logger.LogError($"Index generation failed with exit code {indexGeneration.ExitCode}. There may be a corrupt shortcuts file in \"{ManifestInterpreter.PathOfManifestFiles}\".");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to start or wait for index generation process '{indexGeneratorPath}'.", ex);
                    return;
                }

                try
                {
                    PowerToysShortcutsPopulator.Populate();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to populate PowerToys shortcuts in manifest.", ex);
                }
            });
            CopyAndIndexGenerationThread.IsBackground = true;
            CopyAndIndexGenerationThread.Start();

            WinRT.ComWrappersSupport.InitializeComWrappers();

            var instanceKey = AppInstance.FindOrRegisterForKey("PowerToys_ShortcutGuide_Instance");

            if (instanceKey.IsCurrent)
            {
                Application.Start((p) =>
                {
                    var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    _ = new App();
                });
            }
            else
            {
                Logger.LogWarning("Another instance of ShortcutGuide is running. Exiting ShortcutGuide");
            }
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
                Logger.LogWarning($"PowerToys runner process (PID={runnerPID}) is no longer available. Exiting ShortcutGuide.");
                _runnerExitEvent.Set();
                return;
            }

            var runnerWatcher = new Thread(() =>
            {
                try
                {
                    runnerProcess.WaitForExit();
                    Logger.LogInfo($"PowerToys runner process (PID={runnerPID}) exited. Exiting ShortcutGuide.");
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
                {
                    Logger.LogWarning($"Failed while waiting for PowerToys runner process (PID={runnerPID}): {ex.Message}");
                }
                finally
                {
                    runnerProcess.Dispose();
                    _runnerExitEvent.Set();
                }
            })
            {
                IsBackground = true,
                Name = "ShortcutGuide-RunnerWatcher",
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
                    PowerToysTelemetry.Log.WriteEvent(new ShortcutGuideSettingsEvent(
                        props.OpenShortcutGuide?.ToString() ?? string.Empty,
                        props.Theme?.Value ?? "system",
                        props.DisabledApps?.Value ?? string.Empty));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to send settings telemetry.", ex);
            }
        }

        /// <summary>
        /// Logs the foreground window captured at startup so we can see in
        /// the SG log which app was foreground at the moment SG.exe began
        /// running. The dictionary populated from this HWND drives the order
        /// of nav items, so a wrong/empty capture surfaces as the wrong app
        /// being auto-selected.
        /// </summary>
        private static void LogForegroundCapture(nint hwnd)
        {
            try
            {
                if (hwnd == nint.Zero)
                {
                    Logger.LogInfo("Foreground capture: HWND=0 (no foreground window).");
                    return;
                }

                if (NativeMethods.GetWindowThreadProcessId(hwnd, out uint processId) == 0)
                {
                    Logger.LogInfo($"Foreground capture: HWND=0x{hwnd:X}; GetWindowThreadProcessId failed.");
                    return;
                }

                string moduleName;
                try
                {
                    using var proc = Process.GetProcessById((int)processId);
                    moduleName = proc.MainModule?.ModuleName ?? "(null)";
                }
                catch (Exception ex)
                {
                    moduleName = $"(failed: {ex.GetType().Name})";
                }

                Logger.LogInfo($"Foreground capture: HWND=0x{hwnd:X}, PID={processId}, Module={moduleName}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to log foreground capture.", ex);
            }
        }
    }
}
