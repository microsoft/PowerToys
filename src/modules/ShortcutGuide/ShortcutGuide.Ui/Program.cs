// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using ShortcutGuide.Helpers;
using Application = Microsoft.UI.Xaml.Application;

namespace ShortcutGuide
{
    public sealed class Program
    {
        [STAThread]
        public static void Main()
        {
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
                Logger.LogError("Index generation failed with exit code: " + indexGeneration.ExitCode);
                MessageBox.Show($"Shortcut Guide encountered an error while generating the index file. There is likely a corrupt shortcuts file in \"{ManifestInterpreter.PathOfManifestFiles}\". Try deleting this directory.", "Error displaying shortcuts", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            PowerToysShortcutsPopulator.Populate();

            Logger.InitializeLogger("\\ShortcutGuide\\Logs");
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

            // Something prevents the process from exiting, so we need to kill it manually.
            Process.GetCurrentProcess().Kill();
        }
    }
}
