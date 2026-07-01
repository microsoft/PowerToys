// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;

using ManagedCommon;
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

            if (args.Contains("--prepare-phi-silica", StringComparer.OrdinalIgnoreCase))
            {
                return PreparePhiSilica();
            }

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

                Console.Error.WriteLine($"[phi-silica] LAF unlock status: {PhiSilicaLafHelper.LastUnlockStatus}; ReadyState: {readyState}");

                switch (readyState)
                {
                    case Microsoft.Windows.AI.AIFeatureReadyState.Ready:
                        Console.Out.WriteLine("Available");
                        return 0;
                    case Microsoft.Windows.AI.AIFeatureReadyState.NotReady:
                        Console.Out.WriteLine("NotReady");
                        return 1;
                    default:
                        // NotSupportedOnCurrentSystem, DisabledByUser, CapabilityMissing,
                        // NotCompatibleWithSystemHardware, OSUpdateNeeded, or any future state:
                        // the model isn't usable and "Download model" (EnsureReadyAsync) won't fix it.
                        // CapabilityMissing in particular means the systemAIModels capability isn't
                        // authorized for the app, so EnsureReadyAsync throws E_ACCESSDENIED (0x80070005).
                        Console.Out.WriteLine("NotSupported");
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Out.WriteLine("NotSupported");
                return 2;
            }
        }

        /// <summary>
        /// Triggers Phi Silica model preparation (download) without starting the WinUI app.
        /// Moves the model from NotReady to Ready by calling EnsureReadyAsync.
        /// Exit codes: 0 = ready, 1 = preparation failed, 2 = not supported or error.
        /// </summary>
        private static int PreparePhiSilica()
        {
            try
            {
                PhiSilicaLafHelper.TryUnlock();
                var readyState = Microsoft.Windows.AI.Text.LanguageModel.GetReadyState();

                Console.Error.WriteLine($"[phi-silica] LAF unlock status: {PhiSilicaLafHelper.LastUnlockStatus}; ReadyState: {readyState}");

                if (readyState is Microsoft.Windows.AI.AIFeatureReadyState.NotSupportedOnCurrentSystem
                    or Microsoft.Windows.AI.AIFeatureReadyState.DisabledByUser)
                {
                    Console.Out.WriteLine("NotSupported");
                    return 2;
                }

                if (readyState == Microsoft.Windows.AI.AIFeatureReadyState.Ready)
                {
                    Console.Out.WriteLine("Ready");
                    return 0;
                }

                // Run on a thread-pool (MTA) thread: the WinRT async operation does not
                // marshal correctly when blocked on from the [STAThread] entry point.
                var result = System.Threading.Tasks.Task.Run(
                    () => Microsoft.Windows.AI.Text.LanguageModel.EnsureReadyAsync().AsTask()).GetAwaiter().GetResult();

                if (result.Status != Microsoft.Windows.AI.AIFeatureReadyResultState.Success)
                {
                    int hresult = result.ExtendedError?.HResult ?? 0;
                    Console.Error.WriteLine($"[phi-silica] EnsureReadyAsync Status: {result.Status}; HRESULT: 0x{hresult:X8}; Message: {result.ExtendedError?.Message}");
                    Console.Error.WriteLine(result.ExtendedError?.Message ?? result.Status.ToString());
                    Console.Out.WriteLine("Failed");
                    return 1;
                }

                Console.Out.WriteLine("Ready");
                return 0;
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
