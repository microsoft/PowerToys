#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using ImageResizer.Models;
using ImageResizer.Properties;
using ImageResizer.Utilities;
using ImageResizer.ViewModels;
using ImageResizer.Views;
using ManagedCommon;

namespace ImageResizer
{
    public partial class App : Application, IDisposable
    {
        private const string LogSubFolder = "\\ImageResizer\\Logs";

        /// <summary>
        /// Gets cached AI availability state, checked at app startup.
        /// Can be updated after model download completes or background initialization.
        /// </summary>
        public static AiAvailabilityState AiAvailabilityState { get; internal set; }

        /// <summary>
        /// Event fired when AI initialization completes in background.
        /// Allows UI to refresh state when initialization finishes.
        /// </summary>
        public static event EventHandler<AiAvailabilityState> AiInitializationCompleted;

        static App()
        {
            try
            {
                // Initialize logger early (mirroring PowerOCR pattern)
                Logger.InitializeLogger(LogSubFolder);
            }
            catch
            {
                /* swallow logger init issues silently */
            }

            try
            {
                string appLanguage = LanguageHelper.LoadLanguage();
                if (!string.IsNullOrEmpty(appLanguage))
                {
                    System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo(appLanguage);
                }
            }
            catch (CultureNotFoundException ex)
            {
                Logger.LogError("CultureNotFoundException: " + ex.Message);
            }

            Console.InputEncoding = Encoding.Unicode;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Fix for .net 3.1.19 making Image Resizer not adapt to DPI changes.
            NativeMethods.SetProcessDPIAware();

            // Check for AI detection mode (called by Runner in background)
            if (e?.Args?.Length > 0 && e.Args[0] == "--detect-ai")
            {
                RunAiDetectionMode();
                return;
            }

            if (PowerToys.GPOWrapperProjection.GPOWrapper.GetConfiguredImageResizerEnabledValue() == PowerToys.GPOWrapperProjection.GpoRuleConfigured.Disabled)
            {
                /* TODO: Add logs to ImageResizer.
                 * Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
                 */
                Logger.LogWarning("GPO policy disables ImageResizer. Exiting.");
                Environment.Exit(0); // Current.Exit won't work until there's a window opened.
                return;
            }

            // AI Super Resolution is not supported on Windows 10 - skip check entirely
            if (OSVersionHelper.IsWindows10())
            {
                AiAvailabilityState = AiAvailabilityState.NotSupported;
                ResizeBatch.SetAiSuperResolutionService(Services.NoOpAiSuperResolutionService.Instance);
                Logger.LogInfo("AI Super Resolution not supported on Windows 10");
            }
            else
            {
                // Default to ModelNotReady initially, will be updated after window loads
                AiAvailabilityState = AiAvailabilityState.ModelNotReady;
                ResizeBatch.SetAiSuperResolutionService(Services.NoOpAiSuperResolutionService.Instance);
            }

            var batch = ResizeBatch.FromCommandLine(Console.In, e?.Args);

            // TODO: Add command-line parameters that can be used in lieu of the input page (issue #14)
            var mainWindow = new MainWindow(new MainViewModel(batch, Settings.Default));
            mainWindow.Show();

            // Temporary workaround for issue #1273
            WindowHelpers.BringToForeground(new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle);

            // Check AI availability after window is loaded
            // WinAiSuperResolutionService handles UI thread dispatching internally
            if (!OSVersionHelper.IsWindows10())
            {
                mainWindow.Loaded += async (s, args) =>
                {
                    await InitializeAiAsync();
                };
            }
        }

        /// <summary>
        /// Initialize AI availability check.
        /// WinAiSuperResolutionService handles UI thread dispatching internally.
        /// </summary>
        private static async System.Threading.Tasks.Task InitializeAiAsync()
        {
            try
            {
                var readyState = Services.WinAiSuperResolutionService.GetModelReadyState();

                switch (readyState)
                {
                    case Microsoft.Windows.AI.AIFeatureReadyState.Ready:
                        AiAvailabilityState = AiAvailabilityState.Ready;
                        // Initialize the AI service
                        var aiService = await Services.WinAiSuperResolutionService.CreateAsync();
                        if (aiService != null)
                        {
                            ResizeBatch.SetAiSuperResolutionService(aiService);
                        }

                        break;

                    case Microsoft.Windows.AI.AIFeatureReadyState.NotReady:
                        AiAvailabilityState = AiAvailabilityState.ModelNotReady;
                        break;

                    case Microsoft.Windows.AI.AIFeatureReadyState.DisabledByUser:
                    case Microsoft.Windows.AI.AIFeatureReadyState.NotSupportedOnCurrentSystem:
                    default:
                        AiAvailabilityState = AiAvailabilityState.NotSupported;
                        break;
                }

                // Notify UI that AI state has been determined
                AiInitializationCompleted?.Invoke(null, AiAvailabilityState);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to check AI availability: {ex.Message}");
                AiAvailabilityState = AiAvailabilityState.NotSupported;
                AiInitializationCompleted?.Invoke(null, AiAvailabilityState);
            }
        }

        /// <summary>
        /// AI detection mode: perform detection, write to cache, and exit.
        /// Called by Runner in background to avoid blocking ImageResizer UI startup.
        /// </summary>
        private void RunAiDetectionMode()
        {
            try
            {
                Logger.LogInfo("Running AI detection mode...");

                // AI Super Resolution is not supported on Windows 10
                if (OSVersionHelper.IsWindows10())
                {
                    Logger.LogInfo("AI detection skipped: Windows 10 does not support AI Super Resolution");
                    Services.AiAvailabilityCacheService.SaveCache(AiAvailabilityState.NotSupported);
                    Environment.Exit(0);
                    return;
                }

                // Perform detection (reuse existing logic)
                var state = CheckAiAvailability();

                // Write result to cache file
                Services.AiAvailabilityCacheService.SaveCache(state);

                Logger.LogInfo($"AI detection complete: {state}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"AI detection failed: {ex.Message}");
                Services.AiAvailabilityCacheService.SaveCache(AiAvailabilityState.NotSupported);
            }

            // Exit silently without showing UI
            Environment.Exit(0);
        }

        /// <summary>
        /// Check AI Super Resolution availability on this system.
        /// Performs architecture check and model availability check.
        /// </summary>
        private static AiAvailabilityState CheckAiAvailability()
        {
            try
            {
                // Check Windows AI service model ready state
                // it's so slow, why?
                var readyState = Services.WinAiSuperResolutionService.GetModelReadyState();

                // Map AI service state to our availability state
                switch (readyState)
                {
                    case Microsoft.Windows.AI.AIFeatureReadyState.Ready:
                        return AiAvailabilityState.Ready;

                    case Microsoft.Windows.AI.AIFeatureReadyState.NotReady:
                        return AiAvailabilityState.ModelNotReady;

                    case Microsoft.Windows.AI.AIFeatureReadyState.DisabledByUser:
                    case Microsoft.Windows.AI.AIFeatureReadyState.NotSupportedOnCurrentSystem:
                    default:
                        return AiAvailabilityState.NotSupported;
                }
            }
            catch (Exception)
            {
                return AiAvailabilityState.NotSupported;
            }
        }

        public void Dispose()
        {
            // Dispose AI Super Resolution service
            ResizeBatch.DisposeAiSuperResolutionService();

            GC.SuppressFinalize(this);
        }
    }
}
