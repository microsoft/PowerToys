#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Globalization;
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
        /// Can be updated after model download completes.
        /// </summary>
        public static AiAvailabilityState AiAvailabilityState { get; internal set; }

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

        protected override async void OnStartup(StartupEventArgs e)
        {
            // Fix for .net 3.1.19 making Image Resizer not adapt to DPI changes.
            NativeMethods.SetProcessDPIAware();

            if (PowerToys.GPOWrapperProjection.GPOWrapper.GetConfiguredImageResizerEnabledValue() == PowerToys.GPOWrapperProjection.GpoRuleConfigured.Disabled)
            {
                /* TODO: Add logs to ImageResizer.
                 * Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
                 */
                Logger.LogWarning("GPO policy disables ImageResizer. Exiting.");
                Environment.Exit(0); // Current.Exit won't work until there's a window opened.
                return;
            }

            // Check AI availability at startup (not relying on cached settings)
            AiAvailabilityState = CheckAiAvailability();
            Logger.LogInfo($"AI availability checked at startup: {AiAvailabilityState}");

            // Initialize AI service early if supported
            await InitializeAiServiceAsync();

            var batch = ResizeBatch.FromCommandLine(Console.In, e?.Args);

            // TODO: Add command-line parameters that can be used in lieu of the input page (issue #14)
            var mainWindow = new MainWindow(new MainViewModel(batch, Settings.Default));
            mainWindow.Show();
            Logger.LogInfo("MainWindow shown (unpackaged or activation fallback path).");

            // Temporary workaround for issue #1273
            WindowHelpers.BringToForeground(new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle);
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

        /// <summary>
        /// Initialize AI Super Resolution service at application startup.
        /// This ensures the service is ready before the UI is shown.
        /// </summary>
        private static async System.Threading.Tasks.Task InitializeAiServiceAsync()
        {
            // Check if AI is supported on this system (use cached state from OnStartup)
            if (AiAvailabilityState != AiAvailabilityState.Ready)
            {
                // AI not supported or model not ready - use NoOp service
                ResizeBatch.SetAiSuperResolutionService(Services.NoOpAiSuperResolutionService.Instance);
                return;
            }

            try
            {
                // Create and initialize AI service using async factory
                var aiService = await Services.WinAiSuperResolutionService.CreateAsync();

                if (aiService != null)
                {
                    ResizeBatch.SetAiSuperResolutionService(aiService);
                    Logger.LogInfo("AI Super Resolution service initialized successfully.");
                }
                else
                {
                    // Initialization failed - fallback to NoOp
                    ResizeBatch.SetAiSuperResolutionService(Services.NoOpAiSuperResolutionService.Instance);
                    Logger.LogWarning("AI Super Resolution service initialization failed. Using fallback.");
                }
            }
            catch (Exception ex)
            {
                // Log error and use NoOp service as fallback
                ResizeBatch.SetAiSuperResolutionService(Services.NoOpAiSuperResolutionService.Instance);
                Logger.LogError($"Exception during AI service initialization: {ex.Message}");
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
