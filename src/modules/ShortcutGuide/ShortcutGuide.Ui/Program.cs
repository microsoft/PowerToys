// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

        public static Task ManifestInitializationTask { get; private set; } = Task.CompletedTask;

        public static nint ForegroundWindowHandle { get; set; } = nint.Zero;

        internal static WaitHandle RunnerExitEvent => _runnerExitEvent;

        [STAThread]
        public static void Main(string[] args)
        {
            ForegroundWindowHandle = NativeMethods.GetForegroundWindow();
            Logger.InitializeLogger("\\ShortcutGuide\\Logs");

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

            // Copy manifest files from the Assets folder to the user's AppData folder,
            // but only if the destination is missing or the source is newer.
            string sourceManifestFolder = Path.Combine(
                Path.GetDirectoryName(Environment.ProcessPath)!,
                "Assets",
                "ShortcutGuide",
                "Manifests");

            ManifestInitializationTask = Task.Run(() =>
            {
                var copyStopwatch = Stopwatch.StartNew();
                int copyCount = 0;
                try
                {
                    foreach (string sourceFile in Directory.EnumerateFiles(sourceManifestFolder, "*.yml"))
                    {
                        string destinationFile = Path.Combine(
                            ManifestInterpreter.PathOfManifestFiles, Path.GetFileName(sourceFile));

                        // Only copy if the destination manifest is missing or the bundled
                        // source is newer. This avoids rewriting unchanged files on every
                        // launch and prevents triggering anti-virus scans on the
                        // destination folder unnecessarily.
                        if (!File.Exists(destinationFile) ||
                            File.GetLastWriteTimeUtc(sourceFile) > File.GetLastWriteTimeUtc(destinationFile))
                        {
                            File.Copy(sourceFile, destinationFile, true);
                            copyCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to copy bundled shortcut manifests from '{PathAnonymizer.Anonymize(sourceManifestFolder)}'.", ex);
                }

                copyStopwatch.Stop();

                // Populate PowerToys dynamic hotkeys into the manifest before index
                // generation so index.yml reflects the current shortcut definitions.
                try
                {
                    PowerToysShortcutsPopulator.Populate();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to populate PowerToys shortcuts in manifest.", ex);
                }

                bool needsRegeneration = IndexYmlGenerator.ManifestIndexGenerator.NeedsIndexRegeneration(
                    ManifestInterpreter.PathOfManifestFiles,
                    [Path.GetFileName(PowerToysShortcutsPopulator.PowerToysManifestPath)]);

                if (!needsRegeneration)
                {
                    Logger.LogInfo($"Shortcut Guide index is up-to-date. Skipped generation (checked manifests in {copyStopwatch.ElapsedMilliseconds} ms).");
                }
                else
                {
                    // Generate the index.yml file in-process.
                    try
                    {
                        var stopwatch = Stopwatch.StartNew();
                        var result = IndexYmlGenerator.ManifestIndexGenerator.CreateIndexYmlFile(
                            ManifestInterpreter.PathOfManifestFiles);
                        stopwatch.Stop();

                        foreach (var (fileName, warning) in result.Warnings)
                        {
                            Logger.LogWarning($"Skipping manifest '{fileName}': {warning}");
                        }

                        foreach (var (fileName, error) in result.Errors)
                        {
                            Logger.LogError($"Error processing shortcut manifest '{fileName}'.", error);
                        }

                        Logger.LogInfo($"Shortcut Guide index generated in-process in {stopwatch.ElapsedMilliseconds} ms ({result.IndexedFiles}/{result.TotalFiles} indexed, copied {copyCount} manifests in {copyStopwatch.ElapsedMilliseconds} ms).");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to generate index in-process. There may be a corrupt shortcuts file in \"{PathAnonymizer.Anonymize(ManifestInterpreter.PathOfManifestFiles)}\".", ex);
                    }
                }
            });

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

            Task.Run(async () =>
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
            });
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
        /// Logs the foreground window's executable name captured on activation. This
        /// provides diagnostic context for troubleshooting issues where Shortcut
        /// Guide does not show the expected application's shortcuts.
        /// </summary>
        internal static void LogForegroundCapture(nint hwnd)
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
