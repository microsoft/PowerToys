// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;

namespace AdvancedPaste
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            Logger.InitializeLogger("\\AdvancedPaste\\Logs");

            WinRT.ComWrappersSupport.InitializeComWrappers();

            if (args.Contains("--check-phi-silica", StringComparer.OrdinalIgnoreCase))
            {
                return CheckPhiSilicaAvailability();
            }

            // Workaround for https://github.com/microsoft/microsoft-ui-xaml/issues/10856:
            // With sparse package identity, MRT only loads resources.pri and ignores
            // module-specific PRI files. Copy our PRI as resources.pri before WinUI starts.
            EnsureResourcesPri();

            if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredAdvancedPasteEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
                return 1;
            }

            var instanceKey = AppInstance.FindOrRegisterForKey("PowerToys_AdvancedPaste_Instance");

            if (instanceKey.IsCurrent)
            {
                Microsoft.UI.Xaml.Application.Start((p) =>
                {
                    var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    _ = new App();
                });
            }
            else
            {
                Logger.LogWarning("Another instance of AdvancedPasteUI is running. Exiting.");
            }

            return 0;
        }

        /// <summary>
        /// Copies PowerToys.AdvancedPaste.pri to resources.pri so MRT can find it
        /// when the app runs with sparse package identity.
        /// </summary>
        private static void EnsureResourcesPri()
        {
            try
            {
                var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var modulePri = Path.Combine(exeDir, "PowerToys.AdvancedPaste.pri");
                var resourcesPri = Path.Combine(exeDir, "resources.pri");

                if (File.Exists(modulePri))
                {
                    File.Copy(modulePri, resourcesPri, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to copy PRI for sparse identity", ex);
            }
        }

        /// <summary>
        /// Checks Phi Silica availability without starting the WinUI app.
        /// Used by Settings UI to probe API status via subprocess.
        /// Exit codes: 0 = available, 1 = not ready (model needs download), 2 = not supported or error.
        /// </summary>
        private static int CheckPhiSilicaAvailability()
        {
            try
            {
                PhiSilicaLafHelper.TryUnlock();
                var readyState = Microsoft.Windows.AI.Text.LanguageModel.GetReadyState();

                switch (readyState)
                {
                    case Microsoft.Windows.AI.AIFeatureReadyState.NotSupportedOnCurrentSystem:
                        Console.Out.WriteLine("NotSupported");
                        return 2;
                    case Microsoft.Windows.AI.AIFeatureReadyState.NotReady:
                        Console.Out.WriteLine("NotReady");
                        return 1;
                    default:
                        Console.Out.WriteLine("Available");
                        return 0;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Out.WriteLine("NotSupported");
                return 2;
            }
        }
    }
}
