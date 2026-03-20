#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Globalization;
using System.Text;
using ImageResizer.Models;
using ImageResizer.Properties;
using ImageResizer.ViewModels;
using ImageResizer.Views;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace ImageResizer
{
    public partial class App : Application, IDisposable
    {
        private const string LogSubFolder = "\\Image Resizer\\Logs";

        public static AiAvailabilityState AiAvailabilityState { get; internal set; }

        public static event EventHandler<AiAvailabilityState> AiInitializationCompleted;

        public static Window MainWindow { get; private set; }

        private static string[] _args;

        public App()
        {
            this.InitializeComponent();
        }

        public static void SetArgs(string[] args)
        {
            _args = args;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // Store DispatcherQueue for Settings.Reload thread dispatching
            Settings.UIDispatcherQueue = DispatcherQueue.GetForCurrentThread();

            // TODO: Re-enable AI Super Resolution in next release by removing this #if block
#if true
            AiAvailabilityState = AiAvailabilityState.NotSupported;
            ResizeBatch.SetAiSuperResolutionService(Services.NoOpAiSuperResolutionService.Instance);

            if (_args?.Length > 0 && _args[0] == "--detect-ai")
            {
                Services.AiAvailabilityCacheService.SaveCache(AiAvailabilityState.NotSupported);
                Environment.Exit(0);
                return;
            }
#else
            if (_args?.Length > 0 && _args[0] == "--detect-ai")
            {
                RunAiDetectionMode();
                return;
            }
#endif

            if (PowerToys.GPOWrapperProjection.GPOWrapper.GetConfiguredImageResizerEnabledValue() == PowerToys.GPOWrapperProjection.GpoRuleConfigured.Disabled)
            {
                Logger.LogWarning("GPO policy disables ImageResizer. Exiting.");
                Environment.Exit(0);
                return;
            }

            if (OSVersionHelper.IsWindows10())
            {
                AiAvailabilityState = AiAvailabilityState.NotSupported;
                ResizeBatch.SetAiSuperResolutionService(Services.NoOpAiSuperResolutionService.Instance);
                Logger.LogInfo("AI Super Resolution not supported on Windows 10");
            }
            else
            {
                var cachedState = Services.AiAvailabilityCacheService.LoadCache();

                if (cachedState.HasValue)
                {
                    AiAvailabilityState = cachedState.Value;
                    Logger.LogInfo($"AI state loaded from cache: {AiAvailabilityState}");
                }
                else
                {
                    AiAvailabilityState = AiAvailabilityState.NotSupported;
                    Logger.LogInfo("No AI cache found, defaulting to NotSupported");
                }

                if (AiAvailabilityState == AiAvailabilityState.Ready)
                {
                    _ = InitializeAiServiceAsync();
                }
                else
                {
                    ResizeBatch.SetAiSuperResolutionService(Services.NoOpAiSuperResolutionService.Instance);
                }
            }

            var batch = ResizeBatch.FromCommandLine(Console.In, _args);

            var mainWindow = new MainWindow(new MainViewModel(batch, Settings.Default));
            MainWindow = mainWindow;
            mainWindow.Activate();
        }

        private void RunAiDetectionMode()
        {
            try
            {
                Logger.LogInfo("Running AI detection mode...");

                if (OSVersionHelper.IsWindows10())
                {
                    Logger.LogInfo("AI detection skipped: Windows 10 does not support AI Super Resolution");
                    Services.AiAvailabilityCacheService.SaveCache(AiAvailabilityState.NotSupported);
                    Environment.Exit(0);
                    return;
                }

                var state = CheckAiAvailability();
                Services.AiAvailabilityCacheService.SaveCache(state);
                Logger.LogInfo($"AI detection complete: {state}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"AI detection failed: {ex.Message}");
                Services.AiAvailabilityCacheService.SaveCache(AiAvailabilityState.NotSupported);
            }

            Environment.Exit(0);
        }

        private static AiAvailabilityState CheckAiAvailability()
        {
            return AiAvailabilityState.NotSupported;
        }

        private static async System.Threading.Tasks.Task InitializeAiServiceAsync()
        {
            AiAvailabilityState finalState;

            try
            {
                var aiService = await Services.WinAiSuperResolutionService.CreateAsync();

                if (aiService != null)
                {
                    ResizeBatch.SetAiSuperResolutionService(aiService);
                    Logger.LogInfo("AI Super Resolution service initialized successfully.");
                    finalState = AiAvailabilityState.Ready;
                }
                else
                {
                    ResizeBatch.SetAiSuperResolutionService(Services.NoOpAiSuperResolutionService.Instance);
                    Logger.LogWarning("AI Super Resolution service initialization failed. Using default service.");
                    finalState = AiAvailabilityState.NotSupported;
                }
            }
            catch (Exception ex)
            {
                ResizeBatch.SetAiSuperResolutionService(Services.NoOpAiSuperResolutionService.Instance);
                Logger.LogError($"Exception during AI service initialization: {ex.Message}");
                finalState = AiAvailabilityState.NotSupported;
            }

            AiAvailabilityState = finalState;
            AiInitializationCompleted?.Invoke(null, finalState);
        }

        public void Dispose()
        {
            ResizeBatch.DisposeAiSuperResolutionService();
            GC.SuppressFinalize(this);
        }
    }
}
