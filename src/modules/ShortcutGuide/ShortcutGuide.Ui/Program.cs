// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
        [STAThread]
        public static void Main(string[] args)
        {
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

            Directory.CreateDirectory(ManifestInterpreter.PathOfManifestFiles);

            if (NativeMethods.IsCurrentWindowExcludedFromShortcutGuide())
            {
                return;
            }

            // Copy every shipped manifest from the install directory to the per-user manifest folder.
            // Enumerating the source folder avoids drift between the deployed assets and a hard-coded list.
            // Todo: Only copy files after an update.
            string sourceManifestFolder = Path.Combine(
                Path.GetDirectoryName(Environment.ProcessPath)!,
                "Assets",
                "ShortcutGuide",
                "Manifests");

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

            Process indexGeneration = Process.Start(Path.GetDirectoryName(Environment.ProcessPath) + "\\PowerToys.ShortcutGuide.IndexYmlGenerator.exe");
            indexGeneration.WaitForExit();
            if (indexGeneration.ExitCode != 0)
            {
                Logger.LogError($"Index generation failed with exit code {indexGeneration.ExitCode}. There may be a corrupt shortcuts file in \"{ManifestInterpreter.PathOfManifestFiles}\".");
                return;
            }

            PowerToysShortcutsPopulator.Populate();

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

            // The WinRT/WinUI dispatcher thread doesn't terminate cleanly; force exit.
            Environment.Exit(0);
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
    }
}
